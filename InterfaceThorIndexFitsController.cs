using GM.CommonLibs;
using GM.CommonLibs.Common;
using GM.DataAccess.UnitOfWork;
using GM.Model.Common;
using GM.Model.ExternalInterface;
using GM.Model.ExternalInterface.InterfaceThorIndex;
using InterfaceBondPledgeFitsWSDL;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.ServiceModel;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class InterfaceThorIndexFitsController : ControllerBase
    {
        private readonly IUnitOfWork _uow;
        private static LogFile _log;
        public InterfaceThorIndexFitsController(IUnitOfWork uow)
        {
            _uow = uow;
            _log = new LogFile(_uow.GetConfiguration, "InterfaceThorIndexFits");
        }

        [HttpPost]
        [Route("[action]")]
        public ResultWithModel ImportThorIndexFits(InterfaceReqThorIndexFitsModel model)
        {
            string strMsg = string.Empty;
            ServiceInOutReqModel inOutModel = new ServiceInOutReqModel();
            ResultWithModel rwm = new ResultWithModel();
            ThorIndexReq thorIndexModel = new ThorIndexReq();
            InterfaceResThorIndexFitsModel res = new InterfaceResThorIndexFitsModel();

            try
            {
                _log.WriteLog("Start ImportThorIndexFits ==========");
                // Step 1 : Set Config
                if (!Set_Config(ref strMsg, ref thorIndexModel, model))
                {
                    throw new Exception("Set_Config() : " + strMsg);
                }

                // Step 2 : Call Service FITS
                WS_INTERFACE_NEW_REPOSoapClient WsFits = new WS_INTERFACE_NEW_REPOSoapClient(WS_INTERFACE_NEW_REPOSoapClient.EndpointConfiguration.WS_INTERFACE_NEW_REPOSoap);

                WsFits.Endpoint.Address = new EndpointAddress(model.ServiceUrl);
                WsFits.Endpoint.Binding.SendTimeout = TimeSpan.FromMilliseconds(model.ServiceTimeOut);

                string resText = WsFits.Interface_RepoThorIndexAsync(thorIndexModel).Result;
                res = JsonConvert.DeserializeObject<InterfaceResThorIndexFitsModel>(resText);

                _log.WriteLog("Data : " + JsonConvert.SerializeObject(res, Formatting.Indented));

                // Step 3 : Import Data to REPO
                if (res.ReturnCode == 0)
                {
                    rwm.RefCode = 0;
                    rwm.Message = "Success";
                    rwm.Serverity = "low";
                    rwm.Success = true;


                    if (res.Data != null && res.Data.Count > 0)
                    {
                        _log.WriteLog("ImportThorIndexFits To Repo");

                        _uow.BeginTransaction();
                        if (!ClearTemp(ref strMsg, model))
                        {
                            throw new Exception("ImportThorIndexFits() = > Clear Temp : " + strMsg);
                        }

                        foreach (var item in res.Data)
                        {
                            if (!Processing(ref strMsg, item, model.create_by))
                            {
                                throw new Exception("ImportThorIndexFits() = > Processing : " + strMsg);
                            }
                        }
                        _uow.Commit();

                        _uow.BeginTransaction();
                        if (!Import(ref strMsg, model))
                        {
                            throw new Exception("ImportThorIndexFits() = > Import : " + strMsg);
                        }
                        _uow.Commit();

                        _log.WriteLog("Import Rate Success.");

                        rwm.RefCode = 0;
                        rwm.HowManyRecord = res.Data.Count;
                        rwm.Message = "Import into table GM_thor_index_cal Success.";
                        rwm.Success = true;
                    }

                }
                else
                {
                    rwm.RefCode = res.ReturnCode;
                    rwm.Message = res.Msg;
                    rwm.Serverity = "high";
                    rwm.Success = false;
                    _log.WriteLog("Error " + res.Msg);
                }
            }
            catch (Exception ex)
            {
                _uow.Rollback();
                rwm.RefCode = -999;
                rwm.Message = ex.Message;
                rwm.Serverity = "high";
                rwm.Success = false;
                _log.WriteLog("Error ImportThorIndexFits() : " + ex.Message);
            }
            finally
            {
                // Step 4 : Insert Log In/Out
                if (!Set_LogInOut(ref strMsg, ref inOutModel, thorIndexModel, res))
                {
                    rwm.RefCode = -999;
                    rwm.Message = "Set_LogInOut() : " + strMsg;
                    rwm.Serverity = "high";
                    rwm.Success = false;
                    _log.WriteLog("Error " + rwm.Message);
                }

                if (!Insert_LogInOut(ref strMsg, inOutModel))
                {
                    rwm.RefCode = -999;
                    rwm.Message = "Insert_LogInOut() : " + strMsg;
                    rwm.Serverity = "high";
                    rwm.Success = false;
                    _log.WriteLog("Error " + rwm.Message);
                }

                _log.WriteLog("End ImportThorIndexFits ==========");
            }

            return rwm;
        }

        private bool ClearTemp(ref string returnMsg, InterfaceReqThorIndexFitsModel iModel)
        {
            try
            {
                ThorIndexModel model = new ThorIndexModel()
                {
                    create_by = iModel.create_by,
                    next_business_date = iModel.AsOfDate.ToString("yyyyMMdd")
                };
                ResultWithModel rwm = _uow.External.InterfaceThorIndex.Remove(model);
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

        private bool Processing(ref string returnMsg, ThorIndexModel model, string userId)
        {
            try
            {
                model.create_by = userId;
                ResultWithModel rwm = _uow.External.InterfaceThorIndex.Add(model);
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

        private bool Import(ref string returnMsg, InterfaceReqThorIndexFitsModel iModel)
        {
            try
            {
                ThorIndexModel model = new ThorIndexModel()
                {
                    create_by = iModel.create_by,
                    next_business_date = iModel.AsOfDate.ToString("yyyyMMdd")
                };
                ResultWithModel rwm = _uow.External.InterfaceThorIndex.Update(model);
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

        private bool Set_Config(ref string returnMsg, ref ThorIndexReq req, InterfaceReqThorIndexFitsModel model)
        {
            try
            {
                req.channel_id = model.RPConfigModel.FirstOrDefault(a => a.item_code == "CHANNEL")?.item_value;
                req.asof_date_from = model.AsOfDate.ToString("yyyy-MM-dd");
                req.asof_date_to = model.AsOfDate.ToString("yyyy-MM-dd");
                req.ref_no = Guid.NewGuid().ToString().ToUpper();
                req.request_date = DateTime.Now.ToString("yyyy-MM-dd");
                req.request_time = DateTime.Now.ToString("HH:mm:ss");
                req.mode = model.RPConfigModel.FirstOrDefault(a => a.item_code == "MODE")?.item_value;

                model.ServiceUrl = model.RPConfigModel.FirstOrDefault(a => a.item_code == "SERVICE_URL")?.item_value;
                if (model.RPConfigModel.FirstOrDefault(a => a.item_code == "SERVICE_TIMEOUT") != null)
                {
                    model.ServiceTimeOut = Convert.ToInt32(model.RPConfigModel.FirstOrDefault(a => a.item_code == "SERVICE_TIMEOUT")?.item_value);
                }
            }
            catch (Exception Ex)
            {
                returnMsg = Ex.Message;
                return false;
            }

            return true;
        }

        private bool Set_LogInOut(ref string ReturnMsg, ref ServiceInOutReqModel InOutModel, ThorIndexReq model, InterfaceResThorIndexFitsModel res)
        {
            try
            {
                InOutModel.guid = model.ref_no;
                InOutModel.svc_req = JsonConvert.SerializeObject(model);
                InOutModel.svc_res = JsonConvert.SerializeObject(new InterfaceResThorIndexFitsModel()
                {
                    ReturnCode = res.ReturnCode,
                    Msg = res.Msg,
                    Serverity = res.Serverity,
                    HowManyRecord = res.HowManyRecord
                }, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                InOutModel.svc_type = "OUT";
                InOutModel.module_name = "InterfaceThorIndexFitsController";
                InOutModel.action_name = "ImportThorIndexFits";
                InOutModel.ref_id = model.ref_no;
                InOutModel.status = res.ReturnCode.ToString();
                InOutModel.status_desc = res.Msg;
                InOutModel.create_by = "WebService";
            }
            catch (Exception Ex)
            {
                ReturnMsg = Ex.Message;
                return false;
            }

            return true;
        }

        private bool Insert_LogInOut(ref string returnMsg, ServiceInOutReqModel model)
        {
            try
            {
                ResultWithModel rwm = _uow.External.ServiceInOutReq.Add(model);
                if (!rwm.Success)
                {
                    throw new Exception("[" + rwm.RefCode.ToString() + "] " + rwm.Message);
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
        [Route("GetThorIndexFITS")]
        public ResultWithModel GetThorIndexFITS(ThorIndexModel model)
        {
            return _uow.External.InterfaceThorIndex.Get(model);
        }

    }
}