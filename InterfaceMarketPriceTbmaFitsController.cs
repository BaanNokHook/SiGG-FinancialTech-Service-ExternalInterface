using System;
using System.Collections.Generic;
using System.Data;
using System.ServiceModel;
using System.Threading.Tasks;
using GM.CommonLibs.Common;
using GM.DataAccess.UnitOfWork;
using GM.Model.Common;
using GM.Model.ExternalInterface;
using InterfaceMarketPriceBBGWSDL;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class InterfaceMarketPriceTbmaFitsController : ControllerBase
    {
        private readonly IUnitOfWork _uow;
        private static LogFile _log;

        public InterfaceMarketPriceTbmaFitsController(IUnitOfWork uow)
        {
            _uow = uow;
            _log = new LogFile(_uow.GetConfiguration, "InterfaceMarketPricetbmaFits");
        }


        [HttpPost]
        [Route("ImportMarketPriceTbmaFits")]
        public ResultWithModel ImportMarketPriceTbmaFits(InterfaceMarketPriceTbmaModel model)
        {
            ResultWithModel rwm = new ResultWithModel();

            try
            {
                string market_date_t = model.market_date_t;
                string ref_no = model.ref_no;
                string asof_date = model.asof_date;
                string reRun= "";

                _log.WriteLog("Start ImportMarketPriceTbma ==========");
                _log.WriteLog("- Channel = " + model.channel);
                _log.WriteLog("- Mode = " + model.mode.ToString());
                _log.WriteLog("- AsOfDate = " + model.asof_date);
                _log.WriteLog("- SourceType = " + model.source_type);
                _log.WriteLog("- market_date_t = " + model.market_date_t);
                _log.WriteLog("- SecurityCode = " + model.security_code);
                _log.WriteLog("- Service Url = " + model.urlservice);
                _log.WriteLog("- create by = " + model.create_by);

                // Step 0 : Check MarketPriceTbma
                if (Check_MarketPriceTbma(ref model, ref reRun) == false)
                {
                    throw new Exception("Check_MarketPriceTbma() : [" + model.return_code + "] " + model.return_msg);
                }

                _log.WriteLog("Check MarketPriceTbma[T" + market_date_t + "] Success.");
                _log.WriteLog("- reRun = " + reRun);

                if (reRun == "Y")
                {
                    // Step 1 : Request MarketPriceTbma
                    if (Request_MarketPriceTbma(ref model) == false)
                    {
                        throw new Exception("Request_MarketPriceTbma() : [" + model.return_code + "] " + model.return_msg);
                    }
                    _log.WriteLog("Request MarketPriceTbma = Success.");

                    List<InterfaceMarketPriceTbmaDetailModel> marketPrices = model.detail;

                    _log.WriteLog("- MarketPriceTbma [" + marketPrices.Count + "] Item.");
                    if (marketPrices.Count == 0)
                    {
                        rwm.RefCode = 0;
                        rwm.Message = "Success: MarketPriceTbma Not Found From Service.";
                        rwm.Success = true;

                        _log.WriteLog("Success: MarketPriceTbma Not Found From Service");
                        _log.WriteLog("End ImportMarketPriceTbma ==========");
                        return rwm;
                    }

                    try
                    {
                        _uow.BeginTransaction();

                        _log.WriteLog("Import MarketPriceTbma[T" + market_date_t + "] To Repo");
                        // Step 2 : Delete RP_Interface_marketpriceTbma(Temp)
                        model.asof_date = asof_date;
                        model.market_date_t = market_date_t;
                        rwm = _uow.External.InterfaceMarketPriceTbma.Remove(model);
                        if (!rwm.Success)
                        {
                            throw new Exception("InterfaceMarketPriceTbma.Remove() : [" + rwm.RefCode + "] " + rwm.Message);
                        }

                        for (int i = 0; i < marketPrices.Count; i++)
                        {
                            // Step 3 : Insert RpReferenceTbma(Temp)
                            marketPrices[i].ref_no = model.ref_no;
                            marketPrices[i].market_date_t = market_date_t;
                            rwm = _uow.External.InterfaceMarketPriceTbma.Add(marketPrices[i]);
                            if (!rwm.Success)
                            {
                                throw new Exception("InterfaceMarketPriceTbma.Add() : [" + marketPrices[i].security_code + "] Row : " + i + ": [" + rwm.RefCode + "] " + rwm.Message);
                            }
                        }

                        _uow.Commit();

                        _log.WriteLog("Insert MarketPriceTbma To Temp Success.");

                        rwm.RefCode = 0;
                        rwm.Message = "Insert into table RP_interface_marketpriceTbma  Success.";
                        rwm.Success = true;
                    }
                    catch (Exception ex)
                    {
                        _uow.Rollback();
                        rwm.RefCode = -999;
                        rwm.Message = "Fail : " + ex.Message;
                        rwm.Serverity = "high";
                        rwm.Success = false;
                    }

                    if (rwm.Success)
                    {
                        model.ref_no = ref_no;
                        model.market_date_t = market_date_t;
                        model.asof_date = asof_date;

                        rwm = _uow.External.InterfaceMarketPriceTbma.Import(model);
                        if (!rwm.Success)
                        {
                            rwm.RefCode = -999;
                            rwm.Message = "Fail InterfaceMarketPriceTbma.Import() : [" + rwm.RefCode + "] " + rwm.Message;
                        }
                        else
                        {
                            rwm.RefCode = 0;
                            rwm.Message = "Import Market Price Tbma Success.";
                        }

                        _log.WriteLog("Import MarketPriceTbma[T" + market_date_t + "] Success.");

                    }
                }
                else
                {
                    rwm.RefCode = 0;
                    rwm.Message = "Success: MarketPriceTbma [" + model.asof_date +  "_T"+ market_date_t  + "] Is Exists. [Console]";
                    rwm.Success = true;

                    _log.WriteLog("Success: MarketPriceTbma [" + model.asof_date + "_T" + market_date_t + "] Is Exists. [Console]");
                    _log.WriteLog("End ImportMarketPriceTbma ==========");
                    return rwm;
                }

            }
            catch (Exception ex)
            {
                rwm.RefCode = -999;
                rwm.Message = ex.Message;
                rwm.Serverity = "high";
                rwm.Success = false;
                _log.WriteLog("Error ImportMarketPriceTbma() : " + ex.Message);
            }

            _log.WriteLog("End ImportMarketPriceTbma ==========");
            return rwm;

        }

        private bool Request_MarketPriceTbma(ref InterfaceMarketPriceTbmaModel model)
        {
            WS_ICM_MARKET_PRICE_INFSoapClient wsMarketPrice = new WS_ICM_MARKET_PRICE_INFSoapClient(WS_ICM_MARKET_PRICE_INFSoapClient.EndpointConfiguration.WS_ICM_MARKET_PRICE_INFSoap);
            ReqMarketPrice reqMarketPrice = new ReqMarketPrice();

            try
            {
                reqMarketPrice.channel = model.channel;
                reqMarketPrice.ref_no = model.ref_no;
                reqMarketPrice.request_date = model.request_date;
                reqMarketPrice.request_time = model.request_time;
                reqMarketPrice.mode = model.mode;
                reqMarketPrice.asof_date = model.asof_date;
                reqMarketPrice.source_type = model.source_type;
                reqMarketPrice.security_code = model.security_code;

                wsMarketPrice.Endpoint.Address = new EndpointAddress(model.urlservice);
                Task<string> task = wsMarketPrice.InterfaceMarketPriceAsync(reqMarketPrice);
                model = JsonConvert.DeserializeObject<InterfaceMarketPriceTbmaModel>(task.Result);

                if (model.return_code != 0)
                {
                    return false;
                }
            }
            catch (Exception Ex)
            {
                model.return_code = -999;
                model.return_msg = Ex.Message;
                return false;
            }

            return true;
        }

        private bool Check_MarketPriceTbma(ref InterfaceMarketPriceTbmaModel model, ref string reRun)
        {
            try
            {
                if (model.create_by == "console")
                {
                    ResultWithModel rwm = new ResultWithModel();

                    rwm = _uow.External.InterfaceMarketPriceTbma.Check(model);

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