using System;
using System.Collections.Generic;
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
    public class InterfaceMarketPriceBBGController : ControllerBase
    {
        private readonly IUnitOfWork _uow;
        private static LogFile _log;

        public InterfaceMarketPriceBBGController(IUnitOfWork uow)
        {
            _uow = uow;
            _log = new LogFile(_uow.GetConfiguration, "InterfaceMarketPriceBBG");
        }

        [HttpPost]
        [Route("ImportMarketPriceBBGList")]
        public ResultWithModel ImportMarketPriceBBGList(InterfaceMarketPriceModel model)
        {
            return _uow.External.InterfaceMarketPrice.Get(model);
        }

        [HttpPost]
        [Route("ImportMarketPriceBBG")]
        public ResultWithModel ImportMarketPriceBBG(InterfaceMarketPriceModel model)
        {
            ResultWithModel rwm = new ResultWithModel();

            try
            {
                _log.WriteLog("Start ImportMarketPriceBBG ==========");
                _log.WriteLog("- Channel = " + model.channel);
                _log.WriteLog("- Mode = " + model.mode.ToString());
                _log.WriteLog("- AsOfDate = " + model.asof_date);
                _log.WriteLog("- SourceType = " + model.source_type);
                _log.WriteLog("- SecurityCode = " + model.security_code);
                _log.WriteLog("- Service Url = " + model.urlservice);

                // Step 1 : Request MarketPrice
                if (Request_MarketPrice(ref model) == false)
                {
                    throw new Exception("Request_MarketPrice() : [" + model.return_code + "] " + model.return_msg);
                }
                _log.WriteLog("Request MarketPrice = Success.");

                List<InterfaceMarketPriceDetailModel> marketPrices = model.detail;

                _log.WriteLog("- MarketPrice [" + marketPrices.Count + "] Item.");
                if (marketPrices.Count == 0)
                {
                    rwm.RefCode = 0;
                    rwm.Message = "Success: MarketPrice Not Found From Service.";
                    rwm.Success = true;

                    _log.WriteLog("Success: MarketPrice Not Found From Service");
                    _log.WriteLog("End ImportMarketPriceBBG ==========");
                    return rwm;
                }

                try
                {
                    _uow.BeginTransaction();

                    _log.WriteLog("Import MarketPrice To Repo");
                    // Step 2 : Delete RP_Interface_marketprice(Temp)
                    rwm = _uow.External.InterfaceMarketPrice.Remove(model);
                    if (!rwm.Success)
                    {
                        throw new Exception("Delete_MarketPrice_Temp() : [" + rwm.RefCode + "] " + rwm.Message);
                    }

                    for (int i = 0; i < marketPrices.Count; i++)
                    {
                        // Step 3 : Insert RpReferenceTbma(Temp)
                        rwm = _uow.External.InterfaceMarketPrice.Add(marketPrices[i]);
                        if (!rwm.Success)
                        {
                            throw new Exception("Insert_MarketPrice_To_Temp() : [" + marketPrices[i].security_code + "] Row : " + i + ": [" + rwm.RefCode + "] " + rwm.Message);
                        }
                    }

                    _uow.Commit();

                    _log.WriteLog("Import Rate Success.");

                    rwm.RefCode = 0;
                    rwm.Message = "Insert into table RP_interface_marketprice  Success.";
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
                    rwm = _uow.External.InterfaceMarketPrice.Import(model);
                    if (!rwm.Success)
                    {
                        rwm.RefCode = -999;
                        rwm.Message = "Fail Insert_Interface_Market_Price_BBG_Temp_To_Face() : [" + rwm.RefCode + "] " + rwm.Message;
                    }
                    else
                    {
                        rwm.RefCode = 0;
                        rwm.Message = "Import BBG Market Price Success.";
                    }

                }

            }
            catch (Exception ex)
            {
                rwm.RefCode = -999;
                rwm.Message = ex.Message;
                rwm.Serverity = "high";
                rwm.Success = false;
                _log.WriteLog("Error ImportMarketPriceBBG() : " + ex.Message);
            }

            _log.WriteLog("End ImportMarketPriceBBG ==========");
            return rwm;
        }

        private bool Request_MarketPrice(ref InterfaceMarketPriceModel model)
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
                model = JsonConvert.DeserializeObject<InterfaceMarketPriceModel>(task.Result);

                if (model.return_code != 0)
                {
                    return false;
                }
            }
            catch (Exception Ex)
            {
                model.return_code = 999;
                model.return_msg = Ex.Message;
                return false;
            }

            return true;
        }
    }
}