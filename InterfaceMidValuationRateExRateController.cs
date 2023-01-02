using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.ServiceModel;
using System.Text;
using GM.CommonLibs.Common;
using GM.CommonLibs.Helper;
using GM.DataAccess.UnitOfWork;
using GM.Model.Common;
using GM.Model.ExternalInterface;
using GM.Model.ExternalInterface.ExchRateMidRate;
using InterfaceMidValuationRateExRateWSDL;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class InterfaceMidValuationRateExRateController : ControllerBase
    {
        private readonly IUnitOfWork _uow;
        private static LogFile _log;

        public InterfaceMidValuationRateExRateController(IUnitOfWork uow)
        {
            _uow = uow;
            _log = new LogFile(_uow.GetConfiguration, "InterfaceMidValuationRateExRate");
        }

        [HttpPost]
        [Route("ImportMidValuationExRate")]
        public ResultWithModel ImportMidValuationExRate(InterfaceMidValuationRateExRateModel model)
        {
            ResultWithModel rwm = new ResultWithModel();
            string guid = Guid.NewGuid().ToString();
            try
            {
                _log.WriteLog("Start ImportMidValuationExRate ==========");
                _log.WriteLog("- ExDate = " + model.exDate);
                _log.WriteLog("- Channel = " + model.channel);
                _log.WriteLog("- ServiceID = " + model.serviceID);
                _log.WriteLog("- ExCurrency = " + model.exCurrency);
                _log.WriteLog("- ServiceUrl = " + model.ServiceUrl);
                _log.WriteLog("- ServiceType = " + model.ServiceType);
                _log.WriteLog("- ApiAuthenUrl = " + model.ApiAuthenUrl);
                _log.WriteLog("- ApiRateUrl = " + model.ApiRateUrl);

                model.requestID = guid;
                if (model.ServiceType == "API")
                {
                    //login service
                    ReqAuthenExchRateMidRate reqAuthenExchRate = new ReqAuthenExchRateMidRate();
                    reqAuthenExchRate.username = model.ApiUsername;
                    reqAuthenExchRate.password = model.ApiPassword;

                    ResAuthenExchRateMidRate resAuthen = new ResAuthenExchRateMidRate();
                    
                    using (var clientHandler = new HttpClientHandler())
                    {
                        if (model.ApiAuthenUrl.StartsWith("https://"))
                            clientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; };
                        
                        using (HttpClient client = new HttpClient(clientHandler))
                        {
                            client.BaseAddress = new Uri(model.ApiAuthenUrl);
                            var requestData = JsonConvert.SerializeObject(reqAuthenExchRate, new IsoDateTimeConverter() { DateTimeFormat = "dd/MM/yyyy HH:mm:ss" });
                            var httpContent = new StringContent(requestData, Encoding.UTF8, "application/json");
                            var response = client.PostAsync(model.ApiAuthenUrl, httpContent).Result;
                            if (response.IsSuccessStatusCode)
                            {
                                var result = response.Content.ReadAsStringAsync().Result;
                                resAuthen = JsonConvert.DeserializeObject<ResAuthenExchRateMidRate>(result);

                                if (resAuthen.status != "SUCCESS")
                                {
                                    throw new Exception("AuthenExchRateCounterRate() : " + resAuthen.status + " : " + resAuthen.message);
                                }
                            }
                            else
                            {
                                throw new Exception("AuthenExchRateCounterRate() : " + (int)response.StatusCode + " : " + response.StatusCode);
                            }
                        }
                    }

                    if (resAuthen.status == "SUCCESS")
                    {
                        //getMidRates
                        ReqGetMidRates reqGetMidRates = new ReqGetMidRates();
                        reqGetMidRates.exDate = model.exDate;
                        if (model.exCurrency != "ALL")
                        {
                            string[] arr = model.exCurrency.Split(',');
                            reqGetMidRates.ccyCodes = arr;
                        }

                        ResGetMidRates resGetMidRates = new ResGetMidRates();
                        
                        using (var clientHandler = new HttpClientHandler())
                        {
                            if (model.ApiRateUrl.StartsWith("https://"))
                                clientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; };
                            clientHandler.ClientCertificateOptions = ClientCertificateOption.Automatic;
                            using (HttpClient client = new HttpClient(clientHandler))
                            {
                                client.BaseAddress = new Uri(model.ApiRateUrl);
                                client.DefaultRequestHeaders.Add("Authorization", string.Format("{0} {1}", resAuthen.authenType, resAuthen.token));

                                var requestData = JsonConvert.SerializeObject(reqGetMidRates, new IsoDateTimeConverter() { DateTimeFormat = "dd/MM/yyyy HH:mm:ss" });
                                var httpContent = new StringContent(requestData, Encoding.UTF8, "application/json");
                                var response = client.PostAsync(model.ApiRateUrl, httpContent).Result;
                                if (response.IsSuccessStatusCode)
                                {
                                    var result = response.Content.ReadAsStringAsync().Result;
                                    resGetMidRates = JsonConvert.DeserializeObject<ResGetMidRates>(result);

                                    if (resGetMidRates.status != "SUCCESS")
                                    {
                                        throw new Exception("GetMidRates() : " + resGetMidRates.status + " : " + resGetMidRates.message);
                                    }

                                    if (resGetMidRates.exRound == "1")
                                    {
                                        model.type = "MIDRATE";
                                    }
                                    else if (resGetMidRates.exRound == "2")
                                    {
                                        model.type = "VALUATIONRATE";
                                    }
                                    else
                                    {
                                        throw new Exception("GetMidRates() : exRound not type");
                                    }

                                    _log.WriteLog("Request GetMidRates = Success.");
                                    if (resGetMidRates.midRateList.Count > 0)
                                    {
                                        try
                                        {
                                            _log.WriteLog("Import MidValuationRate To Repo");
                                            _uow.BeginTransaction();
                                            // Step 3 : Delete RP_Interface_marketprice(Temp)
                                            rwm = DeleteExchangeRateMidValuationTemp(new InterfaceMidValuationRateExRateReqModel()
                                            {
                                                asof_date = model.asof_date,
                                                exRound = string.IsNullOrEmpty(resGetMidRates.exRound) ? 0 :
                                                    Convert.ToInt32(resGetMidRates.exRound)
                                            });
                                            if (!rwm.Success)
                                            {
                                                throw new Exception("Delete_Exchange_Rate_Mid_Valuation_Temp() : " + rwm.Message);
                                            }

                                            foreach (var item in resGetMidRates.midRateList)
                                            {
                                                InterfaceMidValuationRateExRateReqModel insert = new InterfaceMidValuationRateExRateReqModel();
                                                insert.asof_date = model.asof_date;
                                                insert.channel = "EXRATE";//fix
                                                insert.requestID = model.requestID;
                                                insert.serviceID = model.serviceID;
                                                insert.ExRateResponse.returnCode = resGetMidRates.status;
                                                insert.ExRateResponse.errorDesc = resGetMidRates.message;
                                                insert.exDate = resGetMidRates.exDate;
                                                insert.exRound = string.IsNullOrEmpty(resGetMidRates.exRound) ? 0 :
                                                    Convert.ToInt32(resGetMidRates.exRound);

                                                insert.ExRateResponse.mos1 = item.mos1;
                                                insert.ExRateResponse.mos2 = item.mos2;
                                                insert.ExRateResponse.mos3 = item.mos3;
                                                insert.ExRateResponse.mos6 = item.mos6;
                                                insert.ExRateResponse.mos9 = item.mos9;

                                                insert.ExRateResponse.yr1 = item.yr1;
                                                insert.ExRateResponse.yr2 = item.yr2;
                                                insert.ExRateResponse.yr3 = item.yr3;
                                                insert.ExRateResponse.yr4 = item.yr4;
                                                insert.ExRateResponse.yr5 = item.yr5;
                                                insert.ExRateResponse.yr6 = item.yr6;
                                                insert.ExRateResponse.yr7 = item.yr7;
                                                insert.ExRateResponse.yr8 = item.yr8;
                                                insert.ExRateResponse.yr9 = item.yr9;
                                                insert.ExRateResponse.yr10 = item.yr10;
                                                insert.ExRateResponse.currency = item.currency;
                                                insert.ExRateResponse.currency2 = item.currency2;
                                                insert.ExRateResponse.midRate = item.midRate;

                                                // Step 4 : Insert RpReferenceTbma(Temp)
                                                rwm = InsertExchanceRateMidValuationToTemp(insert);
                                                if (!rwm.Success)
                                                {
                                                    throw new Exception("Insert_Exchance_Rate_Mid_Valuation_To_Temp() : [" + insert.exDate + "] Round [" + insert.exRound + "] " + rwm.Message);
                                                }
                                            }

                                            _uow.Commit();
                                            _log.WriteLog("Import Rate Success.");

                                            rwm.RefCode = 0;
                                            rwm.Message = "Insert into table GM_exchange_rate_mid_valuation_rate Success.";
                                            rwm.Success = true;

                                            // Step 5 : Import CounterRate
                                            rwm = ImportMidValuationRate(model);
                                            if (!rwm.Success)
                                            {
                                                throw new Exception("Import_Mid_Valuation_Rate() : " + rwm.Message);
                                            }
                                        }
                                        catch(Exception ex)
                                        {
                                            _uow.Rollback();
                                            rwm.RefCode = -999;
                                            rwm.Message = "Fail : " + ex.Message;
                                            rwm.Serverity = "high";
                                            rwm.Success = false;
                                        }
                                    }
                                }
                                else
                                {
                                    throw new Exception("GetMidRates() : " + (int)response.StatusCode + " : " + response.StatusCode);
                                }
                            }
                        }
                    }
                }
                else
                {
                    EXMIDSEIClient eXCT = new EXMIDSEIClient();
                    exMIDws exMIDws = new exMIDws();
                    exMIDws.EXRateRequestType_1 = new EXRateRequestType();
                    exMIDws.EXRateRequestType_1.header = new EXRateHeaderType();
                    exMIDws.EXRateRequestType_1.body = new EXRateRequestBodyType();
                    exMIDws.EXRateRequestType_1.header.channel = model.channel;
                    exMIDws.EXRateRequestType_1.header.requestID = guid;
                    exMIDws.EXRateRequestType_1.header.serviceID = model.serviceID;
                    exMIDws.EXRateRequestType_1.body.exCurrency = model.exCurrency;
                    exMIDws.EXRateRequestType_1.body.exDate = model.exDate;

                    _log.WriteLog("Request MidValuation ExRate");

                    eXCT.Endpoint.Address = new EndpointAddress(model.ServiceUrl);
                    eXCT.Endpoint.Binding.SendTimeout = TimeSpan.FromMilliseconds(model.ServiceTimeOut);

                    exMIDwsResponse res = eXCT.exMIDwsAsync(exMIDws).Result.exMIDwsResponse;

                    if (res.result.body.returnCode != "0040000")
                    {
                        throw new Exception("exMIDws() : " + res.result.body.errorDesc);
                    }
                    _log.WriteLog("Request MidValuationRate = Success.");

                    if (res.result.body.midRateResult.exRound == "1")
                    {
                        model.type = "MIDRATE";
                    }
                    else if (res.result.body.midRateResult.exRound == "2")
                    {
                        model.type = "VALUATIONRATE";
                    }
                    else
                    {
                        throw new Exception("GetMidRates() : exRound not type");
                    }

                    try
                    {
                        _uow.BeginTransaction();

                        _log.WriteLog("Import MidValuationRate To Repo");
                        // Step 3 : Delete RP_Interface_marketprice(Temp)
                        rwm = DeleteExchangeRateMidValuationTemp(new InterfaceMidValuationRateExRateReqModel()
                        {
                            asof_date = model.asof_date,
                            exRound = string.IsNullOrEmpty(res.result.body.midRateResult.exRound) ? 0 :
                                Convert.ToInt32(res.result.body.midRateResult.exRound)
                        });

                        if (!rwm.Success)
                        {
                            throw new Exception("Delete_Exchange_Rate_Mid_Valuation_Temp() : " + rwm.Message);
                        }

                        exMIDwsResponse datares = new exMIDwsResponse();

                        for (int i = 0; i < res.result.body.midRateResult.midRateItem.Count(); i++)
                        {
                            InterfaceMidValuationRateExRateReqModel insert = new InterfaceMidValuationRateExRateReqModel();
                            insert.asof_date = model.asof_date;
                            insert.channel = res.result.header.channel;
                            insert.requestID = res.result.header.requestID;
                            insert.serviceID = res.result.header.serviceID;
                            insert.ExRateResponse.errorDesc = res.result.body.errorDesc;
                            insert.ExRateResponse.returnCode = res.result.body.returnCode;
                            insert.exDate = res.result.body.midRateResult.exDate;
                            insert.exRound = string.IsNullOrEmpty(res.result.body.midRateResult.exRound) ? 0 :
                                Convert.ToInt32(res.result.body.midRateResult.exRound);

                            insert.ExRateResponse.mos1 = res.result.body.midRateResult.midRateItem[i].MOS1;
                            insert.ExRateResponse.mos2 = res.result.body.midRateResult.midRateItem[i].MOS2;
                            insert.ExRateResponse.mos3 = res.result.body.midRateResult.midRateItem[i].MOS3;
                            insert.ExRateResponse.mos6 = res.result.body.midRateResult.midRateItem[i].MOS6;
                            insert.ExRateResponse.mos9 = res.result.body.midRateResult.midRateItem[i].MOS9;

                            insert.ExRateResponse.yr1 = res.result.body.midRateResult.midRateItem[i].YR1;
                            insert.ExRateResponse.yr2 = res.result.body.midRateResult.midRateItem[i].YR2;
                            insert.ExRateResponse.yr3 = res.result.body.midRateResult.midRateItem[i].YR3;
                            insert.ExRateResponse.yr4 = res.result.body.midRateResult.midRateItem[i].YR4;
                            insert.ExRateResponse.yr5 = res.result.body.midRateResult.midRateItem[i].YR5;
                            insert.ExRateResponse.yr6 = res.result.body.midRateResult.midRateItem[i].YR6;
                            insert.ExRateResponse.yr7 = res.result.body.midRateResult.midRateItem[i].YR7;
                            insert.ExRateResponse.yr8 = res.result.body.midRateResult.midRateItem[i].YR8;
                            insert.ExRateResponse.yr9 = res.result.body.midRateResult.midRateItem[i].YR9;
                            insert.ExRateResponse.yr10 = res.result.body.midRateResult.midRateItem[i].YR10;
                            insert.ExRateResponse.currency = res.result.body.midRateResult.midRateItem[i].exCurrency;
                            insert.ExRateResponse.currency2 = res.result.body.midRateResult.midRateItem[i].exCurrency2;
                            insert.ExRateResponse.midRate = res.result.body.midRateResult.midRateItem[i].midRate;

                            // Step 4 : Insert RpReferenceTbma(Temp)
                            rwm = InsertExchanceRateMidValuationToTemp(insert);
                            if (!rwm.Success)
                            {
                                throw new Exception("Insert_Exchance_Rate_Mid_Valuation_To_Temp() : [" + insert.exDate + "] Round [" + datares.result.body.midRateResult.exRound + "]  Row : " + i + ";" + rwm.Message);
                            }
                        }

                        _uow.Commit();
                        _log.WriteLog("Import Rate Success.");

                        rwm.RefCode = 0;
                        rwm.Message = "Insert into table GM_exchange_rate_mid_valuation_rate Success.";
                        rwm.Success = true;

                        // Step 5 : Import CounterRate
                        rwm = ImportMidValuationRate(model);
                        if (!rwm.Success)
                        {
                            throw new Exception("Import_Mid_Valuation_Rate() : " + rwm.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        _uow.Rollback();
                        rwm.RefCode = -999;
                        rwm.Message = "Fail : " + ex.Message;
                        rwm.Serverity = "high";
                        rwm.Success = false;
                    }
                }
            }
            catch (Exception ex)
            {
                rwm.RefCode = -999;
                rwm.Message = ex.Message;
                rwm.Serverity = "high";
                rwm.Success = false;
                _log.WriteLog("Error ImportMidValuationExRate() : " + ex.Message);
            }
            finally
            {
                _log.WriteLog("End ImportMidValuationExRate ==========");
            }

            DataSet dsResult = new DataSet();
            DataTable dt = new List<InterfaceMidValuationRateExRateModel>() { model }.ToDataTable<InterfaceMidValuationRateExRateModel>();
            dt.TableName = "InterfaceMidValuationRateExRateResultModel";
            dsResult.Tables.Add(dt);
            rwm.Data = dsResult;
            return rwm;
        }

        private ResultWithModel InsertExchanceRateMidValuationToTemp(InterfaceMidValuationRateExRateReqModel model)
        {
            ResultWithModel rwm = new ResultWithModel();
            try
            {
                rwm = _uow.External.InterfaceMidValuationRateExRate.Add(model);

                if (!rwm.Success)
                {
                    throw new Exception("[" + rwm.RefCode + "] " + rwm.Message);
                }
            }
            catch (Exception ex)
            {
                rwm.Message = ex.Message;
                rwm.Success = false;
            }
            return rwm;
        }

        private ResultWithModel DeleteExchangeRateMidValuationTemp(InterfaceMidValuationRateExRateReqModel model)
        {
            ResultWithModel rwm = new ResultWithModel();
            try
            {
                rwm = _uow.External.InterfaceMidValuationRateExRate.Remove(model);

                if (!rwm.Success)
                {
                    throw new Exception("[" + rwm.RefCode + "] " + rwm.Message);
                }
            }
            catch (Exception ex)
            {
                rwm.Message = ex.Message;
                rwm.Success = false;
            }
            return rwm;
        }

        private ResultWithModel ImportMidValuationRate(InterfaceMidValuationRateExRateModel model)
        {
            ResultWithModel rwm = new ResultWithModel();
            try
            {
                rwm = _uow.External.InterfaceMidValuationRateExRate.Update(new InterfaceMidValuationRateExRateReqModel()
                {
                    asof_date = model.asof_date,
                    requestID = model.requestID,
                    type = model.type
                });

                if (rwm.Success == false)
                {
                    throw new Exception("[" + rwm.RefCode + "] " + rwm.Message);
                }
            }
            catch (Exception ex)
            {
                rwm.Message = ex.Message;
                rwm.Success = false;
            }

            return rwm;
        }
    }
}