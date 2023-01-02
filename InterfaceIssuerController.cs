using GM.DataAccess.UnitOfWork;
using GM.Model.Common;
using GM.Model.InterfaceIssuer;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class InterfaceIssuerController : ControllerBase
    {
        private readonly IUnitOfWork _uow;
        public InterfaceIssuerController(IUnitOfWork uow)
        {
            _uow = uow;
        }

        [HttpPost]
        [Route("[action]")]
        public string ImportIssuer(reqIssuer reqIssuer)
        {
            string strMsg = string.Empty;
            resIssuerHeader resIssuerHeader = new resIssuerHeader();
            reqIssuerHeader header = new reqIssuerHeader();

            try
            {
                // Step 1 : Read Json
                string input = JsonConvert.SerializeObject(reqIssuer);
                header = reqIssuer.reqIssuerHeader;
                List<reqIssuerList>  issuerList = reqIssuer.reqIssuerList;

                header.JsonValues = input;
                header.CountData = issuerList.Count;

                if (!InsertFitsIssuerRequestLog(ref strMsg, header))
                {
                    throw new Exception("Insert_Fits_IssuerRequestLog() : " + strMsg);
                }

                // Loop IssuerList
                for (int i = 0; i < issuerList.Count; i++)
                {
                    resIssuerList respDetail = new resIssuerList();
                    strMsg = string.Empty;
                    try
                    {
                        // Step 2 : Insert IssuerList
                        issuerList[i].Ref_code = header.ref_code;
                        if (!InsertTempIssuer(ref strMsg, issuerList[i]))
                        {
                            throw new Exception("Insert_TempIssuer() : " + strMsg);
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
                        respDetail.issuer = issuerList[i].issuer_id + "|" + issuerList[i].issuer_code;
                    }

                    resIssuerHeader.response_details.Add(respDetail);
                }

                // Step 3 : Import Temp To Issuer
                if (!ImportIssuer(ref strMsg, header.ref_code))
                {
                    throw new Exception("Import_Issuer() : " + strMsg);
                }

                resIssuerHeader.response_code = "0";
                resIssuerHeader.response_message = "Success";
            }
            catch (Exception Ex)
            {
                resIssuerHeader.response_code = "-999";
                resIssuerHeader.response_message = "Fail : " + Ex.Message;
            }
            finally
            {
                resIssuerHeader.channel = header.channel;
                resIssuerHeader.ref_code = header.ref_code;
                resIssuerHeader.mode = header.mode;
                resIssuerHeader.response_date = DateTime.Now.ToString("yyyyMMdd");
                resIssuerHeader.response_time = DateTime.Now.ToString("HH:mm:ss");
            }

            var responseIssuer = (new { responseIssuer = resIssuerHeader });
            string result = JsonConvert.SerializeObject(responseIssuer);

            if (!InsertFitsIssuerResultLog(ref strMsg, resIssuerHeader))
            {
                resIssuerHeader.response_code = "-999";
                resIssuerHeader.response_message = "Fail : Insert_Fits_IssuerResultLog() : " + strMsg;
                responseIssuer = (new { responseIssuer = resIssuerHeader });
                result = JsonConvert.SerializeObject(responseIssuer);
            }

            return result;
        }

        private bool InsertTempIssuer(ref string returnMsg, reqIssuerList model)
        {
            try
            {
                ResultWithModel rwm = _uow.External.InterfaceIssuer.Add(model);
                if (rwm.Success == false)
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

        private bool ImportIssuer(ref string returnMsg, string ref_code)
        {
            try
            {
                ResultWithModel rwm = _uow.External.InterfaceIssuerImport.Add(new reqIssuer() { RefCode = ref_code, Function = "FITS" });
                if (rwm.Success == false)
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

        private bool InsertFitsIssuerRequestLog(ref string returnMsg, reqIssuerHeader model)
        {
            try
            {
                ResultWithModel rwm = _uow.External.InterfaceIssuerHeaderReq.Add(model);
                if (rwm.Success == false)
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

        private bool InsertFitsIssuerResultLog(ref string ReturnMsg, resIssuerHeader model)
        {
            try
            {
                ResultWithModel rwm = _uow.External.InterfaceIssuerHeaderRes.Add(model);

                if (rwm.Success == false)
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