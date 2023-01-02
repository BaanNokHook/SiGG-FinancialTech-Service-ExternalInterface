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
using GM.Model.Static;
using GM.SFTPUtility;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class InterfaceSwiftSftpController : ControllerBase
    {

        private static LogFile _log;
        private readonly IUnitOfWork _uow;
        private readonly IHostingEnvironment _env;

        public InterfaceSwiftSftpController(IUnitOfWork uow, IHostingEnvironment env)
        {
            _uow = uow;
            _env = env;
            _log = new LogFile(_uow.GetConfiguration, "InterfaceSwiftSftp");
        }

        [HttpPost]
        [Route("SwiftManual")]
        public ResultWithModel SwiftManual(InterfaceSwiftSftpModel model)
        {
            string strMsg = string.Empty;
            ResultWithModel rwm = new ResultWithModel();
            ServiceInOutReqModel inOutModel = new ServiceInOutReqModel();

            try
            {
                _log.WriteLog("Start SWIFT Manual ==========");
                SftpEntity sftpOutEnt = new SftpEntity();
                SftpEntity sftpBackOutEnt = new SftpEntity();
                SftpUtility ObjectSftp = new SftpUtility();

                //set folder
                model.FilePath = @"\MANUAL";

                if (SearchConfigReleaseMsg(ref strMsg, ref sftpOutEnt, ref sftpBackOutEnt, model) == false)
                {
                    throw new Exception("SearchConfigReleaseMsg() => " + strMsg);
                }

                _log.WriteLog("Write File");

                if (WriteFileManual(ref strMsg, model) == false)
                {
                    throw new Exception("WriteFileManual() => " + strMsg);
                }

                if (sftpOutEnt.Enable == "Y")
                {
                    // Step 1 : Sftp REPO_OUT
                    ArrayList ListFile = new ArrayList();
                    ArrayList ListFileSuccess = new ArrayList();
                    ArrayList ListFileError = new ArrayList();

                    #region Sftp REPO_OUT
                    _log.WriteLog("SFTP REPO_OUT");
                    ListFile.Add(model.FileName);

                    if (!ObjectSftp.UploadSFTPList(ref strMsg, sftpOutEnt, ListFile, ref ListFileError,
                        ref ListFileSuccess))
                    {
                        throw new Exception("UploadSFTPList() => " + strMsg);
                    }

                    if (ListFileError.Count > 0)
                    {
                        _log.WriteLog("- SFTP Fail" + sftpOutEnt.RemoteServerPath + " " + ListFileError[0]);
                    }
                    else
                    {
                        foreach (var FileSuccess in ListFileSuccess)
                        {
                            _log.WriteLog("- SFTP " + sftpOutEnt.RemoteServerPath + " " + FileSuccess + " Success.");
                        }
                    }
                    #endregion

                    // Step 2 : Sftp REPO_BACKOUT
                    #region Sftp REPO_BACKOUT
                    _log.WriteLog("SFTP REPO_OUT");
                    ListFile = new ArrayList();
                    ListFileSuccess = new ArrayList();
                    ListFileError = new ArrayList();

                    ListFile.Add(model.FileName);

                    ObjectSftp = new SftpUtility();
                    if (!ObjectSftp.UploadSFTPList(ref strMsg, sftpBackOutEnt, ListFile, ref ListFileError, ref ListFileSuccess))
                    {
                        throw new Exception("UploadSFTPList() => " + strMsg);
                    }

                    if (ListFileError.Count > 0)
                    {
                        _log.WriteLog("- SFTP Fail" + sftpBackOutEnt.RemoteServerPath + " " + ListFileError[0]);
                    }
                    else
                    {
                        foreach (var FileSuccess in ListFileSuccess)
                        {
                            _log.WriteLog("- SFTP " + sftpBackOutEnt.RemoteServerPath + " " + FileSuccess + " Success.");
                        }
                    }
                    #endregion
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
                _log.WriteLog("Error SwiftManual() : " + ex.Message);
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
                inOutModel.module_name = "InterfaceSwiftSftp";
                inOutModel.action_name = "SwiftManual";
                inOutModel.ref_id = model.FileName;
                inOutModel.status = rwm.RefCode.ToString();
                inOutModel.status_desc = rwm.Message;
                inOutModel.create_by = "WebService";

                _uow.External.ServiceInOutReq.Add(inOutModel);

                _log.WriteLog("End Start SWIFT ==========");
            }

            DataSet dsResult = new DataSet();
            DataTable dt = new List<InterfaceSwiftSftpModel>() { model }.ToDataTable<InterfaceSwiftSftpModel>();
            dt.TableName = "InterfaceSwiftSftpResultModel";
            dsResult.Tables.Add(dt);
            rwm.Data = dsResult;

            return rwm;
        }

        private bool SearchConfigReleaseMsg(ref string returnMsg, ref SftpEntity sftpOutEnt, ref SftpEntity sftpBackOutEnt, InterfaceSwiftSftpModel model)
        {
            try
            {
                sftpOutEnt.Enable = model.RpConfigModel.FirstOrDefault(a => a.item_code == "ENABLE")?.item_value;
                sftpOutEnt.RemoteServerName = model.RpConfigModel.FirstOrDefault(a => a.item_code == "IP")?.item_value;
                sftpOutEnt.RemotePort = model.RpConfigModel.FirstOrDefault(a => a.item_code == "PORT")?.item_value;
                sftpOutEnt.RemoteUserName = model.RpConfigModel.FirstOrDefault(a => a.item_code == "USER")?.item_value;
                sftpOutEnt.RemotePassword = model.RpConfigModel.FirstOrDefault(a => a.item_code == "PASSWORD")?.item_value;
                sftpOutEnt.RemoteServerPath = model.RpConfigModel.FirstOrDefault(a => a.item_code == "PATH_SFTP_REPO_OUT")?.item_value;
                sftpOutEnt.RemoteSshHostKeyFingerprint = model.RpConfigModel.FirstOrDefault(a => a.item_code == "SSHHOSTKEYFINGERPRINT")?.item_value;
                sftpOutEnt.RemoteSshPrivateKeyPath = model.RpConfigModel.FirstOrDefault(a => a.item_code == "SSHPRIVATEKEYPATH")?.item_value;
                sftpOutEnt.NoOfFailRetry = Convert.ToInt32(model.RpConfigModel.FirstOrDefault(a => a.item_code == "FAIL_RETRY")?.item_value);
                string filePath = model.RpConfigModel.FirstOrDefault(a => a.item_code == "PATH_WEB")?.item_value;
                model.FilePath = filePath + model.FilePath;
                sftpOutEnt.LocalPath = Path.Combine(_env.ContentRootPath, model.FilePath.Replace("~\\", ""));
                model.FilePath = sftpOutEnt.LocalPath;

                _log.WriteLog("SFTP OutEnt");
                _log.WriteLog("Enable = " + sftpOutEnt.Enable);
                _log.WriteLog("RemoteServerName = " + sftpOutEnt.RemoteServerName);
                _log.WriteLog("RemotePort = " + sftpOutEnt.RemotePort);
                _log.WriteLog("RemoteServerPath = " + sftpOutEnt.RemoteServerPath);
                _log.WriteLog("RemoteSshHostKeyFingerprint = " + sftpOutEnt.RemoteSshHostKeyFingerprint);
                _log.WriteLog("RemoteSshPrivateKeyPath = " + sftpOutEnt.RemoteSshPrivateKeyPath);
                _log.WriteLog("NoOfFailRetry File = " + sftpOutEnt.NoOfFailRetry);
                _log.WriteLog("PATH_WEB = " + sftpOutEnt.LocalPath);

                sftpBackOutEnt.RemoteServerName = model.RpConfigModel.FirstOrDefault(a => a.item_code == "IP")?.item_value;
                sftpBackOutEnt.RemotePort = model.RpConfigModel.FirstOrDefault(a => a.item_code == "PORT")?.item_value;
                sftpBackOutEnt.RemoteUserName = model.RpConfigModel.FirstOrDefault(a => a.item_code == "USER")?.item_value;
                sftpBackOutEnt.RemotePassword = model.RpConfigModel.FirstOrDefault(a => a.item_code == "PASSWORD")?.item_value;
                sftpBackOutEnt.RemoteServerPath = model.RpConfigModel.FirstOrDefault(a => a.item_code == "PATH_SFTP_REPO_BACKOUT")?.item_value;
                sftpBackOutEnt.RemoteSshHostKeyFingerprint = model.RpConfigModel.FirstOrDefault(a => a.item_code == "SSHHOSTKEYFINGERPRINT")?.item_value;
                sftpBackOutEnt.RemoteSshPrivateKeyPath = model.RpConfigModel.FirstOrDefault(a => a.item_code == "SSHPRIVATEKEYPATH")?.item_value;
                sftpBackOutEnt.NoOfFailRetry = System.Convert.ToInt32(model.RpConfigModel.FirstOrDefault(a => a.item_code == "FAIL_RETRY")?.item_value);
                sftpBackOutEnt.LocalPath = model.FilePath;

                _log.WriteLog("SFTP BackOutEnt");
                _log.WriteLog("RemoteServerName = " + sftpBackOutEnt.RemoteServerName);
                _log.WriteLog("RemotePort = " + sftpBackOutEnt.RemotePort);
                _log.WriteLog("RemoteServerPath = " + sftpBackOutEnt.RemoteServerPath);
                _log.WriteLog("RemoteSshHostKeyFingerprint = " + sftpBackOutEnt.RemoteSshHostKeyFingerprint);
                _log.WriteLog("RemoteSshPrivateKeyPath = " + sftpBackOutEnt.RemoteSshPrivateKeyPath);
                _log.WriteLog("NoOfFailRetry File = " + sftpBackOutEnt.NoOfFailRetry);
                _log.WriteLog("PATH_WEB = " + sftpBackOutEnt.LocalPath);
            }
            catch (Exception Ex)
            {
                returnMsg = Ex.Message;
                return false;
            }

            return true;
        }
        private bool WriteFileManual(ref string returnMsg, InterfaceSwiftSftpModel model)
        {
            try
            {
                WriteFile ObjWriteFile = new WriteFile();

                FileEntity fileEnt = new FileEntity();
                fileEnt.FileName = model.FileName;
                fileEnt.FilePath = model.FilePath;
                fileEnt.Values = model.Text.Replace("\n", "\r\n") + "\r\n";
                fileEnt.UseEncoding = new UTF8Encoding(false);

                if (ObjWriteFile.StreamWriter(ref fileEnt) == false)
                {
                    throw new Exception(fileEnt.Msg);
                }
                _log.WriteLog("- Write File " + fileEnt.FileName + " Success.");
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