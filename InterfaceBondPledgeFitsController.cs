using GM.CommonLibs.Common;
using GM.CommonLibs.Helper;
using GM.DataAccess.UnitOfWork;
using GM.Model.Common;
using GM.Model.ExternalInterface;
using InterfaceBondPledgeFitsWSDL;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.ServiceModel;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class InterfaceBondPledgeFitsController : ControllerBase
    {
        private readonly IUnitOfWork _uow;
        private static LogFile _log;
        public InterfaceBondPledgeFitsController(IUnitOfWork uow)
        {
            _uow = uow;
            _log = new LogFile(_uow.GetConfiguration, "InterfaceBondPledgeFits");
        }

        [HttpPost]
        [Route("[action]")]
        public ResultWithModel ExportBondPledgeFits(InterfaceBondPledgeFitsModel model)
        {
            string strMsg = string.Empty;
            ServiceInOutReqModel inOutModel = new ServiceInOutReqModel();
            ResultWithModel rwm = new ResultWithModel();

            RespBondPledge respBondPledge = new RespBondPledge();
            ReqBondPledge ReqBondPledge = new ReqBondPledge();
            string JsonReqBondPledge = string.Empty;
            string JsonRespBondPledge = string.Empty;

            try
            {
                _log.WriteLog("Start ExportBondPledgeFITS ==========");
                // Step 1 : Set Config
                if (!Set_Config(ref strMsg, ref ReqBondPledge, model))
                {
                    throw new Exception("Set_Config() : " + strMsg);
                }

                // Step 2 : Select Data BondPledge & Set Request
                var res = new List<ResInterfaceBondPledgeFitsModel>();
                if (!Search_BondPledge(ref strMsg, ref res, model))
                {
                    throw new Exception("Search_BondPledge() : " + strMsg);
                }

                if (Set_RequestBondPledge(ref strMsg, ref ReqBondPledge, res) == false)
                {
                    throw new Exception("Set_RequestBondPledge() : " + strMsg);
                }

                // Step 3 : Call Service FITS
                WS_INTERFACE_NEW_REPOSoapClient WsFits = new WS_INTERFACE_NEW_REPOSoapClient(WS_INTERFACE_NEW_REPOSoapClient.EndpointConfiguration.WS_INTERFACE_NEW_REPOSoap);

                WsFits.Endpoint.Address = new EndpointAddress(model.ServiceUrl);
                WsFits.Endpoint.Binding.SendTimeout = TimeSpan.FromMilliseconds(model.ServiceTimeOut);

                respBondPledge = WsFits.Interface_RepoBondPledgeAsync(ReqBondPledge).Result;

                if (respBondPledge.return_code == 0)
                {
                    rwm.RefCode = 0;
                    rwm.Message = "Success";
                    rwm.Serverity = "low";
                    rwm.Success = true;
                }
                else
                {
                    rwm.RefCode = respBondPledge.return_code;
                    rwm.Message = respBondPledge.return_msg;
                    rwm.Serverity = "high";
                    rwm.Success = false;
                    _log.WriteLog("Error " + respBondPledge.return_msg);
                }
            }
            catch (Exception ex)
            {
                rwm.RefCode = -999;
                rwm.Message = ex.Message;
                rwm.Serverity = "high";
                rwm.Success = false;
                _log.WriteLog("Error " + ex.Message);
            }
            finally
            {
                // Step 4 : Insert Log In/Out
                if (!Set_LogInOut(ref strMsg, ref inOutModel, ReqBondPledge, respBondPledge))
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

                _log.WriteLog("End ExportBondPledgeFITS ==========");

                DataSet dsResult = new DataSet();
                DataTable dt = new List<InterfaceBondPledgeFitsModel>() { model }.ToDataTable<InterfaceBondPledgeFitsModel>();
                dt.TableName = "InterfaceBondPledgeFitsResultModel";
                dsResult.Tables.Add(dt);
                rwm.Data = dsResult;
            }

            return rwm;
        }

        private bool Search_BondPledge(ref string returnMsg, ref List<ResInterfaceBondPledgeFitsModel> res, InterfaceBondPledgeFitsModel model)
        {
            try
            {
                ResultWithModel rwm = _uow.External.InterfaceBondPledgeFits.Get(model);
                if (rwm.Success)
                {
                    if (((DataSet)rwm.Data).Tables.Count > 0)
                    {
                        res = ConvertHelper.DataTableToList<ResInterfaceBondPledgeFitsModel>(((DataSet)rwm.Data).Tables[0]);
                        model.BondPledgeTotal = res.Count;
                    }
                    else
                    {
                        res = new List<ResInterfaceBondPledgeFitsModel>();
                    }
                }
                else
                {
                    throw new Exception("[" + rwm.RefCode.ToString() + "] " + rwm.Message);
                }

                _log.WriteLog("- BondPledgeTotal = [" + model.BondPledgeTotal + "] Rows.");
            }
            catch (Exception ex)
            {
                returnMsg = ex.Message;
                return false;
            }

            return true;
        }

        private bool Set_RequestBondPledge(ref string returnMsg, ref ReqBondPledge reqBondPledge, List<ResInterfaceBondPledgeFitsModel> res)
        {
            try
            {
                List<BondPledgeDetail> listDetail = new List<BondPledgeDetail>();

                foreach (var item in res)
                {
                    BondPledgeDetail model = new BondPledgeDetail();

                    model.asof_date = item.asof_date;
                    model.instrument_code = item.instrument_code;
                    model.port = item.port;
                    model.pledge_coll_unit = item.pledge_coll_unit;
                    model.pledge_coll_cost = item.pledge_coll_cost;
                    model.pledge_Coll_Booking = item.pledge_coll_booking;
                    listDetail.Add(model);
                }

                reqBondPledge.Detail = listDetail.ToArray();
            }
            catch (Exception ex)
            {
                returnMsg = ex.Message;
                return false;
            }

            return true;
        }

        private bool Set_Config(ref string returnMsg, ref ReqBondPledge reqBondPledge, InterfaceBondPledgeFitsModel model)
        {
            try
            {
                BondPledgeHeader Header = new BondPledgeHeader();

                Header.channel_id = model.RPConfigModel.FirstOrDefault(a => a.item_code == "CHANNEL")?.item_value;
                Header.ref_no = Guid.NewGuid().ToString().ToUpper();
                Header.asof_date = model.AsOfDate.ToString("yyyyMMdd");
                Header.request_date = DateTime.Now.ToString("yyyyMMdd");
                Header.request_time = DateTime.Now.ToString("HH:mm:ss");
                Header.mode = model.RPConfigModel.FirstOrDefault(a => a.item_code == "MODE")?.item_value;

                reqBondPledge.Header = Header;
            }
            catch (Exception Ex)
            {
                returnMsg = Ex.Message;
                return false;
            }

            return true;
        }

        private bool Set_LogInOut(ref string ReturnMsg, ref ServiceInOutReqModel InOutModel, ReqBondPledge ReqBondPledge, RespBondPledge RespBondPledge)
        {
            try
            {
                InOutModel.guid = ReqBondPledge.Header.ref_no;
                InOutModel.svc_req = JsonConvert.SerializeObject(ReqBondPledge);
                InOutModel.svc_res = JsonConvert.SerializeObject(RespBondPledge);
                InOutModel.svc_type = "OUT";
                InOutModel.module_name = "InterfaceBondPledgeFitsController";
                InOutModel.action_name = "ExportBondPledgeFITS";
                InOutModel.ref_id = ReqBondPledge.Header.ref_no;
                InOutModel.status = RespBondPledge.return_code.ToString();
                InOutModel.status_desc = RespBondPledge.return_msg;
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
    }
}