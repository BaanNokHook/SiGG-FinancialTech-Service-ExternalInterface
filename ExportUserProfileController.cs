using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GM.CommonLibs.Common;
using GM.CommonLibs.Helper;
using GM.DataAccess.UnitOfWork;
using GM.DocumentUtility;
using GM.Model.Common;
using GM.Model.ExternalInterface;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.SS.Util;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class ExportUserProfileController : ControllerBase
    {
        private static LogFile _log;
        private readonly IUnitOfWork _uow;
        private readonly IHostingEnvironment _env;
        public ExportUserProfileController(IUnitOfWork uow, IHostingEnvironment env)
        {
            _uow = uow;
            _env = env;
            _log = new LogFile(_uow.GetConfiguration, "ExportUserProfile");
        }

        [HttpPost]
        [Route("ExportUserProfileMonthlyMail")]
        public ResultWithModel ExportUserProfileMonthlyMail(ExportUserProfileMonthlyMailModel model)
        {
            string StrMsg = string.Empty;
            ResultWithModel rwm = new ResultWithModel();
            Mail_ClientEntity MailClientEnt = new Mail_ClientEntity();

            try
            {
                _log.WriteLog("Start ExportUserProfileMonthlyMail ==========");
                // Step 1 : Set Config
                if (Set_ConfigUserProfileMonthlyMail(ref StrMsg, ref MailClientEnt, ref model) == false)
                {
                    throw new Exception("Set_ConfigUserProfileMonthlyMail() : " + StrMsg);
                }

                // Step 2 : Search UserProfile Monthly Report
                _log.WriteLog("Search UserProfileMonthly");
                DataSet Ds_UserProfileMonthly = new DataSet();
                if (Search_UserProfileMonthly(ref StrMsg, ref Ds_UserProfileMonthly, model) == false)
                {
                    throw new Exception("Search_UserProfileMonthly() : " + StrMsg);
                }
                _log.WriteLog(" - UserProfileMonthly = [" + Ds_UserProfileMonthly.Tables[0].Rows.Count + "] Rows.");
                _log.WriteLog("Search UserProfileMonthly = Success");

                // Step 3 : Set Mail Body
                if (Set_MailUserProfileMonthly(ref StrMsg, ref MailClientEnt, Ds_UserProfileMonthly) == false)
                {
                    throw new Exception("Set_MailUserProfileMonthly() : " + StrMsg);
                }

                // Step 4 : Write Excel
                _log.WriteLog("Write Excel");
                _log.WriteLog(" - File = " + model.FileName);
                _log.WriteLog(" - To Path = " + model.FilePath);
                if (Ds_UserProfileMonthly.Tables.Count == 1)
                {
                    if (Write_UserProfileMonthly(ref StrMsg, Ds_UserProfileMonthly, model) == false)
                    {
                        throw new Exception("Write_UserProfileMonthly() : " + StrMsg);
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
                _log.WriteLog("Error ExportUserProfileMonthlyMail() : " + ex.Message);
            }
            finally
            {
                _log.WriteLog("End ExportUserProfileMonthlyMail ==========");
            }

            DataSet dsResult = new DataSet();
            DataTable dt = new List<ExportUserProfileMonthlyMailModel>() { model }.ToDataTable<ExportUserProfileMonthlyMailModel>();
            dt.TableName = "ExportUserProfileMonthlyMailResultModel";
            dsResult.Tables.Add(dt);
            rwm.Data = dsResult;

            return rwm;
        }

        private bool Set_ConfigUserProfileMonthlyMail(ref string returnMsg, ref Mail_ClientEntity mailClientEnt, ref ExportUserProfileMonthlyMailModel model)
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
                mailClientEnt.Subject = mailClientEnt.Subject.Replace("{MMM yyyy}", model.AsofDate.ToString("MMM yyyy"));
                mailClientEnt.Subject = mailClientEnt.Subject.Replace("{1}", "Run at " + DateTime.Now.ToString("dd MMM yyyy HH:mm:ss"));
                mailClientEnt.Body = model.RpConfigModel.FirstOrDefault(a => a.item_code == "MAIL_BODY")?.item_value;
                mailClientEnt.Body = mailClientEnt.Body.Replace("{MMM yyyy}", model.AsofDate.ToString("MMM yyyy"));
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

        private bool Set_MailUserProfileMonthly(ref string returnMsg, ref Mail_ClientEntity mailClientEnt, DataSet Ds_UserProfileMonthly)
        {
            try
            {
                string MailBody = mailClientEnt.Body;
                mailClientEnt.Body = MailBody;
            }
            catch (Exception Ex)
            {
                returnMsg = Ex.Message;
                return false;
            }

            return true;
        }

        private bool Search_UserProfileMonthly(ref string returnMsg, ref DataSet ds_UserProfileMonthly, ExportUserProfileMonthlyMailModel model)
        {
            try
            {
                ResultWithModel rwm = _uow.External.ExportUserProfile.GetUserProfileMonthly(model);

                if (!rwm.Success)
                {
                    throw new Exception("[" + rwm.RefCode + "] " + rwm.Message);
                }

                ds_UserProfileMonthly = (DataSet)rwm.Data;
            }
            catch (Exception Ex)
            {
                returnMsg = Ex.Message;
                return false;
            }

            return true;
        }

        private bool Write_UserProfileMonthly(ref string returnMsg, DataSet ds_UserProfileMonthly, ExportUserProfileMonthlyMailModel model)
        {
            try
            {
                HSSFWorkbook workbook = new HSSFWorkbook();
                ISheet sheet = workbook.CreateSheet("UserProfile");

                ExcelTemplate excelTemplate = new ExcelTemplate(workbook);

                // Add Header 
                int rowIndex = 0;
                IRow excelRow = sheet.CreateRow(rowIndex);

                excelTemplate.CreateCellColHead(excelRow, 0, "user id");
                excelTemplate.CreateCellColHead(excelRow, 1, "user name");
                excelTemplate.CreateCellColHead(excelRow, 2, "user tname");
                excelTemplate.CreateCellColHead(excelRow, 3, "role name");
                excelTemplate.CreateCellColHead(excelRow, 4, "screen name");
                excelTemplate.CreateCellColHead(excelRow, 5, "menu name");
                rowIndex++;
                excelRow = sheet.CreateRow(rowIndex);

                // Add Data Rows
                for (int i = 0; i < ds_UserProfileMonthly.Tables[0].Rows.Count; i++)
                {
                    excelTemplate.CreateCellColCenter(excelRow, 0, ds_UserProfileMonthly.Tables[0].Rows[i]["user_id"].ToString());
                    excelTemplate.CreateCellColLeft(excelRow, 1, ds_UserProfileMonthly.Tables[0].Rows[i]["user_name"].ToString());
                    excelTemplate.CreateCellColLeft(excelRow, 2, ds_UserProfileMonthly.Tables[0].Rows[i]["user_thai_name"].ToString());
                    excelTemplate.CreateCellColLeft(excelRow, 3, ds_UserProfileMonthly.Tables[0].Rows[i]["role_name"].ToString());
                    excelTemplate.CreateCellColLeft(excelRow, 4, ds_UserProfileMonthly.Tables[0].Rows[i]["screen_name"].ToString());
                    excelTemplate.CreateCellColLeft(excelRow, 5, ds_UserProfileMonthly.Tables[0].Rows[i]["menu_name"].ToString());

                    rowIndex++;
                    excelRow = sheet.CreateRow(rowIndex);
                }

                for (int i = 0; i <= 6; i++)
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