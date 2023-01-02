using GM.CommonLibs.Common;
using GM.DataAccess.UnitOfWork;
using GM.Model.Common;
using GM.Model.ExternalInterface;
using GM.Model.ExternalInterface.ExchRateSummit;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class InterfaceExchRateSummitController : ControllerBase
    {
        private readonly IUnitOfWork _uow;
        private static LogFile _log;

        public InterfaceExchRateSummitController(IUnitOfWork uow)
        {
            _uow = uow;
            _log = new LogFile(_uow.GetConfiguration, "InterfaceExchRateSummit");
        }

        [HttpPost]
        [Route("[action]")]
        public ResultWithModel ImportExchangeRateSSMD(InterfaceReqExchRateHeaderSummitModel interfaceReqExchRate)
        {
            ResultWithModel rwm = new ResultWithModel();
            string StrMsg = string.Empty;
            if (interfaceReqExchRate == null)
            {
                interfaceReqExchRate = new InterfaceReqExchRateHeaderSummitModel();
            }
            try
            {
                _log.WriteLog("Start ImportExchangeRateSSMD ==========");
                _log.WriteLog("- url_ticket" + interfaceReqExchRate.url_ticket);
                _log.WriteLog("- url_rate" + interfaceReqExchRate.url_rate);
                _log.WriteLog("- mode" + interfaceReqExchRate.mode);

                InterfaceReqHeaderTicketSummitModel reqheaderTicket = new InterfaceReqHeaderTicketSummitModel();
                InterfaceReqBodyTicketSummitModel bodyticket = new InterfaceReqBodyTicketSummitModel();

                string guid = Guid.NewGuid().ToString();
                reqheaderTicket.ref_id = guid;
                reqheaderTicket.Content_Type = "application/json";
                reqheaderTicket.request_date = DateTime.Now.ToString("yyyyMMdd");
                reqheaderTicket.request_time = DateTime.Now.ToString("HH:mm:ss.fff");
                reqheaderTicket.mode = "4";
                bodyticket.authorization = interfaceReqExchRate.authorization;
                reqheaderTicket.req_bodyticket_model = bodyticket;
                reqheaderTicket.ticket_url = interfaceReqExchRate.url_ticket;

                _log.WriteLog("Request TicketSSMD");
                InterfaceResHeaderTicketSummitModel resticket = GetTicket(reqheaderTicket);
                if (resticket.is_error)
                {
                    throw new Exception("Get_ticket() : " + resticket.error_message);
                }

                _log.WriteLog("- Ref_id = " + guid);
                _log.WriteLog("- Ticket = " + resticket.resbodyticket_model.ticket);
                _log.WriteLog("Request TicketSSMD = Success.");

                _log.WriteLog("Request RateExchangeRate SSMD");
                guid = Guid.NewGuid().ToString();
                interfaceReqExchRate.ticket = resticket.resbodyticket_model.ticket;
                interfaceReqExchRate.ref_id = guid;
                interfaceReqExchRate.request_date = DateTime.Now.ToString("yyyyMMdd");
                interfaceReqExchRate.request_time = DateTime.Now.ToString("HH:mm:ss.fff");
                interfaceReqExchRate.mode = "4";

                InterfaceResExchRateFXSpotModel res_rate = GetExchangeRateFXSPOTDataModel(interfaceReqExchRate);
                if (res_rate.is_error)
                {
                    throw new Exception("GetExchangeRateFXSPOTDataModel() : " + res_rate.error_message);
                }

                List<InterfaceResExchRateFXSpotDetailModel> detaildata = res_rate.listdetail;

                _log.WriteLog("- Rate [" + detaildata.Count + "] Item.");
                _log.WriteLog("Request RateSSMD = Success.");

                _uow.BeginTransaction();

                try
                {
                    _log.WriteLog("Import ExchangeRate To Repo");
                    // Step 3 : Delete_Exchange_Rate_Temp(Temp)
                    if (!DeleteExchangeRate(ref StrMsg, interfaceReqExchRate))
                    {
                        throw new Exception("DeleteExchangeRate() : " + StrMsg);
                    }

                    for (int i = 0; i < detaildata.Count; i++)
                    {
                        // Step 4 : Insert Exchange Rate(Temp)
                        if (!InsertExchangeRateSummit(ref StrMsg, detaildata[i]))
                        {
                            throw new Exception("InsertExchangeRateSummit() : [ccy1 : " + detaildata[i].ccy1 + "] ,[ccy2 : " + detaildata[i].ccy2 + "]  seq : " + detaildata[i].seq + ";" + StrMsg);
                        }
                    }

                    _uow.Commit();
                    _log.WriteLog("Import Rate Success.");

                    rwm.RefCode = 0;
                    rwm.HowManyRecord = int.Parse(res_rate.total_number);
                    rwm.Message = "Insert into table GM_exchange_rate_summit_temp  Success.";
                    rwm.Success = true;
                }
                catch (Exception ex)
                {
                    _uow.Rollback();
                    rwm.RefCode = -999;
                    rwm.Message = "Fail : " + ex.Message;
                    rwm.Serverity = "high";
                    rwm.Success = false;
                    _log.WriteLog("Error ImportExchangeRateSSMD() : " + ex.Message);
                }

                if (rwm.RefCode == 0)
                {
                    if (!InsertInterfaceExchangeRate(ref StrMsg, interfaceReqExchRate))
                    {
                        rwm.RefCode = -999;
                        rwm.Message = "Fail Insert_Interface_Exchange_Rate_To_Face() : " + StrMsg;
                    }
                    else
                    {
                        rwm.RefCode = 0;
                        rwm.Message = "Import SSMD ExchangeRate Success.";
                    }
                }

            }
            catch (Exception ex)
            {
                rwm.RefCode = -999;
                rwm.Message = ex.Message;
                rwm.Serverity = "high";
                rwm.Success = false;
                _log.WriteLog("Error ImportExchangeRateSSMD() : " + ex.Message);
            }

            _log.WriteLog("End ImportExchangeRateSSMD ==========");
            return rwm;
        }

        private InterfaceResExchRateFXSpotModel GetExchangeRateFXSPOTDataModel(InterfaceReqExchRateHeaderSummitModel req)
        {
            InterfaceResExchRateFXSpotModel res = new InterfaceResExchRateFXSpotModel();
            //req.url_rate = "http://10.9.16.191:8080/SSMDWebApi/v1/SSMD/RequestRate";
            string urlServer = req.url_rate;

            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(urlServer);
                client.DefaultRequestHeaders.Add("ticket", req.ticket);
                client.DefaultRequestHeaders.Add("ref_id", req.ref_id);
                client.DefaultRequestHeaders.Add("request_date", req.request_date);
                client.DefaultRequestHeaders.Add("request_time", req.request_time);
                client.DefaultRequestHeaders.Add("mode", req.mode);

                var requestData = JsonConvert.SerializeObject(req.reqbody, new IsoDateTimeConverter() { DateTimeFormat = "dd/MM/yyyy HH:mm:ss" });
                //Insert Log Before Call API
                ServiceInOutReqModel ServiceInOutReqModel = new ServiceInOutReqModel();
                ServiceInOutReqModel.guid = req.ref_id;
                ServiceInOutReqModel.svc_req = requestData;
                ServiceInOutReqModel.svc_type = "OUT";
                ServiceInOutReqModel.module_name = "InterfaceExchRateSummit";
                ServiceInOutReqModel.action_name = "InterfaceExchRateFXSPOT | GetExchangeRateFXSPOTDataModel | " + urlServer;
                ServiceInOutReqModel.ref_id = req.ref_id;
                ServiceInOutReqModel.create_by = "Console";

                InsertLogServiceReq(ServiceInOutReqModel);

                _log.WriteLog("- Req Header");
                _log.WriteLog("- Req ref_id = " + req.ref_id);
                _log.WriteLog("- Req request_date = " + req.request_date);
                _log.WriteLog("- Req request_time = " + req.request_time);
                _log.WriteLog("- Req mode = " + req.mode);
                _log.WriteLog("- Req ticket = " + req.ticket);
                _log.WriteLog("- ");
                _log.WriteLog("- Req Body");
                _log.WriteLog("- Req data_type = " + req.reqbody.data_type);
                _log.WriteLog("- Req as_of_date = " + req.reqbody.as_of_date);
                _log.WriteLog("- Req curve_id = " + req.reqbody.curve_id);
                _log.WriteLog("- Req ccy1 = " + req.reqbody.ccy1);
                _log.WriteLog("- Req ccy2 = " + req.reqbody.ccy2);

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
                        throw new Exception("ExchRate-Spot not found");
                    }

                    List<InterfaceResExchRateFXSpotDetailModel> reslistDetail = JsonConvert.DeserializeObject<List<InterfaceResExchRateFXSpotDetailModel>>(result);
                    res.channel = response.Headers.GetValues("channel").FirstOrDefault();
                    res.ref_id = response.Headers.GetValues("ref_id").FirstOrDefault();
                    res.response_date = response.Headers.GetValues("response_date").FirstOrDefault();
                    res.response_time = response.Headers.GetValues("response_time").FirstOrDefault();
                    res.response_code = response.Headers.GetValues("response_code").FirstOrDefault();
                    res.response_message = response.Headers.GetValues("response_message").FirstOrDefault();
                    res.data_type = response.Headers.GetValues("data_type").FirstOrDefault();
                    res.total_number = response.Headers.GetValues("total_number").FirstOrDefault();
                    res.listdetail = reslistDetail;
                }
                else
                {
                    res.error_message = ((int)response.StatusCode) + " : " + response.StatusCode;
                }
                var ResultJsonData = JsonConvert.SerializeObject(res, new IsoDateTimeConverter() { DateTimeFormat = "dd/MM/yyyy HH:mm:ss" });
                ServiceInOutReqModel.svc_res = ResultJsonData;
                ServiceInOutReqModel.ref_id = res.ref_id;
                ServiceInOutReqModel.status = ((int)response.StatusCode).ToString();
                ServiceInOutReqModel.status_desc = response.IsSuccessStatusCode ? "Success" : "False";

                UpdateLogServiceReq(ServiceInOutReqModel);
                res.is_error = !response.IsSuccessStatusCode;
            }
            
            return res;
        }

        private InterfaceResHeaderTicketSummitModel GetTicket(InterfaceReqHeaderTicketSummitModel model)
        {
            InterfaceResHeaderTicketSummitModel res = new InterfaceResHeaderTicketSummitModel();
            //model.ticket_url = "http://10.9.16.191:8080/SSMDWebApi/v1/SSMD/RequestTicket";
            string UrlServer = model.ticket_url; // "http://10.9.16.191:8080/SSMDWebApi/v1/SSMD/RequestTicket";
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(UrlServer);
            client.DefaultRequestHeaders.Add("ref_id", model.ref_id);
            client.DefaultRequestHeaders.Add("request_date", model.request_date);
            client.DefaultRequestHeaders.Add("request_time", model.request_time);
            client.DefaultRequestHeaders.Add("mode", model.mode);

            var requestData = JsonConvert.SerializeObject(model.req_bodyticket_model, new IsoDateTimeConverter() { DateTimeFormat = "dd/MM/yyyy HH:mm:ss" });
            //Insert Log Before Call API
            ServiceInOutReqModel ServiceInOutReqModel = new ServiceInOutReqModel();
            ServiceInOutReqModel.guid = model.ref_id;
            ServiceInOutReqModel.svc_req = requestData;
            ServiceInOutReqModel.svc_type = "OUT";
            ServiceInOutReqModel.module_name = "InterfaceExchRateSummit";
            ServiceInOutReqModel.action_name = "InterfaceExchRateFXSPOT | Get_ticket | " + UrlServer;
            ServiceInOutReqModel.ref_id = model.ref_id;
            ServiceInOutReqModel.create_by = "Console";
            InsertLogServiceReq(ServiceInOutReqModel);
            //END Insert Log Before Call API
            //var httpContent = new StringContent(requestData, Encoding.UTF8, "application/json");

            _log.WriteLog("-Req authorization = " + model.req_bodyticket_model.authorization);

            var Values = new Dictionary<string, string>
            {
                { "authorization", model.req_bodyticket_model.authorization}
            };

            var Content = new FormUrlEncodedContent(Values);
            var response = client.PostAsync(UrlServer, Content).Result;
            _log.WriteLog("- " + response.StatusCode);
            _log.WriteLog("-Resp StatusCode = " + response.StatusCode);
            if (response.IsSuccessStatusCode)
            {
                _log.WriteLog("-Resp IsSuccessStatusCode = " + response.IsSuccessStatusCode);
                _log.WriteLog("-Resp ResultValues = " + response.Content.ReadAsStringAsync().Result);
                InterfaceResBodyTicketSummitModel InterfaceResBodyTicketSummit = JsonConvert.DeserializeObject<InterfaceResBodyTicketSummitModel>(response.Content.ReadAsStringAsync().Result);
                res.channel = response.Headers.GetValues("channel").FirstOrDefault();
                res.ref_id = response.Headers.GetValues("ref_id").FirstOrDefault();
                res.response_date = response.Headers.GetValues("response_date").FirstOrDefault();
                res.response_time = response.Headers.GetValues("response_time").FirstOrDefault();
                res.response_code = response.Headers.GetValues("response_code").FirstOrDefault();
                res.response_message = response.Headers.GetValues("response_message").FirstOrDefault();
                res.Content_Type = response.Content.Headers.ContentType.ToString();
                res.resbodyticket_model = InterfaceResBodyTicketSummit;
            }
            else
            {
                res.error_message = ((int)response.StatusCode).ToString() + " : " + response.StatusCode;
            }
            var ResultJsonData = JsonConvert.SerializeObject(res, new IsoDateTimeConverter() { DateTimeFormat = "dd/MM/yyyy HH:mm:ss" });
            ServiceInOutReqModel.svc_res = ResultJsonData;
            ServiceInOutReqModel.ref_id = res.ref_id;
            ServiceInOutReqModel.status = ((int)response.StatusCode).ToString();
            ServiceInOutReqModel.status_desc = response.IsSuccessStatusCode ? "Success" : "False";

            UpdateLogServiceReq(ServiceInOutReqModel);
            res.is_error = !response.IsSuccessStatusCode;
            return res;
        }

        private void InsertLogServiceReq(ServiceInOutReqModel model)
        {
            _uow.External.ServiceInOutReq.Add(model);
        }

        private void UpdateLogServiceReq(ServiceInOutReqModel model)
        {
            _uow.External.ServiceInOutReq.Update(model);
        }

        private bool DeleteExchangeRate(ref string ReturnMsg, InterfaceReqExchRateHeaderSummitModel model)
        {
            try
            {
                ResultWithModel rwm = _uow.External.InterfaceReqExchRateSummit.Remove(model);
                if (!rwm.Success)
                {
                    throw new Exception("[" + rwm.RefCode + "] " + rwm.Message);
                }
            }
            catch (Exception Ex)
            {
                ReturnMsg = Ex.Message;
                return false;
            }
            return true;
        }

        private bool InsertExchangeRateSummit(ref string returnMsg, InterfaceResExchRateFXSpotDetailModel model)
        {
            try
            {
                ResultWithModel rwm = _uow.External.InterfaceResExchRateFXSpot.Add(model);
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

        private bool InsertInterfaceExchangeRate(ref string returnMsg, InterfaceReqExchRateHeaderSummitModel model)
        {
            try
            {
                ResultWithModel rwm = _uow.External.InterfaceReqExchRateSummit.Update(model);
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
    }
}