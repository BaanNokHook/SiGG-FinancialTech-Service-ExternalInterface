using GM.CommonLibs.Helper;
using GM.DataAccess.UnitOfWork;
using GM.Model.Common;
using GM.Model.ExternalInterface;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using GM.CommonLibs.Common;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class InterfaceConfirmationController : ControllerBase
    {
        private static LogFile _log;
        private readonly IUnitOfWork _uow;
        private readonly IHostingEnvironment _env;
        public InterfaceConfirmationController(IUnitOfWork uow, IHostingEnvironment env)
        {
            _uow = uow;
            _env = env;
            _log = new LogFile(_uow.GetConfiguration, "InterfaceConfirmation");
        }

        [HttpPost]
        [Route("GetInterfaceCCMList")]
        public ResultWithModel GetInterfaceCCMList(InterfaceCCMSearch model)
        {
            ResultWithModel rwm = new ResultWithModel();

            try
            {
                rwm = _uow.External.InterfaceConfirmation.GetList(model);
            }
            catch (Exception ex)
            {
                rwm.RefCode = 999;
                rwm.Message = ex.Message;
                rwm.Serverity = "high";
            }

            return rwm;
        }

        [HttpPost]
        [Route("SendConfirmation")]
        public ResultWithModel SendConfirmation(InterfaceConfirmationModel model)
        {
            ResultWithModel rwm = new ResultWithModel();
            
            try
            {
                _log.WriteLog("Start InterfaceConfirmation ==========");

                //set config
                _log.WriteLog("Set Config");
                string ENABLE_WS_CCM = model.RpConfigModel.FirstOrDefault(a => a.item_code == "ENABLE_WS_CCM")?.item_value;
                string SERVICE_CCM_URL = model.RpConfigModel.FirstOrDefault(a => a.item_code == "SERVICE_CCM_URL")?.item_value;
                string PATH_SERVICE = model.RpConfigModel.FirstOrDefault(a => a.item_code == "PATH_SERVICE")?.item_value;
                string REPORT_URL = _uow.GetConfiguration.GetSection("ReportSite").Value + "RPConfirmationReport/DownloadPDF?trans_no={0}&print_confirm_bo1_by={1}&print_confirm_bo2_by={2}&type_code={3}&access_token=repo2022";

                _log.WriteLog("- ENABLE_WS_CCM = " + ENABLE_WS_CCM);
                _log.WriteLog("- SERVICE_CCM_URL = " + SERVICE_CCM_URL);
                _log.WriteLog("- PATH_SERVICE = " + PATH_SERVICE);
                _log.WriteLog("- REPORT_URL = " + REPORT_URL);

                //for test
                //REPORT_URL = "http://localhost:56080/RPConfirmationReport/DownloadPDF?trans_no={0}&print_confirm_bo1_by={1}&print_confirm_bo2_by={2}";

                _log.WriteLog("Set CreateConfirmationRequest" + model.RefId);
                _log.WriteLog("- RefId = " + model.RefId);
                _log.WriteLog("- tradeId = " + model.tradeId);
                _log.WriteLog("- transTypeCode = " + model.transTypeCode);
                CreateConfirmationRequest createConfirmationRequest = new CreateConfirmationRequest();

                //header
                createConfirmationRequest.ChannelId = "REPO";//fix
                createConfirmationRequest.RefId = model.RefId;
                createConfirmationRequest.TransDate = model.TransDate;
                createConfirmationRequest.TransTime = model.TransTime;
                createConfirmationRequest.transTypeCode = model.transTypeCode;

                //Detail
                #region Detail
                createConfirmationRequest.tradeId = model.tradeId;
                createConfirmationRequest.confirmId = model.confirmId;
                createConfirmationRequest.CIFID = model.CIFID;
                createConfirmationRequest.CIFName = model.CIFName;
                //createConfirmationRequest.productType = "CCSREPO";//fix
                createConfirmationRequest.productType = model.productType;
                //createConfirmationRequest.costcenter = "108373";//fix
                createConfirmationRequest.costcenter = model.costcenter;
                createConfirmationRequest.instumentCode = model.instumentCode;
                createConfirmationRequest.tradeEvent = model.tradeEvent;
                createConfirmationRequest.customerEmail = model.customerEmail;
                createConfirmationRequest.documentType = "RE01";//fix
                createConfirmationRequest.tradeDate = model.tradeDate;
                createConfirmationRequest.valueDate = model.valueDate;
                createConfirmationRequest.createDate = model.createDate;
                createConfirmationRequest.settlementDate = model.settlementDate;
                createConfirmationRequest.maturityDate = model.maturityDate;
                createConfirmationRequest.expiryDate = model.expiryDate;
                createConfirmationRequest.ccy1 = model.ccy1;
                createConfirmationRequest.amount1 = model.amount1;
                createConfirmationRequest.ccy2 = model.ccy2;
                createConfirmationRequest.amount2 = model.amount2;
                createConfirmationRequest.rate1 = model.rate1;
                createConfirmationRequest.amount3 = model.amount3;
                createConfirmationRequest.amount4 = model.amount4;
                createConfirmationRequest.rate2 = model.rate2;
                createConfirmationRequest.counterPartyCode = model.counterPartyCode;
                createConfirmationRequest.counterPartyNameThi = model.counterPartyNameThi;
                createConfirmationRequest.counterPartyNameEng = model.counterPartyNameEng;
                createConfirmationRequest.partyACode = model.partyACode;
                createConfirmationRequest.partyAName = model.partyAName;
                createConfirmationRequest.partyBCode = model.partyBCode;
                createConfirmationRequest.partyBName = model.partyBName;
                createConfirmationRequest.val1 = model.val1;
                createConfirmationRequest.val2 = model.val2;
                createConfirmationRequest.val3 = model.val3;
                createConfirmationRequest.val4 = model.val4;
                createConfirmationRequest.val5 = model.val5;
                createConfirmationRequest.val6 = model.val6;
                createConfirmationRequest.val7 = model.val7;
                createConfirmationRequest.val8 = model.val8;
                createConfirmationRequest.val9 = model.val9;
                createConfirmationRequest.val10 = model.val10;
                createConfirmationRequest.val11 = model.val11;
                createConfirmationRequest.val12 = model.val12;
                createConfirmationRequest.val13 = model.val13;
                createConfirmationRequest.val14 = model.val14;
                createConfirmationRequest.val15 = model.val15;
                createConfirmationRequest.val16 = model.val16;
                createConfirmationRequest.val17 = model.val17;
                createConfirmationRequest.val18 = "0";//fix
                //createConfirmationRequest.val19 = "CCSREPO";//fix
                createConfirmationRequest.val19 = model.val19;
                createConfirmationRequest.val20 = model.val20;
                #endregion

                //ResultWithModel rwmSignname = new ResultWithModel();
                //rwmSignname = GetSignName(model.tradeId, model.create_by);
                _log.WriteLog("Get SignName");
                DataTable dtSignname = GetSignName(model.tradeId, model.create_by);

                string print_confirm_bo1_by = "";
                string print_confirm_bo2_by = "";

                if (dtSignname.Rows.Count > 0)
                {
                    print_confirm_bo1_by = dtSignname.Rows[0]["print_confirm_bo1_by"].ToString();
                    print_confirm_bo2_by = dtSignname.Rows[0]["print_confirm_bo2_by"].ToString();
                }

                //file
                createConfirmationRequest.fileName = model.RefId + ".pdf";
                _log.WriteLog("Download File " + createConfirmationRequest.fileName);
                byte[] byteFile = DownloadData(string.Format(REPORT_URL, model.tradeId, print_confirm_bo1_by, print_confirm_bo2_by, model.transTypeCode));

                if (byteFile == null)
                {
                    rwm.RefCode = 999;
                    rwm.Message = model.tradeId + " error: Download is null";
                    rwm.Serverity = "high";
                    rwm.Success = false;
                    return rwm;
                }

                ////write file
                _log.WriteLog("Write File " + createConfirmationRequest.fileName);
                var filePath = Path.Combine(_env.ContentRootPath, PATH_SERVICE.Replace("~\\", ""));
                WriteFile(filePath, createConfirmationRequest.fileName, byteFile);

                if (ENABLE_WS_CCM == "Y")
                {
                    //insert log in out
                    _log.WriteLog("Insert LogInOut");
                    string req = JsonConvert.SerializeObject(createConfirmationRequest);
                    ServiceInOutReqModel serviceInOutReq = new ServiceInOutReqModel();
                    serviceInOutReq.guid = model.guid;
                    serviceInOutReq.svc_req = req;
                    serviceInOutReq.svc_res = null;
                    serviceInOutReq.svc_type = "OUT";
                    serviceInOutReq.module_name = "CCM";
                    serviceInOutReq.action_name = "CONFIRMATION";
                    serviceInOutReq.ref_id = model.RefId;
                    serviceInOutReq.status = null;
                    serviceInOutReq.status_desc = null;
                    serviceInOutReq.create_by = model.create_by;
                    _uow.External.ServiceInOutReq.Add(serviceInOutReq);
                    //InsertServiceInOutReq(serviceInOutReq);

                    createConfirmationRequest.fileContent = byteFile;

                    InterfaceConfirmationModel interfaceConfirmationModel = new InterfaceConfirmationModel();
                    string xmlPlayload = interfaceConfirmationModel.XmlPlayload(createConfirmationRequest);

                    _log.WriteLog("Send createConfirmation");
                    string response = SOAPHelper.SendSOAPRequest(SERVICE_CCM_URL, "createConfirmation", xmlPlayload, "createConfirmation", false);
                    if (response.Length > 0)
                    {
                        try
                        {
                            if (response.Contains("<?xml") && response.Contains("--MIMEBoundary"))
                            {

                                int startIndex = response.IndexOf("<?xml", StringComparison.Ordinal);
                                int endIndex = response.IndexOf("--MIMEBoundary", startIndex, StringComparison.Ordinal);
                                string strResponse = response.Substring(startIndex, endIndex - startIndex);

                                XDocument xdc = XDocument.Parse(strResponse);
                                XmlNamespaceManager nsManager = new XmlNamespaceManager(new NameTable());
                                nsManager.AddNamespace("soapenv", "http://schemas.xmlsoap.org/soap/envelope/");
                                nsManager.AddNamespace("ns1", "http://service.cfmt.ktb.co.th/");
                                var eleList = xdc.XPathSelectElements("//ns1:CreateConfirmationResponse", nsManager);

                                CreateConfirmationResponse createConfirmationResponse = new CreateConfirmationResponse();
                                foreach (var xElement in eleList)
                                {
                                    createConfirmationResponse.ResponseCode = xElement.Elements()
                                        .FirstOrDefault(a => a.Name.LocalName == "ResponseCode")?.Value;
                                    createConfirmationResponse.ResponseDesc = xElement.Elements()
                                        .FirstOrDefault(a => a.Name.LocalName == "ResponseDesc")?.Value;
                                }

                                //0 = Successfully, 1 = Not Success, 2 = The Confirmation have already confirmed by customer and stored in system
                                if (createConfirmationResponse.ResponseCode == "0")
                                {
                                    _log.WriteLog("Successfully");
                                    _uow.External.InterfaceConfirmation.AddLog(model.RefId, model.tradeId, model.transTypeCode, model.create_by);

                                    //update log in out
                                    string res = JsonConvert.SerializeObject(createConfirmationResponse);
                                    serviceInOutReq.status = "0";
                                    serviceInOutReq.status_desc = "Success";
                                    serviceInOutReq.svc_res = res;
                                    _uow.External.ServiceInOutReq.Update(serviceInOutReq);

                                    rwm.RefCode = 0;
                                    rwm.Message = "[Success] tradeId: " + model.tradeId + " guid: " + model.guid + " transTypeCode: " + model.transTypeCode;
                                    rwm.Serverity = "Low";
                                    rwm.Success = true;
                                    return rwm;
                                }
                                else if (createConfirmationResponse.ResponseCode == "1" ||
                                         createConfirmationResponse.ResponseCode == "2")
                                {
                                    _log.WriteLog("Not Success - ResponseCode = " + createConfirmationResponse.ResponseCode);

                                    //update log in out
                                    string res = JsonConvert.SerializeObject(createConfirmationResponse);
                                    serviceInOutReq.status = "-1";
                                    serviceInOutReq.status_desc = "Not Success";
                                    serviceInOutReq.svc_res = res;
                                    _uow.External.ServiceInOutReq.Update(serviceInOutReq);

                                    rwm.RefCode = 999;
                                    rwm.Message = "[Not Success] tradeId: " + model.tradeId + " guid: " + model.guid + " transTypeCode: " + model.transTypeCode;
                                    rwm.Serverity = "high";
                                    rwm.Success = false;
                                    return rwm;
                                }
                                else
                                {
                                    _log.WriteLog("Not Success - ResponseCode = " + createConfirmationResponse.ResponseCode + ", strResponse = " + strResponse);

                                    //update log in out
                                    serviceInOutReq.status = "-1";
                                    serviceInOutReq.status_desc = "Not Success";
                                    serviceInOutReq.svc_res = strResponse;
                                    _uow.External.ServiceInOutReq.Update(serviceInOutReq);

                                    rwm.RefCode = 999;
                                    rwm.Message = "[Not Success] tradeId: " + model.tradeId + " guid: " + model.guid + " transTypeCode: " + model.transTypeCode;
                                    rwm.Serverity = "high";
                                    rwm.Success = false;
                                    return rwm;
                                }
                            }
                            else
                            {
                                _log.WriteLog("Not Success - response = " + response);

                                //update log in out
                                serviceInOutReq.status = "-1";
                                serviceInOutReq.status_desc = "Not Success";
                                serviceInOutReq.svc_res = response;
                                _uow.External.ServiceInOutReq.Update(serviceInOutReq);

                                rwm.RefCode = 999;
                                rwm.Message = "[Not Success] tradeId: " + model.tradeId + " guid: " + model.guid + " transTypeCode: " + model.transTypeCode;
                                rwm.Serverity = "high";
                                rwm.Success = false;
                                return rwm;
                            }
                        }
                        catch (Exception ex)
                        {
                            _log.WriteLog("Error Response = " + ex.Message);

                            //update log in out
                            serviceInOutReq.status = "-1";
                            serviceInOutReq.status_desc = "Not Success";
                            serviceInOutReq.svc_res = ex.Message;
                            _uow.External.ServiceInOutReq.Update(serviceInOutReq);

                            rwm.RefCode = 999;
                            rwm.Message = "[Error] tradeId: " + model.tradeId + " guid: " + model.guid + " transTypeCode: " + model.transTypeCode + " description " + ex.Message;
                            rwm.Serverity = "high";
                            rwm.Success = false;
                            return rwm;
                        }
                    }
                }

                rwm.RefCode = 0;
                rwm.Message = "[Success] tradeId: " + model.tradeId + " guid: " + model.guid + " transTypeCode: " + model.transTypeCode;
                rwm.Serverity = "Low";
                rwm.Success = true;
            }
            catch (Exception ex)
            {
                _log.WriteLog("Error = " + ex.Message);

                rwm.RefCode = 999;
                rwm.Message = "[Error] tradeId: " + model.tradeId + " guid: " + model.guid + " transTypeCode: " + model.transTypeCode + " description " + ex.Message;
                rwm.Serverity = "high";
                rwm.Success = false;
            }
            finally
            {
                _log.WriteLog("End InterfaceConfirmation ==========");
            }

            return rwm;
        }

        //public ResultWithModel InsertServiceInOutReq(ServiceInOutReqModel model)
        //{
        //    ResultWithModel rwm = new ResultWithModel();
        //    BaseParameterModel Parameter = new BaseParameterModel();

        //    Parameter.ProcedureName = "GM_Service_in_out_req_Insert_Proc";
        //    Parameter.Parameters.Add(new Field { Name = "guid", Value = model.guid });
        //    Parameter.Parameters.Add(new Field { Name = "svc_req", Value = model.svc_req });
        //    Parameter.Parameters.Add(new Field { Name = "svc_res", Value = model.svc_res });
        //    Parameter.Parameters.Add(new Field { Name = "svc_type", Value = model.svc_type });
        //    Parameter.Parameters.Add(new Field { Name = "module_name", Value = model.module_name });
        //    Parameter.Parameters.Add(new Field { Name = "action_name", Value = model.action_name });
        //    Parameter.Parameters.Add(new Field { Name = "ref_id", Value = model.ref_id });
        //    Parameter.Parameters.Add(new Field { Name = "status", Value = model.status });
        //    Parameter.Parameters.Add(new Field { Name = "status_desc", Value = model.status_desc });
        //    Parameter.Parameters.Add(new Field { Name = "recorded_by", Value = model.create_by });
        //    Parameter.ResultModelNames.Add("InterfaceConfirmationResultModel");

        //    rwm = db.ExecuteNonQuery(Parameter);

        //    return rwm;
        //}

        //public ResultWithModel UpdateServiceInOutReq(ServiceInOutReqModel model)
        //{
        //    ResultWithModel rwm = new ResultWithModel();
        //    BaseParameterModel Parameter = new BaseParameterModel();

        //    Parameter.ProcedureName = "GM_Service_in_out_req_Update_Proc";
        //    Parameter.Parameters.Add(new Field { Name = "guid", Value = model.guid });
        //    Parameter.Parameters.Add(new Field { Name = "svc_res", Value = model.svc_res });
        //    Parameter.Parameters.Add(new Field { Name = "ref_id", Value = model.ref_id });
        //    Parameter.Parameters.Add(new Field { Name = "status", Value = model.status });
        //    Parameter.Parameters.Add(new Field { Name = "status_desc", Value = model.status_desc });
        //    Parameter.Parameters.Add(new Field { Name = "recorded_by", Value = model.create_by });
        //    Parameter.ResultModelNames.Add("InterfaceConfirmationResultModel");

        //    rwm = db.ExecuteNonQuery(Parameter);

        //    return rwm;
        //}

        private void WriteFile(string pathFile, string fileName, byte[] data)
        {
            if (!Directory.Exists(@pathFile))
            {
                Directory.CreateDirectory(@pathFile);
            }

            if (System.IO.File.Exists(@pathFile + "\\" + fileName))
            {
                System.IO.File.Delete(@pathFile + "\\" + fileName);
            }

            using (FileStream fs = new FileStream(@pathFile + "\\" + fileName, FileMode.CreateNew, FileAccess.Write))
            {
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    bw.Write(data);
                }
            }
        }

        private DataTable GetSignName(string trans_no, string create_by)
        {
            DataTable dt = new DataTable();

            ResultWithModel rwm = _uow.External.InterfaceConfirmation.GetSignName(trans_no, create_by);

            DataSet Ds = new DataSet();
            Ds = (DataSet)rwm.Data;
            dt = Ds.Tables[0];

            return dt;
        }

        private byte[] DownloadData(string url, string action, object obj)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                    return null;

                var requestData = JsonConvert.SerializeObject(obj, new IsoDateTimeConverter() { DateTimeFormat = "dd/MM/yyyy HH:mm:ss" });

                var client = new HttpClient();
                client.BaseAddress = new Uri(url);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage response = client.PostAsJsonAsync(action, requestData).Result;
                if (response.IsSuccessStatusCode)
                {
                    return response.Content.ReadAsByteArrayAsync().Result;
                }

            }
            catch (Exception ex)
            {

            }

            return null;
        }

        private byte[] DownloadData(string serverUrlAddress)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(serverUrlAddress))
                    throw new Exception("url download not found");

                // Create a new WebClient instance
                using (WebClient client = new WebClient())
                {
                    if (serverUrlAddress.StartsWith("https://"))
                        ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, errors) => true;

                    var byteFile = client.DownloadData(serverUrlAddress);
                    string type = client.ResponseHeaders["Content-Type"];
                    if (!type.Contains("pdf"))
                    {
                        throw new Exception("content type not PDF");
                    }

                    return byteFile;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("DownloadData: " + ex.Message);
            }
        }

    }
}