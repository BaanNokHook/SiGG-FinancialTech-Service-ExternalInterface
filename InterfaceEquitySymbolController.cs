using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using GM.CommonLibs.Common;
using GM.DataAccess.UnitOfWork;
using GM.Model.Common;
using GM.Model.ExternalInterface;
using GM.Model.ExternalInterface.InterfaceEquitySymbol;
using InterfaceEquityDefWSDL;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class InterfaceEquitySymbolController : ControllerBase
    {
        private readonly IUnitOfWork _uow;
        private static LogFile _log;

        public InterfaceEquitySymbolController(IUnitOfWork uow)
        {
            _uow = uow;
            _log = new LogFile(_uow.GetConfiguration, "InterfaceEquitySymbol");
        }

        [HttpPost]
        [Route("ImportEquitySymbol")]
        public ResultWithModel ImportEquitySymbol(ReqEquitySymbol model)
        {
            ResultWithModel res = new ResultWithModel();
            ResEquitySymbol resEquitySymbol = new ResEquitySymbol();

            _log.WriteLog("Start ImportEquitySymbol ==========");

            try
            {
                _log.WriteLog(" - channel_id = " + model.datas.channel_id);
                _log.WriteLog(" - ref_no = " + model.datas.ref_no);
                _log.WriteLog(" - total_rec = " + model.datas.total_rec.ToString() + " Rows");
                _log.WriteLog(" - asof_date = " + model.datas.asof_date);
                _log.WriteLog(" - asof_time = " + model.datas.asof_time);
                _log.WriteLog(" - create_by = " + model.datas.create_by);

                _uow.BeginTransaction();

                _log.WriteLog("Step 1 : Check Exists & Move EquitySymbol To History");
                res = _uow.External.InterfaceEquitySymbol.Remove(model);
                if (!res.Success)
                {
                    throw new Exception("InterfaceEquitySymbol.Remove() : " + res.Message);
                }
                _log.WriteLog(" - Check & Move Success.");

                _log.WriteLog("Step 2 : Insert EquitySymbol To Temp");
                foreach (var reqEquitySymbolList in model.datas.EquitySymbol)
                {
                    // Step 4 : Insert InterfaceEquitySymbol(Temp)
                    res = _uow.External.InterfaceEquitySymbol.Add(reqEquitySymbolList);
                    if (!res.Success)
                    {
                        throw new Exception("InterfaceEquitySymbol.Add() : [" + reqEquitySymbolList.instrument_code + "] " + res.Message);
                    }
                }
                _log.WriteLog(" - Insert Success.");
                _uow.Commit();

                resEquitySymbol.response_code = "0";
                resEquitySymbol.response_message = "Import EquitySymbol Temp Success.";
            }
            catch (Exception ex)
            {
                _uow.Rollback();
                resEquitySymbol.response_code = "-999";
                resEquitySymbol.response_message = "Fail : " + ex.Message;
                _log.WriteLog(" - [" + resEquitySymbol.response_code + "] " + resEquitySymbol.response_message);
            }

            try
            {
                _log.WriteLog("Step 3 : Import EquitySymbol To GM_security_def");
                if (resEquitySymbol.response_code == "0")
                {
                    res = _uow.External.InterfaceEquitySymbol.Import(model);
                    if (!res.Success)
                    {
                        resEquitySymbol.response_code = "-999";
                        resEquitySymbol.response_message = "Fail InterfaceEquitySymbol.Import() : " + res.Message;
                        _log.WriteLog(" - [" + resEquitySymbol.response_code + "] " + resEquitySymbol.response_message);
                    }
                    else
                    {
                        resEquitySymbol.response_code = "0";
                        resEquitySymbol.response_message = "Import EquitySymbol Success.";
                        _log.WriteLog(" - Insert Success.");
                    }
                }
            }
            catch (Exception ex)
            {
                resEquitySymbol.response_code = "-999";
                resEquitySymbol.response_message = "Fail : " + ex.Message;
            }

            resEquitySymbol.refcode = model.datas.ref_no;
            resEquitySymbol.count_data = model.datas.EquitySymbol.Count.ToString();

            res.Data = resEquitySymbol;

            _log.WriteLog("End ImportEquitySymbol ==========");
            return res;
        }

        [HttpPost]
        [Route("InterfaceEquitySymbol")]
        public ResultWithModel InterfaceEquitySymbol(InterfaceReqEquitySymbolModel reqModel)
        {
            string strMsg = string.Empty;
            ResultWithModel res = new ResultWithModel();
            ReqEquitySymbol model = new ReqEquitySymbol();
            model.datas = new ReqEquitySymbolHeader();
            InterfaceResEquitySymbolModel resModel = new InterfaceResEquitySymbolModel();

            _log.WriteLog("Start InterfaceEquitySymbol ==========");
            try
            {
                _log.WriteLog("Step 1 : Request EquitySymbol");
                if (Request_EquitySymbol(reqModel, ref resModel) == false)
                {
                    throw new Exception("Request_EquitySymbol() : [" + resModel.ReturnCode + "] " + resModel.ReturnMsg);
                }
                _log.WriteLog(" - Request Success.");

                if (resModel.TotalRows == 0)
                {
                    res.RefCode = 1;
                    res.Message = "Success : EquitySymbol [" + reqModel.code + "] Not Found From Service.";
                    res.Serverity = "low";
                    res.Success = true;
                    _log.WriteLog(res.Message);
                    return res;
                }

                int count_item = 0;
                foreach (var reqEquitySymbolList in resModel.Detail)
                {
                    if (reqModel.code == reqEquitySymbolList.instrument_code)
                    {
                        count_item = count_item + 1;
                    }
                }

                if (count_item == 0)
                {
                    res.RefCode = 1;
                    res.Message = "Success : EquitySymbol [" + reqModel.code + "] Not Found From Service.";
                    res.Serverity = "low";
                    res.Success = true;
                    _log.WriteLog(res.Message);
                    return res;
                }

                try
                {
                    _uow.BeginTransaction();

                    _log.WriteLog("Step 2 : Check Exists & Move EquitySymbol To History");

                    model.datas.ref_no = reqModel.ref_no;
                    model.datas.asof_date = reqModel.asof_date.ToString("yyyyMMdd");
                    model.datas.create_by = reqModel.create_by;

                    res = _uow.External.InterfaceEquitySymbol.Remove(model);
                    if (!res.Success)
                    {
                        throw new Exception("InterfaceEquitySymbol.Remove() : " + res.Message);
                    }
                    _log.WriteLog(" - Check & Move Success.");

                    _log.WriteLog("Step 3 : Insert EquitySymbol To Temp");
                    foreach (var reqEquitySymbolList in resModel.Detail)
                    {
                        reqEquitySymbolList.ref_no = reqModel.ref_no;
                        reqEquitySymbolList.asof_date = reqModel.asof_date.ToString("yyyyMMdd");

                        res = _uow.External.InterfaceEquitySymbol.Add(reqEquitySymbolList);
                        if (!res.Success)
                        {
                            throw new Exception("InterfaceEquitySymbol.Add() : [" + reqEquitySymbolList.instrument_code + "] " + res.Message);
                        }
                    }
                    _uow.Commit();
                    _log.WriteLog(" - Insert Success.");

                }
                catch (Exception ex)
                {
                    _uow.Rollback();
                    res.RefCode = -999;
                    res.Message = "Fail : " + ex.Message;
                    res.Serverity = "high";
                    res.Success = false;
                    return res;
                }

                _log.WriteLog("Step 4 : Import EquitySymbol To GM_security_def");

                res = _uow.External.InterfaceEquitySymbol.Import(model);
                if (!res.Success)
                {
                    throw new Exception("InterfaceEquitySymbol.Import() : " + res.Message);
                }
                _log.WriteLog(" - Insert Success.");

                res.RefCode = 0;
                res.Message = "Import Success";
                res.Serverity = "low";
                res.Success = true;
            }
            catch (Exception ex)
            {
                res.RefCode = -999;
                res.Message = "Fail : " + ex.Message;
                res.Serverity = "high";
                res.Success = false;
            }
            finally
            {
                _log.WriteLog("Step 5 : Insert LogInOut");
                ServiceInOutReqModel inOutModel = new ServiceInOutReqModel();

                if (!Set_LogInOut(ref strMsg, ref inOutModel, reqModel, resModel))
                {
                    res.RefCode = -999;
                    res.Message = "Fail : Set_LogInOut() : " + strMsg;
                    res.Serverity = "high";
                    res.Success = false;
                    _log.WriteLog(" - Error " + res.Message);
                }

                if (!Insert_LogInOut(ref strMsg, inOutModel))
                {
                    res.RefCode = -999;
                    res.Message = "Fail : Insert_LogInOut() : " + strMsg;
                    res.Serverity = "high";
                    res.Success = false;
                    _log.WriteLog(" - Error " + res.Message);
                }

                _log.WriteLog(" - Insert Success.");

                _log.WriteLog("End InterfaceEquitySymbol ==========");
            }

            return res;

        }

        private bool Request_EquitySymbol(InterfaceReqEquitySymbolModel reqModel, ref InterfaceResEquitySymbolModel resModel)
        {
            WS_EQUITY_DEF_INFSoapClient wsEquityDef = new WS_EQUITY_DEF_INFSoapClient(WS_EQUITY_DEF_INFSoapClient.EndpointConfiguration.WS_EQUITY_DEF_INFSoap);
            GetEquityDefRequest reqEquityDef = new GetEquityDefRequest();
            reqEquityDef.req = new ReqGetEquityDef();
            resModel = new InterfaceResEquitySymbolModel();

            try
            {
                reqEquityDef.req.Channel = reqModel.channel;
                reqEquityDef.req.RequestDate = reqModel.request_date;
                reqEquityDef.req.RequestTime = reqModel.request_time; 
                reqEquityDef.req.Code = reqModel.code;
                reqEquityDef.req.Type = reqModel.type;

                reqEquityDef.req.IssuerCode = reqModel.issuer_code;
                reqEquityDef.req.ParFrom = reqModel.par_from;
                reqEquityDef.req.ParTo = reqModel.par_to;
                reqEquityDef.req.IssueDate = reqModel.issue_date;
                reqEquityDef.req.Product = reqModel.product;
                reqEquityDef.req.SubProductCode = reqModel.sub_product_code;
                reqEquityDef.req.Cur = reqModel.cur;

                wsEquityDef.Endpoint.Address = new EndpointAddress(reqModel.url_service);
                var task = wsEquityDef.GetEquityDefAsync(reqEquityDef).Result;
                resModel = JsonConvert.DeserializeObject<InterfaceResEquitySymbolModel>(task.GetEquityDefResult.ToString());

                if (resModel.ReturnCode != 0)
                {
                    return false;
                }

            }
            catch (Exception Ex)
            {
                resModel.ReturnCode = 999;
                resModel.ReturnMsg = Ex.Message;
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

        private bool Set_LogInOut(ref string ReturnMsg, ref ServiceInOutReqModel InOutModel, InterfaceReqEquitySymbolModel ReqEquitySymbolModel, InterfaceResEquitySymbolModel ResEquitySymbolModel)
        {
            try
            {
                InOutModel.guid = ReqEquitySymbolModel.ref_no;
                InOutModel.svc_req = JsonConvert.SerializeObject(ReqEquitySymbolModel);
                InOutModel.svc_res = JsonConvert.SerializeObject(ResEquitySymbolModel);
                InOutModel.svc_type = "IN";
                InOutModel.module_name = "InterfaceEquitySymbolController";
                InOutModel.action_name = "InterfaceEquitySymbol";
                InOutModel.ref_id = ReqEquitySymbolModel.ref_no;
                InOutModel.status = ResEquitySymbolModel.ReturnCode.ToString();
                InOutModel.status_desc = ResEquitySymbolModel.ReturnMsg;
                InOutModel.create_by = "WebService";
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