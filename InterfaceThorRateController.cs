using GM.CommonLibs.Common;
using GM.DataAccess.UnitOfWork;
using GM.Model.Common;
using GM.Model.ExternalInterface;
using GM.Model.ExternalInterface.ExchRateSummit;
using GM.Model.ExternalInterface.InterfaceThorRate;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class InterfaceThorRateController : ControllerBase
    {
        private readonly IUnitOfWork _uow;
        private static LogFile _log;

        public InterfaceThorRateController(IUnitOfWork uow)
        {
            _uow = uow;
            _log = new LogFile(_uow.GetConfiguration, "InterfaceThorRate");
        }

        [HttpPost]
        [Route("[action]")]
        public ResultWithModel ImportThorRateSSMD(InterfaceReqThorRateModel reqHeader)
        {
            ResultWithModel rwm = new ResultWithModel();
            string StrMsg = string.Empty;
            if (reqHeader == null)
            {
                reqHeader = new InterfaceReqThorRateModel();
            }
            try
            {
                _log.WriteLog("Start ImportThorRateSSMD ==========");
                _log.WriteLog("- url_ticket : " + reqHeader.url_ticket);
                _log.WriteLog("- url_rate : " + reqHeader.url_rate);
                _log.WriteLog("- mode : " + reqHeader.mode);

                InterfaceReqHeaderTicketSummitModel reqheaderTicket = new InterfaceReqHeaderTicketSummitModel();
                InterfaceReqBodyTicketSummitModel bodyticket = new InterfaceReqBodyTicketSummitModel();
                string guid = Guid.NewGuid().ToString();
                reqHeader.ref_id = guid;
                reqHeader.request_date = DateTime.Now.ToString("yyyyMMdd");
                reqHeader.request_time = DateTime.Now.ToString("HH:mm:ss.fff");

                _log.WriteLog("Request TicketSSMD");
                InterfaceResTicketHeaderThorRateModel resTicket = GetTicket(reqHeader);
                if (resTicket.is_error)
                {
                    throw new Exception("Get_ticket : " + resTicket.error_message);
                }

                _log.WriteLog("- Ref_id = " + guid);
                _log.WriteLog("- Ticket = " + resTicket.resBody.ticket);
                _log.WriteLog("Request TicketSSMD = Success.");

                _log.WriteLog("Request RateExchangeRate SSMD");
                guid = Guid.NewGuid().ToString();
                reqHeader.ticket = resTicket.resBody.ticket;
                reqHeader.ref_id = guid;
                reqHeader.request_date = DateTime.Now.ToString("yyyyMMdd");
                reqHeader.request_time = DateTime.Now.ToString("HH:mm:ss.fff");
                reqHeader.mode = reqHeader.mode;

                InterfaceResRateHeaderThorRateModel resRate = GetThorRate(reqHeader);
                if (resRate.is_error)
                {
                    throw new Exception("ImportThorRateSSMD() : " + resRate.error_message);
                }

                if (resRate.listResBody == null || resRate.listResBody.Count == 0)
                {
                    throw new Exception("ImportThorRateSSMD() => Thor Rate Not Found.");
                }

                _log.WriteLog("- Rate [" + resRate.listResBody.Count + "] Item.");
                _log.WriteLog("Request RateSSMD = Success.");

                _uow.BeginTransaction();

                try
                {
                    _log.WriteLog("ImportThorRateSSMD To Repo");

                    if (!Processing(ref StrMsg, resRate))
                    {
                        throw new Exception("ImportThorRateSSMD() = > Processing : " + StrMsg);
                    }


                    _uow.Commit();
                    _log.WriteLog("Import Rate Success.");

                    rwm.RefCode = 0;
                    rwm.HowManyRecord = int.Parse(resRate.total_number);
                    rwm.Message = "Import into table GM_thor_rate Success.";
                    rwm.Success = true;
                }
                catch (Exception ex)
                {
                    _uow.Rollback();
                    rwm.RefCode = -999;
                    rwm.Message = ex.Message;
                    rwm.Serverity = "high";
                    rwm.Success = false;
                    _log.WriteLog("Error ImportThorRateSSMD() : " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                rwm.RefCode = -999;
                rwm.Message = ex.Message;
                rwm.Serverity = "high";
                rwm.Success = false;
                _log.WriteLog("Error ImportThorRateSSMD() : " + ex.Message);
            }

            _log.WriteLog("End ImportThorRateSSMD ==========");
            return rwm;
        }

        private InterfaceResTicketHeaderThorRateModel GetTicket(InterfaceReqThorRateModel req)
        {
            InterfaceResTicketHeaderThorRateModel res = new InterfaceResTicketHeaderThorRateModel();

            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(req.url_ticket);
            client.DefaultRequestHeaders.Add("ref_id", req.ref_id);
            client.DefaultRequestHeaders.Add("request_date", req.request_date);
            client.DefaultRequestHeaders.Add("request_time", req.request_time);
            client.DefaultRequestHeaders.Add("mode", req.mode);
            client.Timeout = TimeSpan.FromMilliseconds(req.time_out);

            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

            var requestData = JsonConvert.SerializeObject(req.authorization, new IsoDateTimeConverter() { DateTimeFormat = "dd/MM/yyyy HH:mm:ss" });
            //Insert Log Before Call API
            ServiceInOutReqModel logModel = new ServiceInOutReqModel();
            logModel.guid = req.ref_id;
            logModel.svc_req = requestData;
            logModel.svc_type = "OUT";
            logModel.module_name = "InterfaceThorRate";
            logModel.action_name = "InterfaceThorRate | Get_ticket | " + req.url_ticket;
            logModel.ref_id = req.ref_id;
            logModel.create_by = "Console";
            _uow.External.ServiceInOutReq.Add(logModel);

            _log.WriteLog("-Req authorization = " + req.authorization);

            var Values = new Dictionary<string, string>
            {
                { "authorization", req.authorization}
            };

            var Content = new FormUrlEncodedContent(Values);
            var response = client.PostAsync(req.url_ticket, Content).Result;
            _log.WriteLog("- " + response.StatusCode);
            _log.WriteLog("-Resp StatusCode = " + response.StatusCode);
            if (response.IsSuccessStatusCode)
            {
                _log.WriteLog("-Resp IsSuccessStatusCode = " + response.IsSuccessStatusCode);
                _log.WriteLog("-Resp ResultValues = " + response.Content.ReadAsStringAsync().Result);
                InterfaceResTicketBodyThorRateModel resBodyTicket = JsonConvert.DeserializeObject<InterfaceResTicketBodyThorRateModel>(response.Content.ReadAsStringAsync().Result);
                res.channel = response.Headers.GetValues("channel").FirstOrDefault();
                res.ref_id = response.Headers.GetValues("ref_id").FirstOrDefault();
                res.response_date = response.Headers.GetValues("response_date").FirstOrDefault();
                res.response_time = response.Headers.GetValues("response_time").FirstOrDefault();
                res.response_code = response.Headers.GetValues("response_code").FirstOrDefault();
                res.response_message = response.Headers.GetValues("response_message").FirstOrDefault();
                res.Content_Type = response.Content.Headers.ContentType.ToString();
                res.resBody = resBodyTicket;
            }
            else
            {
                res.error_message = ((int)response.StatusCode).ToString() + " : " + response.StatusCode;
            }
            var ResultJsonData = JsonConvert.SerializeObject(res, new IsoDateTimeConverter() { DateTimeFormat = "dd/MM/yyyy HH:mm:ss" });
            logModel.svc_res = ResultJsonData;
            logModel.ref_id = res.ref_id;
            logModel.status = ((int)response.StatusCode).ToString();
            logModel.status_desc = response.IsSuccessStatusCode ? "Success" : "False";

            _uow.External.ServiceInOutReq.Update(logModel);
            res.is_error = !response.IsSuccessStatusCode;
            return res;
        }

        private InterfaceResRateHeaderThorRateModel GetThorRate(InterfaceReqThorRateModel req)
        {
            InterfaceResRateHeaderThorRateModel res = new InterfaceResRateHeaderThorRateModel();
            string urlServer = req.url_rate;

            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(urlServer);
                client.DefaultRequestHeaders.Add("ticket", req.ticket);
                client.DefaultRequestHeaders.Add("ref_id", req.ref_id);
                client.DefaultRequestHeaders.Add("request_date", req.request_date);
                client.DefaultRequestHeaders.Add("request_time", req.request_time);
                client.DefaultRequestHeaders.Add("mode", req.mode);
                client.Timeout = TimeSpan.FromMilliseconds(req.time_out);

                ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

                var requestData = JsonConvert.SerializeObject(req.reqBody, new IsoDateTimeConverter() { DateTimeFormat = "dd/MM/yyyy HH:mm:ss" });
                //Insert Log Before Call API
                ServiceInOutReqModel logModel = new ServiceInOutReqModel();
                logModel.guid = req.ref_id;
                logModel.svc_req = requestData;
                logModel.svc_type = "OUT";
                logModel.module_name = "InterfaceThorRate";
                logModel.action_name = "InterfaceThorRate | GetThorRate | " + urlServer;
                logModel.ref_id = req.ref_id;
                logModel.create_by = "Console";
                _uow.External.ServiceInOutReq.Add(logModel);

                _log.WriteLog("- Req Header");
                _log.WriteLog("- Req ref_id = " + req.ref_id);
                _log.WriteLog("- Req request_date = " + req.request_date);
                _log.WriteLog("- Req request_time = " + req.request_time);
                _log.WriteLog("- Req mode = " + req.mode);
                _log.WriteLog("- Req ticket = " + req.ticket);
                _log.WriteLog("- ");
                _log.WriteLog("- Req Body");
                _log.WriteLog("- Req data_type = " + req.reqBody.data_type);
                _log.WriteLog("- Req as_of_date = " + req.reqBody.as_of_date);
                _log.WriteLog("- Req curve_id = " + req.reqBody.curve_id);
                _log.WriteLog("- Req ccy = " + req.reqBody.ccy);
                _log.WriteLog("- Req index = " + req.reqBody.index);

                //END Insert Log Before Call API
                var httpContent = new StringContent(requestData, Encoding.UTF8, "application/json");
                var response = client.PostAsync(urlServer, httpContent).Result;
                _log.WriteLog("- ");
                _log.WriteLog("-Resp StatusCode = " + response.StatusCode);
                if (response.IsSuccessStatusCode)
                {
                    _log.WriteLog("-Resp IsSuccessStatusCode = " + response.IsSuccessStatusCode);
                    var result = response.Content.ReadAsStringAsync().Result;
                    _log.WriteLog("-Resp ResultValues = " + result);

                    if (result == "null")
                    {
                        throw new Exception("Thor Index not found");
                    }

                    List<InterfaceResRateBodyThorRateModel> resBody = JsonConvert.DeserializeObject<List<InterfaceResRateBodyThorRateModel>>(result);
                    res.channel = response.Headers.GetValues("channel").FirstOrDefault();
                    res.ref_id = response.Headers.GetValues("ref_id").FirstOrDefault();
                    res.response_date = response.Headers.GetValues("response_date").FirstOrDefault();
                    res.response_time = response.Headers.GetValues("response_time").FirstOrDefault();
                    res.response_code = response.Headers.GetValues("response_code").FirstOrDefault();
                    res.response_message = response.Headers.GetValues("response_message").FirstOrDefault();
                    res.data_type = response.Headers.GetValues("data_type").FirstOrDefault();
                    res.total_number = response.Headers.GetValues("total_number").FirstOrDefault();
                    res.listResBody = resBody;
                }
                else
                {
                    res.error_message = ((int)response.StatusCode) + " : " + response.StatusCode;
                }
                var resultJsonData = JsonConvert.SerializeObject(res, new IsoDateTimeConverter() { DateTimeFormat = "dd/MM/yyyy HH:mm:ss" });
                logModel.svc_res = resultJsonData;
                logModel.ref_id = res.ref_id;
                logModel.status = ((int)response.StatusCode).ToString();
                logModel.status_desc = response.IsSuccessStatusCode ? "Success" : "False";

                _uow.External.ServiceInOutReq.Update(logModel);
                res.is_error = !response.IsSuccessStatusCode;
            }

            return res;
        }

        private bool Processing(ref string returnMsg, InterfaceResRateHeaderThorRateModel model)
        {
            try
            {
                ResultWithModel rwm = _uow.External.InterfaceThorRate.Add(model);
                if (!rwm.Success)
                {
                    throw new Exception("[" + rwm.RefCode + "] " + rwm.Message);
                }
            }
            catch (Exception ex)
            {
                returnMsg = ex.Message;
                return false;
            }
            return true;
        }

        [HttpGet]
        [Route("GetDDLCur")]
        public ResultWithModel GetDDLCur(string cur)
        {
            DropdownModel model = new DropdownModel();
            model.ProcedureName = "GM_DDL_List_Proc";
            model.DdltTableList = "GM_currency";
            model.SearchValue = cur;
            return _uow.Dropdown.Get(model);
        }

        [HttpPost]
        [Route("GetThorRate")]
        public ResultWithModel GetThorRate(ThorRateModel model)
        {
            return _uow.External.ThorRate.Get(model);
        }

        [HttpPost]
        [Route("CreateThorRate")]
        public ResultWithModel CreateThorRate(ThorRateModel model)
        {
            return _uow.External.ThorRate.Add(model);
        }

        [HttpPost]
        [Route("UpdateThorRate")]
        public ResultWithModel UpdateThorRate(ThorRateModel model)
        {
            return _uow.External.ThorRate.Update(model);
        }

        [HttpPost]
        [Route("DeleteThorRate")]
        public ResultWithModel DeleteThorRate(ThorRateModel model)
        {
            return _uow.External.ThorRate.Remove(model);
        }

    }
}
