using System;
using System.Data;
using System.ServiceModel;
using GM.CommonLibs.Common;
using GM.DataAccess.UnitOfWork;
using GM.Model.Common;
using GM.Model.ExternalInterface.InterfaceNavPrice;
using GM.Model.InterfaceNavPrice;
using InterfaceNavPriceEquityWSDL;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class InterfaceNavPriceController : ControllerBase
    {
        private readonly IUnitOfWork _uow;
        private static LogFile _log;

        public InterfaceNavPriceController(IUnitOfWork uow)
        {
            _uow = uow;
            _log = new LogFile(_uow.GetConfiguration, "InterfaceNavPrice");
        }

        [HttpPost]
        [Route("ImportNavPrice")]
        public ResultWithModel ImportNavPrice(ReqNavPrice model)
        {
            ResultWithModel res = new ResultWithModel();
            ResNavPrice resNavPrice = new ResNavPrice();

            _log.WriteLog("Start ImportNavPrice ==========");
            try
            {
                _log.WriteLog(" - channel_id = " + model.datas.channel_id);
                _log.WriteLog(" - ref_no = " + model.datas.ref_no);
                _log.WriteLog(" - total_rec = " + model.datas.total_rec.ToString() + " Rows");
                _log.WriteLog(" - asof_date = " + model.datas.asof_date);
                _log.WriteLog(" - asof_time = " + model.datas.asof_time);
                _log.WriteLog(" - create_by = " + model.datas.create_by);

                _uow.BeginTransaction();

                _log.WriteLog("Step 1 : Check Exists & Move NavPrice To History");
                res = _uow.External.InterfaceNavPrice.Remove(model);
                if (!res.Success)
                {
                    throw new Exception("InterfaceNavPrice.Remove() : " + res.Message);
                }
                _log.WriteLog(" - Check & Move Success.");

                _log.WriteLog("Step 2 : Insert NavPrice To Temp");
                foreach (var reqNavPriceList in model.datas.NavPrice)
                {
                    res = _uow.External.InterfaceNavPrice.Add(reqNavPriceList);
                    if (!res.Success)
                    {
                        throw new Exception("InterfaceNavPrice.Add() : [" + reqNavPriceList.symbol + "] " + res.Message);
                    }
                }
                _log.WriteLog(" - Insert Success.");
                _uow.Commit();

                resNavPrice.response_code = "0";
                resNavPrice.response_message = "Import NavPrice Temp Success.";
            }
            catch (Exception ex)
            {
                _uow.Rollback();
                resNavPrice.response_code = "-999";
                resNavPrice.response_message = "Fail : " + ex.Message;
                _log.WriteLog(" - [" + resNavPrice.response_code + "] " + resNavPrice.response_message);
            }

            try
            {
                _log.WriteLog("Step 3 : Import NavPrice To Market Price");
                if (resNavPrice.response_code == "0")
                {
                    res = _uow.External.InterfaceNavPrice.Import(model);
                    if (!res.Success)
                    {
                        resNavPrice.response_code = "-999";
                        resNavPrice.response_message = "Fail InterfaceNavPrice.Import() : " + res.Message;
                        _log.WriteLog(" - [" + resNavPrice.response_code + "] " + resNavPrice.response_message);
                    }
                    else
                    {
                        resNavPrice.response_code = "0";
                        resNavPrice.response_message = "Import NavPrice Success.";
                        _log.WriteLog(" - Insert Success.");
                    }
                }
            }
            catch (Exception ex)
            {
                resNavPrice.response_code = "-999";
                resNavPrice.response_message = "Fail : " + ex.Message;
            }

            resNavPrice.refcode = model.datas.ref_no;
            resNavPrice.count_data = model.datas.NavPrice.Count.ToString();

            res.Data = resNavPrice;

            _log.WriteLog("End ImportNavPrice ==========");
            return res;
        }

        [HttpPost]
        [Route("InterfaceNavPriceEquity")]
        public ResultWithModel InterfaceNavPriceEquity(InterfaceReqNavPriceModel reqModel)
        {
            ResultWithModel res = new ResultWithModel();
            InterfaceResNavPriceModel resModel = new InterfaceResNavPriceModel();
            string reRun = "";

            _log.WriteLog("Start InterfaceNavPriceEquity ==========");
            try
            {
                _log.WriteLog(" - ref_no = " + reqModel.ref_no);
                _log.WriteLog(" - channel = " + reqModel.channel);
                _log.WriteLog(" - asof_date = " + reqModel.asof_date);
                _log.WriteLog(" - request_date = " + reqModel.request_date);
                _log.WriteLog(" - request_time = " + reqModel.request_time);
                _log.WriteLog(" - insuer_code = " + reqModel.insuer_code);
                _log.WriteLog(" - insument_type = " + reqModel.insument_type);
                _log.WriteLog(" - url_service = " + reqModel.url_service);

                _log.WriteLog("Step 0 : Check NavPrice");
                if (Check_NavPrice(ref reqModel, ref reRun) == false)
                {
                    throw new Exception("Check_MarketPriceTbma() : [" + reqModel.return_code + "] " + reqModel.return_msg);
                }

                _log.WriteLog("Check NavPrice[" + reqModel.asof_date + "] Success.");
                _log.WriteLog("- reRun = " + reRun);

                if (reRun == "Y")
                {
                    _log.WriteLog("Step 1 : Request NavPrice");
                    if (Request_NavPrice(reqModel, ref resModel) == false)
                    {
                        throw new Exception("Request_EquitySymbol() : [" + resModel.ReturnCode + "] " + resModel.ReturnMsg);
                    }
                    _log.WriteLog("Request NavPrice = Success.");

                    if (resModel.TotalRows == 0)
                    {
                        res.RefCode = 0;
                        res.Message = "Success : NavPrice Not Found From Service.";
                        res.Serverity = "low";
                        res.Success = true;
                        _log.WriteLog(res.Message);
                        return res;
                    }

                    try
                    {
                        _uow.BeginTransaction();

                        _log.WriteLog("Step 2 : Check Exists & Move NavPrice To History");
                        res = _uow.External.InterfaceNavPriceEquity.Remove(reqModel);
                        if (!res.Success)
                        {
                            throw new Exception("InterfaceNavPriceEquity.Remove() : " + res.Message);
                        }
                        _log.WriteLog(" - Check & Move Success.");

                        _log.WriteLog("Step 3 : Insert NavPrice To Temp");

                        foreach (var reqNavPriceList in resModel.Detail)
                        {
                            res = _uow.External.InterfaceNavPriceEquity.Add(reqModel, reqNavPriceList);
                            if (!res.Success)
                            {
                                throw new Exception("InterfaceNavPriceEquity.Add() : [" + reqNavPriceList.symbols + "] " + res.Message);
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

                    _log.WriteLog("Step 4 : Import NavPrice To Market Price");
                    res = _uow.External.InterfaceNavPriceEquity.Import(reqModel);
                    if (!res.Success)
                    {
                        throw new Exception("InterfaceNavPriceEquity.Import() : " + res.Message);
                    }
                    _log.WriteLog(" - Insert Success.");

                    res.RefCode = 0;
                    res.Message = "Import Nav Price Success.";
                    res.Success = true;
                }
                else
                {
                    res.RefCode = 0;
                    res.Message = "Success: NavPrice [" + reqModel.asof_date + "] Is Exists. [Console]";
                    res.Success = true;

                    _log.WriteLog("Success: NavPrice [" + reqModel.asof_date + "] Is Exists. [Console]");

                    return res;
                }

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
                _log.WriteLog("End InterfaceNavPriceEquity ==========");
            }

            return res;
        }

        private bool Request_NavPrice(InterfaceReqNavPriceModel reqModel, ref InterfaceResNavPriceModel resModel)
        {
            WS_EQUITY_NAV_INFSoapClient wsEquityNav = new WS_EQUITY_NAV_INFSoapClient(WS_EQUITY_NAV_INFSoapClient.EndpointConfiguration.WS_EQUITY_NAV_INFSoap);
            GetPriceRequest reqEquityNav = new GetPriceRequest();
            reqEquityNav.req = new ReqGetEquityNav();
            resModel = new InterfaceResNavPriceModel();

            try
            {
                reqEquityNav.req.RefNo = reqModel.ref_no;
                reqEquityNav.req.Channel = reqModel.channel;
                reqEquityNav.req.AsofDate = reqModel.asof_date;
                reqEquityNav.req.RequestDate = reqModel.request_date;
                reqEquityNav.req.RequestTime = reqModel.request_time;
                reqEquityNav.req.instrument_code = reqModel.insuer_code; //instrument_code
                reqEquityNav.req.InsumentType = reqModel.insument_type; //instrumenttype

                wsEquityNav.Endpoint.Address = new EndpointAddress(reqModel.url_service);
                var task = wsEquityNav.GetPriceAsync(reqEquityNav).Result;
                resModel = JsonConvert.DeserializeObject<InterfaceResNavPriceModel>(task.GetPriceResult.ToString());

                if (resModel.ReturnCode != 0)
                {
                    return false;
                }

            }
            catch (Exception ex)
            {
                resModel.ReturnCode = 999;
                resModel.ReturnMsg = ex.Message;
                return false;
            }

            return true;
        }

        private bool Check_NavPrice(ref InterfaceReqNavPriceModel model, ref string reRun)
        {
            try
            {
                if (model.create_by == "console")
                {
                    ResultWithModel rwm = new ResultWithModel();

                    rwm = _uow.External.InterfaceNavPriceEquity.Check(model);

                    if (!rwm.Success)
                    {
                        throw new Exception(rwm.Message);
                    }

                    DataSet dsResult = (DataSet)rwm.Data;

                    if (dsResult != null && dsResult.Tables.Count > 0)
                    {
                        if (dsResult.Tables[0].Rows.Count > 0)
                        {
                            reRun = "N";
                        }
                        else
                        {
                            reRun = "Y";
                        }
                    }
                    else
                    {
                        throw new Exception("DataSet dsResult Not Found.");
                    }
                }
                else
                {
                    reRun = "Y";
                }

            }
            catch (Exception ex)
            {
                model.return_code = -999;
                model.return_msg = ex.Message;
                return false;
            }

            return true;
        }
    }
}