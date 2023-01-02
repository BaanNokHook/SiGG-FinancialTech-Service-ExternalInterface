using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
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
    public class InterfaceGlController : ControllerBase
    {
        private readonly IUnitOfWork _uow;
        private readonly IHostingEnvironment _env;
        private static LogFile _log;

        public InterfaceGlController(IUnitOfWork uow, IHostingEnvironment env)
        {
            _uow = uow;
            _env = env;
            _log = new LogFile(_uow.GetConfiguration, "InterfaceGl");
        }

        [HttpPost]
        [Route("ExportInterfaceGl")]
        public ResultWithModel ExportInterfaceGl(InterfaceGlSftpModel GlSftpModel)
        {
            string StrMsg = string.Empty;
            ServiceInOutReqModel InOutModel = new ServiceInOutReqModel();
            ResultWithModel rwm = new ResultWithModel();
            SftpEntity SftpEnt = new SftpEntity();
            try
            {
                _log.WriteLog("Start ExportInterfaceGl ==========");

                // Step 1 : Set Config
                if (Set_ConfigGl(ref StrMsg, ref SftpEnt, ref GlSftpModel) == false)
                {
                    throw new Exception("Set_ConfigGl() : " + StrMsg);
                }

                // Step 2 : Search Gl
                _log.WriteLog("Search GL");
                DataSet Ds_Gl = new DataSet();
                if (Search_Gl(ref StrMsg, ref Ds_Gl, GlSftpModel) == false)
                {
                    throw new Exception("Search_Gl() : " + StrMsg);
                }

                if (Ds_Gl.Tables.Count != 3)
                {
                    throw new Exception("Table GL Not Equal To [3] Table.");
                }

                int CountHead = Ds_Gl.Tables[0].Rows.Count;
                int CountDetail = Ds_Gl.Tables[1].Rows.Count;
                int CountTrail = Ds_Gl.Tables[2].Rows.Count;

                _log.WriteLog("- Head [" + CountHead.ToString() + "] Rows");
                _log.WriteLog("- Detail [" + CountDetail.ToString() + "] Rows");
                _log.WriteLog("- Trail [" + CountTrail.ToString() + "] Rows");
                _log.WriteLog("Search GL = Success");

                // Step 3 : Write File GL
                _log.WriteLog("Write File");
                if (Write_Gl(ref StrMsg, Ds_Gl, GlSftpModel) == false)
                {
                    throw new Exception("Write_Gl() : " + StrMsg);
                }

                // Step 4 : Send File To SFTP
                _log.WriteLog("SFTP File");
                if (SftpEnt.Enable == "Y")
                {
                    ArrayList ListFile = new ArrayList();
                    ArrayList ListFileSuccess = new ArrayList();
                    ArrayList ListFileError = new ArrayList();

                    ListFile.Add(GlSftpModel.FileGLREPO);
                    ListFile.Add(GlSftpModel.FileSWREPO);

                    SftpUtility ObjectSftp = new SftpUtility();
                    if (!ObjectSftp.UploadSFTPList(ref StrMsg, SftpEnt, ListFile, ref ListFileError, ref ListFileSuccess))
                    {
                        throw new Exception("UploadSFTPList() : " + StrMsg);
                    }

                    GlSftpModel.FileFail = new List<string>();
                    foreach (var file in ListFileError)
                    {
                        GlSftpModel.FileFail.Add(file.ToString());
                        _log.WriteLog("- SFTP " + file);
                    }

                    GlSftpModel.FileSuccess = new List<string>();
                    foreach (var file in ListFileSuccess)
                    {
                        GlSftpModel.FileSuccess.Add(file.ToString());
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
                _log.WriteLog("Error ExportInterfaceGl() : " + ex.Message);
            }
            finally
            {
                string Json_Req = JsonConvert.SerializeObject(GlSftpModel);
                string Json_Resp = JsonConvert.SerializeObject(rwm);

                string StrGuid = Guid.NewGuid().ToString().ToUpper();
                InOutModel.guid = StrGuid;
                InOutModel.svc_req = Json_Req;
                InOutModel.svc_res = Json_Resp;
                InOutModel.svc_type = "OUT";
                InOutModel.module_name = "InterfaceGl";
                InOutModel.action_name = "ExportInterfaceGl";
                InOutModel.ref_id = StrGuid;
                InOutModel.status = rwm.RefCode.ToString();
                InOutModel.status_desc = rwm.Message;
                InOutModel.create_by = "WebService";

                rwm = _uow.External.ServiceInOutReq.Add(InOutModel);
                if (!rwm.Success)
                {
                    rwm.Message = InOutModel.status_desc + " | Insert_LogInOut() : [" + rwm.RefCode + "] " + rwm.Message;
                    rwm.RefCode = -999;
                    rwm.Serverity = "high";
                    rwm.Success = false;
                    _log.WriteLog("Error Insert_LogInOut() : " + StrMsg);
                }

                _log.WriteLog("End ExportInterfaceGl ==========");
            }

            DataSet dsResult = new DataSet();
            DataTable dt = new List<InterfaceGlSftpModel>() { GlSftpModel }.ToDataTable<InterfaceGlSftpModel>();
            dt.TableName = "InterfaceGlSftpResultModel";
            dsResult.Tables.Add(dt);
            rwm.Data = dsResult;
            return rwm;
        }

        private bool Search_Gl(ref string ReturnMsg, ref DataSet Ds_Gl, InterfaceGlSftpModel GlSftpModel)
        {
            try
            {
                BaseParameterModel parameter = new BaseParameterModel();

                parameter.ProcedureName = "RP_Interface_GL_Batch_Proc";
                parameter.Parameters.Add(new Field { Name = "gl_creation_date", Value = GlSftpModel.AsofDate });
                parameter.Parameters.Add(new Field { Name = "recorded_by", Value = GlSftpModel.create_by });
                parameter.ResultModelNames.Add("InterfeGlResultModel");
                parameter.Paging = new PagingModel(){ PageNumber = 1, RecordPerPage = 999999};

                //Add Orderby
                parameter.Orders = new List<OrderByModel>();

                ResultWithModel rwm = _uow.ExecDataProc(parameter);

                if (!rwm.Success)
                {
                    throw new Exception("[" + rwm.RefCode + "] " + rwm.Message);
                }

                Ds_Gl = (DataSet)rwm.Data;
            }
            catch (Exception Ex)
            {
                ReturnMsg = Ex.Message;
                return false;
            }

            return true;
        }

        private bool Write_Gl(ref string ReturnMsg, DataSet Ds_Gl, InterfaceGlSftpModel GlSftpModel)
        {
            try
            {
                string FILE_NAME = GlSftpModel.FilePath + "\\" + GlSftpModel.FileGLREPO;

                if (!Directory.Exists(GlSftpModel.FilePath))
                {
                    Directory.CreateDirectory(GlSftpModel.FilePath);
                }

                StreamWriter oWrite = System.IO.File.CreateText(FILE_NAME);

                DataTable dtHead = Ds_Gl.Tables[0];
                DataTable dtDetail = Ds_Gl.Tables[1];
                DataTable dtTrail = Ds_Gl.Tables[2];

                for (int iHead = 0; iHead < dtHead.Rows.Count; iHead++)
                {
                    string[] strDate1 = dtHead.Rows[iHead]["eff_date"].ToString().Split('/');
                    string strDate = strDate1[1] + "/" + strDate1[0] + "/" + strDate1[2].Substring(2, 2);
                    string batchid = dtHead.Rows[iHead]["batch_id"].ToString().Trim().PadLeft(2, '0');

                    var strHead = dtHead.Rows[iHead]["detail_iden"]
                                     + retStringLengthRequest(dtHead.Rows[iHead]["company"].ToString(), 12)
                                     + strDate
                                     + retStringLengthRequest("RPO" + batchid, 15)
                                     + retStringLengthRequest(dtHead.Rows[iHead]["rec_size"].ToString(), 3)
                                     + dtHead.Rows[iHead]["currency"]
                                     + retStringLengthRequest(dtHead.Rows[iHead]["head_available"].ToString(), 90);

                    oWrite.WriteLine(strHead);

                    DataView dvDetail = dtDetail.DefaultView;
                    dvDetail.RowFilter = "batch_id='" + dtHead.Rows[iHead]["batch_id"].ToString().Trim() + "'";
                    DataTable dtDetailFilter = dvDetail.ToTable();
                    for (int iDetail = 0; iDetail < dtDetailFilter.Rows.Count; iDetail++)
                    {
                            string strDetail = dtDetailFilter.Rows[iDetail]["DETAIL_IDEN"]
                                               + retStringLengthRequestRightZero(dtDetailFilter.Rows[iDetail]["GL_NUMBER"].ToString().Replace("-", "").Trim(), 12)
                                               + retStringLengthRequestRightZero(dtDetailFilter.Rows[iDetail]["COST_CENTER"].ToString().Replace(".", "").Trim(), 6)
                                               + retStringLengthRequest(dtDetailFilter.Rows[iDetail]["TRANS_CODE"].ToString(), 2)
                                               + retStringLengthRequestRightZero(dtDetailFilter.Rows[iDetail]["TRANS_AMOUNT"].ToString().Replace(".", "").Trim(), 17)
                                               + retStringLengthRequest(dtDetailFilter.Rows[iDetail]["TRANS_DESC"].ToString(), 40)
                                               + retStringLengthRequest(dtDetailFilter.Rows[iDetail]["ICA_METHOD"].ToString(), 10)
                                               + retStringLengthRequest(dtDetailFilter.Rows[iDetail]["MISC_SUB_IDEN"].ToString(), 12)
                                               + retStringLengthRequest(dtDetailFilter.Rows[iDetail]["REF_NUMBER"].ToString(), 15)
                                               + retStringLengthRequest(dtDetailFilter.Rows[iDetail]["MEMO_ACCT"].ToString(), 1)
                                               + retStringLengthRequest(dtDetailFilter.Rows[iDetail]["DETAIL_AVAILABLE"].ToString(), 16);

                            oWrite.WriteLine(strDetail);
                    }

                    DataView dvTrail = dtTrail.DefaultView;
                    dvTrail.RowFilter = "batch_id='" + dtHead.Rows[iHead]["batch_id"].ToString().Trim() + "'";
                    DataTable dtTrailFilter = dvTrail.ToTable();
                    for (int iTrail = 0; iTrail < dtTrailFilter.Rows.Count; iTrail++)
                    {
                            strDate1 = dtTrailFilter.Rows[iTrail]["EFF_DATE"].ToString().Split('/');
                            strDate = strDate1[1] + "/" + strDate1[0] + "/" + strDate1[2].Substring(2, 2);

                            string strTrail = dtTrailFilter.Rows[iTrail]["TRAIL_IDEN"]
                                              + retStringLengthRequest(dtTrailFilter.Rows[iTrail]["COMPANY"].ToString(), 12)
                                              + strDate
                                              + retStringLengthRequest("RPO" + batchid, 15)
                                              + retStringLengthRequestRightZero(dtTrailFilter.Rows[iTrail]["TOTAL_DR_AMT"].ToString().Replace(".", "").Trim(), 17)
                                              + retStringLengthRequestRightZero(dtTrailFilter.Rows[iTrail]["TOTAL_CR_AMT"].ToString().Replace(".", "").Trim(), 17)
                                              + retStringLengthRequestRightZero(dtTrailFilter.Rows[iTrail]["TOTAL_DETAIL_REC"].ToString().Replace(".", "").Trim(), 6)
                                              + retStringLengthRequestRightZero(dtTrailFilter.Rows[iTrail]["TOTAL_MEMO_DR"].ToString().Replace(".", "").Trim(), 17)
                                              + retStringLengthRequestRightZero(dtTrailFilter.Rows[iTrail]["TOTAL_MEMO_CR"].ToString().Replace(".", "").Trim(), 17)
                                              + retStringLengthRequest(dtTrailFilter.Rows[iTrail]["TRAIL_AVAILABLE"].ToString().Trim(), 22);

                            oWrite.WriteLine(strTrail);
                    }
                }

                oWrite.Close();
                oWrite.Dispose();

                string StringReadFile;
                StreamReader objStreamReader;

                if (System.IO.File.Exists(FILE_NAME))
                {
                    objStreamReader = new StreamReader(FILE_NAME);
                    StringReadFile = objStreamReader.ReadToEnd().Replace("\r\n", "\n");
                    objStreamReader.Close();
                    objStreamReader.Dispose();

                    System.IO.File.Delete(FILE_NAME);
                    oWrite = System.IO.File.CreateText(FILE_NAME);
                    oWrite.Write(StringReadFile);
                    oWrite.Close();
                    oWrite.Dispose();
                }

                _log.WriteLog("- Write File " + GlSftpModel.FileGLREPO + " Success.");

                //=================================================================================

                FILE_NAME = GlSftpModel.FilePath + "\\" + GlSftpModel.FileSWREPO;
                oWrite = System.IO.File.CreateText(FILE_NAME);
                if (dtHead.Rows.Count > 0)
                {
                    string[] strDate2 = dtHead.Rows[0]["eff_date"].ToString().Split('/');
                    string writeDate = "20" + strDate2[2] + strDate2[1] + strDate2[0];
                    oWrite.WriteLine(strDate2[2] + strDate2[1] + strDate2[0]);
                }
                else
                {
                    oWrite.WriteLine(GlSftpModel.AsofDate.ToString("yyyyMMdd"));
                }

                oWrite.Close();
                oWrite.Dispose();

                if (System.IO.File.Exists(FILE_NAME))
                {
                    objStreamReader = new StreamReader(FILE_NAME);
                    StringReadFile = objStreamReader.ReadToEnd().Replace("\r\n", "\n");
                    objStreamReader.Close();
                    objStreamReader.Dispose();

                    System.IO.File.Delete(FILE_NAME);
                    oWrite = System.IO.File.CreateText(FILE_NAME);
                    oWrite.Write(StringReadFile);
                    oWrite.Close();
                    oWrite.Dispose();
                }

                _log.WriteLog("- Write File " + GlSftpModel.FileSWREPO + " Success.");

            }
            catch (Exception Ex)
            {
                ReturnMsg = Ex.Message;
                return false;
            }

            return true;
        }

        private string retStringLengthRequest(string _field, int length)
        {
            if (_field.Length < length)
            {
                for (int j = 0; j <= length; j++)
                {
                    _field = _field + " ";
                    if (_field.Length == length)
                        break;
                }
            }

            return _field;
        }

        private string retStringLengthRequestRightZero(string _field, int length)
        {
            if (_field.Length < length)
            {
                for (int j = 0; j <= length; j++)
                {
                    _field = "0" + _field;
                    if (_field.Length == length)
                        break;
                }
            }

            return _field;
        }

        private bool Set_ConfigGl(ref string ReturnMsg, ref SftpEntity SftpEnt, ref InterfaceGlSftpModel GlSftpModel)
        {
            try
            {
                var filePath = Path.Combine(_env.ContentRootPath, GlSftpModel.RpConfigModel.FirstOrDefault(a => a.item_code == "PATH_SERVICE")?.item_value);
                SftpEnt.Enable = GlSftpModel.RpConfigModel.FirstOrDefault(a => a.item_code == "ENABLE_SFTP")?.item_value;
                SftpEnt.RemoteServerName = GlSftpModel.RpConfigModel.FirstOrDefault(a => a.item_code == "IP")?.item_value;
                SftpEnt.RemotePort = GlSftpModel.RpConfigModel.FirstOrDefault(a => a.item_code == "PORT")?.item_value;
                SftpEnt.RemoteUserName = GlSftpModel.RpConfigModel.FirstOrDefault(a => a.item_code == "USER")?.item_value;
                SftpEnt.RemotePassword = GlSftpModel.RpConfigModel.FirstOrDefault(a => a.item_code == "PASSWORD")?.item_value;
                SftpEnt.RemoteSshHostKeyFingerprint = GlSftpModel.RpConfigModel.FirstOrDefault(a => a.item_code == "SSHHOSTKEYFINGERPRINT")?.item_value;
                SftpEnt.RemoteSshPrivateKeyPath = GlSftpModel.RpConfigModel.FirstOrDefault(a => a.item_code == "SSHPRIVATEKEYPATH")?.item_value;
                SftpEnt.RemoteServerPath = GlSftpModel.RpConfigModel.FirstOrDefault(a => a.item_code == "PATH_SFTP")?.item_value;
                SftpEnt.NoOfFailRetry = Convert.ToInt32(GlSftpModel.RpConfigModel.FirstOrDefault(a => a.item_code == "FAIL_RETRY")?.item_value);
                SftpEnt.LocalPath = filePath.Replace("~\\", "");
                GlSftpModel.FilePath = filePath.Replace("~\\", "");
                GlSftpModel.FileGLREPO = GlSftpModel.RpConfigModel.FirstOrDefault(a => a.item_code == "FILE_NAME_GLREPO")?.item_value;
                GlSftpModel.FileSWREPO = GlSftpModel.RpConfigModel.FirstOrDefault(a => a.item_code == "FILE_NAME_SWREPO")?.item_value;

                _log.WriteLog("- AsOfDate = " + GlSftpModel.AsofDate.ToString("yyyyMMdd"));
                _log.WriteLog("- Sftp Enable = " + SftpEnt.Enable);
                _log.WriteLog("- Sftp RemoteServerName = " + SftpEnt.RemoteServerName);
                _log.WriteLog("- Sftp RemotePort = " + SftpEnt.RemotePort);
                _log.WriteLog("- Sftp RemoteSshHostKeyFingerprint = " + SftpEnt.RemoteSshHostKeyFingerprint);
                _log.WriteLog("- Sftp RemoteSshPrivateKeyPath = " + SftpEnt.RemoteSshPrivateKeyPath);
                _log.WriteLog("- Sftp RemoteServerPath = " + SftpEnt.RemoteServerPath);
                _log.WriteLog("- Sftp NoOfFailRetry = " + SftpEnt.NoOfFailRetry.ToString());
                _log.WriteLog("- Sftp LocalPath = " + SftpEnt.LocalPath);
                _log.WriteLog("- File GLREPO = " + GlSftpModel.FileGLREPO);
                _log.WriteLog("- File SWREPO = " + GlSftpModel.FileSWREPO);

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