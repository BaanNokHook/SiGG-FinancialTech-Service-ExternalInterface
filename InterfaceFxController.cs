using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using GM.CommonLibs;
using GM.CommonLibs.Common;
using GM.CommonLibs.Helper;
using GM.DataAccess.UnitOfWork;
using GM.Model.Common;
using GM.Model.ExternalInterface;
using GM.SFTPUtility;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class InterfaceFxController : ControllerBase
    {
        private static LogFile _log;
        private readonly IUnitOfWork _uow;
        private readonly IHostingEnvironment _env;
        public InterfaceFxController(IUnitOfWork uow, IHostingEnvironment env)
        {
            _uow = uow;
            _env = env;
            _log = new LogFile(_uow.GetConfiguration, "InterfaceFx");
        }

        [HttpPost]
        [Route("ExportInterfaceFxReconcile")]
        public ResultWithModel ExportInterfaceFxReconcile(InterfaceFxReconcileSftpModel model)
        {
            string strMsg = string.Empty;
            ServiceInOutReqModel inOutModel = new ServiceInOutReqModel();
            ResultWithModel rwm = new ResultWithModel();
            SftpEntity sftpEnt = new SftpEntity();

            try
            {
                _log.WriteLog("Start ExportInterfaceFxReconcile ==========");

                // Step 1 : Set Config
                if (Set_ConfigFxReconcile(ref strMsg, ref sftpEnt, ref model) == false)
                {
                    throw new Exception("Set_ConfigFxReconcile() : " + strMsg);
                }

                DataTable dt_Transaction = new DataTable();
                DataTable dt_Position = new DataTable();
                DataTable dt_PostingEvent = new DataTable();

                _log.WriteLog("Write File");

                if (model.FileTransaction != string.Empty)
                {
                    // Step 2 : Search Transaction
                    if (Search_Transaction(ref strMsg, ref dt_Transaction, model) == false)
                    {
                        throw new Exception("Search_Transaction() : " + strMsg);
                    }

                    // Step 3 : Write File Fx Transaction
                    if (Write_Transaction(ref strMsg, dt_Transaction, model) == false)
                    {
                        throw new Exception("Write_Transaction() : " + strMsg);
                    }
                }

                if (model.FilePosition != string.Empty)
                {
                    // Step 4 : Search Position
                    if (Search_Position(ref strMsg, ref dt_Position, model) == false)
                    {
                        throw new Exception("Search_Position() : " + strMsg);
                    }

                    // Step 5 : Write File Fx Position
                    if (Write_Position(ref strMsg, dt_Position, model) == false)
                    {
                        throw new Exception("Write_Position() : " + strMsg);
                    }
                }

                if (model.FilePostingEvent != string.Empty)
                {
                    // Step 6 : Search PostingEvent
                    if (Search_PostingEvent(ref strMsg, ref dt_PostingEvent, model) == false)
                    {
                        throw new Exception("Search_PostingEvent() : " + strMsg);
                    }

                    // Step 7 : Write File Fx PostingEvent
                    if (Write_PostingEvent(ref strMsg, dt_PostingEvent, model) == false)
                    {
                        throw new Exception("Write_PostingEvent() : " + strMsg);
                    }
                }

                // Step 8 : Send File To SFTP
                _log.WriteLog("SFTP File");
                ArrayList listFile = new ArrayList();
                ArrayList listFileSuccess = new ArrayList();
                ArrayList listFileError = new ArrayList();
                model.FileFail = new List<string>();
                model.FileSuccess = new List<string>();

                if (sftpEnt.Enable == "Y")
                {
                    listFile.Add(model.FileTransaction);
                    listFile.Add(model.FilePosition);
                    listFile.Add(model.FilePostingEvent);

                    SftpUtility objectSftp = new SftpUtility();
                    if (!objectSftp.UploadSFTPList(ref strMsg, sftpEnt, listFile, ref listFileError, ref listFileSuccess))
                    {
                        throw new Exception("UploadSFTPList() : " + strMsg);
                    }

                    foreach (var file in listFileError)
                    {
                        model.FileFail.Add(file.ToString());
                        _log.WriteLog("- SFTP " + file);
                    }

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
                _log.WriteLog("Error ExportInterfaceFxReconcile() : " + ex.Message);
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
                inOutModel.module_name = "InterfaceFxController";
                inOutModel.action_name = "ExportInterfaceFxReconcile";
                inOutModel.ref_id = strGuid;
                inOutModel.status = rwm.RefCode.ToString();
                inOutModel.status_desc = rwm.Message;
                inOutModel.create_by = "WebService";

                _uow.External.ServiceInOutReq.Add(inOutModel);

                _log.WriteLog("End ExportInterfaceFxReconcile ==========");
            }

            DataSet dsResult = new DataSet();
            DataTable dt = new List<InterfaceFxReconcileSftpModel>() { model }.ToDataTable<InterfaceFxReconcileSftpModel>();
            dt.TableName = "InterfaceFxReconcileSftpResultModel";
            dsResult.Tables.Add(dt);
            rwm.Data = dsResult;

            return rwm;
        }

        private bool Set_ConfigFxReconcile(ref string returnMsg, ref SftpEntity sftpEnt, ref InterfaceFxReconcileSftpModel model)
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
                sftpEnt.NoOfFailRetry = Convert.ToInt32(model.RpConfigModel.FirstOrDefault(a => a.item_code == "FAIL_RETRY")?.item_value);
                sftpEnt.LocalPath = Path.Combine(_env.ContentRootPath, model.FilePath.Replace("~\\", ""));
                model.FilePath = sftpEnt.LocalPath;

                model.FileTransaction = model.RpConfigModel.FirstOrDefault(a => a.item_code == "FILE_NAME_TRANSACTION")?.item_value;
                model.FilePosition = model.RpConfigModel.FirstOrDefault(a => a.item_code == "FILE_NAME_POSITION")?.item_value;
                model.FilePostingEvent = model.RpConfigModel.FirstOrDefault(a => a.item_code == "FILE_NAME_POSTINGEVENT")?.item_value;
                model.FileTransaction = model.FileTransaction?.Replace("yyyyMMdd", model.AsofDate.ToString("yyyyMMdd"));
                model.FilePosition = model.FilePosition?.Replace("yyyyMMdd", model.AsofDate.ToString("yyyyMMdd"));
                model.FilePostingEvent = model.FilePostingEvent?.Replace("yyyyMMdd", model.AsofDate.ToString("yyyyMMdd"));
                
                _log.WriteLog("- AsOfDate = " + model.AsofDate.ToString("yyyyMMdd"));
                _log.WriteLog("- Sftp Enable = " + sftpEnt.Enable);
                _log.WriteLog("- Sftp RemoteServerName = " + sftpEnt.RemoteServerName);
                _log.WriteLog("- Sftp RemotePort = " + sftpEnt.RemotePort);
                _log.WriteLog("- Sftp RemoteSshHostKeyFingerprint = " + sftpEnt.RemoteSshHostKeyFingerprint);
                _log.WriteLog("- Sftp RemoteSshPrivateKeyPath = " + sftpEnt.RemoteSshPrivateKeyPath);
                _log.WriteLog("- Sftp RemoteServerPath = " + sftpEnt.RemoteServerPath);
                _log.WriteLog("- Sftp NoOfFailRetry = " + sftpEnt.NoOfFailRetry);
                _log.WriteLog("- Sftp LocalPath = " + sftpEnt.LocalPath);
                _log.WriteLog("- File Transaction = " + model.FileTransaction);
                _log.WriteLog("- File FilePosition = " + model.FilePosition);
                _log.WriteLog("- File FilePostingEvent = " + model.FilePostingEvent);
            }
            catch (Exception ex)
            {
                returnMsg = ex.Message;
                return false;
            }
            return true;
        }

        private bool Search_Transaction(ref string returnMsg, ref DataTable dt_Transaction, InterfaceFxReconcileSftpModel model)
        {
            try
            {
                ResultWithModel rwm = _uow.External.InterfaceFx.GetTransaction(model);

                if (!rwm.Success)
                {
                    throw new Exception("[" + rwm.RefCode + "] " + rwm.Message);
                }

                DataSet ds = (DataSet)rwm.Data;
                dt_Transaction = ds.Tables[0];
            }
            catch (Exception ex)
            {
                returnMsg = ex.Message;
                return false;
            }

            return true;
        }

        private bool Search_Position(ref string returnMsg, ref DataTable dt_Position, InterfaceFxReconcileSftpModel model)
        {
            try
            {
                ResultWithModel rwm = _uow.External.InterfaceFx.GetPosition(model);

                if (!rwm.Success)
                {
                    throw new Exception("[" + rwm.RefCode + "] " + rwm.Message);
                }

                DataSet ds = (DataSet)rwm.Data;
                dt_Position = ds.Tables[0];
            }
            catch (Exception ex)
            {
                returnMsg = ex.Message;
                return false;
            }

            return true;
        }

        private bool Search_PostingEvent(ref string returnMsg, ref DataTable dt_PostingEvent, InterfaceFxReconcileSftpModel model)
        {
            try
            {
                ResultWithModel rwm = _uow.External.InterfaceFx.GetPostingEvent(model);

                if (!rwm.Success)
                {
                    throw new Exception("[" + rwm.RefCode + "] " + rwm.Message);
                }

                DataSet ds = (DataSet)rwm.Data;
                dt_PostingEvent = ds.Tables[0];
            }
            catch (Exception ex)
            {
                returnMsg = ex.Message;
                return false;
            }

            return true;
        }

        private bool Write_Transaction(ref string returnMsg, DataTable dt_Transaction, InterfaceFxReconcileSftpModel model)
        {
            try
            {
                WriteFile ObjWriteFile = new WriteFile();
                StringBuilder sb = new StringBuilder();
                if (dt_Transaction.Rows.Count > 0)
                {
                    foreach (DataRow dr in dt_Transaction.Rows)
                    {
                        foreach (DataColumn col in dt_Transaction.Columns)
                        {
                            sb.Append(dr[col]);
                        }
                        sb.AppendLine();
                    }

                    FileEntity fileEnt = new FileEntity();
                    fileEnt.FileName = model.FileTransaction;
                    fileEnt.FilePath = model.FilePath;
                    fileEnt.Values = sb.ToString();
                    fileEnt.UseEncoding = new UTF8Encoding(false);

                    if (ObjWriteFile.StreamWriterEncoding(ref fileEnt) == false)
                    {
                        throw new Exception(fileEnt.Msg);
                    }
                    _log.WriteLog("- Write File " + fileEnt.FileName + " Success.");
                }
            }
            catch (Exception Ex)
            {
                returnMsg = Ex.Message;
                return false;
            }

            return true;
        }

        private bool Write_Position(ref string returnMsg, DataTable dt_Position, InterfaceFxReconcileSftpModel model)
        {
            try
            {
                WriteFile ObjWriteFile = new WriteFile();

                StringBuilder sb = new StringBuilder();
                if (dt_Position.Rows.Count > 0)
                {
                    foreach (DataRow dr in dt_Position.Rows)
                    {
                        foreach (DataColumn col in dt_Position.Columns)
                        {
                            sb.Append(dr[col]);
                        }
                        sb.AppendLine();
                    }

                    FileEntity fileEnt = new FileEntity();
                    fileEnt.FileName = model.FilePosition;
                    fileEnt.FilePath = model.FilePath;
                    fileEnt.Values = sb.ToString();
                    fileEnt.UseEncoding = new UTF8Encoding(false);

                    if (ObjWriteFile.StreamWriterEncoding(ref fileEnt) == false)
                    {
                        throw new Exception(fileEnt.Msg);
                    }
                    _log.WriteLog("Write File " + fileEnt.FileName + " Success.");
                }
            }
            catch (Exception Ex)
            {
                returnMsg = Ex.Message;
                return false;
            }

            return true;
        }

        private bool Write_PostingEvent(ref string returnMsg, DataTable dt_PostingEvent, InterfaceFxReconcileSftpModel model)
        {
            try
            {
                WriteFile ObjWriteFile = new WriteFile();

                StringBuilder sb = new StringBuilder();
                if (dt_PostingEvent.Rows.Count > 0)
                {
                    foreach (DataRow dr in dt_PostingEvent.Rows)
                    {
                        foreach (DataColumn col in dt_PostingEvent.Columns)
                        {
                            sb.Append(dr[col]);
                        }
                        sb.AppendLine();
                    }

                    FileEntity fileEnt = new FileEntity();
                    fileEnt.FileName = model.FilePostingEvent;
                    fileEnt.FilePath = model.FilePath;
                    fileEnt.Values = sb.ToString();
                    fileEnt.UseEncoding = new UTF8Encoding(false);

                    if (!ObjWriteFile.StreamWriterEncoding(ref fileEnt))
                    {
                        throw new Exception(fileEnt.Msg);
                    }
                    _log.WriteLog("Write File " + fileEnt.FileName + " Success.");
                }
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