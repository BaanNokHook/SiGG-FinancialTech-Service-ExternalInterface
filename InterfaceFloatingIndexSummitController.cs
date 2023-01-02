using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Http;
using GM.CommonLibs.Common;
using GM.CommonLibs.Helper;
using GM.DataAccess.UnitOfWork;
using GM.Model.Common;
using GM.Model.ExternalInterface.FloatingIndexSummit;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;


namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class InterfaceFloatingIndexSummitController : ControllerBase
    {
        private readonly IUnitOfWork _uow;
        private static LogFile _log;

        public InterfaceFloatingIndexSummitController(IUnitOfWork uow)
        {
            _uow = uow;
            _log = new LogFile(_uow.GetConfiguration, "InterfaceFloatingIndexSummit");
        }

        [HttpPost]
        [Route("[action]")]
        public ResultWithModel ImportFloatingIndexSSMD(InterfaceFloatingIndexSummitModel model)
        {
            string strMsg = string.Empty;
            ResultWithModel rwm = new ResultWithModel();
            try
            {
                _log.WriteLog("Start ImportFloatingIndexSSMD ==========");
                _log.WriteLog("- AsOfDate = " + model.as_of_date);
                _log.WriteLog("- CurveId = " + model.curve_id);
                _log.WriteLog("- Ccy = " + model.ccy);
                _log.WriteLog("- Mode = " + model.mode);

                // Step 1 : Request Ticket SSMD
                _log.WriteLog("Request TicketSSMD");
                model.ref_id = Guid.NewGuid().ToString().ToUpper();
                model.request_date = DateTime.Now.ToString("yyyyMMdd");
                model.request_time = DateTime.Now.ToString("HH:MM:ss.FFF");

                if (!RequestTicketSSMD(ref strMsg, ref model))
                {
                    throw new Exception("Request_TicketSSMD() : " + strMsg);
                }

                _log.WriteLog("- Ref_id = " + model.ref_id);
                _log.WriteLog("- Ticket = " + model.ticket);
                _log.WriteLog("Request TicketSSMD = Success.");

                // Step 2 : Request RateFloatingIndex SSMD
                _log.WriteLog("Request RateFloatingIndex SSMD");
                List<FloatingIndexEntity> FloatingIndexEnt = new List<FloatingIndexEntity>();
                model.request_date = DateTime.Now.ToString("yyyyMMdd");
                model.request_time = DateTime.Now.ToString("HH:MM:ss.FFF");
                if (!RequestRateFloatingIndexSSMD(ref strMsg, ref FloatingIndexEnt, model))
                {
                    throw new Exception("Request_RateFloatingIndexSSMD() : " + strMsg);
                }

                model.FloatingIndex_Item = FloatingIndexEnt.Count;
                _log.WriteLog("- Rate [" + FloatingIndexEnt.Count + "] Item.");
                _log.WriteLog("Request RateSSMD = Success.");

                _uow.BeginTransaction();

                try
                {
                    _log.WriteLog("Import Rate FloatingIndex To Repo");
                    if (!DeleteFloatingIndex(ref strMsg, model))
                    {
                        throw new Exception("DeleteFloatingIndex() : " + strMsg);
                    }

                    _log.WriteLog("- Delete FloatingIndex " + model.as_of_date + " Success.");

                    for (int i = 0; i < FloatingIndexEnt.Count; i++)
                    {
                        if (!InsertFloatingIndex(ref strMsg, FloatingIndexEnt[i], model))
                        {
                            throw new Exception(strMsg);
                        }
                    }

                    _uow.Commit();

                    _log.WriteLog("Import Rate Success.");
                }
                catch (Exception ex)
                {
                    _uow.Rollback();
                    throw new Exception(ex.Message);
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
                _log.WriteLog("Error ImportFloatingIndexSSMD() : " + ex.Message);
            }

            _log.WriteLog("End ImportFloatingIndexSSMD ==========");

            DataSet dsResult = new DataSet();
            DataTable dt = new List<InterfaceFloatingIndexSummitModel>() { model }.ToDataTable<InterfaceFloatingIndexSummitModel>();
            dt.TableName = "InterfaceFloatingIndexSummitResultModel";
            dsResult.Tables.Add(dt);
            rwm.Data = dsResult;
            return rwm;
        }

        private bool RequestTicketSSMD(ref string returnMsg, ref InterfaceFloatingIndexSummitModel model)
        {
            try
            {
                InterfaceResBodyTicketSummit res;
                HttpClient client = new HttpClient();

                client.BaseAddress = new Uri(model.url_ticket);
                client.DefaultRequestHeaders.Add("ref_id", model.ref_id);
                client.DefaultRequestHeaders.Add("request_date", model.request_date);
                client.DefaultRequestHeaders.Add("request_time", model.request_time);
                client.DefaultRequestHeaders.Add("mode", model.mode);

                _log.WriteLog("- Req authorization = " + model.authorization);

                var auth = new Dictionary<string, string>
                {
                    { "authorization", model.authorization}
                };

                var Content = new FormUrlEncodedContent(auth);
                var response = client.PostAsync(model.url_ticket, Content).Result;
                _log.WriteLog("- " + response.StatusCode);
                _log.WriteLog("- Resp StatusCode = " + response.StatusCode);
                if (response.IsSuccessStatusCode)
                {
                    _log.WriteLog("- Resp IsSuccessStatusCode = " + response.IsSuccessStatusCode);
                    var ResultValues = response.Content.ReadAsStringAsync().Result;
                    _log.WriteLog("- Resp ResultValues = " + ResultValues);
                    res = JsonConvert.DeserializeObject<InterfaceResBodyTicketSummit>(ResultValues);
                }
                else
                {
                    throw new Exception(((int)response.StatusCode) + " : " + response.StatusCode);
                }

                model.ticket = res.ticket;
            }
            catch (Exception ex)
            {
                returnMsg = ex.Message;
                return false;
            }

            return true;
        }

        private bool RequestRateFloatingIndexSSMD(ref string returnMsg, ref List<FloatingIndexEntity> lstFloatModel, InterfaceFloatingIndexSummitModel model)
        {
            try
            {
                using (HttpClient Client = new HttpClient())
                {
                    Client.BaseAddress = new Uri(model.url_rate);
                    Client.DefaultRequestHeaders.Add("ref_id", model.ref_id);
                    Client.DefaultRequestHeaders.Add("request_date", model.request_date);
                    Client.DefaultRequestHeaders.Add("request_time", model.request_time);
                    Client.DefaultRequestHeaders.Add("mode", model.mode);
                    Client.DefaultRequestHeaders.Add("ticket", model.ticket);

                    var Values = new Dictionary<string, string>
                {
                    { "data_type", model.data_type},
                    { "as_of_date", model.as_of_date},
                    { "curve_id", model.curve_id},
                    { "ccy", model.ccy},
                    { "index", model.index}
                };

                    _log.WriteLog("- Req Header");
                    _log.WriteLog("- Req ref_id = " + model.ref_id);
                    _log.WriteLog("- Req request_date = " + model.request_date);
                    _log.WriteLog("- Req request_time = " + model.request_time);
                    _log.WriteLog("- Req mode = " + model.mode);
                    _log.WriteLog("- Req ticket = " + model.ticket);
                    _log.WriteLog("- ");
                    _log.WriteLog("- Req Body");
                    _log.WriteLog("- Req data_type = " + model.data_type);
                    _log.WriteLog("- Req as_of_date = " + model.as_of_date);
                    _log.WriteLog("- Req curve_id = " + model.curve_id);
                    _log.WriteLog("- Req ccy = " + model.ccy);
                    _log.WriteLog("- Req index = " + model.index);

                    var content = new FormUrlEncodedContent(Values);
                    var response = Client.PostAsync(model.url_rate, content).Result;
                    _log.WriteLog("- " + response.StatusCode);
                    _log.WriteLog("-Resp StatusCode = " + response.StatusCode);
                    if (response.IsSuccessStatusCode)
                    {
                        _log.WriteLog("-Resp IsSuccessStatusCode = " + response.IsSuccessStatusCode);
                        var result = response.Content.ReadAsStringAsync().Result;

                        _log.WriteLog("-Resp ResultValues = " + result);

                        if (result == "null")
                        {
                            throw new Exception("FloatingIndexRate not found");
                        }

                        lstFloatModel = JsonConvert.DeserializeObject<List<FloatingIndexEntity>>(result);
                    }
                    else
                    {
                        throw new Exception(((int)response.StatusCode) + " : " + response.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                returnMsg = ex.Message;
                return false;
            }

            return true;
        }

        private bool DeleteFloatingIndex(ref string returnMsg, InterfaceFloatingIndexSummitModel model)
        {
            try
            {
                //InterfaceFloatingIndexSummitReqModel req = model as InterfaceFloatingIndexSummitReqModel;
                //ResultWithModel rwm = _uow.External.InterfaceFloatingIndexSummit.Remove(req);

                var serialized = JsonConvert.SerializeObject(model);
                var child = JsonConvert.DeserializeObject<InterfaceFloatingIndexSummitReqModel>(serialized);
                ResultWithModel rwm = _uow.External.InterfaceFloatingIndexSummit.Remove(child);
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

        private bool InsertFloatingIndex(ref string returnMsg, FloatingIndexEntity floatModel, InterfaceFloatingIndexSummitModel model)
        {
            try
            {
                InterfaceFloatingIndexSummitReqModel item = new InterfaceFloatingIndexSummitReqModel();
                item.FloatModel = floatModel;
                item.create_by = model.create_by;
                ResultWithModel rwm = _uow.External.InterfaceFloatingIndexSummit.Add(item);

                if (!rwm.Success)
                {
                    throw new Exception("[" + rwm.RefCode + "] curve_id = " + floatModel.curve_id + " " + rwm.Message);
                }
            }
            catch (Exception ex)
            {
                returnMsg = ex.Message;
                return false;
            }

            return true;
        }

        private bool InsertLogRequest(ref string returnMsg)
        {
            try
            {
            }
            catch (Exception Ex)
            {
                returnMsg = Ex.Message;
                return false;
            }

            return true;
        }

        private bool InsertLogResponse(ref string returnMsg)
        {
            try
            {
            }
            catch (Exception Ex)
            {
                returnMsg = Ex.Message;
                return false;
            }

            return true;
        }

    }
}
