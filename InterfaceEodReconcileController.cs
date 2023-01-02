using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using GM.CommonLibs.Common;
using GM.CommonLibs.Helper;
using GM.DataAccess.UnitOfWork;
using GM.DocumentUtility;
using GM.Model.Common;
using GM.Model.InterfaceEodReconcile;
using GM.Model.Report;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class InterfaceEodReconcileController : ControllerBase
    {
        private string _pathFile;
        private string _filenameEodReconcileExcel;
        private string _filenameEodReconcilePdf;
        private string _filenameEodReconcileCallMarginPdf;

        private static LogFile _log;
        private readonly IUnitOfWork _uow;
        private readonly IHostingEnvironment _env;
        public InterfaceEodReconcileController(IUnitOfWork uow, IHostingEnvironment env)
        {
            _uow = uow;
            _env = env;
            _log = new LogFile(_uow.GetConfiguration, "InterfaceEodReconcile");
        }

        [HttpPost]
        [Route("ImportPti")]
        public ResultWithModel ImportPti(ReqEodReconcilePtiHeader model)
        {
            ResultWithModel res = new ResultWithModel();
            try
            {
                _uow.BeginTransaction();

                //remove
                res = _uow.External.InterfaceEodReconcile.RemovePTI(model.asOfDate, model.recordedBy);
                if (!res.Success)
                {
                    throw new Exception("RemovePTI() : " + res.Message);
                }

                //insert temp
                foreach (var data in model.listData)
                {
                    data.filename = model.filename;
                    data.create_by = model.recordedBy;
                    res = _uow.External.InterfaceEodReconcile.AddPTI(model.asOfDate, data);
                    if (!res.Success)
                    {
                        throw new Exception("AddPTI() : [" + data.sender_ref + "] " + res.Message);
                    }
                }

                _uow.Commit();

                res.RefCode = 0;
                res.Success = true;
                res.Message = "Import EOD Reconcile PTI Success.";
            }
            catch (Exception ex)
            {
                _uow.Rollback();
                res.RefCode = -999;
                res.Success = false;
                res.Message = "Fail : " + ex.Message;
            }

            return res;
        }


        [HttpPost]
        [Route("ImportBahtnet")]
        public ResultWithModel ImportBahtnet(ReqEodReconcileBahtnetHeader model)
        {
            ResultWithModel res = new ResultWithModel();
            try
            {
                _uow.BeginTransaction();

                //remove
                res = _uow.External.InterfaceEodReconcile.RemoveBahtnet(model.asOfDate, model.recordedBy);
                if (!res.Success)
                {
                    throw new Exception("RemoveBahtnet() : " + res.Message);
                }

                //insert temp
                foreach (var data in model.listData)
                {
                    data.filename = model.filename;
                    data.create_by = model.recordedBy;
                    res = _uow.External.InterfaceEodReconcile.AddBahtnet(model.asOfDate, data);
                    if (!res.Success)
                    {
                        throw new Exception("AddBahtnet() : [" + data.sender_ref + "] " + res.Message);
                    }
                }

                _uow.Commit();

                res.RefCode = 0;
                res.Success = true;
                res.Message = "Import EOD Reconcile Bahtnet Success.";
            }
            catch (Exception ex)
            {
                _uow.Rollback();
                res.RefCode = -999;
                res.Success = false;
                res.Message = "Fail : " + ex.Message;
            }

            return res;
        }

        [HttpGet]
        [Route("GetEODReconcile")]
        public ResultWithModel GetEODReconcile(string asofDate)
        {
            DateTime date = DateTime.ParseExact(asofDate, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
            return _uow.External.InterfaceEodReconcile.Get(date);
        }

        [HttpPost]
        [Route("SaveEODReconcile")]
        public ResultWithModel SaveEODReconcile(RPEodReconcileModel model)
        {
            return _uow.External.InterfaceEodReconcile.Add(model);
        }

        [HttpPost]
        [Route("SendEmailEODReconcile")]
        public ResultWithModel SendEmailEODReconcile(RPEodReconcileModel model)
        {
            string StrMsg = string.Empty;
            ResultWithModel rwm = new ResultWithModel();
            Mail_ClientEntity MailClientEnt = new Mail_ClientEntity();
            try
            {
                _log.WriteLog("Start EODReconcileMail ==========");
                // Step 1 : Set Config
                if (Set_ConfigEodReconcileMail(ref StrMsg, ref MailClientEnt, model) == false)
                {
                    throw new Exception("Set_ConfigEodReconcileMail() : " + StrMsg);
                }

                // Step 4 : Write Excel
                _log.WriteLog("Write EodReconcileReportExcel");
                _log.WriteLog(" - Filename EodReconcileExcel = " + _filenameEodReconcileExcel);
                _log.WriteLog(" - To Path = " + _pathFile);
                if (Write_EodReconcileReportExcel(ref StrMsg, model) == false)
                {
                    throw new Exception("Write_EodReconcileReportExcel() : " + StrMsg);
                }
                _log.WriteLog("Write EodReconcileReportExcel = Success");
                MailClientEnt.AttachFile.Add(_filenameEodReconcileExcel);

                //Download EodBoReconcile
                string REPORT_URL = _uow.GetConfiguration.GetSection("ReportSite").Value;
                byte[] byteFileEodBoReconcile = DownloadData(string.Format(REPORT_URL + "ReportEodBoReconcile?asofDate={0}&access_token=repo2022", model.ASOF_DATE.Value.ToString("dd/MM/yyyy")));

                if (byteFileEodBoReconcile == null)
                {
                    throw new Exception("EodBoReconcile error: Download is null");
                }
                _log.WriteLog("Write EodBoReconcile = Success");
                WriteFile(_pathFile, _filenameEodReconcilePdf, byteFileEodBoReconcile);
                MailClientEnt.AttachFile.Add(_filenameEodReconcilePdf);

                //Download EodReconcileCallMargin
                byte[] byteFileEodReconcileCallMargin = DownloadData(string.Format(REPORT_URL + "ReportEodReconcileCallMargin?asofDate={0}&type=BILATERAL_MARGIN_PAY,PRIVATE_MARGIN_PAY&access_token=repo2022", model.ASOF_DATE.Value.ToString("dd/MM/yyyy")));

                if (byteFileEodReconcileCallMargin == null)
                {
                    throw new Exception("EodReconcileCallMargin error: Download is null");
                }
                _log.WriteLog("Write EodReconcileCallMargin = Success");
                WriteFile(_pathFile, _filenameEodReconcileCallMarginPdf, byteFileEodReconcileCallMargin);
                MailClientEnt.AttachFile.Add(_filenameEodReconcileCallMarginPdf);

                // Step 5 : Send Mail To Client
                _log.WriteLog("Send Mail Client");
                if (MailClientEnt.Enable == "Y")
                {
                    if (MailClientEnt.To.Count != 0)
                    {
                        foreach (var to in MailClientEnt.To)
                        {
                            _log.WriteLog(" - To = " + to);
                        }
                    }
                    if (MailClientEnt.Cc.Count != 0)
                    {
                        foreach (var cc in MailClientEnt.Cc)
                        {
                            _log.WriteLog(" - Cc = " + cc);
                        }
                    }

                    SendMail ObjectMail = new SendMail();
                    if (!ObjectMail.SendMailClient(ref StrMsg, MailClientEnt))
                    {
                        throw new Exception("SendMailClient() : " + StrMsg);
                    }
                    _log.WriteLog("Send Mail = Success.");
                }
                else
                {
                    _log.WriteLog("Send Mail Disable.");
                }

                rwm.RefCode = 0;
                rwm.Message = "Success";
                rwm.Serverity = "low";
                rwm.Success = true;
            }
            catch (Exception ex)
            {
                rwm.RefCode = -999;
                rwm.Message = ex.Message;
                rwm.Serverity = "high";
                rwm.Success = false;
                _log.WriteLog("Error SendEmailEODReconcile() : " + ex.Message);
            }
            finally
            {
                _log.WriteLog("End SendEmailEODReconcile ==========");
            }

            return rwm;
        }

        private bool Set_ConfigEodReconcileMail(ref string returnMsg, ref Mail_ClientEntity mailClientEnt, RPEodReconcileModel model)
        {
            try
            {
                _pathFile = Path.Combine(_env.ContentRootPath, model.RpConfigModel.FirstOrDefault(a => a.item_code == "PATH_SERVICE")?.item_value.Replace("~\\", ""));
                _filenameEodReconcileExcel = model.RpConfigModel.FirstOrDefault(a => a.item_code == "FILE_NAME_RECONCILE_EXCEL")?.item_value;
                _filenameEodReconcilePdf = model.RpConfigModel.FirstOrDefault(a => a.item_code == "FILE_NAME_RECONCILE_PDF")?.item_value;
                _filenameEodReconcileCallMarginPdf = model.RpConfigModel.FirstOrDefault(a => a.item_code == "FILE_NAME_RECONCILE_CALLMARGIN_PDF")?.item_value;
                mailClientEnt.Enable = model.RpConfigModel.FirstOrDefault(a => a.item_code == "ENABLE_MAIL")?.item_value;
                mailClientEnt.Host = model.RpConfigModel.FirstOrDefault(a => a.item_code == "MAIL_SERVER")?.item_value;

                if (!string.IsNullOrEmpty(model.RpConfigModel.FirstOrDefault(a => a.item_code == "MAIL_PORT")?.item_value))
                {
                    mailClientEnt.Port = Convert.ToInt32(model.RpConfigModel.FirstOrDefault(a => a.item_code == "MAIL_PORT")?.item_value);
                }

                mailClientEnt.From = model.RpConfigModel.FirstOrDefault(a => a.item_code == "MAIL_SENDER")?.item_value;
                string Mail_To_Tmp = model.RpConfigModel.FirstOrDefault(a => a.item_code == "MAIL_TO")?.item_value;
                if (!string.IsNullOrEmpty(Mail_To_Tmp))
                {
                    string[] mail_to_array = Mail_To_Tmp.Split(',');
                    foreach (string mail_to in mail_to_array)
                    {
                        mailClientEnt.To.Add(mail_to);
                    }
                }

                string Mail_Cc_Tmp = model.RpConfigModel.FirstOrDefault(a => a.item_code == "MAIL_CC")?.item_value;
                if (!string.IsNullOrEmpty(Mail_Cc_Tmp))
                {
                    string[] mail_cc_array = Mail_Cc_Tmp.Split(',');
                    foreach (string mail_cc in mail_cc_array)
                    {
                        mailClientEnt.Cc.Add(mail_cc);
                    }
                }

                mailClientEnt.Subject = model.RpConfigModel.FirstOrDefault(a => a.item_code == "MAIL_SUBJECT")?.item_value;
                mailClientEnt.Subject = mailClientEnt.Subject.Replace("{dd MMM yyyy}", model.ASOF_DATE.Value.ToString("dd MMM yyyy"));
                mailClientEnt.Subject = mailClientEnt.Subject.Replace("{1}", "Run at " + DateTime.Now.ToString("dd MMM yyyy HH:mm:ss"));
                mailClientEnt.Body = model.RpConfigModel.FirstOrDefault(a => a.item_code == "MAIL_BODY")?.item_value;
                mailClientEnt.Body = mailClientEnt.Body.Replace("{MMM yyyy}", model.ASOF_DATE.Value.ToString("MMM yyyy"));
                mailClientEnt.PathAttachFile = _pathFile;

                _log.WriteLog("- Mail Enable = " + mailClientEnt.Enable);
                _log.WriteLog("- Mail Server = " + mailClientEnt.Host);
                _log.WriteLog("- Mail Port = " + mailClientEnt.Port);
                _log.WriteLog("- Mail From = " + mailClientEnt.From);
                _log.WriteLog("- Mail To = " + Mail_To_Tmp);
                _log.WriteLog("- Mail Cc = " + Mail_Cc_Tmp);
                _log.WriteLog("- Mail Subject = " + mailClientEnt.Subject);
                _log.WriteLog("- Mail Body = " + mailClientEnt.Body);
                _log.WriteLog("- Mail PathAttachFile = " + mailClientEnt.PathAttachFile);
            }
            catch (Exception ex)
            {
                returnMsg = ex.Message;
                return false;
            }

            return true;
        }

        private bool Write_EodReconcileReportExcel(ref string returnMsg, RPEodReconcileModel model)
        {
            try
            {

                ResultWithModel rwm = _uow.Report.EodReconcile(model.ASOF_DATE.Value, true, true, true, true, true, true, true,true, true, true);

                if (!rwm.Success)
                {
                    throw new Exception(rwm.Message);
                }

                List<EodReconcileReportModel> dataResultModel = ((DataSet)rwm.Data).Tables[0].DataTableToList<EodReconcileReportModel>();

                HSSFWorkbook workbook = new HSSFWorkbook();
                ISheet sheet = workbook.CreateSheet("EOD Bo Reconcile");

                ExcelTemplate excelTemplate = new ExcelTemplate(workbook);

                // Add Header 
                int rowIndex = 0;
                IRow excelRow = sheet.CreateRow(rowIndex);

                excelTemplate.CreateCellColHead(excelRow, 0, "Trans No");
                excelTemplate.CreateCellColHead(excelRow, 1, "Security");
                excelTemplate.CreateCellColHead(excelRow, 2, "Trade Date");
                excelTemplate.CreateCellColHead(excelRow, 3, "Settlement Date");
                excelTemplate.CreateCellColHead(excelRow, 4, "Maturity Date");
                excelTemplate.CreateCellColHead(excelRow, 5, "Trans State");
                excelTemplate.CreateCellColHead(excelRow, 6, "Purchase Price");
                excelTemplate.CreateCellColHead(excelRow, 7, "Interest Amount");
                excelTemplate.CreateCellColHead(excelRow, 8, "With HoldingTax Amt");
                excelTemplate.CreateCellColHead(excelRow, 9, "Termination Value");
                excelTemplate.CreateCellColHead(excelRow, 10, "Payment Method");

                #region Add Data Rows
                WriteExcelRowData(ref excelTemplate, ref sheet, ref excelRow, ref rowIndex, dataResultModel, "Bilateral Repo", "Trade Verify");
                WriteExcelRowData(ref excelTemplate, ref sheet, ref excelRow, ref rowIndex, dataResultModel, "Bilateral Repo", "Settlement", "DVP");
                WriteExcelRowData(ref excelTemplate, ref sheet, ref excelRow, ref rowIndex, dataResultModel, "Bilateral Repo", "Settlement", "RVP");
                WriteExcelRowData(ref excelTemplate, ref sheet, ref excelRow, ref rowIndex, dataResultModel, "Bilateral Repo", "Settlement", "MT202");

                WriteExcelRowData(ref excelTemplate, ref sheet, ref excelRow, ref rowIndex, dataResultModel, "Private Repo", "Trade Verify");
                WriteExcelRowData(ref excelTemplate, ref sheet, ref excelRow, ref rowIndex, dataResultModel, "Private Repo", "Settlement", "DVP");
                WriteExcelRowData(ref excelTemplate, ref sheet, ref excelRow, ref rowIndex, dataResultModel, "Private Repo", "Settlement", "RVP");

                WriteExcelRowData(ref excelTemplate, ref sheet, ref excelRow, ref rowIndex, dataResultModel, "Bilateral Call Margin", "Settlement", "Payment");
                WriteExcelRowData(ref excelTemplate, ref sheet, ref excelRow, ref rowIndex, dataResultModel, "Bilateral Call Margin", "Settlement", "Receive");

                WriteExcelRowData(ref excelTemplate, ref sheet, ref excelRow, ref rowIndex, dataResultModel, "Private Call Margin", "Settlement", "Payment");
                WriteExcelRowData(ref excelTemplate, ref sheet, ref excelRow, ref rowIndex, dataResultModel, "Private Call Margin", "Settlement", "Receive");
                #endregion

                for (int i = 0; i <= 10; i++)
                {
                    if (i > 0)
                    {
                        sheet.AutoSizeColumn(i);
                    }

                    int colWidth = sheet.GetColumnWidth(i);
                    if (colWidth < 4000)
                    {
                        sheet.SetColumnWidth(i, 4000);
                    }
                    else
                    {
                        sheet.SetColumnWidth(i, colWidth + 1500);
                    }
                }

                if (!Directory.Exists(_pathFile))
                {
                    Directory.CreateDirectory(_pathFile);
                }

                if (System.IO.File.Exists(_pathFile + @"\\" + _filenameEodReconcileExcel))
                {
                    System.IO.File.Delete(_pathFile + @"\\" + _filenameEodReconcileExcel);
                }


                using (FileStream FileData = new FileStream(_pathFile + @"\\" + _filenameEodReconcileExcel, FileMode.Create, FileAccess.Write))
                {
                    workbook.Write(FileData);
                }

            }
            catch (Exception ex)
            {
                returnMsg = ex.Message;
                return false;
            }

            return true;
        }

        private void WriteExcelRowData(ref ExcelTemplate excelTemplate, ref ISheet sheet, ref IRow excelRow, ref int rowIndex,
            List<EodReconcileReportModel> eodList, string repoDealTypeName, string boState, string mtCode = null)
        {
            List<EodReconcileReportModel>  groupList = !string.IsNullOrEmpty(mtCode) ? eodList.Where(a => a.repo_deal_type_name == repoDealTypeName && a.bo_state == boState && a.mt_code == mtCode).ToList() : eodList.Where(a => a.repo_deal_type_name == repoDealTypeName && a.bo_state == boState).ToList();

            if (!groupList.Any()) return;

            rowIndex++;
            excelRow = sheet.CreateRow(rowIndex);
            excelTemplate.CreateCellColCenter(excelRow, 0, repoDealTypeName);

            rowIndex++;
            excelRow = sheet.CreateRow(rowIndex);
            excelTemplate.CreateCellColCenter(excelRow, 1, boState);

            if (!string.IsNullOrEmpty(mtCode))
            {
                excelTemplate.CreateCellColCenter(excelRow, 2, mtCode);
            }

            foreach (var item in groupList)
            {
                rowIndex++;
                excelRow = sheet.CreateRow(rowIndex);
                excelTemplate.CreateCellColCenter(excelRow, 0, item.trans_no);
                excelTemplate.CreateCellColCenter(excelRow, 1, item.security);
                excelTemplate.CreateCellColCenter(excelRow, 2, item.trade_date.ToString("dd/MM/yyyy"));
                excelTemplate.CreateCellColCenter(excelRow, 3, item.settlement_date.ToString("dd/MM/yyyy"));
                excelTemplate.CreateCellColCenter(excelRow, 4, item.maturity_date.ToString("dd/MM/yyyy"));
                excelTemplate.CreateCellColCenter(excelRow, 5, item.trans_state);
                excelTemplate.CreateCellCol2Decimal(excelRow, 6, (double)item.purchase_price);
                excelTemplate.CreateCellCol2Decimal(excelRow, 7, (double)item.interest_amount);
                excelTemplate.CreateCellCol2Decimal(excelRow, 8, (double)item.holdingTax_amt);
                excelTemplate.CreateCellCol2Decimal(excelRow, 9, (double)item.termination_value);
                excelTemplate.CreateCellColCenter(excelRow, 10, item.payment_method);
            }
        }

        private void WriteFile(string pathFile, string fileName, byte[] data)
        {
            if (!Directory.Exists(@pathFile))
            {
                Directory.CreateDirectory(@pathFile);
            }

            if (System.IO.File.Exists(@pathFile + "\\" + fileName))
            {
                System.IO.File.Delete(@pathFile + "\\" + fileName);
            }

            using (FileStream fs = new FileStream(@pathFile + "\\" + fileName, FileMode.CreateNew, FileAccess.Write))
            {
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    bw.Write(data);
                }
            }
        }

        private byte[] DownloadData(string serverUrlAddress)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(serverUrlAddress))
                    throw new Exception("url download not found");

                // Create a new WebClient instance
                using (WebClient client = new WebClient())
                {
                    if (serverUrlAddress.StartsWith("https://"))
                        ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, errors) => true;

                    var byteFile = client.DownloadData(serverUrlAddress);
                    string type = client.ResponseHeaders["Content-Type"];
                    if (!type.Contains("pdf"))
                    {
                        throw new Exception("content type not PDF");
                    }

                    return byteFile;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("DownloadData: " + ex.Message);
            }
        }
    }
}