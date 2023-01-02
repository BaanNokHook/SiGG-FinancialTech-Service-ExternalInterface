using GM.CommonLibs;
using GM.CommonLibs.Common;
using GM.CommonLibs.Helper;
using GM.DataAccess.UnitOfWork;
using GM.Model.Common;
using GM.Model.ExternalInterface;
using GM.SFTPUtility;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class InterfaceTrpFITSController : ControllerBase
    {
        private static LogFile _log;
        private readonly IUnitOfWork _uow;
        private readonly IHostingEnvironment _env;

        public InterfaceTrpFITSController(IUnitOfWork uow, IHostingEnvironment env)
        {
            _uow = uow;
            _env = env;
            _log = new LogFile(_uow.GetConfiguration, "InterfaceBondPledgeFits");
        }

        [HttpPost]
        [Route("[action]")]
        public ResultWithModel ExportInterfaceTrpFits(InterfaceTrpFitsSftpModel model)
        {

            string strMsg = string.Empty;
            ResultWithModel rwm = new ResultWithModel();
            SftpEntity sftpEnt = new SftpEntity();
            DataSet ds_Trp = new DataSet();
            ArrayList listFile = new ArrayList();

            try
            {
                // Step 1 : Set Config
                if (!SetConfig(ref strMsg, ref sftpEnt, model))
                {
                    throw new Exception("SetConfig() : " + strMsg);
                }

                // Step 2 : Search Trans & Collateral
                if (!SearchTrpFits(ref strMsg, ref ds_Trp, model))
                {
                    throw new Exception("SearchTrpFits() : " + strMsg);
                }

                if (ds_Trp.Tables.Count > 0)
                {
                    if (ds_Trp.Tables[0].Rows.Count > 0 && ds_Trp.Tables[1].Rows.Count > 0)
                    {
                        FileEntity fileEnt = new FileEntity();
                        fileEnt.FileName = model.file_trans_name;
                        fileEnt.FilePath = model.file_path;

                        // Step 3 : Write File Trans
                        if (!WriteTrpFits(ref strMsg, ds_Trp.Tables[0], fileEnt))
                        {
                            throw new Exception("WriteTrpFits() : " + strMsg);
                        }
                        listFile.Add(model.file_trans_name);

                        fileEnt = new FileEntity();
                        fileEnt.FileName = model.file_collateral_name;
                        fileEnt.FilePath = model.file_path;

                        // Step 4 : Write File Collateral
                        if (WriteTrpFits(ref strMsg, ds_Trp.Tables[1], fileEnt) == false)
                        {
                            throw new Exception("WriteTrpFits() : " + strMsg);
                        }
                        listFile.Add(model.file_collateral_name);

                        // Step 4 : SFTP FIle
                        if (sftpEnt.Enable == "Y")
                        {
                            ArrayList listFileSuccess = new ArrayList();
                            ArrayList listFileError = new ArrayList();

                            SftpUtility ObjectSftp = new SftpUtility();
                            if (!ObjectSftp.UploadSFTPList(ref strMsg, sftpEnt, listFile, ref listFileError, ref listFileSuccess))
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

                            foreach (var row in listFileSuccess)
                            {
                                _log.WriteLog(row.ToString());
                            }
                        }
                        else
                        {
                            _log.WriteLog("UploadSFTP Disable.");
                        }
                    }
                }
                else
                {
                    throw new Exception("Table Not Found.");
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
                _log.WriteLog("Error ExportInterfaceTrpFits() : " + ex.Message);
            }

            DataSet dsResult = new DataSet();
            DataTable dt = new List<InterfaceTrpFitsSftpModel>() { model }.ToDataTable();
            dt.TableName = "InterfaceTrpFitsSftpResultModel";
            dsResult.Tables.Add(dt);
            rwm.Data = dsResult;

            return rwm;
        }

        private bool SetConfig(ref string strMsg, ref SftpEntity sftpEnt, InterfaceTrpFitsSftpModel model)
        {
            try
            {
                var filePath = Path.Combine(_env.ContentRootPath, model.RpConfigModel.FirstOrDefault(a => a.item_code == "PATH_SERVICE")?.item_value).Replace("~\\", "");

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
                model.file_trans_name = model.RpConfigModel.FirstOrDefault(a => a.item_code == "FILE_TRANS")?.item_value;
                model.file_trans_name = model.file_trans_name?.Replace("yyyyMMdd", model.asof_date.ToString("yyyyMMdd"));
                model.file_collateral_name = model.RpConfigModel.FirstOrDefault(a => a.item_code == "FILE_COLLATERAL")?.item_value;
                model.file_collateral_name = model.file_collateral_name?.Replace("yyyyMMdd", model.asof_date.ToString("yyyyMMdd"));
            }
            catch (Exception ex)
            {
                strMsg = ex.Message;
                return false;
            }

            return true;
        }

        private bool SearchTrpFits(ref string strMsg, ref DataSet ds_Trp, InterfaceTrpFitsSftpModel model)
        {
            try
            {
                ResultWithModel rwm = _uow.External.InterfaceTrpFitsSftp.Get(model);

                if (rwm.Success)
                {
                    ds_Trp = (DataSet)rwm.Data;
                }
                else
                {
                    throw new Exception("[" + rwm.RefCode.ToString() + "] " + rwm.Message);
                }
            }
            catch (Exception ex)
            {
                strMsg = ex.Message;
                return false;
            }
            return true;
        }

        private bool WriteTrpFits(ref string strMsg, DataTable dt_Trp, FileEntity fileEnt)
        {
            try
            {
                WriteFile ObjWriteFile = new WriteFile();
                StringBuilder sb = new StringBuilder();

                foreach (DataRow dr in dt_Trp.Rows)
                {
                    foreach (DataColumn col in dt_Trp.Columns)
                    {
                        sb.Append(dr[col]);
                    }
                    sb.AppendLine();
                }

                fileEnt.Values = sb.ToString();

                if (ObjWriteFile.StreamWriter(ref fileEnt) == false)
                {
                    throw new Exception(fileEnt.Msg);
                }
                _log.WriteLog("Write File " + fileEnt.FileName + " Success.");

            }
            catch (Exception Ex)
            {
                strMsg = Ex.Message;
                return false;
            }

            return true;
        }
    }
}