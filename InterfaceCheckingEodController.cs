using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using GM.CommonLibs.Common;
using GM.CommonLibs.Helper;
using GM.DataAccess.UnitOfWork;
using GM.Model.Common;
using GM.Model.ExternalInterface;
using Microsoft.AspNetCore.Mvc;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class InterfaceCheckingEodController : ControllerBase
    {
        private readonly IUnitOfWork _uow;
        private static LogFile _log;

        public InterfaceCheckingEodController(IUnitOfWork uow)
        {
            _uow = uow;
            _log = new LogFile(_uow.GetConfiguration, "InterfaceCheckingEod");
        }

        [HttpPost]
        [Route("CheckingEodList")]
        public ResultWithModel CheckingEodList(InterfaceCheckingEodModel CheckingEodModel)
        {
            string StrMsg = string.Empty;
            ResultWithModel resModel = new ResultWithModel();
            Mail_ClientEntity MailClientEnt = new Mail_ClientEntity();
            try
            {
                _log.WriteLog("Start ExportAmendCancelDailyMail ==========");

                resModel = _uow.External.InterfaceCheckingEod.Get();

                if (!resModel.Success)
                {
                    throw new Exception("[" + resModel.RefCode.ToString() + "] " + resModel.Message);
                }

                string body = EmailBody((DataSet)resModel.Data);

                if (Set_ConfigCheckingEodList(ref StrMsg, ref MailClientEnt, CheckingEodModel) == false)
                {
                    throw new Exception(StrMsg);
                }

                MailClientEnt.Body = body;
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

                resModel.RefCode = 0;
                resModel.Message = "Success";
                resModel.Serverity = "low";
                resModel.Success = true;
            }
            catch (Exception ex)
            {
                resModel.RefCode = -999;
                resModel.Message = ex.Message;
                resModel.Serverity = "high";
                resModel.Success = false;
                _log.WriteLog("Error CheckingEodList() : " + ex.Message);
            }
            finally
            {
                _log.WriteLog("End CheckingEodList ==========");
            }
            DataSet dsResult = new DataSet();
            DataTable dt = new List<InterfaceCheckingEodModel>() { CheckingEodModel }.ToDataTable<InterfaceCheckingEodModel>();
            dt.TableName = "InterfaceCheckingEodResultModel";
            dsResult.Tables.Add(dt);
            resModel.Data = dsResult;
            return resModel;
        }

        [HttpPost]
        [Route("UpdateCheckingEod")]
        public ResultWithModel UpdateCheckingEod(InterfaceCheckingEodModel model)
        {
            return _uow.External.InterfaceCheckingEod.Update(model.task_name);
        }

        private string EmailBody(DataSet ds)
        {
            string body = "";

            var groupEvery = ds.Tables[0].AsEnumerable().GroupBy(a => a.Field<string>("every_group")).ToList();

            foreach (var group in groupEvery)
            {
                body += "<h2>" + group.Key + "</h2>";

                body += "<table cellpadding=\"0\" cellspacing=\"0\" width=\"100%\" style=\"font-family:Arial,sans-serif;font-size: 14px;\">" +
                "<thead><tr style=\"color: #ffffff;\">" +
                "<th style=\"background-color:#08088a;text-align:center;width:300px;\">Task Name</th>" +
                "<th style=\"background-color:#08088a;text-align:center;\">Every</th>" +
                "<th style=\"background-color:#08088a;text-align:center;\">Last Run Time</th>" +
                "<th style=\"background-color:#08088a;text-align:center;\">Status</th>" +
                "<th style=\"background-color:#08088a;text-align:center;\">Remark</th></tr></thead>";

                int rowCount = 0;

                var everyDatas = ds.Tables[0].AsEnumerable().Where(a => a.Field<string>("every_group") == group.Key).ToArray();
                foreach (DataRow dataRow in everyDatas)
                {
                    string styleBg = " style=\"border-bottom:1px solid #dddddd;\"";
                    string styleStatus = "style=\"text-align:center\"";
                    if (rowCount % 2 == 1)
                    {
                        styleBg = " style=\"background-color:#effbfb; border-bottom: 1px solid #dddddd;\"";
                    }

                    if (dataRow["result_status"].ToString() == "OK")
                    {
                        styleStatus =
                            "style=\"text-align:center;color:#008000;\"";
                    }
                    else if (dataRow["result_status"].ToString() == "FAIL")
                    {
                        styleStatus =
                            "style=\"text-align:center;color:#ff0000;\"";
                    }

                    string runDate = "";
                    DateTime dateConvert;
                    if (DateTime.TryParse(dataRow["run_date"].ToString().Trim(),
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out dateConvert))
                    {
                        runDate = dateConvert.ToString("dd/MM/yyyy HH:mm:ss");
                    }

                    body += string.Format("<tr{0}><td>{1}</td><td>{2}</td><td>{3}</td><td {4}><b>{5}</b></td><td>{6}</td></tr>",
                        styleBg,
                        dataRow["task_name"],
                        dataRow["run_every"],
                        runDate,
                        styleStatus,
                        dataRow["result_status"],
                        dataRow["remark"]);
                    rowCount++;
                }

                body += "</table><br\\>";
            }

            return body;
        }

        private bool Set_ConfigCheckingEodList(ref string ReturnMsg, ref Mail_ClientEntity MailClientEnt, InterfaceCheckingEodModel CheckingEodModel)
        {
            try
            {
                MailClientEnt.Host = CheckingEodModel.RpConfigModel.FirstOrDefault(a => a.item_code == "MAIL_SERVER")?.item_value;

                if (!String.IsNullOrEmpty(CheckingEodModel.RpConfigModel.FirstOrDefault(a => a.item_code == "MAIL_PORT")?.item_value))
                {
                    MailClientEnt.Port = Convert.ToInt32(CheckingEodModel.RpConfigModel.FirstOrDefault(a => a.item_code == "MAIL_PORT")?.item_value);
                }

                MailClientEnt.From = CheckingEodModel.RpConfigModel.FirstOrDefault(a => a.item_code == "MAIL_SENDER")?.item_value;
                string Mail_To_Tmp = CheckingEodModel.RpConfigModel.FirstOrDefault(a => a.item_code == "MAIL_TO")?.item_value;
                if (!String.IsNullOrEmpty(Mail_To_Tmp))
                {
                    string[] mail_to_array = Mail_To_Tmp.Split(',');
                    foreach (string mail_to in mail_to_array)
                    {
                        MailClientEnt.To.Add(mail_to);
                    }
                }

                string Mail_Cc_Tmp = CheckingEodModel.RpConfigModel.FirstOrDefault(a => a.item_code == "MAIL_CC")?.item_value;
                if (!String.IsNullOrEmpty(Mail_Cc_Tmp))
                {
                    string[] mail_cc_array = Mail_Cc_Tmp.Split(',');
                    foreach (string mail_cc in mail_cc_array)
                    {
                        MailClientEnt.Cc.Add(mail_cc);
                    }
                }

                MailClientEnt.Subject = CheckingEodModel.RpConfigModel.FirstOrDefault(a => a.item_code == "MAIL_SUBJECT")?.item_value;
                MailClientEnt.Subject = MailClientEnt.Subject?.Replace("{1}", "Run at " + DateTime.Now.ToString("dd MMM yyyy HH:mm:ss"));
                MailClientEnt.Body = CheckingEodModel.RpConfigModel.FirstOrDefault(a => a.item_code == "MAIL_BODY")?.item_value;

                _log.WriteLog("- Mail Enable = " + MailClientEnt.Enable);
                _log.WriteLog("- Mail Server = " + MailClientEnt.Host);
                _log.WriteLog("- Mail Port = " + MailClientEnt.Port);
                _log.WriteLog("- Mail From = " + MailClientEnt.From);
                _log.WriteLog("- Mail To = " + Mail_To_Tmp);
                _log.WriteLog("- Mail Cc = " + Mail_Cc_Tmp);
                _log.WriteLog("- Mail Subject = " + MailClientEnt.Subject);
                _log.WriteLog("- Mail Body = " + MailClientEnt.Body);
            }
            catch (Exception Ex)
            {
                ReturnMsg = Ex.Message;
                return false;
            }
            return true;
        }
    }
}