using GM.DataAccess.UnitOfWork;
using GM.Model.Common;
using GM.Model.InterfaceSecurity;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class InterfaceSecurityController : ControllerBase
    {
        private readonly IUnitOfWork _uow;
        public InterfaceSecurityController(IUnitOfWork uow)
        {
            _uow = uow;
        }

        [HttpPost]
        [Route("[action]")]
        public string ImportSecurity(ReqSecurity reqSecurityList)
        {
            string strMsg = string.Empty;
            resSecurityHeader resSecurityHeader = new resSecurityHeader();
            ReqSecurityHeader header = new ReqSecurityHeader();

            try
            {
                // Step 1 : Read Json
                string input = JsonConvert.SerializeObject(reqSecurityList);
                header = reqSecurityList.reqSecurityHeader;
                List<ReqSecurityList>  securityList = reqSecurityList.reqSecurityList;

                if (!InsertFitsSecurityRequestLog(ref strMsg, header, input, securityList.Count))
                {
                    throw new Exception("InsertFitsSecurityRequestLog() : " + strMsg);
                }

                // Loop SecurityList
                for (int i = 0; i < securityList.Count; i++)
                {
                    ResSecurityList respDetail = new ResSecurityList();
                    try
                    {
                        // Step 2 : Insert SecurityList
                        if (!InsertTempSecurity(ref strMsg, securityList[i], header.ref_code))
                        {
                            throw new Exception("InsertTempSecurity() : " + strMsg);
                        }

                        // Step 3 : Insert RatingList
                        if (!InsertTempRating(ref strMsg, securityList[i].reqSecurityRatingList, header.ref_code))
                        {
                            throw new Exception("InsertTempRating() : " + strMsg);
                        }

                        // Step 4 : Insert CashflowList
                        if (!InsertTempCashflow(ref strMsg, securityList[i].reqCashFlowList, header.ref_code))
                        {
                            throw new Exception("InsertTempCashflow() : " + strMsg);
                        }

                        respDetail.response_code = "0";
                        respDetail.response_message = "Success";
                    }
                    catch (Exception Ex)
                    {
                        respDetail.response_code = "-999";
                        respDetail.response_message = "Fail : " + Ex.Message;
                    }
                    finally
                    {
                        respDetail.security = securityList[i].instrument_id + "|" + securityList[i].instrument_code;
                    }

                    resSecurityHeader.response_details.Add(respDetail);
                }


                // Step 5 : Import Temp To Security
                if (!ImportSecurity(ref strMsg, header.ref_code))
                {
                    throw new Exception("ImportSecurity() : " + strMsg);
                }

                resSecurityHeader.response_code = "0";
                resSecurityHeader.response_message = "Success";
            }
            catch (Exception Ex)
            {
                resSecurityHeader.response_code = "-999";
                resSecurityHeader.response_message = "Fail : " + Ex.Message;
            }
            finally
            {
                resSecurityHeader.channel = header.channel;
                resSecurityHeader.ref_code = header.ref_code;
                resSecurityHeader.mode = header.mode;
                resSecurityHeader.response_date = DateTime.Now.ToString("yyyyMMdd");
                resSecurityHeader.response_time = DateTime.Now.ToString("HH:mm:ss");
            }

            var responseSecurity = (new { responseSecurity = resSecurityHeader });
            string result = JsonConvert.SerializeObject(responseSecurity);

            if (!InsertFitsSecurityResultLog(ref strMsg, resSecurityHeader))
            {
                resSecurityHeader.response_code = "-999";
                resSecurityHeader.response_message = "Fail : InsertFitsSecurityResultLog() : " + strMsg;
                responseSecurity = (new { responseSecurity = resSecurityHeader });
                result = JsonConvert.SerializeObject(responseSecurity);
            }

            return result;
        }

        private bool InsertTempSecurity(ref string ReturnMsg, ReqSecurityList model, string ref_code)
        {
            try
            {
                model.ref_code = ref_code;
                ResultWithModel rwm = _uow.External.InterfaceSecurity.Add(model);

                if (!rwm.Success)
                {
                    throw new Exception("[" + rwm.RefCode.ToString() + "] " + rwm.Message);
                }
            }
            catch (Exception Ex)
            {
                ReturnMsg = Ex.Message;
                return false;
            }
            return true;
        }

        private bool InsertTempRating(ref string ReturnMsg, List<ReqSecurityRatingList> models, string ref_code)
        {
            try
            {
                if (models.Count == 1 && models[0].instrument_id.Trim() == string.Empty)
                {
                    return true;
                }

                for (int i = 0; i < models.Count; i++)
                {
                    models[i].ref_code = ref_code;
                    ResultWithModel rwm = _uow.External.InterfaceSecurityRating.Add(models[i]);

                    if (!rwm.Success)
                    {
                        throw new Exception("[" + rwm.RefCode.ToString() + "] " + rwm.Message);
                    }
                }
            }
            catch (Exception Ex)
            {
                ReturnMsg = Ex.Message;
                return false;
            }
            return true;
        }

        private bool InsertTempCashflow(ref string returnMsg, List<ReqCashFlowList> models, string ref_code)
        {
            try
            {
                if (models.Count == 1 && models[0].instrument_id.Trim() == string.Empty)
                {
                    return true;
                }

                for (int i = 0; i < models.Count; i++)
                {
                    models[i].ref_code = ref_code;
                    ResultWithModel rwm = _uow.External.InterfaceSecurityCashFlow.Add(models[i]);

                    if (!rwm.Success)
                    {
                        throw new Exception("[" + rwm.RefCode.ToString() + "] " + rwm.Message);
                    }
                }
            }
            catch (Exception Ex)
            {
                returnMsg = Ex.Message;
                return false;
            }
            return true;
        }

        private bool ImportSecurity(ref string returnMsg, string ref_code)
        {
            try
            {
                ResultWithModel rwm = _uow.External.InterfaceSecurityImport.Add(new ReqSecurity { RefCode = ref_code, Function = "FITS" });
                if (!rwm.Success)
                {
                    throw new Exception("[" + rwm.RefCode.ToString() + "] " + rwm.Message);
                }
            }
            catch (Exception Ex)
            {
                returnMsg = Ex.Message;
                return false;
            }
            return true;
        }

        private bool InsertFitsSecurityRequestLog(ref string returnMsg, ReqSecurityHeader model, string input, int count)
        {
            try
            {
                model.jsonValues = input;
                model.count_data = count;

                ResultWithModel rwm = _uow.External.InterfaceSecurityReq.Add(model);

                if (!rwm.Success)
                {
                    throw new Exception("[" + rwm.RefCode.ToString() + "] " + rwm.Message);
                }
            }
            catch (Exception Ex)
            {
                returnMsg = Ex.Message;
                return false;
            }
            return true;
        }

        private bool InsertFitsSecurityResultLog(ref string ReturnMsg, resSecurityHeader model)
        {
            try
            {

                ResultWithModel rwm = _uow.External.InterfaceSecurityRes.Add(model);

                if (!rwm.Success)
                {
                    throw new Exception("[" + rwm.RefCode + "] " + rwm.Message);
                }
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