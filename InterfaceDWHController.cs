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
using GM.Model.Static;
using GM.SFTPUtility;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class InterfaceDWHController : ControllerBase
    {
        private readonly IUnitOfWork _uow;
        private readonly IHostingEnvironment _env;
        private static LogFile _log;

        public InterfaceDWHController(IUnitOfWork uow, IHostingEnvironment env)
        {
            _uow = uow;
            _env = env;
            _log = new LogFile(_uow.GetConfiguration, "InterfaceDWH");
        }

        [HttpPost]
        [Route("GetInterfaceDwhSftpList")]
        public ResultWithModel GetInterfaceDwhSftpList(InterfaceDwhSftpModel model)
        {
            ResultWithModel rwm = new ResultWithModel();
            try
            {
                rwm = _uow.External.InterfaceDWHsftp.GetSftp(model);
            }
            catch (Exception ex)
            {
                rwm.RefCode = -999;
                rwm.Message = ex.Message;
                rwm.Serverity = "high";
                rwm.Success = false;
                _log.WriteLog("Error GetInterfaceDwhSftpList() : " + ex.Message);
            }

            return rwm;
        }

        [HttpPost]
        [Route("ExportInterfaceDwh")]
        public ResultWithModel ExportInterfaceDwh(InterfaceDwhSftpModel model)
        {
            string strMsg = string.Empty;
            ResultWithModel rwm = new ResultWithModel();
            ConfigEntity configEnt = new ConfigEntity();
            SftpEntity sftpEnt = new SftpEntity();

            try
            {
                _log.WriteLog("Start Export DWH [" + model.dwh_name + "]");
                // Step 1 : Set Config DWH
                if (SetConfig(ref strMsg, ref configEnt, ref sftpEnt, model.RpConfigModel) == false)
                {
                    throw new Exception("Set_Config() : " + strMsg);
                }

                if (configEnt.EnableGen == "Y")
                {
                    // Step 3 : Select And Insert Data To Table
                    rwm = _uow.External.InterfaceDWHsftp.Add(model);
                    if (!rwm.Success)
                    {
                        throw new Exception("Gen_InterfaceDwh() : [" + rwm.RefCode + "] " + rwm.Message);
                    }
                    _log.WriteLog("Gen DWH " + model.dwh_name + " Success.");
                }
                else
                {
                    _log.WriteLog("Gen DWH Disable.");
                }

                // Step 4 : Select Data To Export File
                rwm = _uow.External.InterfaceDWHsftp.GetList(model);
                if (!rwm.Success)
                {
                    throw new Exception("Select_InterfaceDwh() : [" + rwm.RefCode + "] " + rwm.Message);
                }

                DataSet ds = (DataSet)rwm.Data;
                DataTable dtInterfaceDwh = ds.Tables[0];

                // Step 5 : Write File
                _log.WriteLog("Select DWH " + model.dwh_name + " [" + dtInterfaceDwh.Rows.Count + "] Rows.");
                if (WriterInterfaceDwh(ref strMsg, dtInterfaceDwh, configEnt, ref model) == false)
                {
                    throw new Exception("Writer_InterfaceDwh() : " + strMsg);
                }

                // Step 6 : Send File To SFTP
                if (configEnt.EnableSftp == "Y")
                {
                    _log.WriteLog("SFTP File");
                    ArrayList listFile = new ArrayList();
                    ArrayList listFileSuccess = new ArrayList();
                    ArrayList listFileError = new ArrayList();

                    _log.WriteLog("- SftpEnt.RemoteServerPath = " + sftpEnt.RemoteServerPath);
                    _log.WriteLog("- SftpEnt.LocalPath = " + sftpEnt.LocalPath);
                    _log.WriteLog("- List MasterFile = " + configEnt.MasterFile);
                    listFile.Add(configEnt.MasterFile);

                    if (model.flag_ctrl == "Y")
                    {
                        listFile.Add(configEnt.CtrlFile);
                        _log.WriteLog("- List CtrlFile = " + configEnt.CtrlFile);
                    }

                    SftpUtility objectSftp = new SftpUtility();
                    if (!objectSftp.UploadSFTPList(ref strMsg, sftpEnt, listFile, ref listFileError, ref listFileSuccess))
                    {
                        throw new Exception("UploadSFTPList() : " + strMsg);
                    }

                    foreach (var row in listFileError)
                    {
                        rwm.Message += row + ",";
                        _log.WriteLog(row.ToString());
                    }

                    if (listFileError.Count > 0)
                    {
                        throw new Exception(rwm.Message);
                    }

                    foreach (var row in listFileSuccess)
                    {
                        _log.WriteLog("SFTP " + row + " Success.");
                    }

                }
                else
                {
                    _log.WriteLog("UploadSFTP Disable.");
                }
                
                rwm.RefCode = 0;
                rwm.Message = "Success";
                rwm.Serverity = "Low";
                rwm.Success = true;
            }
            catch (Exception ex)
            {
                rwm.RefCode = -999;
                rwm.Message = ex.Message;
                rwm.Serverity = "high";
                rwm.Success = false;
                _log.WriteLog("Error ExportInterfaceDwh() : " + ex.Message);
            }

            _log.WriteLog("End Export DWH [" + model.dwh_name + "]");

            DataSet dsResult = new DataSet();
            DataTable dt = new List<InterfaceDwhSftpModel> { model }.ToDataTable<InterfaceDwhSftpModel>();
            dt.TableName = "InterfaceDwhSftpResultModel";
            dsResult.Tables.Add(dt);
            rwm.Data = dsResult;
            return rwm;
        }

        private class ConfigEntity
        {
            public string EnableGen;
            public string EnableSftp;
            public string EnableGenFcy;
            public string EnableSftpFcy;
            public string PathService;
            public string MasterFile;
            public string CtrlFile;
        }
        
        private bool SetConfig(ref string returnMsg, ref ConfigEntity configEnt, ref SftpEntity sftpEnt, List<RpConfigModel> rpConfig)
        {
            try
            {
                var filePath = Path.Combine(_env.ContentRootPath, rpConfig.FirstOrDefault(a => a.item_code == "PATH_SERVICE")?.item_value);
                configEnt.EnableGen = rpConfig.FirstOrDefault(a => a.item_code == "ENABLE_GEN")?.item_value;
                configEnt.EnableSftp = rpConfig.FirstOrDefault(a => a.item_code == "ENABLE_SFTP")?.item_value;
                sftpEnt.RemoteServerName = rpConfig.FirstOrDefault(a => a.item_code == "IP")?.item_value;
                sftpEnt.RemotePort = rpConfig.FirstOrDefault(a => a.item_code == "PORT")?.item_value;
                sftpEnt.RemoteUserName = rpConfig.FirstOrDefault(a => a.item_code == "USER")?.item_value;
                sftpEnt.RemotePassword = rpConfig.FirstOrDefault(a => a.item_code == "PASSWORD")?.item_value;
                sftpEnt.RemoteSshHostKeyFingerprint = rpConfig.FirstOrDefault(a => a.item_code == "SSHHOSTKEYFINGERPRINT")?.item_value;
                sftpEnt.RemoteSshPrivateKeyPath = rpConfig.FirstOrDefault(a => a.item_code == "SSHPRIVATEKEYPATH")?.item_value;
                sftpEnt.NoOfFailRetry = Convert.ToInt32(rpConfig.FirstOrDefault(a => a.item_code == "FAIL_RETRY")?.item_value);
                sftpEnt.RemoteServerPath = rpConfig.FirstOrDefault(a => a.item_code == "PATH_SFTP")?.item_value;
                sftpEnt.LocalPath = filePath.Replace("~\\", "");
                configEnt.PathService = rpConfig.FirstOrDefault(a => a.item_code == "PATH_SERVICE")?.item_value;
            }
            catch (Exception ex)
            {
                returnMsg = ex.Message;
                return false;
            }
            return true;
        }

        private bool WriterInterfaceDwh(ref string returnMsg, DataTable dt, ConfigEntity configEnt, ref InterfaceDwhSftpModel model)
        {
            try
            {
                StringBuilder strLine = new StringBuilder();
                FileEntity fileEnt = new FileEntity();
                WriteFile objWriteFile = new WriteFile();
                if (model.cur_type == "THB")
                {
                    configEnt.MasterFile = model.file_title + model.asof_date.ToString("yyyyMMdd") + ".txt";
                    configEnt.CtrlFile = model.file_title + "_CTRL" + model.asof_date.ToString("yyyyMMdd") + ".txt";
                }
                else
                {
                    configEnt.MasterFile = model.file_title + model.asof_date.ToString("yyyyMMdd") + ".TXT";
                    configEnt.CtrlFile = model.file_title + "_CTRL" + model.asof_date.ToString("yyyyMMdd") + ".TXT";
                }
                
                model.file_title = configEnt.MasterFile + " || " + configEnt.CtrlFile;

                // Step 1 : Writer Master File 
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    for (int j = 0; j <= dt.Columns.Count - 3; j++)
                    {
                        strLine.Append(dt.Rows[i][j]);
                    }
                    strLine.AppendLine();
                }
                strLine.Replace("\r\n", "\n");

                var filePath = Path.Combine(_env.ContentRootPath, configEnt.PathService.Replace("~\\", ""));
                
                fileEnt.FileName = configEnt.MasterFile;
                fileEnt.FilePath = filePath;
                fileEnt.Values = strLine.ToString();
                fileEnt.UseEncoding = Encoding.GetEncoding("TIS-620");

                if (objWriteFile.StreamWriterEncoding(ref fileEnt) == false)
                {
                    throw new Exception(fileEnt.Msg);
                }
                _log.WriteLog("Writer File " + configEnt.MasterFile + " Success.");

                // Step 2 : Writer CTRL File
                if (model.flag_ctrl == "Y")
                {
                    string asOfDateNumOfrec;
                    if (dt.Rows.Count > 0)
                    {
                        asOfDateNumOfrec = dt.Rows[0]["AS_OF_DATE"] + dt.Rows[0]["NO_REC"].ToString();
                    }
                    else
                    {
                        asOfDateNumOfrec = model.asof_date.ToString("yyyyMMdd") + "000000";
                    }

                    fileEnt = new FileEntity();
                    fileEnt.FileName = configEnt.CtrlFile;
                    fileEnt.FilePath = filePath;
                    fileEnt.Values = asOfDateNumOfrec;
                    fileEnt.UseEncoding = Encoding.GetEncoding("TIS-620");

                    if (objWriteFile.StreamWriter(ref fileEnt) == false)
                    {
                        throw new Exception(fileEnt.Msg);
                    }
                    _log.WriteLog("Writer File " + configEnt.CtrlFile + " Success.");
                }
                else
                {
                    _log.WriteLog("CTRL File not write file.");
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