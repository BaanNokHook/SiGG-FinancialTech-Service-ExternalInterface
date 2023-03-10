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

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class InterfaceDMSController : ControllerBase
    {
        private readonly IUnitOfWork _uow;
        private readonly IHostingEnvironment _env;
        private static LogFile _log;

        public InterfaceDMSController(IUnitOfWork uow, IHostingEnvironment env)
        {
            _uow = uow;
            _env = env;
            _log = new LogFile(_uow.GetConfiguration, "InterfaceDMS");
        }

        [HttpPost]
        [Route("ExportInterfaceDms")]
        public ResultWithModel ExportInterfaceDms(InterfaceDmsSftpModel model)
        {
            string strMsg = string.Empty;
            ResultWithModel rwm = new ResultWithModel();
            SftpEntity sftpEnt = new SftpEntity();

            try
            {
                // Step 1 : Set Config
                if (SetConfig(ref strMsg, ref sftpEnt, ref model) == false)
                {
                    throw new Exception("SetConfig() : " + strMsg);
                }

                // Step 2 : Select And Insert Data To Table
                rwm = _uow.External.InterfaceDMSsftp.Add(model);
                if (!rwm.Success)
                {
                    throw new Exception("GenInterfaceDms() : " + "[" + rwm.RefCode + "] " + rwm.Message);
                }

                // Step 3 : Select Data To Export File
                rwm = _uow.External.InterfaceDMSsftp.Get(model);
                if (!rwm.Success)
                {
                    throw new Exception("SelectInterfaceDms() : " + "[" + rwm.RefCode + "] " + rwm.Message);
                }

                DataSet ds = (DataSet)rwm.Data;
                DataTable dtInterfaceDms = ds.Tables[0];

                // Step 4 : Write File
                if (WriteInterfaceDms(ref strMsg, dtInterfaceDms, model) == false)
                {
                    throw new Exception("WriteInterfaceDms() : " + strMsg);
                }

                // Step 5 : Send File To SFTP
                if (sftpEnt.Enable == "Y")
                {
                    ArrayList listFile = new ArrayList();
                    ArrayList listFileSuccess = new ArrayList();
                    ArrayList listFileError = new ArrayList();

                    listFile.Add(model.file_name);

                    SftpUtility objectSftp = new SftpUtility();
                    if (!objectSftp.UploadSFTPList(ref strMsg, sftpEnt, listFile, ref listFileError, ref listFileSuccess))
                    {
                        throw new Exception("UploadSFTPList() : " + strMsg);
                    }

                    foreach (var row in listFileError)
                    {
                        rwm.Message = row.ToString();
                        _log.WriteLog(row.ToString());
                    }

                    if (listFileError.Count > 0)
                    {
                        throw new Exception(rwm.Message);
                    }

                    foreach (var row in listFileError)
                    {
                        _log.WriteLog(row.ToString());
                    }
                }
                else
                {
                    _log.WriteLog("UploadSFTP Disable.");
                }

                rwm.Message = "Success";
                rwm.RefCode = 0;
                rwm.Serverity = "Low";
                rwm.Success = true;
            }
            catch (Exception ex)
            {
                rwm.RefCode = -999;
                rwm.Message = ex.Message;
                rwm.Serverity = "high";
                rwm.Success = false;
                _log.WriteLog("Error ExportInterfaceDms() : " + ex.Message);
            }

            DataSet dsResult = new DataSet();
            DataTable dt = new List<InterfaceDmsSftpModel> { model }.ToDataTable<InterfaceDmsSftpModel>();
            dt.TableName = "InterfaceDmsSftpResultModel";
            dsResult.Tables.Add(dt);
            rwm.Data = dsResult;
            return rwm;
        }

        private bool SetConfig(ref string returnMsg, ref SftpEntity sftpEnt, ref InterfaceDmsSftpModel model)
        {
            try
            {
                var filePath = Path.Combine(_env.ContentRootPath, model.file_path.Replace("~\\", ""));
                sftpEnt.Enable = model.RpConfigModel.FirstOrDefault(a => a.item_code == "ENABLE_SFTP")?.item_value;
                sftpEnt.RemoteServerName = model.RpConfigModel.FirstOrDefault(a => a.item_code == "IP")?.item_value;
                sftpEnt.RemotePort = model.RpConfigModel.FirstOrDefault(a => a.item_code == "PORT")?.item_value;
                sftpEnt.RemoteUserName = model.RpConfigModel.FirstOrDefault(a => a.item_code == "USER")?.item_value;
                sftpEnt.RemotePassword = model.RpConfigModel.FirstOrDefault(a => a.item_code == "PASSWORD")?.item_value;
                sftpEnt.RemoteSshHostKeyFingerprint = model.RpConfigModel.FirstOrDefault(a => a.item_code == "SSHHOSTKEYFINGERPRINT")?.item_value;
                sftpEnt.RemoteSshPrivateKeyPath = model.RpConfigModel.FirstOrDefault(a => a.item_code == "SSHPRIVATEKEYPATH")?.item_value;
                sftpEnt.NoOfFailRetry = Convert.ToInt32(model.RpConfigModel.FirstOrDefault(a => a.item_code == "FAIL_RETRY")?.item_value);
                sftpEnt.RemoteServerPath = model.RpConfigModel.FirstOrDefault(a => a.item_code == "PATH_SFTP")?.item_value;
                sftpEnt.LocalPath = filePath;
                model.file_path = filePath;
            }
            catch (Exception ex)
            {
                returnMsg = ex.Message;
                return false;
            }
            return true;
        }

        private bool WriteInterfaceDms(ref string returnMsg, DataTable dtInterfaceDms, InterfaceDmsSftpModel model)
        {
            try
            {
                FileEntity fileEnt = new FileEntity();
                WriteFile objWriteFile = new WriteFile();

                // Step 1 : Writer Master File 
                StringBuilder sb = new StringBuilder();
                foreach (DataRow dr in dtInterfaceDms.Rows)
                {
                    foreach (DataColumn col in dtInterfaceDms.Columns)
                    {
                        sb.Append(dr[col]);
                    }
                    sb.AppendLine();
                }
                
                fileEnt.FileName = model.file_name;
                fileEnt.FilePath = model.file_path;
                fileEnt.Values = sb.ToString();

                if (objWriteFile.StreamWriter(ref fileEnt) == false)
                {
                    throw new Exception(fileEnt.Msg);
                }
                _log.WriteLog("Write File " + fileEnt.FileName + " Success.");

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