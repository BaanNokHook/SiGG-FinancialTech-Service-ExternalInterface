using GM.CommonLibs.Common;
using GM.CommonLibs.Helper;
using GM.DataAccess.UnitOfWork;
using GM.Model.Common;
using GM.Model.ExternalInterface;
using GM.Model.ExternalInterface.InterfaceEquityPledge;
using InterfaceBondPledgeEquityWSDL;
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
    public class InterfaceEquityPledgeController : ControllerBase
    {
        private readonly IUnitOfWork _uow;
        private static LogFile _log;
        public InterfaceEquityPledgeController(IUnitOfWork uow)
        {
            _uow = uow;
            _log = new LogFile(_uow.GetConfiguration, "InterfaceEquityPledgeController");
        }

        [HttpPost]
        [Route("[action]")]
        public ResultWithModel InterfaceEquityPledge(InterfaceEquityPledgeModel model)
        {
            string strMsg = string.Empty;
            ServiceInOutReqModel inOutModel = new ServiceInOutReqModel();
            ResultWithModel rwm = new ResultWithModel();

            ResBondPledge resBondPledge = new ResBondPledge();
            ReqBondPledge reqBondPledge = new ReqBondPledge();
            string JsonReqBondPledge = string.Empty;
            string JsonRespBondPledge = string.Empty;

            try
            {
                _log.WriteLog("Start InterfaceEquityPledge ==========");
                // Step 1 : Set Config
                if (!Set_Config(ref strMsg, ref reqBondPledge, model))
                {
                    throw new Exception("Set_Config() : " + strMsg);
                }

                // Step 2 : Select Data BondPledge & Set Request
                var res = new List<ResInterfaceEquityPledgeModel>();
                if (!Search_EquityPledge(ref strMsg, ref res, model))
                {
                    throw new Exception("Search_BondPledge() : " + strMsg);
                }

                if (Set_RequestBondPledge(ref strMsg, ref reqBondPledge, res) == false)
                {
                    throw new Exception("Set_RequestBondPledge() : " + strMsg);
                }

                if (reqBondPledge.Detail.Length == 0)
                {
                    rwm.RefCode = 0;
                    rwm.Message = "EquityPledge Not Found.";
                    rwm.Serverity = "low";
                    rwm.Success = true;
                    return rwm;
                }

                // Step 3 : Call Service FITS
                WS_INTERFACE_BOND_PLEDGESoapClient WsEquity = new WS_INTERFACE_BOND_PLEDGESoapClient(WS_INTERFACE_BOND_PLEDGESoapClient.EndpointConfiguration.WS_INTERFACE_BOND_PLEDGESoap);

                WsEquity.Endpoint.Address = new EndpointAddress(model.ServiceUrl);
                WsEquity.Endpoint.Binding.SendTimeout = TimeSpan.FromMilliseconds(model.ServiceTimeOut);

                resBondPledge = WsEquity.Interface_RepoBondPledgeAsync(reqBondPledge).Result;

                if (resBondPledge.return_code == 0)
                {
                    rwm.RefCode = 0;
                    rwm.Message = "Success";
                    rwm.Serverity = "low";
                    rwm.Success = true;
                }
                else
                {
                    rwm.RefCode = resBondPledge.return_code;
                    rwm.Message = resBondPledge.return_msg;
                    rwm.Serverity = "high";
                    rwm.Success = false;
                    _log.WriteLog("Error " + resBondPledge.return_msg);
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
                if (!Set_LogInOut(ref strMsg, ref inOutModel, reqBondPledge, resBondPledge))
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

                _log.WriteLog("End InterfaceEquityPledge ==========");

                DataSet dsResult = new DataSet();
                DataTable dt = new List<InterfaceEquityPledgeModel>() { model }.ToDataTable<InterfaceEquityPledgeModel>();
                dt.TableName = "InterfaceEquityPledgeResultModel";
                dsResult.Tables.Add(dt);
                rwm.Data = dsResult;
            }

            return rwm;
        }

        private bool Search_EquityPledge(ref string returnMsg, ref List<ResInterfaceEquityPledgeModel> res, InterfaceEquityPledgeModel model)
        {
            try
            {
                ResultWithModel rwm = _uow.External.InterfaceEquityPledge.Get(model);
                if (rwm.Success)
                {
                    if (((DataSet)rwm.Data).Tables.Count > 0)
                    {
                        res = ConvertHelper.DataTableToList<ResInterfaceEquityPledgeModel>(((DataSet)rwm.Data).Tables[0]);
                        model.BondPledgeTotal = res.Count;
                    }
                    else
                    {
                        res = new List<ResInterfaceEquityPledgeModel>();
                    }
                }
                else
                {
                    throw new Exception("[" + rwm.RefCode.ToString() + "] " + rwm.Message);
                }

                _log.WriteLog("- PledgeTotal = [" + model.BondPledgeTotal + "] Rows.");
            }
            catch (Exception ex)
            {
                returnMsg = ex.Message;
                return false;
            }

            return true;
        }

        private bool Set_RequestBondPledge(ref string returnMsg, ref ReqBondPledge reqPledge, List<ResInterfaceEquityPledgeModel> res)
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

                reqPledge.Detail = listDetail.ToArray();
            }
            catch (Exception ex)
            {
                returnMsg = ex.Message;
                return false;
            }

            return true;
        }

        private bool Set_Config(ref string returnMsg, ref ReqBondPledge reqPledge, InterfaceEquityPledgeModel model)
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

                reqPledge.Header = Header;
            }
            catch (Exception Ex)
            {
                returnMsg = Ex.Message;
                return false;
            }

            return true;
        }

        private bool Set_LogInOut(ref string ReturnMsg, ref ServiceInOutReqModel InOutModel, ReqBondPledge reqPledge, ResBondPledge respPledge)
        {
            try
            {
                InOutModel.guid = reqPledge.Header.ref_no;
                InOutModel.svc_req = JsonConvert.SerializeObject(reqPledge);
                InOutModel.svc_res = JsonConvert.SerializeObject(respPledge);
                InOutModel.svc_type = "OUT";
                InOutModel.module_name = "InterfaceEquityPledgeController";
                InOutModel.action_name = "InterfaceEquityPledge";
                InOutModel.ref_id = reqPledge.Header.ref_no;
                InOutModel.status = respPledge.return_code.ToString();
                InOutModel.status_desc = respPledge.return_msg;
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