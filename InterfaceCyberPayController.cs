using GM.CommonLibs;
using GM.CommonLibs.Common;
using GM.CommonLibs.Helper;
using GM.DataAccess.UnitOfWork;
using GM.Model.Common;
using GM.Model.Provider;
using GM.Model.Static;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Data;
using System.Net.Http;
using System.Net.Http.Headers;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class InterfaceCyberPayController : ControllerBase
    {
        private readonly IUnitOfWork _uow;
        private static LogFile _log;
        public InterfaceCyberPayController(IUnitOfWork uow)
        {
            _uow = uow;
            _log = new LogFile(_uow.GetConfiguration, "InterfaceCyberPay");
        }

        [HttpPost]
        [Route("CheckSettlementStatus")]
        public ResultWithModel<CheckSettlementStatusResponseModel> CheckSettlementStatus(CheckSettlementStatusRequestModel model)
        {
            _log.WriteLog("CheckSettlementStatus Start");
            ResultWithModel<CheckSettlementStatusResponseModel> rwm = new ResultWithModel<CheckSettlementStatusResponseModel>();
            CheckSettlementStatusResponseModel resultModel = new CheckSettlementStatusResponseModel();
            DataTable dtConfig = new DataTable();
            FileEntity fileEnt = new FileEntity();
            WriteFile objWriteFile = new WriteFile();
            string strMsg = string.Empty;

            try
            {
                if (!SearchConfig(ref strMsg, ref dtConfig, "RP_CYBER_PAY_INTERFACE"))
                {
                    throw new Exception("SearchConfig CyberPay : " + strMsg);
                }

                var config = dtConfig.DataTableToListForNull<ConfigModel>();

                string isEnable = config.Find(x => x.item_code == "ENABLE_CHECK").item_value;

                if (isEnable == "Y")
                {
                    string logPath = config.Find(x => x.item_code == "LOG_PATH").item_value;
                    string formatName = config.Find(x => x.item_code == "API_SEARCH_LOG_FILE_NAME").item_value;
                    string url = config.Find(x => x.item_code == "API_CHECK_URL").item_value;
                    int timeOut = Convert.ToInt32(config.Find(x => x.item_code == "API_CHECK_TIMEOUT").item_value);

                    fileEnt.FileName = "check_" + DateTime.Now.ToString(formatName) + ".json";
                    fileEnt.FilePath = logPath + @"\CheckSettlementStatus\";
                    fileEnt.Values = "Request : " + System.Environment.NewLine + JsonConvert.SerializeObject(model, Formatting.Indented) + System.Environment.NewLine;

                    //var requestData = JsonConvert.SerializeObject(model, new IsoDateTimeConverter() { DateTimeFormat = "dd/MM/yyyy HH:mm:ss" });

                    HttpClient client = new HttpClient();
                    client.BaseAddress = new Uri(url);
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.Timeout = TimeSpan.FromMilliseconds(timeOut);

                    HttpResponseMessage response = client.PostAsJsonAsync("", model).Result;
                    rwm.Success = response.IsSuccessStatusCode;

                    if (rwm.Success)
                    {
                        var temp = response.Content.ReadAsStringAsync().Result;
                        resultModel = JsonConvert.DeserializeObject<CheckSettlementStatusResponseModel>(temp);
                        rwm.Data = resultModel;
                        if (string.IsNullOrEmpty(rwm.Message))
                        {
                            rwm.Message = string.Empty;
                        }
                    }
                    else
                    {
                        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            this.Unauthorized();
                        }

                        rwm.Data = default(CheckSettlementStatusResponseModel);
                        rwm.Message = response.ReasonPhrase;
                    }

                    fileEnt.Values += "Respond : " + System.Environment.NewLine + JsonConvert.SerializeObject(rwm.Data, Formatting.Indented);
                    if (objWriteFile.StreamWriter(ref fileEnt) == false)
                    {
                        _log.WriteLog(fileEnt.Msg);
                    }
                }
                else
                {
                    rwm.Success = false;
                    rwm.Message = "CheckSettlementStatus is not available";
                    rwm.Serverity = "Low";
                }
            }
            catch (Exception ex)
            {
                rwm.Success = false;
                rwm.Message = ex.Message;
                rwm.Serverity = "Low";
            }

            _log.WriteLog("CheckSettlementStatus Stop");
            return rwm;
        }

        [HttpPost]
        [Route("InsertSettlementInfo")]
        public ResultWithModel<InsertSettlementInfoResponseModel> InsertSettlementInfo(InsertSettlementInfoRequestModel model)
        {
            _log.WriteLog("InsertSettlementInfo Start");
            ResultWithModel<InsertSettlementInfoResponseModel> rwm = new ResultWithModel<InsertSettlementInfoResponseModel>();
            InsertSettlementInfoResponseModel resultModel = new InsertSettlementInfoResponseModel();
            DataTable dtConfig = new DataTable();
            FileEntity fileEnt = new FileEntity();
            WriteFile objWriteFile = new WriteFile();
            string strMsg = string.Empty;

            try
            {
                if (!SearchConfig(ref strMsg, ref dtConfig, "RP_CYBER_PAY_INTERFACE"))
                {
                    throw new Exception("SearchConfig CyberPay : " + strMsg);
                }

                var config = dtConfig.DataTableToListForNull<ConfigModel>();

                string isEnable = config.Find(x => x.item_code == "ENABLE_INSERT").item_value;

                if (isEnable == "Y")
                {
                    string logPath = config.Find(x => x.item_code == "LOG_PATH").item_value;
                    string formatName = config.Find(x => x.item_code == "API_SEARCH_LOG_FILE_NAME").item_value;
                    string url = config.Find(x => x.item_code == "API_INSERT_URL").item_value;
                    int timeOut = Convert.ToInt32(config.Find(x => x.item_code == "API_INSERT_TIMEOUT").item_value);

                    fileEnt.FileName = "insert_" + DateTime.Now.ToString(formatName) + ".json";
                    fileEnt.FilePath = logPath + @"\InsertSettlementInfo\";
                    fileEnt.Values = "Request : " + System.Environment.NewLine + JsonConvert.SerializeObject(model, Formatting.Indented) + System.Environment.NewLine;

                    //var requestData = JsonConvert.SerializeObject(model, new IsoDateTimeConverter() { DateTimeFormat = "dd/MM/yyyy HH:mm:ss" });

                    HttpClient client = new HttpClient();
                    client.BaseAddress = new Uri(url);
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.Timeout = TimeSpan.FromMilliseconds(timeOut);

                    HttpResponseMessage response = client.PostAsJsonAsync("", model).Result;
                    rwm.Success = response.IsSuccessStatusCode;

                    if (rwm.Success)
                    {
                        var temp = response.Content.ReadAsStringAsync().Result;
                        resultModel = JsonConvert.DeserializeObject<InsertSettlementInfoResponseModel>(temp);
                        rwm.Data = resultModel;
                        if (string.IsNullOrEmpty(rwm.Message))
                        {
                            rwm.Message = string.Empty;
                        }
                    }
                    else
                    {
                        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            this.Unauthorized();
                        }

                        rwm.Data = default(InsertSettlementInfoResponseModel);
                        rwm.Message = response.ReasonPhrase;
                    }

                    fileEnt.Values += "Respond : " + System.Environment.NewLine + JsonConvert.SerializeObject(rwm.Data, Formatting.Indented);
                    if (objWriteFile.StreamWriter(ref fileEnt) == false)
                    {
                        _log.WriteLog(fileEnt.Msg);
                    }
                }
                else
                {
                    rwm.Success = false;
                    rwm.Message = "InsertSettlementInfo is not available";
                    rwm.Serverity = "Low";
                }
            }
            catch (Exception ex)
            {
                rwm.Success = false;
                rwm.Message = ex.Message;
                rwm.Serverity = "Low";
            }

            _log.WriteLog("InsertSettlementInfo Stop");
            return rwm;
        }

        private bool SearchConfig(ref string ReturnMsg, ref DataTable dtConfig, string Category)
        {
            try
            {
                ConfigParameterModel model = new ConfigParameterModel();
                model.ProcedureName = "GM_Config_List_Proc";
                model.ModelResult = "ConfigResultModel";
                model.Parameters.Add(new Field { Name = "category", Value = Category });
                model.Paging.PageNumber = 1;
                model.Paging.RecordPerPage = 999999;
                ResultWithModel rwm = _uow.Static.Config.Get(model);

                if (rwm.Success == false)
                {
                    throw new Exception("[" + rwm.RefCode.ToString() + "] " + rwm.Message);
                }

                DataSet ds = (DataSet)rwm.Data;
                dtConfig = ds.Tables[0];
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
