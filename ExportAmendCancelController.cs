using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using GM.CommonLibs.Common;
using GM.CommonLibs.Helper;
using GM.DataAccess.UnitOfWork;
using GM.DocumentUtility;
using GM.Model.Common;
using GM.Model.ExternalInterface;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.SS.Util;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class ExportAmendCancelController : ControllerBase
    {
        private static LogFile _log;
        private readonly IUnitOfWork _uow;
        private readonly IHostingEnvironment _env;
        public ExportAmendCancelController(IUnitOfWork uow, IHostingEnvironment env)
        {
            _uow = uow;
            _env = env;
            _log = new LogFile(_uow.GetConfiguration, "ExportAmendCancel");
        }

        [HttpPost]
        [Route("ExportAmendCancelDailyMail")]
        public ResultWithModel ExportAmendCancelDailyMail(ExportAmendCancelDailyMailModel model)
        {
            string StrMsg = string.Empty;
            ResultWithModel rwm = new ResultWithModel();
            Mail_ClientEntity MailClientEnt = new Mail_ClientEntity();

            try
            {
                _log.WriteLog("Start ExportAmendCancelDailyMail ==========");
                // Step 1 : Set Config
                if (Set_ConfigAmendCancelDailyMail(ref StrMsg, ref MailClientEnt, ref model) == false)
                {
                    throw new Exception("Set_ConfigAmendCancelDailyMail() : " + StrMsg);
                }

                // Step 2 : Search AmendCancel Daily Report
                _log.WriteLog("Search AmendCancelDaily");
                DataSet Ds_AmendCancelDaily = new DataSet();
                if (Search_AmendCancelDaily(ref StrMsg, ref Ds_AmendCancelDaily, model) == false)
                {
                    throw new Exception("Search_AmendCancelDaily() : " + StrMsg);
                }
                _log.WriteLog(" - AmendCancelDaily = [" + Ds_AmendCancelDaily.Tables.Count + "] Table.");
                _log.WriteLog("Search AmendCancelDaily = Success");

                // Step 3 : Set Mail Body
                if (Set_MailAmendCancelDaily(ref StrMsg, ref MailClientEnt, Ds_AmendCancelDaily) == false)
                {
                    throw new Exception("Set_MailAmendCancelDaily() : " + StrMsg);
                }

                // Step 4 : Write Excel
                _log.WriteLog("Write Excel");
                _log.WriteLog(" - File = " + model.FileName);
                _log.WriteLog(" - To Path = " + model.FilePath);
                if (Ds_AmendCancelDaily.Tables.Count == 3)
                {
                    if (Write_AmendCancelDaily(ref StrMsg, Ds_AmendCancelDaily, model) == false)
                    {
                        throw new Exception("Write_AmendCancelDaily() : " + StrMsg);
                    }
                    _log.WriteLog("Write Excel = Success");
                    MailClientEnt.AttachFile.Add(model.FileName);
                }
                else
                {
                    _log.WriteLog("Write Excel = Data Not Found");
                }

                // Step 5 : Send Mail To Client
                _log.WriteLog("Send Mail Client");
                if (MailClientEnt.Enable == "Y")
                {
                    if (MailClientEnt.To.Count != 0)
                    {
                        for (int i = 0; i < MailClientEnt.To.Count; i++)
                        {
                            _log.WriteLog(" - To = " + MailClientEnt.To[i]);
                        }
                    }
                    if (MailClientEnt.Cc.Count != 0)
                    {
                        for (int j = 0; j < MailClientEnt.Cc.Count; j++)
                        {
                            _log.WriteLog(" - Cc = " + MailClientEnt.Cc[j]);
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
            catch (Exception Ex)
            {
                rwm.RefCode = -999;
                rwm.Message = Ex.Message;
                rwm.Serverity = "high";
                rwm.Success = false;
                _log.WriteLog("Error ExportInterfaceFxReconcile() : " + Ex.Message);
            }
            finally
            {
                _log.WriteLog("End ExportAmendCancelDailyMail ==========");
            }

            DataSet dsResult = new DataSet();
            DataTable dt = new List<ExportAmendCancelDailyMailModel>() { model }.ToDataTable();
            dt.TableName = "ExportAmendCancelDailyMailResultModel";
            dsResult.Tables.Add(dt);
            rwm.Data = dsResult;

            return rwm;
        }

        private bool Set_ConfigAmendCancelDailyMail(ref string returnMsg, ref Mail_ClientEntity mailClientEnt, ref ExportAmendCancelDailyMailModel model)
        {
            try
            {
                model.FilePath = Path.Combine(_env.ContentRootPath, model.RpConfigModel.FirstOrDefault(a => a.item_code == "PATH_SERVICE")?.item_value.Replace("~\\", ""));
                model.FileName = model.RpConfigModel.FirstOrDefault(a => a.item_code == "FILE_NAME")?.item_value.Replace("yyyyMMdd", model.AsofDate.ToString("yyyyMMdd") + DateTime.Now.ToString("HHmmss"));
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
                mailClientEnt.Subject = mailClientEnt.Subject?.Replace("{1}", "Run at " + DateTime.Now.ToString("dd MMM yyyy HH:mm:ss"));
                mailClientEnt.Body = model.RpConfigModel.FirstOrDefault(a => a.item_code == "MAIL_BODY")?.item_value;
                mailClientEnt.Body += "<br>นำส่งข้อมูล Report ประจำวัน ({system_date}) ดังไฟล์แนบ (ข้อมูลการทำธุรกรรม วันที่  {asof_date})";
                mailClientEnt.Body = mailClientEnt.Body.Replace("{system_date}", DateTime.Now.ToString("dd/MM/yyyy"));
                mailClientEnt.Body = mailClientEnt.Body.Replace("{asof_date}", model.AsofDate.ToString("dd/MM/yyyy"));
                mailClientEnt.PathAttachFile = model.FilePath;

                _log.WriteLog("- AsOfDate = " + model.AsofDate.ToString("yyyyMMdd"));
                _log.WriteLog("- Mail Enable = " + mailClientEnt.Enable);
                _log.WriteLog("- Mail Server = " + mailClientEnt.Host);
                _log.WriteLog("- Mail Port = " + mailClientEnt.Port);
                _log.WriteLog("- Mail From = " + mailClientEnt.From);
                _log.WriteLog("- Mail To = " + Mail_To_Tmp);
                _log.WriteLog("- Mail Cc = " + Mail_Cc_Tmp);
                _log.WriteLog("- Mail Subject = " + mailClientEnt.Subject);
                _log.WriteLog("- Mail Body = " + mailClientEnt.Body);
                _log.WriteLog("- Mail PathAttachFile = " + mailClientEnt.PathAttachFile);
                _log.WriteLog("- Mail AttachFile = " + model.FileName);
            }
            catch (Exception Ex)
            {
                returnMsg = Ex.Message;
                return false;
            }
            return true;
        }

        private bool Set_MailAmendCancelDaily(ref string returnMsg, ref Mail_ClientEntity mailClientEnt, DataSet Ds_AmendCancelDaily)
        {
            try
            {
                string MailBody = mailClientEnt.Body;
                DataTable Dt_Interbank = new DataTable();
                DataTable Dt_Corperate = new DataTable();

                if (Ds_AmendCancelDaily.Tables.Count == 3)
                {
                    Dt_Interbank = Ds_AmendCancelDaily.Tables[1];
                    Dt_Corperate = Ds_AmendCancelDaily.Tables[2];
                }

                MailBody += "<br>";
                MailBody += "<u><b>Interbank</b></u><br>";
                MailBody += "<table>";
                MailBody += "<tr>";
                MailBody += "   <td align='center'>";
                MailBody += "<b>หัวข้อ</b>";
                MailBody += "   </td>";
                MailBody += "   <td align='center'>";
                MailBody += "<b>จำนวนรายการ</b>";
                MailBody += "   </td>";
                MailBody += "</tr>";
                for (int i = 0; i < Dt_Interbank.Rows.Count; i++)
                {
                    MailBody += "<tr>";
                    MailBody += "   <td align='left'>";
                    MailBody += Dt_Interbank.Rows[i]["data_type"].ToString();
                    MailBody += "   </td>";
                    MailBody += "   <td align='center'>";
                    MailBody += Dt_Interbank.Rows[i]["record"].ToString();
                    MailBody += "   </td>";
                    MailBody += "</tr>";
                }
                MailBody += "</table>";

                MailBody += "<br>";
                MailBody += "<u><b>Corperate</b></u><br>";
                MailBody += "<table>";
                MailBody += "<tr>";
                MailBody += "   <td align='center'>";
                MailBody += "<b>หัวข้อ</b>";
                MailBody += "   </td>";
                MailBody += "   <td align='center'>";
                MailBody += "<b>จำนวนรายการ</b>";
                MailBody += "   </td>";
                MailBody += "</tr>";
                for (int j = 0; j < Dt_Corperate.Rows.Count; j++)
                {
                    MailBody += "<tr>";
                    MailBody += "   <td align='left'>";
                    MailBody += Dt_Corperate.Rows[j]["data_type"].ToString();
                    MailBody += "   </td>";
                    MailBody += "   <td align='center'>";
                    MailBody += Dt_Corperate.Rows[j]["record"].ToString();
                    MailBody += "   </td>";
                    MailBody += "</tr>";
                }
                MailBody += "</table>";

                mailClientEnt.Body = MailBody;
            }
            catch (Exception Ex)
            {
                returnMsg = Ex.Message;
                return false;
            }

            return true;
        }

        private bool Search_AmendCancelDaily(ref string returnMsg, ref DataSet ds_AmendCancelDaily, ExportAmendCancelDailyMailModel model)
        {
            try
            {
                var rwm = _uow.External.ExportAmendCancel.GetAmendCancelDaily(model);

                if (!rwm.Success)
                {
                    throw new Exception("[" + rwm.RefCode + "] " + rwm.Message);
                }

                ds_AmendCancelDaily = (DataSet)rwm.Data;
            }
            catch (Exception Ex)
            {
                returnMsg = Ex.Message;
                return false;
            }

            return true;
        }

        private bool Write_AmendCancelDaily(ref string returnMsg, DataSet ds_AmendCancelDaily, ExportAmendCancelDailyMailModel model)
        {
            try
            {
                string Report_Bank = "SiGG Financial Bank";
                string Report_Header = "Amend and Cancel Deal Daily Report";

                HSSFWorkbook workbook = new HSSFWorkbook();
                ISheet sheet = workbook.CreateSheet("AmendCancelReport");

                ExcelTemplate excelTemplate = new ExcelTemplate(workbook);

                // Add Header 
                int rowIndex = 0;
                IRow excelRow = sheet.CreateRow(rowIndex);
                excelTemplate.CreateCellHeaderLeft(excelRow, 0, Report_Bank);
                rowIndex++;

                excelRow = sheet.CreateRow(rowIndex);
                excelTemplate.CreateCellHeaderLeft(excelRow, 0, Report_Header);
                rowIndex++;

                excelRow = sheet.CreateRow(rowIndex);
                excelTemplate.CreateCellHeaderLeft(excelRow, 0, model.AsofDate.ToString("dd/MM/yyyy"));
                rowIndex++;

                excelRow = sheet.CreateRow(rowIndex);
                excelTemplate.CreateCellHeaderLeft(excelRow, 0, "System : Repo");
                rowIndex++;

                excelRow = sheet.CreateRow(rowIndex);
                excelTemplate.CreateCellHeaderLeft(excelRow, 0, "Report No.100003");
                rowIndex++;

                excelRow = sheet.CreateRow(rowIndex);
                excelTemplate.CreateCellHeaderLeft(excelRow, 0, "Run date and time " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                rowIndex++;

                excelRow = sheet.CreateRow(rowIndex);
                excelTemplate.CreateCellColHead(excelRow, 0, ds_AmendCancelDaily.Tables[1].Rows[0]["cust_type"].ToString());

                rowIndex++;
                excelRow = sheet.CreateRow(rowIndex);
                excelTemplate.CreateCellColHead(excelRow, 0, "หัวข้อ");
                excelTemplate.CreateCellColHead(excelRow, 1, "จำนวนรายการ");
                //Add Summary 
                for (int i = 0; i < ds_AmendCancelDaily.Tables[1].Rows.Count; i++)
                {
                    rowIndex++;
                    excelRow = sheet.CreateRow(rowIndex);
                    excelTemplate.CreateCellColLeft(excelRow, 0, ds_AmendCancelDaily.Tables[1].Rows[i]["data_type"].ToString());
                    excelTemplate.CreateCellColRight(excelRow, 1, ds_AmendCancelDaily.Tables[1].Rows[i]["record"].ToString());
                }
                rowIndex++;

                excelRow = sheet.CreateRow(rowIndex);
                excelTemplate.CreateCellColHead(excelRow, 0, ds_AmendCancelDaily.Tables[2].Rows[0]["cust_type"].ToString());

                rowIndex++;
                excelRow = sheet.CreateRow(rowIndex);
                excelTemplate.CreateCellColHead(excelRow, 0, "หัวข้อ");
                excelTemplate.CreateCellColHead(excelRow, 1, "จำนวนรายการ");

                //Add Summary 
                for (int i = 0; i < ds_AmendCancelDaily.Tables[2].Rows.Count; i++)
                {
                    rowIndex++;
                    excelRow = sheet.CreateRow(rowIndex);
                    excelTemplate.CreateCellColLeft(excelRow, 0, ds_AmendCancelDaily.Tables[2].Rows[i]["data_type"].ToString());
                    excelTemplate.CreateCellColRight(excelRow, 1, ds_AmendCancelDaily.Tables[2].Rows[i]["record"].ToString());
                }

                // Add Header Table
                rowIndex++;
                excelRow = sheet.CreateRow(rowIndex);

                excelTemplate.CreateCellColHead(excelRow, 0, "Deal Status");
                excelTemplate.CreateCellColHead(excelRow, 1, "Customer Type");
                excelTemplate.CreateCellColHead(excelRow, 2, "Report Type");
                excelTemplate.CreateCellColHead(excelRow, 3, "Old Deal No.");
                excelTemplate.CreateCellColHead(excelRow, 4, "New Deal No.");
                excelTemplate.CreateCellColHead(excelRow, 5, "Portfolio");
                excelTemplate.CreateCellColHead(excelRow, 6, "Deal Type");
                excelTemplate.CreateCellColHead(excelRow, 7, "Deal Date");
                excelTemplate.CreateCellColHead(excelRow, 8, "Maturity Date");
                excelTemplate.CreateCellColHead(excelRow, 9, "Capture Date");
                excelTemplate.CreateCellColHead(excelRow, 10, "Cancel Amend Date");
                excelTemplate.CreateCellColHead(excelRow, 11, "Counterparty Code");
                excelTemplate.CreateCellColHead(excelRow, 12, "Counterparty Name");
                excelTemplate.CreateCellColHead(excelRow, 13, "Commitment CCY");
                excelTemplate.CreateCellColHead(excelRow, 14, "Commitment Amount");
                excelTemplate.CreateCellColHead(excelRow, 15, "Counterparty CCY");
                excelTemplate.CreateCellColHead(excelRow, 16, "Counterparty Amount");
                excelTemplate.CreateCellColHead(excelRow, 17, "Dealer Name");
                excelTemplate.CreateCellColHead(excelRow, 18, "Dealer Cancel Amend Name");
                excelTemplate.CreateCellColHead(excelRow, 19, "Cause");

                // Add Data Rows
                for (int i = 0; i < ds_AmendCancelDaily.Tables[0].Rows.Count; i++)
                {
                    rowIndex++;
                    excelRow = sheet.CreateRow(rowIndex);
                    excelTemplate.CreateCellColCenter(excelRow, 0, ds_AmendCancelDaily.Tables[0].Rows[i]["deal_status"].ToString());
                    excelTemplate.CreateCellColCenter(excelRow, 1, ds_AmendCancelDaily.Tables[0].Rows[i]["cust_type"].ToString());
                    excelTemplate.CreateCellColCenter(excelRow, 2, ds_AmendCancelDaily.Tables[0].Rows[i]["report_type"].ToString());
                    excelTemplate.CreateCellColCenter(excelRow, 3, ds_AmendCancelDaily.Tables[0].Rows[i]["old_deal_no"].ToString());
                    excelTemplate.CreateCellColCenter(excelRow, 4, ds_AmendCancelDaily.Tables[0].Rows[i]["new_deal_no"].ToString());
                    excelTemplate.CreateCellColCenter(excelRow, 5, ds_AmendCancelDaily.Tables[0].Rows[i]["portfolio"].ToString());
                    excelTemplate.CreateCellColCenter(excelRow, 6, ds_AmendCancelDaily.Tables[0].Rows[i]["deal_type"].ToString());
                    excelTemplate.CreateCellColCenter(excelRow, 7, ds_AmendCancelDaily.Tables[0].Rows[i]["deal_date"].ToString());
                    excelTemplate.CreateCellColCenter(excelRow, 8, ds_AmendCancelDaily.Tables[0].Rows[i]["maturity_Date"].ToString());
                    excelTemplate.CreateCellColCenter(excelRow, 9, ds_AmendCancelDaily.Tables[0].Rows[i]["capture_date"].ToString());
                    excelTemplate.CreateCellColCenter(excelRow, 10, ds_AmendCancelDaily.Tables[0].Rows[i]["cancel_amend_date"].ToString());
                    excelTemplate.CreateCellColCenter(excelRow, 11, ds_AmendCancelDaily.Tables[0].Rows[i]["counter_party_code"].ToString());
                    excelTemplate.CreateCellColLeft(excelRow, 12, ds_AmendCancelDaily.Tables[0].Rows[i]["counter_party_name"].ToString());
                    excelTemplate.CreateCellColCenter(excelRow, 13, ds_AmendCancelDaily.Tables[0].Rows[i]["commitment_ccy"].ToString());

                    double commitment_amt = 0;
                    if (ds_AmendCancelDaily.Tables[0].Rows[i]["commitment_amt"].ToString() != string.Empty)
                    {
                        commitment_amt = double.Parse(ds_AmendCancelDaily.Tables[0].Rows[i]["commitment_amt"].ToString());
                    }
                    excelTemplate.CreateCellColNumber(excelRow, 14, commitment_amt);

                    excelTemplate.CreateCellColCenter(excelRow, 15, ds_AmendCancelDaily.Tables[0].Rows[i]["counter_party_ccy"].ToString());

                    double counter_party_amt = 0;
                    if (ds_AmendCancelDaily.Tables[0].Rows[i]["counter_party_amt"].ToString() != string.Empty)
                    {
                        counter_party_amt = double.Parse(ds_AmendCancelDaily.Tables[0].Rows[i]["counter_party_amt"].ToString());
                    }
                    excelTemplate.CreateCellColNumber(excelRow, 16, counter_party_amt);
                    excelTemplate.CreateCellColCenter(excelRow, 17, ds_AmendCancelDaily.Tables[0].Rows[i]["dealer_name"].ToString());
                    excelTemplate.CreateCellColCenter(excelRow, 18, ds_AmendCancelDaily.Tables[0].Rows[i]["dealer_cancel_amend_name"].ToString());
                    excelTemplate.CreateCellColCenter(excelRow, 19, ds_AmendCancelDaily.Tables[0].Rows[i]["remark"].ToString());
                }

                for (int i = 0; i <= 19; i++)
                {
                    sheet.AutoSizeColumn(i);

                    int colWidth = sheet.GetColumnWidth(i);
                    sheet.SetColumnWidth(i, colWidth + 3000);

                    //if (colWidth < 4000)
                    //{
                    //    sheet.SetColumnWidth(i, 4000);
                    //}
                    //else
                    //{
                    //    sheet.SetColumnWidth(i, colWidth + 1500);
                    //}
                }

                sheet.AddMergedRegion(new CellRangeAddress(0, 0, 0, 10));
                sheet.AddMergedRegion(new CellRangeAddress(1, 1, 0, 10));
                sheet.AddMergedRegion(new CellRangeAddress(2, 2, 0, 10));
                sheet.AddMergedRegion(new CellRangeAddress(3, 3, 0, 10));
                sheet.AddMergedRegion(new CellRangeAddress(4, 4, 0, 10));
                sheet.AddMergedRegion(new CellRangeAddress(5, 5, 0, 10));
                sheet.AddMergedRegion(new CellRangeAddress(19, 19, 0, 0));
                sheet.AddMergedRegion(new CellRangeAddress(19, 19, 1, 1));
                sheet.AddMergedRegion(new CellRangeAddress(19, 19, 2, 2));
                sheet.AddMergedRegion(new CellRangeAddress(19, 19, 3, 3));
                sheet.AddMergedRegion(new CellRangeAddress(19, 19, 4, 4));
                sheet.AddMergedRegion(new CellRangeAddress(19, 19, 5, 5));
                sheet.AddMergedRegion(new CellRangeAddress(19, 19, 6, 6));
                sheet.AddMergedRegion(new CellRangeAddress(19, 19, 7, 7));
                sheet.AddMergedRegion(new CellRangeAddress(19, 19, 8, 8));
                sheet.AddMergedRegion(new CellRangeAddress(19, 19, 9, 9));
                sheet.AddMergedRegion(new CellRangeAddress(19, 19, 10, 10));
                sheet.AddMergedRegion(new CellRangeAddress(19, 19, 11, 11));
                sheet.AddMergedRegion(new CellRangeAddress(19, 19, 12, 12));
                sheet.AddMergedRegion(new CellRangeAddress(19, 19, 13, 13));
                sheet.AddMergedRegion(new CellRangeAddress(19, 19, 14, 14));
                sheet.AddMergedRegion(new CellRangeAddress(19, 19, 15, 15));
                sheet.AddMergedRegion(new CellRangeAddress(19, 19, 16, 16));
                sheet.AddMergedRegion(new CellRangeAddress(19, 19, 17, 17));
                sheet.AddMergedRegion(new CellRangeAddress(19, 19, 18, 18));
                sheet.AddMergedRegion(new CellRangeAddress(19, 19, 19, 19));


                if (!Directory.Exists(model.FilePath))
                {
                    Directory.CreateDirectory(model.FilePath);
                }

                if (System.IO.File.Exists(model.FilePath + @"\\" + model.FileName))
                {
                    System.IO.File.Delete(model.FilePath + @"\\" + model.FileName);
                }

                using (FileStream FileData = new FileStream(model.FilePath + @"\\" + model.FileName, FileMode.Create, FileAccess.Write))
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

        [HttpPost]
        [Route("ExportAmendCancelMonthlyMail")]
        public ResultWithModel ExportAmendCancelMonthlyMail(ExportAmendCancelMonthlyMailModel model)
        {
            string StrMsg = string.Empty;
            ResultWithModel rwm = new ResultWithModel();
            Mail_ClientEntity MailClientEnt = new Mail_ClientEntity();

            try
            {
                _log.WriteLog("Start ExportAmendCancelMonthlyMail ==========");
                // Step 1 : Set Config
                if (Set_ConfigAmendCancelMonthlyMail(ref StrMsg, ref MailClientEnt, ref model) == false)
                {
                    throw new Exception("Set_ConfigAmendCancelMonthlyMail() : " + StrMsg);
                }

                // Step 2 : Search AmendCancel Monthly Report
                _log.WriteLog("Search AmendCancelMonthly");
                DataSet Ds_AmendCancelMonthly = new DataSet();
                if (Search_AmendCancelMonthly(ref StrMsg, ref Ds_AmendCancelMonthly, model) == false)
                {
                    throw new Exception("Search_AmendCancelMonthly() : " + StrMsg);
                }
                _log.WriteLog(" - AmendCancelMonthly = [" + Ds_AmendCancelMonthly.Tables[0].Rows.Count + "] Rows.");
                _log.WriteLog("Search AmendCancelMonthly = Success");

                // Step 3 : Set Mail Body
                if (Set_MailAmendCancelMonthly(ref StrMsg, ref MailClientEnt, Ds_AmendCancelMonthly) == false)
                {
                    throw new Exception("Set_MailAmendCancelMonthly() : " + StrMsg);
                }

                // Step 4 : Write Excel
                _log.WriteLog("Write Excel");
                _log.WriteLog(" - File = " + model.FileName);
                _log.WriteLog(" - To Path = " + model.FilePath);
                if (Ds_AmendCancelMonthly.Tables.Count == 1)
                {
                    if (Write_AmendCancelMonthly(ref StrMsg, Ds_AmendCancelMonthly, model) == false)
                    {
                        throw new Exception("Write_AmendCancelMonthly() : " + StrMsg);
                    }
                    _log.WriteLog("Write Excel = Success");
                    MailClientEnt.AttachFile.Add(model.FileName);
                }
                else
                {
                    _log.WriteLog("Write Excel = Data Not Found");
                }

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
                _log.WriteLog("Error ExportAmendCancelMonthlyMail() : " + ex.Message);
            }
            finally
            {
                _log.WriteLog("End ExportAmendCancelMonthlyMail ==========");
            }

            DataSet dsResult = new DataSet();
            DataTable dt = new List<ExportAmendCancelMonthlyMailModel>() { model }.ToDataTable<ExportAmendCancelMonthlyMailModel>();
            dt.TableName = "ExportAmendCancelMonthlyMailResultModel";
            dsResult.Tables.Add(dt);
            rwm.Data = dsResult;

            return rwm;
        }

        private bool Set_ConfigAmendCancelMonthlyMail(ref string returnMsg, ref Mail_ClientEntity mailClientEnt, ref ExportAmendCancelMonthlyMailModel model)
        {
            try
            {
                model.FilePath = Path.Combine(_env.ContentRootPath, model.RpConfigModel.FirstOrDefault(a => a.item_code == "PATH_SERVICE")?.item_value.Replace("~\\", ""));
                model.FileName = model.RpConfigModel.FirstOrDefault(a => a.item_code == "FILE_NAME")?.item_value.Replace("yyyyMM", model.Monthly);
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
                mailClientEnt.Subject = mailClientEnt.Subject?.Replace("{1}", "Run at " + model.AsofDate.ToString("MMM yyyy"));
                mailClientEnt.Body = model.RpConfigModel.FirstOrDefault(a => a.item_code == "MAIL_BODY")?.item_value;
                mailClientEnt.Body += "<br>นำส่งข้อมูล Report ประจำเดือน ({MMMyyyy}) ดังไฟล์แนบ";
                mailClientEnt.Body = mailClientEnt.Body.Replace("{MMMyyyy}", model.AsofDate.ToString("MMM yyyy"));
                mailClientEnt.PathAttachFile = model.FilePath;

                _log.WriteLog("- Monthly = " + model.Monthly);
                _log.WriteLog("- Mail Enable = " + mailClientEnt.Enable);
                _log.WriteLog("- Mail Server = " + mailClientEnt.Host);
                _log.WriteLog("- Mail Port = " + mailClientEnt.Port);
                _log.WriteLog("- Mail From = " + mailClientEnt.From);
                _log.WriteLog("- Mail To = " + Mail_To_Tmp);
                _log.WriteLog("- Mail Cc = " + Mail_Cc_Tmp);
                _log.WriteLog("- Mail Subject = " + mailClientEnt.Subject);
                _log.WriteLog("- Mail Body = " + mailClientEnt.Body);
                _log.WriteLog("- Mail PathAttachFile = " + mailClientEnt.PathAttachFile);
                _log.WriteLog("- Mail AttachFile = " + model.FileName);

            }
            catch (Exception ex)
            {
                returnMsg = ex.Message;
                return false;
            }

            return true;
        }

        private bool Set_MailAmendCancelMonthly(ref string returnMsg, ref Mail_ClientEntity mailClientEnt, DataSet Ds_AmendCancelMonthly)
        {
            try
            {
                string MailBody = mailClientEnt.Body;
                DataTable Dt_Monthly = new DataTable();

                if (Ds_AmendCancelMonthly.Tables.Count > 0)
                {
                    Dt_Monthly = Ds_AmendCancelMonthly.Tables[0];
                }

                decimal totalAmendAndCancel = 0;
                decimal totalAmendAndCancelSameDay = 0;
                decimal totalAmendAndCancelBackDate = 0;

                decimal totalTrade = 0;
                decimal totalPercent = 0;

                decimal totalPercentSameDay = 0;
                decimal totalPercentBackDate = 0;

                string Style_Table = "style='border-collapse: collapse; Width:1200px;'";
                string Style_Head_Tr = "style='background-color: #08088A; color: #FFFFFF;'";
                string Style_Head_Td = "style='text-align: center;'";

                string Style_Odd_Tr = "style='background-color: #EFFBFB;'";

                MailBody += "<table " + Style_Table + ">";
                MailBody += "<tr " + Style_Head_Tr + ">";
                MailBody += "   <td style='text-align: center;Width:50px;' align ='center'>";
                MailBody += "<b>No.</b>";
                MailBody += "   </td>";
                MailBody += "   <td " + Style_Head_Td + " align='center'>";
                MailBody += "<b>Name</b>";
                MailBody += "   </td>";
                MailBody += "   <td " + Style_Head_Td + " align='center'>";
                MailBody += "<b>Amend/Cancel Sameday</b>";
                MailBody += "   </td>";
                MailBody += "   <td " + Style_Head_Td + " align='center'>";
                MailBody += "<b>Amend/Cancel Backdate</b>";
                MailBody += "   </td>";
                MailBody += "   <td " + Style_Head_Td + " align='center'>";
                MailBody += "<b>Total Amend/Cancel</b>";
                MailBody += "   </td>";
                MailBody += "   <td " + Style_Head_Td + " align='center'>";
                MailBody += "<b>Total Trades</b>";
                MailBody += "   </td>";
                MailBody += "   <td " + Style_Head_Td + " align='center'>";
                MailBody += "<b>Amend/Cancel Sameday %</b>";
                MailBody += "   </td>";
                MailBody += "   <td " + Style_Head_Td + " align='center'>";
                MailBody += "<b>Amend/Cancel Backdate %</b>";
                MailBody += "   </td>";
                MailBody += "   <td " + Style_Head_Td + " align='center'>";
                MailBody += "<b>Total Amend/Cancel %</b>";
                MailBody += "   </td>";
                MailBody += "</tr>";

                for (int i = 0; i < Dt_Monthly.Rows.Count; i++)
                {
                    decimal amendCancel = 0;
                    decimal amendCancelSameDay = 0;
                    decimal amendCancelBackDate = 0;
                    decimal trade = 0;

                    if (i != 0)
                    {
                        int ca = i % 2;

                        if (ca == 0)
                        {
                            MailBody += "<tr>";
                        }
                        else
                        {
                            MailBody += "<tr " + Style_Odd_Tr + ">";
                        }
                    }
                    else
                    {
                        MailBody += "<tr>";
                    }

                    MailBody += "   <td style='text-align: center;Width:50px;' align='center'>";
                    MailBody += Dt_Monthly.Rows[i]["Rownumber"].ToString();
                    MailBody += "   </td>";

                    MailBody += "   <td align='left'>";
                    MailBody += Dt_Monthly.Rows[i]["NAME"].ToString();
                    MailBody += "   </td>";

                    MailBody += "   <td align='center'>";
                    MailBody += Dt_Monthly.Rows[i]["TOTAL_AMENDAND_CANCEL_SAME_DAY"].ToString();
                    MailBody += "   </td>";

                    MailBody += "   <td align='center'>";
                    MailBody += Dt_Monthly.Rows[i]["TOTAL_AMENDAND_CANCEL_BACK_DATE"].ToString();
                    MailBody += "   </td>";

                    MailBody += "   <td align='center'>";
                    MailBody += Dt_Monthly.Rows[i]["TOTAL_AMENDAND_CANCEL"].ToString();
                    MailBody += "   </td>";

                    MailBody += "   <td align='center'>";
                    MailBody += Dt_Monthly.Rows[i]["TOTAL_TRADES"].ToString();
                    MailBody += "   </td>";

                    MailBody += "   <td align='center'>";
                    MailBody += Dt_Monthly.Rows[i]["PERCENT_AMEND_CANCEL_SAME_DAY"].ToString();
                    MailBody += "   </td>";

                    MailBody += "   <td align='center'>";
                    MailBody += Dt_Monthly.Rows[i]["PERCENT_AMEND_CANCEL_BACK_DATE"].ToString();
                    MailBody += "   </td>";

                    MailBody += "   <td align='center'>";
                    MailBody += Dt_Monthly.Rows[i]["PERCENT_AMEND_CANCEL"].ToString();
                    MailBody += "   </td>";
                    MailBody += "</tr>";

                    if (Dt_Monthly.Rows[i]["TOTAL_AMENDAND_CANCEL_SAME_DAY"].ToString() != string.Empty)
                    {
                        amendCancelSameDay = Convert.ToDecimal(Dt_Monthly.Rows[i]["TOTAL_AMENDAND_CANCEL_SAME_DAY"].ToString());
                    }
                    else
                    {
                        amendCancelSameDay = 0;
                    }
                    totalAmendAndCancelSameDay += amendCancelSameDay;

                    if (Dt_Monthly.Rows[i]["TOTAL_AMENDAND_CANCEL_BACK_DATE"].ToString() != string.Empty)
                    {
                        amendCancelBackDate = Convert.ToDecimal(Dt_Monthly.Rows[i]["TOTAL_AMENDAND_CANCEL_BACK_DATE"].ToString());
                    }
                    else
                    {
                        amendCancelBackDate = 0;
                    }
                    totalAmendAndCancelBackDate += amendCancelBackDate;

                    if (Dt_Monthly.Rows[i]["TOTAL_AMENDAND_CANCEL"].ToString() != string.Empty)
                    {
                        amendCancel = Convert.ToDecimal(Dt_Monthly.Rows[i]["TOTAL_AMENDAND_CANCEL"].ToString());
                    }
                    else
                    {
                        amendCancel = 0;
                    }
                    totalAmendAndCancel += amendCancel;

                    if (Dt_Monthly.Rows[i]["TOTAL_TRADES"].ToString() != string.Empty)
                    {
                        trade = Convert.ToDecimal(Dt_Monthly.Rows[i]["TOTAL_TRADES"].ToString());
                    }
                    else
                    {
                        trade = 0;
                    }
                    totalTrade += trade;
                }

                if (totalTrade > 0)
                {
                    totalPercent = (totalAmendAndCancel / totalTrade) * 100;
                    totalPercentSameDay = (totalAmendAndCancelSameDay / totalTrade) * 100;
                    totalPercentBackDate = (totalAmendAndCancelBackDate / totalTrade) * 100;
                }

                MailBody += "<tr>";

                MailBody += "   <td align='center'>";
                MailBody += "";
                MailBody += "   </td>";

                MailBody += "   <td align='center'>";
                MailBody += "Total";
                MailBody += "   </td>";

                MailBody += "   <td align='center'>";
                MailBody += totalAmendAndCancelSameDay.ToString();
                MailBody += "   </td>";

                MailBody += "   <td align='center'>";
                MailBody += totalAmendAndCancelBackDate.ToString();
                MailBody += "   </td>";

                MailBody += "   <td align='center'>";
                MailBody += totalAmendAndCancel.ToString();
                MailBody += "   </td>";

                MailBody += "   <td align='center'>";
                MailBody += totalTrade.ToString();
                MailBody += "   </td>";

                MailBody += "   <td align='center'>";
                MailBody += totalPercentSameDay.ToString("0.00");
                MailBody += "   </td>";

                MailBody += "   <td align='center'>";
                MailBody += totalPercentBackDate.ToString("0.00");
                MailBody += "   </td>";

                MailBody += "   <td align='center'>";
                MailBody += totalPercent.ToString("0.00");
                MailBody += "   </td>";
                MailBody += "</tr>";

                MailBody += "</table>";

                mailClientEnt.Body = MailBody;
            }
            catch (Exception Ex)
            {
                returnMsg = Ex.Message;
                return false;
            }

            return true;
        }

        private bool Search_AmendCancelMonthly(ref string returnMsg, ref DataSet ds_AmendCancelMonthly, ExportAmendCancelMonthlyMailModel model)
        {
            try
            {
                ResultWithModel rwm = _uow.External.ExportAmendCancel.GetAmendCancelMonthly(model);

                if (!rwm.Success)
                {
                    throw new Exception("[" + rwm.RefCode.ToString() + "] " + rwm.Message);
                }

                ds_AmendCancelMonthly = (DataSet)rwm.Data;
            }
            catch (Exception Ex)
            {
                returnMsg = Ex.Message;
                return false;
            }

            return true;
        }

        private bool Write_AmendCancelMonthly(ref string returnMsg, DataSet ds_AmendCancelMonthly, ExportAmendCancelMonthlyMailModel model)
        {
            try
            {
                string Report_Bank = "ธนาคารกรุงไทย(KRUNGTHAI BANK)";
                string Report_Header = "Amended and Cancelled Ratio Monthly Report";

                HSSFWorkbook workbook = new HSSFWorkbook();
                ISheet sheet = workbook.CreateSheet("AmendCancelMonthlyReport");

                ExcelTemplate excelTemplate = new ExcelTemplate(workbook);

                // Add Header 
                int rowIndex = 0;
                IRow excelRow = sheet.CreateRow(rowIndex);

                excelTemplate.CreateCellHeaderLeft(excelRow, 0, Report_Bank);
                rowIndex++;
                excelRow = sheet.CreateRow(rowIndex);

                excelTemplate.CreateCellHeaderLeft(excelRow, 0, Report_Header);
                rowIndex++;
                excelRow = sheet.CreateRow(rowIndex);

                excelTemplate.CreateCellHeaderLeft(excelRow, 0, model.AsofDate.ToString("MMM yyyy"));
                rowIndex++; excelRow = sheet.CreateRow(rowIndex);

                excelTemplate.CreateCellHeaderLeft(excelRow, 0, "System : Repo");
                rowIndex++;
                excelRow = sheet.CreateRow(rowIndex);

                excelTemplate.CreateCellHeaderLeft(excelRow, 0, "Report No.100003");
                rowIndex++;
                excelRow = sheet.CreateRow(rowIndex);

                excelTemplate.CreateCellHeaderLeft(excelRow, 0, "Run date and time " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                rowIndex++;
                excelRow = sheet.CreateRow(rowIndex);

                excelTemplate.CreateCellColHead(excelRow, 0, "No");
                excelTemplate.CreateCellColHead(excelRow, 1, "Name");
                excelTemplate.CreateCellColHead(excelRow, 2, " Amend/Cancel Sameday");
                excelTemplate.CreateCellColHead(excelRow, 3, "Amend/Cancel Backdate");
                excelTemplate.CreateCellColHead(excelRow, 4, "Total Amend/Cancel");
                excelTemplate.CreateCellColHead(excelRow, 5, "Total Trades");
                excelTemplate.CreateCellColHead(excelRow, 6, "Amend/Cancel Sameday %");
                excelTemplate.CreateCellColHead(excelRow, 7, "Amend/Cancel Backdate %");
                excelTemplate.CreateCellColHead(excelRow, 8, "Total Amend/Cancel %");
                rowIndex++;
                excelRow = sheet.CreateRow(rowIndex);

                // Add Data Rows

                decimal totalAmendAndCancel = 0;
                decimal totalAmendAndCancelSameDay = 0;
                decimal totalAmendAndCancelBackDate = 0;

                decimal totalTrade = 0;
                decimal totalPercent = 0;

                decimal totalPercentSameDay = 0;
                decimal totalPercentBackDate = 0;

                for (int i = 0; i < ds_AmendCancelMonthly.Tables[0].Rows.Count; i++)
                {
                    decimal amendCancel = 0;
                    decimal amendCancelSameDay = 0;
                    decimal amendCancelBackDate = 0;
                    decimal trade = 0;

                    excelTemplate.CreateCellColCenter(excelRow, 0, ds_AmendCancelMonthly.Tables[0].Rows[i]["RowNumber"].ToString());
                    excelTemplate.CreateCellColLeft(excelRow, 1, ds_AmendCancelMonthly.Tables[0].Rows[i]["NAME"].ToString());
                    excelTemplate.CreateCellColCenter(excelRow, 2, ds_AmendCancelMonthly.Tables[0].Rows[i]["TOTAL_AMENDAND_CANCEL_SAME_DAY"].ToString());
                    excelTemplate.CreateCellColCenter(excelRow, 3, ds_AmendCancelMonthly.Tables[0].Rows[i]["TOTAL_AMENDAND_CANCEL_BACK_DATE"].ToString());
                    excelTemplate.CreateCellColCenter(excelRow, 4, ds_AmendCancelMonthly.Tables[0].Rows[i]["TOTAL_AMENDAND_CANCEL"].ToString());
                    excelTemplate.CreateCellColCenter(excelRow, 5, ds_AmendCancelMonthly.Tables[0].Rows[i]["TOTAL_TRADES"].ToString());
                    excelTemplate.CreateCellColCenter(excelRow, 6, ds_AmendCancelMonthly.Tables[0].Rows[i]["PERCENT_AMEND_CANCEL_SAME_DAY"].ToString());
                    excelTemplate.CreateCellColCenter(excelRow, 7, ds_AmendCancelMonthly.Tables[0].Rows[i]["PERCENT_AMEND_CANCEL_BACK_DATE"].ToString());
                    excelTemplate.CreateCellColCenter(excelRow, 8, ds_AmendCancelMonthly.Tables[0].Rows[i]["PERCENT_AMEND_CANCEL"].ToString());

                    if (ds_AmendCancelMonthly.Tables[0].Rows[i]["TOTAL_AMENDAND_CANCEL_SAME_DAY"].ToString() != string.Empty)
                    {
                        amendCancelSameDay = Convert.ToDecimal(ds_AmendCancelMonthly.Tables[0].Rows[i]["TOTAL_AMENDAND_CANCEL_SAME_DAY"].ToString());
                    }
                    else
                    {
                        amendCancelSameDay = 0;
                    }
                    totalAmendAndCancelSameDay += amendCancelSameDay;

                    if (ds_AmendCancelMonthly.Tables[0].Rows[i]["TOTAL_AMENDAND_CANCEL_BACK_DATE"].ToString() != string.Empty)
                    {
                        amendCancelBackDate = Convert.ToDecimal(ds_AmendCancelMonthly.Tables[0].Rows[i]["TOTAL_AMENDAND_CANCEL_BACK_DATE"].ToString());
                    }
                    else
                    {
                        amendCancelBackDate = 0;
                    }
                    totalAmendAndCancelBackDate += amendCancelBackDate;

                    if (ds_AmendCancelMonthly.Tables[0].Rows[i]["TOTAL_AMENDAND_CANCEL"].ToString() != string.Empty)
                    {
                        amendCancel = Convert.ToDecimal(ds_AmendCancelMonthly.Tables[0].Rows[i]["TOTAL_AMENDAND_CANCEL"].ToString());
                    }
                    else
                    {
                        amendCancel = 0;
                    }
                    totalAmendAndCancel += amendCancel;

                    if (ds_AmendCancelMonthly.Tables[0].Rows[i]["TOTAL_TRADES"].ToString() != string.Empty)
                    {
                        trade = Convert.ToDecimal(ds_AmendCancelMonthly.Tables[0].Rows[i]["TOTAL_TRADES"].ToString());
                    }
                    else
                    {
                        trade = 0;
                    }
                    totalTrade += trade;

                    rowIndex++;
                    excelRow = sheet.CreateRow(rowIndex);
                }

                if (totalTrade > 0)
                {
                    totalPercent = (totalAmendAndCancel / totalTrade) * 100;
                    totalPercentSameDay = (totalAmendAndCancelSameDay / totalTrade) * 100;
                    totalPercentBackDate = (totalAmendAndCancelBackDate / totalTrade) * 100;
                }

                excelTemplate.CreateCellColCenter(excelRow, 1, "Total");
                excelTemplate.CreateCellColCenter(excelRow, 2, totalAmendAndCancelSameDay.ToString());
                excelTemplate.CreateCellColCenter(excelRow, 3, totalAmendAndCancelBackDate.ToString());
                excelTemplate.CreateCellColCenter(excelRow, 4, totalAmendAndCancel.ToString());
                excelTemplate.CreateCellColCenter(excelRow, 5, totalTrade.ToString());
                excelTemplate.CreateCellColCenter(excelRow, 6, totalPercentSameDay.ToString("0.00"));
                excelTemplate.CreateCellColCenter(excelRow, 7, totalPercentBackDate.ToString("0.00"));
                excelTemplate.CreateCellColCenter(excelRow, 8, totalPercent.ToString("0.00"));

                rowIndex++;
                excelRow = sheet.CreateRow(rowIndex);

                for (int i = 1; i <= 8; i++)
                {

                    sheet.AutoSizeColumn(i);
                    int colWidth = sheet.GetColumnWidth(i);
                    if (colWidth < 4000)
                    {
                        sheet.SetColumnWidth(i, 5000);
                    }
                    else
                    {
                        sheet.SetColumnWidth(i, colWidth + 2000);
                    }
                }

                sheet.AddMergedRegion(new CellRangeAddress(0, 0, 0, 10));
                sheet.AddMergedRegion(new CellRangeAddress(1, 1, 0, 10));
                sheet.AddMergedRegion(new CellRangeAddress(2, 2, 0, 10));
                sheet.AddMergedRegion(new CellRangeAddress(3, 3, 0, 10));
                sheet.AddMergedRegion(new CellRangeAddress(4, 4, 0, 10));
                sheet.AddMergedRegion(new CellRangeAddress(5, 5, 0, 10));

                if (!Directory.Exists(model.FilePath))
                {
                    Directory.CreateDirectory(model.FilePath);
                }

                if (System.IO.File.Exists(model.FilePath + @"\\" + model.FileName))
                {
                    System.IO.File.Delete(model.FilePath + @"\\" + model.FileName);
                }

                //FileStream FileData = new FileStream(model.FilePath + @"\\" + model.FileName, FileMode.Create);
                //workbook.Write(FileData);
                //FileData.Close();
                //FileData.Dispose();

                using (FileStream FileData = new FileStream(model.FilePath + @"\\" + model.FileName, FileMode.Create, FileAccess.Write))
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


    }
}