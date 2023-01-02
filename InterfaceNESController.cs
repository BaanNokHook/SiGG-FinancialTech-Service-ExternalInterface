using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GM.CommonLibs;
using GM.CommonLibs.Common;
using GM.CommonLibs.Helper;
using GM.DataAccess.UnitOfWork;
using GM.Model.Common;
using GM.Model.ExternalInterface;
using GM.SFTPUtility;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class InterfaceNESController : ControllerBase
    {
        private static LogFile _log;
        private readonly IUnitOfWork _uow;
        private readonly IHostingEnvironment _env;
        public InterfaceNESController(IUnitOfWork uow, IHostingEnvironment env)
        {
            _uow = uow;
            _env = env;
            _log = new LogFile(_uow.GetConfiguration, "InterfaceNES");
        }

        [HttpPost]
        [Route("ExportInterfaceNES")]
        public ResultWithModel ExportInterfaceNES(InterfaceNesSftpModel model)
        {
            string strMsg = string.Empty;
            ServiceInOutReqModel inOutModel = new ServiceInOutReqModel();
            ResultWithModel rwm = new ResultWithModel();
            SftpEntity sftpEnt = new SftpEntity();
            try
            {
                _log.WriteLog("Start ExportInterfaceNES ==========");

                // Step 1 : Set Config
                if (Set_ConfigNes(ref strMsg, ref sftpEnt, ref model) == false)
                {
                    throw new Exception("Set_ConfigNes() : " + strMsg);
                }

                DataTable dt_Nes = new DataTable();

                _log.WriteLog("Write File");

                if (!string.IsNullOrEmpty(model.FileName))
                {
                    // Step 2 : Search NES
                    if (Search_Nes(ref strMsg, ref dt_Nes, model) == false)
                    {
                        throw new Exception("Search_Nes() : " + strMsg);
                    }

                    // Step 3 : Write File NES
                    if (Write_Nes(ref strMsg, dt_Nes, model) == false)
                    {
                        throw new Exception("Write_Nes() : " + strMsg);
                    }
                }

                // Step 4 : Send File To SFTP
                _log.WriteLog("SFTP File");
                if (sftpEnt.Enable == "Y")
                {
                    ArrayList listFile = new ArrayList();
                    ArrayList listFileSuccess = new ArrayList();
                    ArrayList listFileError = new ArrayList();
                    listFile.Add(model.FileName);

                    SftpUtility ObjectSftp = new SftpUtility();
                    if (!ObjectSftp.UploadSFTPList(ref strMsg, sftpEnt, listFile, ref listFileError,
                        ref listFileSuccess))
                    {
                        throw new Exception("UploadSFTPList() : " + strMsg);
                    }

                    model.FileFail = new List<string>();
                    foreach (var file in listFileError)
                    {
                        model.FileFail.Add(file.ToString());
                        _log.WriteLog("- SFTP " + file);
                    }

                    model.FileSuccess = new List<string>();
                    foreach (var file in listFileSuccess)
                    {
                        model.FileSuccess.Add(file.ToString());
                        _log.WriteLog("- SFTP " + file + " Success.");
                    }
                }
                else
                {
                    _log.WriteLog("UploadSFTP Disable.");
                }

                rwm.RefCode = 0;
                rwm.Message = "Success";
                rwm.Serverity = "low";
                rwm.Success = true;
            }
            catch (Exception ex)
            {
                rwm.RefCode = -999;
                rwm.Message = ex.Message;
                rwm.Serverity = "high";
                rwm.Success = false;
                _log.WriteLog("Error ExportInterfaceNES() : " + ex.Message);
            }
            finally
            {
                string json_Req = JsonConvert.SerializeObject(model);
                string json_Resp = JsonConvert.SerializeObject(rwm);

                string strGuid = Guid.NewGuid().ToString().ToUpper();
                inOutModel.guid = strGuid;
                inOutModel.svc_req = json_Req;
                inOutModel.svc_res = json_Resp;
                inOutModel.svc_type = "OUT";
                inOutModel.module_name = "InterfaceNESController";
                inOutModel.action_name = "ExportInterfaceNES";
                inOutModel.ref_id = strGuid;
                inOutModel.status = rwm.RefCode.ToString();
                inOutModel.status_desc = rwm.Message;
                inOutModel.create_by = "WebService";

                _uow.External.ServiceInOutReq.Add(inOutModel);

                _log.WriteLog("End ExportInterfaceNES ==========");
            }

            DataSet dsResult = new DataSet();
            DataTable dt = new List<InterfaceNesSftpModel>() { model }.ToDataTable<InterfaceNesSftpModel>();
            dt.TableName = "InterfaceNesSftpResultModel";
            dsResult.Tables.Add(dt);
            rwm.Data = dsResult;

            return rwm;
        }

        private bool Set_ConfigNes(ref string returnMsg, ref SftpEntity sftpEnt, ref InterfaceNesSftpModel model)
        {
            try
            {
                sftpEnt.Enable = model.RpConfigModel.FirstOrDefault(a => a.item_code == "ENABLE_SFTP")?.item_value;
                sftpEnt.RemoteServerName = model.RpConfigModel.FirstOrDefault(a => a.item_code == "IP")?.item_value;
                sftpEnt.RemotePort = model.RpConfigModel.FirstOrDefault(a => a.item_code == "PORT")?.item_value;
                sftpEnt.RemoteUserName = model.RpConfigModel.FirstOrDefault(a => a.item_code == "USER")?.item_value;
                sftpEnt.RemotePassword = model.RpConfigModel.FirstOrDefault(a => a.item_code == "PASSWORD")?.item_value;
                sftpEnt.RemoteSshHostKeyFingerprint = model.RpConfigModel.FirstOrDefault(a => a.item_code == "SSHHOSTKEYFINGERPRINT")?.item_value;
                sftpEnt.RemoteSshPrivateKeyPath = model.RpConfigModel.FirstOrDefault(a => a.item_code == "SSHPRIVATEKEYPATH")?.item_value;
                sftpEnt.RemoteServerPath = model.RpConfigModel.FirstOrDefault(a => a.item_code == "PATH_SFTP")?.item_value;
                sftpEnt.NoOfFailRetry = System.Convert.ToInt32(model.RpConfigModel.FirstOrDefault(a => a.item_code == "FAIL_RETRY")?.item_value);
                sftpEnt.LocalPath = Path.Combine(_env.ContentRootPath, model.FilePath.Replace("~\\", ""));
                model.FilePath = sftpEnt.LocalPath;

                model.FileName = model.RpConfigModel.FirstOrDefault(a => a.item_code == "FILE_NAME")?.item_value.Replace("ddMMyy", model.AsofDate.ToString("ddMMyy"));

                _log.WriteLog("- AsOfDate = " + model.AsofDate.ToString("yyyyMMdd"));
                _log.WriteLog("- Sftp Enable = " + sftpEnt.Enable);
                _log.WriteLog("- Sftp RemoteServerName = " + sftpEnt.RemoteServerName);
                _log.WriteLog("- Sftp RemotePort = " + sftpEnt.RemotePort);
                _log.WriteLog("- Sftp RemoteSshHostKeyFingerprint = " + sftpEnt.RemoteSshHostKeyFingerprint);
                _log.WriteLog("- Sftp RemoteSshPrivateKeyPath = " + sftpEnt.RemoteSshPrivateKeyPath);
                _log.WriteLog("- Sftp RemoteServerPath = " + sftpEnt.RemoteServerPath);
                _log.WriteLog("- Sftp NoOfFailRetry = " + sftpEnt.NoOfFailRetry);
                _log.WriteLog("- Sftp LocalPath = " + sftpEnt.LocalPath);
                _log.WriteLog("- File FileName = " + model.FileName);
            }
            catch (Exception Ex)
            {
                returnMsg = Ex.Message;
                return false;
            }
            return true;
        }

        private bool Search_Nes(ref string returnMsg, ref DataTable dt_Nes, InterfaceNesSftpModel model)
        {
            try
            {
                ResultWithModel rwm = _uow.External.InterfaceNes.Get(model);

                if (!rwm.Success)
                {
                    throw new Exception("[" + rwm.RefCode + "] " + rwm.Message);
                }

                DataSet ds = (DataSet)rwm.Data;
                dt_Nes = ds.Tables[0];
            }
            catch (Exception ex)
            {
                returnMsg = ex.Message;
                return false;
            }

            return true;
        }

        private bool Write_Nes(ref string returnMsg, DataTable dt_Nes, InterfaceNesSftpModel model)
        {
            try
            {
                WriteFile ObjWriteFile = new WriteFile();
                StringBuilder sb = new StringBuilder();

                foreach (DataRow dr in dt_Nes.Rows)
                {
                    foreach (DataColumn col in dt_Nes.Columns)
                    {
                        sb.Append(dr[col]);
                    }
                    sb.AppendLine();
                }

                FileEntity fileEnt = new FileEntity();
                fileEnt = new FileEntity();
                fileEnt.FileName = model.FileName;
                fileEnt.FilePath = model.FilePath;
                fileEnt.Values = sb.ToString();
                fileEnt.UseEncoding = new UTF8Encoding(false);

                if (ObjWriteFile.StreamWriterEncoding(ref fileEnt) == false)
                {
                    throw new Exception(fileEnt.Msg);
                }
                _log.WriteLog("Write File " + fileEnt.FileName + " Success.");
                
            }
            catch (Exception Ex)
            {
                returnMsg = Ex.Message;
                return false;
            }

            return true;
        }
    }
}