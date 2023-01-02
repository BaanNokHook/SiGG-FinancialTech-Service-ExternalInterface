using GM.CommonLibs.Common;
using GM.CommonLibs.Helper;
using GM.DataAccess.UnitOfWork;
using GM.Model.Common;
using GM.Model.ExternalInterface;
using InterfaceCounterRateExRateWSDL;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.ServiceModel;
using System.Text;
using GM.Model.ExternalInterface.ExchRateCounterRate;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class InterfaceCounterRateExRateController : ControllerBase
    {
        private readonly IUnitOfWork _uow;
        private static LogFile _log;
        public InterfaceCounterRateExRateController(IUnitOfWork uow)
        {
            _uow = uow;
            _log = new LogFile(_uow.GetConfiguration, "InterfaceCounterRateExRate");
        }

        [HttpPost]
        [Route("ImportCounterRateExRate")]
        public ResultWithModel ImportCounterRateExRate(InterfaceCounterRateExRateModel model)
        {
            ResultWithModel rwm = new ResultWithModel();
            DataSet dsResult;
            DataTable dt;

            string strMsg = string.Empty;
            string guid = Guid.NewGuid().ToString();
            try
            {
                _log.WriteLog("Start ImportCounterRateExRate ==========");
                _log.WriteLog("- ExRound = " + model.exRound);
                _log.WriteLog("- ExDate = " + model.exDate);
                _log.WriteLog("- ExTime = " + model.exTime);
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
                    ReqAuthenExchRateCounterRate reqAuthenExchRate = new ReqAuthenExchRateCounterRate();
                    reqAuthenExchRate.username = model.ApiUsername;
                    reqAuthenExchRate.password = model.ApiPassword;

                    ResAuthenExchRateCounterRate resAuthen = new ResAuthenExchRateCounterRate();

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
                                resAuthen = JsonConvert.DeserializeObject<ResAuthenExchRateCounterRate>(result);

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
                        //getCounterRates
                        ReqGetCounterRates reqGetCounterRates = new ReqGetCounterRates();
                        reqGetCounterRates.exDate = model.exDate;
                        reqGetCounterRates.exTime = model.exTime;
                        reqGetCounterRates.exRound = model.exRound;
                        if (model.exCurrency != "ALL")
                        {
                            string[] arr = model.exCurrency.Split(',');
                            reqGetCounterRates.ccyCodes = arr;
                        }

                        ResGetCounterRates resGetCounterRates = new ResGetCounterRates();
                        
                        using (var clientHandler = new HttpClientHandler())
                        {
                            if (model.ApiRateUrl.StartsWith("https://"))
                                clientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; };

                            using (HttpClient client = new HttpClient(clientHandler))
                            {
                                client.BaseAddress = new Uri(model.ApiRateUrl);
                                client.DefaultRequestHeaders.Add("Authorization", string.Format("{0} {1}", resAuthen.authenType, resAuthen.token));

                                var requestData = JsonConvert.SerializeObject(reqGetCounterRates, new IsoDateTimeConverter() { DateTimeFormat = "dd/MM/yyyy HH:mm:ss" });
                                var httpContent = new StringContent(requestData, Encoding.UTF8, "application/json");
                                var response = client.PostAsync(model.ApiRateUrl, httpContent).Result;
                                if (response.IsSuccessStatusCode)
                                {
                                    var result = response.Content.ReadAsStringAsync().Result;
                                    resGetCounterRates = JsonConvert.DeserializeObject<ResGetCounterRates>(result);

                                    if (resGetCounterRates.status != "SUCCESS")
                                    {
                                        throw new Exception("GetCounterRates() : " + resGetCounterRates.status + " : " + resGetCounterRates.message);
                                    }

                                    _log.WriteLog("Request GetCounterRates = Success.");
                                    if (resGetCounterRates.exRound != "1")
                                    {
                                        dsResult = new DataSet();
                                        dt = new List<InterfaceCounterRateExRateModel>() { model }.ToDataTable<InterfaceCounterRateExRateModel>();
                                        dt.TableName = "InterfaceCounterRateExRateResultModel";
                                        dsResult.Tables.Add(dt);
                                        rwm.Data = dsResult;

                                        rwm.RefCode = 0;
                                        rwm.Message = "CounterRate ExRound != 1";
                                        rwm.Success = true;

                                        _log.WriteLog("- ExRound = " + model.exRound);
                                        return rwm;
                                    }

                                    if (resGetCounterRates.counterRateList.Count > 0)
                                    {
                                        try
                                        {
                                            _uow.BeginTransaction();
                                            #region Delete ExchangeRateExchange
                                            if (!DeleteExchangeRateExchange(ref strMsg, model.asof_date))
                                            {
                                                throw new Exception("DeleteExchangeRateExchange() : " + strMsg);
                                            }
                                            #endregion

                                            #region Insert
                                            var groupRates = resGetCounterRates.counterRateList.GroupBy(a => a.currency);
                                            foreach (var groupRate in groupRates)
                                            {
                                                InterfaceCounterRateExRateReqModel insert = new InterfaceCounterRateExRateReqModel();
                                                insert.asof_date = model.asof_date;
                                                insert.channel = "EXRATE";//fix
                                                insert.requestID = model.requestID;
                                                insert.serviceID = model.serviceID;
                                                insert.ExRateResponse.returnCode = resGetCounterRates.status;
                                                insert.ExRateResponse.errorDesc = resGetCounterRates.message;
                                                insert.exDate = resGetCounterRates.exDate + " " + resGetCounterRates.exTime;
                                                insert.exRound = int.Parse(resGetCounterRates.exRound);
                                                insert.exCurrency = groupRate.Key;

                                                var rates = resGetCounterRates.counterRateList.Where(a => a.currency == groupRate.Key);
                                                int i = 1;
                                                foreach (var rate in rates)
                                                {
                                                    if (i == 1)
                                                    {
                                                        insert.ExRateResponse.bankNoteDenom1 = rate.bankNoteDenom;
                                                        insert.ExRateResponse.sightBillRate1 = rate.sightBillRate;
                                                        insert.ExRateResponse.ttRate1 = rate.ttRate;
                                                        insert.ExRateResponse.sellingRate1 = rate.sellingRate;
                                                        insert.ExRateResponse.bankNoteBuying1 = rate.bankNoteBuying;
                                                        insert.ExRateResponse.bankNoteSelling1 = rate.bankNoteSelling;
                                                    }
                                                    else if (i == 2)
                                                    {
                                                        insert.ExRateResponse.bankNoteDenom2 = rate.bankNoteDenom;
                                                        insert.ExRateResponse.sightBillRate2 = rate.sightBillRate;
                                                        insert.ExRateResponse.ttRate2 = rate.ttRate;
                                                        insert.ExRateResponse.sellingRate2 = rate.sellingRate;
                                                        insert.ExRateResponse.bankNoteBuying2 = rate.bankNoteBuying;
                                                        insert.ExRateResponse.bankNoteSelling2 = rate.bankNoteSelling;
                                                    }
                                                    else if (i == 3)
                                                    {
                                                        insert.ExRateResponse.bankNoteDenom3 = rate.bankNoteDenom;
                                                        insert.ExRateResponse.sightBillRate3 = rate.sightBillRate;
                                                        insert.ExRateResponse.ttRate3 = rate.ttRate;
                                                        insert.ExRateResponse.sellingRate3 = rate.sellingRate;
                                                        insert.ExRateResponse.bankNoteBuying3 = rate.bankNoteBuying;
                                                        insert.ExRateResponse.bankNoteSelling3 = rate.bankNoteSelling;
                                                    }

                                                    i++;
                                                }
                                                rwm = _uow.External.InterfaceCounterRateExRate.Add(insert);

                                                if (!rwm.Success)
                                                {
                                                    throw new Exception("InsertExchanceRate() : [" + insert.exDate + "] Round [" + insert.exRound + "]  Row : " + insert.exCurrency + ";" + "[" + rwm.RefCode + "] " + rwm.Message);
                                                }
                                            }

                                            _uow.Commit();
                                            _log.WriteLog("Import Rate Success.");

                                            rwm.RefCode = 0;
                                            rwm.Message = "Insert into table GM_exchange_rate_exchange_temp  Success.";
                                            rwm.Success = true;

                                            #endregion

                                            #region Update
                                            if (ImportCounterRate(ref strMsg, model) == false)
                                            {
                                                throw new Exception("ImportCounterRate() : " + strMsg);
                                            }
                                            #endregion

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
                                else
                                {
                                    throw new Exception("GetCounterRates() : " + (int)response.StatusCode + " : " + response.StatusCode);
                                }
                            }
                        }
                    }
                }
                else
                {

                    EXCTSEIClient exct = new EXCTSEIClient();
                    exCTws tws = new exCTws();
                    tws.EXRateRequestType_1 = new EXRateRequestType();
                    tws.EXRateRequestType_1.header = new EXRateHeaderType();
                    tws.EXRateRequestType_1.body = new EXRateRequestBodyType();
                    tws.EXRateRequestType_1.header.channel = model.channel;
                    tws.EXRateRequestType_1.header.requestID = guid;
                    tws.EXRateRequestType_1.header.serviceID = model.serviceID;
                    tws.EXRateRequestType_1.body.exCurrency = model.exCurrency;
                    tws.EXRateRequestType_1.body.exDate = model.exDate + " " + model.exTime;

                    _log.WriteLog("Request CounterRate ExRate");
                    exct.Endpoint.Address = new EndpointAddress(model.ServiceUrl);
                    exct.Endpoint.Binding.SendTimeout = TimeSpan.FromMilliseconds(model.ServiceTimeOut);

                    var res = exct.exCTwsAsync(tws).Result;
                    if (res.exCTwsResponse.result.body.returnCode != "0040000")
                    {
                        throw new Exception("exCTws() : " + res.exCTwsResponse.result.body.errorDesc);
                    }

                    model.exRound = Convert.ToInt32(res.exCTwsResponse.result.body.counterRateResult.exRound);

                    _log.WriteLog("Request CounterRate = Success.");
                    if (model.exRound != 1)
                    {
                        dsResult = new DataSet();
                        dt = new List<InterfaceCounterRateExRateModel>() { model }.ToDataTable<InterfaceCounterRateExRateModel>();
                        dt.TableName = "InterfaceCounterRateExRateResultModel";
                        dsResult.Tables.Add(dt);
                        rwm.Data = dsResult;

                        rwm.RefCode = 0;
                        rwm.Message = "CounterRate ExRound != 1";
                        rwm.Success = true;

                        _log.WriteLog("- ExRound = " + model.exRound);
                        return rwm;
                    }

                    _uow.BeginTransaction();

                    try
                    {
                        _log.WriteLog("Import CounterRate To Repo");
                        // Step 3 : Delete RP_Interface_marketprice(Temp)
                        if (!DeleteExchangeRateExchange(ref strMsg, model.asof_date))
                        {
                            throw new Exception("DeleteExchangeRateExchange() : " + strMsg);
                        }

                        EXCounterRateResultType resultType = new EXCounterRateResultType();
                        EXCounterRateItemType eXCounterRateItem = new EXCounterRateItemType();
                        EXCounterRateResponseBodyType resbody = new EXCounterRateResponseBodyType();
                        EXRateHeaderType resheader = new EXRateHeaderType();
                        exCTwsResponse datares = new exCTwsResponse();

                        for (int i = 0; i < res.exCTwsResponse.result.body.counterRateResult.counterRateItem.Length; i++)
                        {

                            resheader.channel = res.exCTwsResponse.result.header.channel;
                            resheader.requestID = res.exCTwsResponse.result.header.requestID;
                            resheader.serviceID = res.exCTwsResponse.result.header.serviceID;

                            eXCounterRateItem.exCurrency = res.exCTwsResponse.result.body.counterRateResult.counterRateItem[i].exCurrency;
                            eXCounterRateItem.exCurrencyData = res.exCTwsResponse.result.body.counterRateResult.counterRateItem[i].exCurrencyData;
                            resultType.counterRateItem = new EXCounterRateItemType[1];
                            resultType.counterRateItem[0] = eXCounterRateItem;
                            resultType.exDate = res.exCTwsResponse.result.body.counterRateResult.exDate;
                            resultType.exRound = res.exCTwsResponse.result.body.counterRateResult.exRound;
                            resbody.counterRateResult = resultType;
                            resbody.errorDesc = res.exCTwsResponse.result.body.errorDesc;
                            resbody.returnCode = res.exCTwsResponse.result.body.returnCode;

                            datares.result = new EXCounterRateResponseType();

                            datares.result.header = resheader;
                            datares.result.body = resbody;

                            // Step 4 : Insert RpReferenceTbma(Temp)
                            if (!InsertExchanceRate(ref strMsg, datares, model))
                            {
                                throw new Exception("InsertExchanceRate() : [" + datares.result.body.counterRateResult.exDate + "] Round [" + datares.result.body.counterRateResult.exRound + "]  Row : " + i + ";" + strMsg);
                            }
                        }

                        _uow.Commit();
                        _log.WriteLog("Import Rate Success.");

                        rwm.RefCode = 0;
                        rwm.Message = "Insert into table GM_exchange_rate_exchange_temp  Success.";
                        rwm.Success = true;

                        // Step 5 : Import CounterRate
                        if (ImportCounterRate(ref strMsg, model) == false)
                        {
                            throw new Exception("ImportCounterRate() : " + strMsg);
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
                _log.WriteLog("Error ImportCounterRateExRate() : " + ex.Message);
            }
            finally
            {
                _log.WriteLog("End ImportCounterRateExRate ==========");
            }

            dsResult = new DataSet();
            dt = new List<InterfaceCounterRateExRateModel>() { model }.ToDataTable<InterfaceCounterRateExRateModel>();
            dt.TableName = "InterfaceCounterRateExRateResultModel";
            dsResult.Tables.Add(dt);
            rwm.Data = dsResult;
            return rwm;
        }

        private bool InsertExchanceRate(ref string returnMsg, exCTwsResponse data, InterfaceCounterRateExRateModel model)
        {
            try
            {
                InterfaceCounterRateExRateReqModel req = new InterfaceCounterRateExRateReqModel();
                req.asof_date = model.asof_date;
                req.channel = data.result.header.channel;
                req.requestID = data.result.header.requestID;
                req.serviceID = data.result.header.serviceID;
                req.ExRateResponse.returnCode = data.result.body.returnCode;
                req.ExRateResponse.errorDesc = data.result.body.errorDesc;
                req.exDate = data.result.body.counterRateResult.exDate;
                req.exRound = string.IsNullOrEmpty(data.result.body.counterRateResult.exRound) ? 0 :
                    Convert.ToInt32(data.result.body.counterRateResult.exRound);
                req.exCurrency = data.result.body.counterRateResult.counterRateItem[0].exCurrency;

                for (int i = 0; i < data.result.body.counterRateResult.counterRateItem[0].exCurrencyData.Length; i++)
                {
                    if (i == 0)
                    {
                        req.ExRateResponse.bankNoteDenom1 = data.result.body.counterRateResult.counterRateItem[0].exCurrencyData[i].bankNoteDenom;
                        req.ExRateResponse.sightBillRate1 = data.result.body.counterRateResult.counterRateItem[0].exCurrencyData[i].sightBillRate;
                        req.ExRateResponse.ttRate1 = data.result.body.counterRateResult.counterRateItem[0].exCurrencyData[i].ttRate;
                        req.ExRateResponse.sellingRate1 = data.result.body.counterRateResult.counterRateItem[0].exCurrencyData[i].sellingRate;
                        req.ExRateResponse.bankNoteBuying1 = data.result.body.counterRateResult.counterRateItem[0].exCurrencyData[i].bankNoteBuying;
                        req.ExRateResponse.bankNoteSelling1 = data.result.body.counterRateResult.counterRateItem[0].exCurrencyData[i].bankNoteSelling;
                    }
                    else if (i == 1)
                    {
                        req.ExRateResponse.bankNoteDenom2 = data.result.body.counterRateResult.counterRateItem[0].exCurrencyData[i].bankNoteDenom;
                        req.ExRateResponse.sightBillRate2 = data.result.body.counterRateResult.counterRateItem[0].exCurrencyData[i].sightBillRate;
                        req.ExRateResponse.ttRate2 = data.result.body.counterRateResult.counterRateItem[0].exCurrencyData[i].ttRate;
                        req.ExRateResponse.sellingRate2 = data.result.body.counterRateResult.counterRateItem[0].exCurrencyData[i].sellingRate;
                        req.ExRateResponse.bankNoteBuying2 = data.result.body.counterRateResult.counterRateItem[0].exCurrencyData[i].bankNoteBuying;
                        req.ExRateResponse.bankNoteSelling2 = data.result.body.counterRateResult.counterRateItem[0].exCurrencyData[i].bankNoteSelling;
                    }
                    else if (i == 2)
                    {
                        req.ExRateResponse.bankNoteDenom3 = data.result.body.counterRateResult.counterRateItem[0].exCurrencyData[i].bankNoteDenom;
                        req.ExRateResponse.sightBillRate3 = data.result.body.counterRateResult.counterRateItem[0].exCurrencyData[i].sightBillRate;
                        req.ExRateResponse.ttRate3 = data.result.body.counterRateResult.counterRateItem[0].exCurrencyData[i].ttRate;
                        req.ExRateResponse.sellingRate3 = data.result.body.counterRateResult.counterRateItem[0].exCurrencyData[i].sellingRate;
                        req.ExRateResponse.bankNoteBuying3 = data.result.body.counterRateResult.counterRateItem[0].exCurrencyData[i].bankNoteBuying;
                        req.ExRateResponse.bankNoteSelling3 = data.result.body.counterRateResult.counterRateItem[0].exCurrencyData[i].bankNoteSelling;
                    }
                }

                ResultWithModel rwm = _uow.External.InterfaceCounterRateExRate.Add(req);

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

        private bool DeleteExchangeRateExchange(ref string returnMsg, DateTime asOfDate)
        {
            try
            {
                InterfaceCounterRateExRateReqModel model = new InterfaceCounterRateExRateReqModel();
                model.asof_date = asOfDate;
                ResultWithModel rwm = _uow.External.InterfaceCounterRateExRate.Remove(model);

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

        private bool ImportCounterRate(ref string returnMsg, InterfaceCounterRateExRateModel model)
        {
            try
            {
                InterfaceCounterRateExRateReqModel req = new InterfaceCounterRateExRateReqModel();
                req.asof_date = model.asof_date;
                req.requestID = model.requestID;

                ResultWithModel rwm = _uow.External.InterfaceCounterRateExRate.Update(req);

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
