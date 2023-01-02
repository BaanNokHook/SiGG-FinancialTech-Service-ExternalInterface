using System;
using System.Collections.Generic;
using System.Data;
using System.ServiceModel;
using GM.CommonLibs.Common;
using GM.CommonLibs.Helper;
using GM.DataAccess.UnitOfWork;
using GM.Model.Common;
using GM.Model.ExternalInterface;
using InterfaceTrasactionCrWSDL;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class InterfaceTransLimitCrController : ControllerBase
    {
        private readonly IUnitOfWork _uow;
        private static LogFile _log;
        private static DataTable Dt_Trans = new DataTable();
        private static DataTable Dt_TransCancel = new DataTable();
        private static DataTable Dt_Coll = new DataTable();
        private static ServiceInOutReqModel InOutModel = new ServiceInOutReqModel();

        public InterfaceTransLimitCrController(IUnitOfWork uow)
        {
            _uow = uow;
            _log = new LogFile(_uow.GetConfiguration, "InterfaceTransLimitCr");
        }

        [HttpPost]
        [Route("ExportTransLimitCr")]
        public ResultWithModel ExportTransLimitCr(InterfaceTransLimitCrModel TransLimitCrModel)
        {
            string StrMsg = string.Empty;
            ResultWithModel rwm = new ResultWithModel();
            TransLimitCrModel.List_RespTrans = new List<InterfaceTransLimitCrModel.ResponsTransLimitCr>();

            try
            {
                _log.WriteLog("Start ExportTransLimitCR ==========");
                _log.WriteLog("- AsOfDate = " + TransLimitCrModel.AsOfDate.ToString("yyyyMMdd"));
                _log.WriteLog("- ServiceUrl = " + TransLimitCrModel.ServiceUrl);
                _log.WriteLog("- ServiceTimeOut = " + TransLimitCrModel.ServiceTimeOut);
                _log.WriteLog("- ChannelId = " + TransLimitCrModel.ChannelId);
                _log.WriteLog("- RegisterCode = " + TransLimitCrModel.RegisterCode);

                // Step 1 : Select Trans
                _log.WriteLog("Search Trans REPO");
                if (Search_Trans(ref StrMsg, ref TransLimitCrModel) == false)
                {
                    throw new Exception("Search_Trans() : " + StrMsg);
                }

                // Step 2 : Loop Insert/Update Call Service CR
                _log.WriteLog("");
                _log.WriteLog("Loop Insert/Update Call Service CR");

                for (int i = 0; i < Dt_Trans.Rows.Count; i++)
                {
                    StrMsg = string.Empty;
                    TRS_TransInfo_Repo_HeaderEntity ReqTrans = new TRS_TransInfo_Repo_HeaderEntity();
                    InterfaceTransLimitCrModel.ResponsTransLimitCr RespTrans = new InterfaceTransLimitCrModel.ResponsTransLimitCr();
                    RespTrans.TransNo = Dt_Trans.Rows[i]["tran_no"].ToString();
                    string TransNo = Dt_Trans.Rows[i]["tran_no"].ToString();
                    _log.WriteLog("- TransNo = " + TransNo);

                    TRS_TransInfo_Repo_ReqInsertTradeInfoEntity TradeInfoEnt = new TRS_TransInfo_Repo_ReqInsertTradeInfoEntity();
                    List<TRS_TransInfo_Repo_ReqInsertCollateralInfoEntity> List_CollEnt = new List<TRS_TransInfo_Repo_ReqInsertCollateralInfoEntity>();

                    try
                    {
                        // Step 2.1 : Search Trans From CR
                        SearchEntity SearchEnt = new SearchEntity();
                        if (InterfaceCR_ModeSearch(ref StrMsg, TransLimitCrModel, ref SearchEnt, TransNo) == false)
                        {
                            throw new Exception("InterfaceCR_ModeSearch() : " + StrMsg);
                        }

                        // Step 2.2 : Set TradeInfoEnt / CollEnt
                        TradeInfoEnt = new TRS_TransInfo_Repo_ReqInsertTradeInfoEntity();
                        List_CollEnt = new List<TRS_TransInfo_Repo_ReqInsertCollateralInfoEntity>();
                        if (Set_TransInfoEntity(ref StrMsg, ref TransLimitCrModel, ref TradeInfoEnt, ref List_CollEnt, i) == false)
                        {
                            throw new Exception("Set_TransInfoEntity() : " + StrMsg);
                        }

                        RespTrans.TotalColl = List_CollEnt.Count;
                        switch (SearchEnt.RespTransStatus)
                        {
                            case false:
                                // Step 2.3 : Insert Trans To CR
                                RespTrans.Action = "Insert";
                                if (InterfaceCR_ModeInsert(ref RespTrans, ref ReqTrans, TransLimitCrModel, TradeInfoEnt, List_CollEnt) == false)
                                {
                                    throw new Exception("InterfaceCR_ModeInsert() : " + "[" + RespTrans.ReturnCode + "] " + RespTrans.ReturnMsg);
                                }
                                break;
                            case true:
                                // Step 2.3 : Update Trans To CR
                                RespTrans.Action = "Update";
                                if (InterfaceCR_ModeUpdate(ref RespTrans, ref ReqTrans, TransLimitCrModel, TradeInfoEnt, List_CollEnt) == false)
                                {
                                    throw new Exception("InterfaceCR_ModeUpdate() : " + "[" + RespTrans.ReturnCode + "] " + RespTrans.ReturnMsg);
                                }
                                break;
                        }

                        TransLimitCrModel.TransSuccess += 1;
                    }
                    catch (Exception Ex)
                    {
                        TransLimitCrModel.TransFail += 1;
                        _log.WriteLog("- Error " + Ex.Message);
                    }

                    // Step 2.4 : Update Log CR
                    rwm = UpdateTransLog(RespTrans, TransLimitCrModel);
                    if (!rwm.Success)
                    {
                        throw new Exception(StrMsg);
                    }

                    TransLimitCrModel.List_RespTrans.Add(RespTrans);
                    _log.WriteLog("- Update TransLogCR = Success");

                    string Json_Trans = JsonConvert.SerializeObject(TradeInfoEnt);
                    string Json_Coll = JsonConvert.SerializeObject(List_CollEnt);
                    string Json_ReqHeader = JsonConvert.SerializeObject(ReqTrans);
                    string Json_ReqTrans = Json_ReqHeader + Json_Trans + Json_Coll;
                    string Json_RespTrans = JsonConvert.SerializeObject(RespTrans);

                    InOutModel = new ServiceInOutReqModel();
                    InOutModel.guid = TransLimitCrModel.RefNo;
                    InOutModel.svc_req = Json_ReqTrans;
                    InOutModel.svc_res = Json_RespTrans;
                    InOutModel.svc_type = "OUT";
                    InOutModel.module_name = "InterfaceTransLimitCrController";
                    InOutModel.action_name = "ExportTransLimitCR";
                    InOutModel.ref_id = TransLimitCrModel.RefNo;
                    InOutModel.status = RespTrans.ReturnCode;
                    InOutModel.status_desc = RespTrans.ReturnMsg;
                    InOutModel.create_by = "WebService";

                    rwm = _uow.External.ServiceInOutReq.Add(InOutModel);
                    if (!rwm.Success)
                    {
                        throw new Exception("[" + rwm.RefCode.ToString() + "] " + rwm.Message);
                    }
                }

                // Step 3 : Loop Cancel Call Service CR
                _log.WriteLog("");
                _log.WriteLog("Loop Cancel Call Service CR");
                for (int j = 0; j < Dt_TransCancel.Rows.Count; j++)
                {
                    StrMsg = string.Empty;
                    TRS_TransInfo_Repo_HeaderEntity ReqTrans = new TRS_TransInfo_Repo_HeaderEntity();
                    InterfaceTransLimitCrModel.ResponsTransLimitCr RespTrans = new InterfaceTransLimitCrModel.ResponsTransLimitCr();
                    RespTrans.TransNo = Dt_TransCancel.Rows[j]["tran_no"].ToString();
                    string TransNo = Dt_TransCancel.Rows[j]["tran_no"].ToString();
                    TransLimitCrModel.RefNo = Dt_TransCancel.Rows[j]["ref_no"].ToString();
                    _log.WriteLog("- TransNo = " + TransNo);

                    try
                    {
                        // Step 3.1 : Search Trans From CR
                        SearchEntity SearchEnt = new SearchEntity();
                        if (InterfaceCR_ModeSearch(ref StrMsg, TransLimitCrModel, ref SearchEnt, TransNo) == false)
                        {
                            throw new Exception("InterfaceCR_ModeSearch() : " + StrMsg);
                        }

                        if (SearchEnt.RespTransStatus)
                        {
                            // Step 2.2 : Cancle Trans To CR
                            RespTrans.Action = "Cancel";
                            if (InterfaceCR_ModeCancel(ref RespTrans, ref ReqTrans, TransLimitCrModel, TransNo) == false)
                            {
                                throw new Exception("InterfaceCR_ModeCancel() : " + "[" + RespTrans.ReturnCode + "] " + RespTrans.ReturnMsg);
                            }
                        }
                        else
                        {
                            RespTrans.Action = "Cancel";
                            RespTrans.ReturnCode = "999";
                            RespTrans.ReturnMsg = "Trans Not Found From CR";
                            throw new Exception("InterfaceCR_ModeCancel() : " + "[" + RespTrans.ReturnCode + "] " + RespTrans.ReturnMsg);
                        }

                        TransLimitCrModel.TransSuccess += 1;
                    }
                    catch (Exception Ex)
                    {
                        TransLimitCrModel.TransFail += 1;
                        _log.WriteLog("- Error " + Ex.Message);
                    }

                    // Step 3.3 : Update Log CR
                    rwm = UpdateTransLog(RespTrans, TransLimitCrModel);
                    if (!rwm.Success)
                    {
                        throw new Exception(StrMsg);
                    }

                    TransLimitCrModel.List_RespTrans.Add(RespTrans);
                    _log.WriteLog("- Update TransLogCR = Success");

                    Dictionary<string, string> Trans = new Dictionary<string, string>();
                    Trans.Add("trans_no", TransNo);
                    string Json_Trans = JsonConvert.SerializeObject(Trans);
                    string Json_ReqHeader = JsonConvert.SerializeObject(ReqTrans);
                    string Json_ReqTrans = Json_ReqHeader + Json_Trans;
                    string Json_RespTrans = JsonConvert.SerializeObject(RespTrans);

                    InOutModel = new ServiceInOutReqModel();
                    InOutModel.guid = TransLimitCrModel.RefNo;
                    InOutModel.svc_req = Json_ReqTrans;
                    InOutModel.svc_res = Json_RespTrans;
                    InOutModel.svc_type = "OUT";
                    InOutModel.module_name = "InterfaceTransLimitCrController";
                    InOutModel.action_name = "ExportTransLimitCR";
                    InOutModel.ref_id = TransLimitCrModel.RefNo;
                    InOutModel.status = RespTrans.ReturnCode;
                    InOutModel.status_desc = RespTrans.ReturnMsg;
                    InOutModel.create_by = "WebService";

                    rwm = _uow.External.ServiceInOutReq.Add(InOutModel);
                    if (!rwm.Success)
                    {
                        throw new Exception("[" + rwm.RefCode.ToString() + "] " + rwm.Message);
                    }
                }

                DataSet dsResult = new DataSet();
                DataTable dt = new List<InterfaceTransLimitCrModel>() { TransLimitCrModel }.ToDataTable();
                dt.TableName = "InterfaceTransLimitCrResultModel";
                dsResult.Tables.Add(dt);
                rwm.Data = dsResult;
                rwm.RefCode = 0;
                rwm.Message = "Success";
                rwm.Serverity = "low";
                rwm.Success = true;

            }
            catch (Exception ex)
            {
                DataSet dsResult = new DataSet();
                DataTable dt = new List<InterfaceTransLimitCrModel>() { TransLimitCrModel }.ToDataTable();
                dt.TableName = "InterfaceTransLimitCrResultModel";
                dsResult.Tables.Add(dt);
                rwm.Data = dsResult;
                rwm.RefCode = -999;
                rwm.Message = ex.Message;
                rwm.Serverity = "high";
                rwm.Success = false;
                _log.WriteLog("Error ExportTransLimitCR() : " + ex.Message);
            }
            finally
            {
                _log.WriteLog("End ExportTransLimitCR ==========");
            }

            return rwm;
        }

        [HttpPost]
        [Route("ExportTransLimitCrEod")]
        public ResultWithModel ExportTransLimitCrEod(InterfaceTransLimitCrModel TransLimitCrModel)
        {
            string StrMsg = string.Empty;
            ResultWithModel rwm = new ResultWithModel();
            TransLimitCrModel.List_RespTrans = new List<InterfaceTransLimitCrModel.ResponsTransLimitCr>();

            try
            {
                _log.WriteLog("Start ExportTransLimitCREod ==========");
                _log.WriteLog("- AsOfDate = " + TransLimitCrModel.AsOfDate.ToString("yyyyMMdd"));
                _log.WriteLog("- ServiceUrl = " + TransLimitCrModel.ServiceUrl);
                _log.WriteLog("- ServiceTimeOut = " + TransLimitCrModel.ServiceTimeOut);
                _log.WriteLog("- ChannelId = " + TransLimitCrModel.ChannelId);
                _log.WriteLog("- RegisterCode = " + TransLimitCrModel.RegisterCode);

                // Step 1 : Select Trans Eod
                _log.WriteLog("Search Trans Eod REPO");
                if (Search_TransEod(ref StrMsg, ref TransLimitCrModel) == false)
                {
                    throw new Exception("Search_TransEod() : " + StrMsg);
                }

                // Step 2 : Loop Insert/Update Call Service CR Eod
                _log.WriteLog("");
                _log.WriteLog("Loop Insert/Update Call Service CR");
                for (int i = 0; i < Dt_Trans.Rows.Count; i++)
                {
                    StrMsg = string.Empty;
                    TRS_TransInfo_Repo_HeaderEntity ReqTrans = new TRS_TransInfo_Repo_HeaderEntity();
                    InterfaceTransLimitCrModel.ResponsTransLimitCr RespTrans = new InterfaceTransLimitCrModel.ResponsTransLimitCr();
                    RespTrans.TransNo = Dt_Trans.Rows[i]["tran_no"].ToString();
                    string TransNo = Dt_Trans.Rows[i]["tran_no"].ToString();
                    _log.WriteLog("- TransNo = " + TransNo);

                    TRS_TransInfo_Repo_ReqInsertTradeInfoEntity TradeInfoEnt = new TRS_TransInfo_Repo_ReqInsertTradeInfoEntity();
                    List<TRS_TransInfo_Repo_ReqInsertCollateralInfoEntity> List_CollEnt = new List<TRS_TransInfo_Repo_ReqInsertCollateralInfoEntity>();

                    try
                    {
                        // Step 2.1 : Search Trans From CR
                        SearchEntity SearchEnt = new SearchEntity();
                        if (InterfaceCR_ModeSearch(ref StrMsg, TransLimitCrModel, ref SearchEnt, TransNo) == false)
                        {
                            throw new Exception("InterfaceCR_ModeSearch() : " + StrMsg);
                        }

                        // Step 2.2 : Set TradeInfoEnt / CollEnt
                        TradeInfoEnt = new TRS_TransInfo_Repo_ReqInsertTradeInfoEntity();
                        List_CollEnt = new List<TRS_TransInfo_Repo_ReqInsertCollateralInfoEntity>();
                        if (Set_TransInfoEntity(ref StrMsg, ref TransLimitCrModel, ref TradeInfoEnt, ref List_CollEnt, i) == false)
                        {
                            throw new Exception("Set_TransInfoEntity() : " + StrMsg);
                        }

                        RespTrans.TotalColl = List_CollEnt.Count;
                        switch (SearchEnt.RespTransStatus)
                        {
                            case false:
                                // Step 2.3 : Insert Trans To CR
                                RespTrans.Action = "Insert";
                                if (InterfaceCR_ModeInsert(ref RespTrans, ref ReqTrans, TransLimitCrModel, TradeInfoEnt, List_CollEnt) == false)
                                {
                                    throw new Exception("InterfaceCR_ModeInsert() : " + "[" + RespTrans.ReturnCode + "] " + RespTrans.ReturnMsg);
                                }
                                break;
                            case true:
                                // Step 2.3 : Update Trans To CR
                                RespTrans.Action = "Update";
                                if (InterfaceCR_ModeUpdate(ref RespTrans, ref ReqTrans, TransLimitCrModel, TradeInfoEnt, List_CollEnt) == false)
                                {
                                    throw new Exception("InterfaceCR_ModeUpdate() : " + "[" + RespTrans.ReturnCode + "] " + RespTrans.ReturnMsg);
                                }
                                break;
                        }

                        TransLimitCrModel.TransSuccess += 1;
                    }
                    catch (Exception Ex)
                    {
                        TransLimitCrModel.TransFail += 1;
                        _log.WriteLog("- Error " + Ex.Message);
                    }

                    // Step 2.4 : Update Log CR
                    rwm = UpdateTransLog(RespTrans, TransLimitCrModel);
                    if (!rwm.Success)
                    {
                        throw new Exception(StrMsg);
                    }

                    TransLimitCrModel.List_RespTrans.Add(RespTrans);
                    _log.WriteLog("- Update TransLogCR = Success");

                    string Json_Trans = JsonConvert.SerializeObject(TradeInfoEnt);
                    string Json_Coll = JsonConvert.SerializeObject(List_CollEnt);
                    string Json_ReqHeader = JsonConvert.SerializeObject(ReqTrans);
                    string Json_ReqTrans = Json_ReqHeader + Json_Trans + Json_Coll;
                    string Json_RespTrans = JsonConvert.SerializeObject(RespTrans);

                    InOutModel = new ServiceInOutReqModel();
                    InOutModel.guid = TransLimitCrModel.RefNo;
                    InOutModel.svc_req = Json_ReqTrans;
                    InOutModel.svc_res = Json_RespTrans;
                    InOutModel.svc_type = "OUT";
                    InOutModel.module_name = "InterfaceTransLimitCrController";
                    InOutModel.action_name = "ExportTransLimitCREod";
                    InOutModel.ref_id = TransLimitCrModel.RefNo;
                    InOutModel.status = RespTrans.ReturnCode;
                    InOutModel.status_desc = RespTrans.ReturnMsg;
                    InOutModel.create_by = "WebService";

                    rwm = _uow.External.ServiceInOutReq.Add(InOutModel);
                    if (!rwm.Success)
                    {
                        throw new Exception("[" + rwm.RefCode.ToString() + "] " + rwm.Message);
                    }
                }

                // Step 3 : Loop Cancel Call Service CR Eod
                _log.WriteLog("");
                _log.WriteLog("Loop Cancel Call Service CR Eod");
                for (int j = 0; j < Dt_TransCancel.Rows.Count; j++)
                {
                    StrMsg = string.Empty;
                    TRS_TransInfo_Repo_HeaderEntity ReqTrans = new TRS_TransInfo_Repo_HeaderEntity();
                    InterfaceTransLimitCrModel.ResponsTransLimitCr RespTrans = new InterfaceTransLimitCrModel.ResponsTransLimitCr();
                    RespTrans.TransNo = Dt_TransCancel.Rows[j]["tran_no"].ToString();
                    string TransNo = Dt_TransCancel.Rows[j]["tran_no"].ToString();
                    TransLimitCrModel.RefNo = Dt_TransCancel.Rows[j]["ref_no"].ToString();
                    _log.WriteLog("- TransNo = " + TransNo);

                    try
                    {
                        // Step 3.1 : Search Trans From CR
                        SearchEntity SearchEnt = new SearchEntity();
                        if (InterfaceCR_ModeSearch(ref StrMsg, TransLimitCrModel, ref SearchEnt, TransNo) == false)
                        {
                            throw new Exception("InterfaceCR_ModeSearch() : " + StrMsg);
                        }

                        if (SearchEnt.RespTransStatus)
                        {
                            // Step 2.2 : Cancle Trans To CR
                            RespTrans.Action = "Cancel";
                            if (InterfaceCR_ModeCancel(ref RespTrans, ref ReqTrans, TransLimitCrModel, TransNo) == false)
                            {
                                throw new Exception("InterfaceCR_ModeCancel() : " + "[" + RespTrans.ReturnCode + "] " + RespTrans.ReturnMsg);
                            }
                        }
                        else
                        {
                            RespTrans.Action = "Cancel";
                            RespTrans.ReturnCode = "0";
                            RespTrans.ReturnMsg = "TrsnNo Is Cancel & Not Found From CR";
                            //throw new Exception("InterfaceCR_ModeCancel() : " + "[" + RespTrans.ReturnCode + "] " + RespTrans.ReturnMsg);
                        }

                        TransLimitCrModel.TransSuccess += 1;
                    }
                    catch (Exception Ex)
                    {
                        TransLimitCrModel.TransFail += 1;
                        _log.WriteLog("- Error " + Ex.Message);
                    }

                    // Step 3.3 : Update Log CR
                    rwm = UpdateTransLog(RespTrans, TransLimitCrModel);
                    if (!rwm.Success)
                    {
                        throw new Exception(StrMsg);
                    }

                    TransLimitCrModel.List_RespTrans.Add(RespTrans);
                    _log.WriteLog("- Update TransLogCR = Success");

                    Dictionary<string, string> Trans = new Dictionary<string, string>();
                    Trans.Add("trans_no", TransNo);
                    string Json_Trans = JsonConvert.SerializeObject(Trans);
                    string Json_ReqHeader = JsonConvert.SerializeObject(ReqTrans);
                    string Json_ReqTrans = Json_ReqHeader + Json_Trans;
                    string Json_RespTrans = JsonConvert.SerializeObject(RespTrans);

                    InOutModel = new ServiceInOutReqModel();
                    InOutModel.guid = TransLimitCrModel.RefNo;
                    InOutModel.svc_req = Json_ReqTrans;
                    InOutModel.svc_res = Json_RespTrans;
                    InOutModel.svc_type = "OUT";
                    InOutModel.module_name = "InterfaceTransLimitCrController";
                    InOutModel.action_name = "ExportTransLimitCREod";
                    InOutModel.ref_id = TransLimitCrModel.RefNo;
                    InOutModel.status = RespTrans.ReturnCode;
                    InOutModel.status_desc = RespTrans.ReturnMsg;
                    InOutModel.create_by = "WebService";

                    rwm = _uow.External.ServiceInOutReq.Add(InOutModel);
                    if (!rwm.Success)
                    {
                        throw new Exception("[" + rwm.RefCode.ToString() + "] " + rwm.Message);
                    }
                }

                DataSet dsResult = new DataSet();
                DataTable dt = new List<InterfaceTransLimitCrModel>() { TransLimitCrModel }.ToDataTable();
                dt.TableName = "InterfaceTransLimitCrResultModel";
                dsResult.Tables.Add(dt);
                rwm.Data = dsResult;
                rwm.RefCode = 0;
                rwm.Message = "Success";
                rwm.Serverity = "low";
                rwm.Success = true;

            }
            catch (Exception ex)
            {
                DataSet dsResult = new DataSet();
                DataTable dt = new List<InterfaceTransLimitCrModel>() { TransLimitCrModel }.ToDataTable();
                dt.TableName = "InterfaceTransLimitCrResultModel";
                dsResult.Tables.Add(dt);
                rwm.Data = dsResult;
                rwm.RefCode = -999;
                rwm.Message = ex.Message;
                rwm.Serverity = "high";
                rwm.Success = false;
                _log.WriteLog("Error ExportTransLimitCREod() : " + ex.Message);
            }
            finally
            {
                _log.WriteLog("End ExportTransLimitCREod ==========");
            }

            return rwm;
        }

        private bool Search_Trans(ref string ReturnMsg, ref InterfaceTransLimitCrModel TransLimitCrModel)
        {
            try
            {
                BaseParameterModel parameter = new BaseParameterModel();
                parameter.ProcedureName = "RP_Interface_Trans_CR_List_Proc";
                parameter.Parameters.Add(new Field { Name = "asof_date", Value = TransLimitCrModel.AsOfDate });
                parameter.Parameters.Add(new Field { Name = "recorded_by", Value = TransLimitCrModel.create_by });
                parameter.ResultModelNames.Add("InterfaceTransCrResultModel");
                parameter.ResultModelNames.Add("InterfaceCollCrResultModel");
                parameter.Paging = new PagingModel() { PageNumber = 1, RecordPerPage = 999999 };

                //Add Orderby
                parameter.Orders = new List<OrderByModel>();

                ResultWithModel rwm = _uow.ExecDataProc(parameter);

                if (!rwm.Success)
                {
                    throw new Exception("[" + rwm.RefCode + "] " + rwm.Message);
                }

                DataSet ds = (DataSet)rwm.Data;
                DataTable dtGroupTrans = ds.Tables[0];
                DataTable dtGroupColl = ds.Tables[1];

                // Step 1 : Filter Trans
                DataView Dv = new DataView(dtGroupTrans);
                Dt_Trans = new DataTable();
                Dv.RowFilter = "trans_status <> 'Cancel'";
                Dt_Trans = Dv.ToTable();

                // Step 2 : Filter TransCancel
                Dt_TransCancel = new DataTable();
                Dv.RowFilter = "trans_status = 'Cancel'";
                Dt_TransCancel = Dv.ToTable();

                // Step 3 : Filter Coll
                Dt_Coll = dtGroupColl;

                TransLimitCrModel.TransTotal = Dt_Trans.Rows.Count;
                TransLimitCrModel.TransCancelTotal = Dt_TransCancel.Rows.Count;
                TransLimitCrModel.TransSuccess = 0;
                TransLimitCrModel.TransFail = 0;

                _log.WriteLog("- TransTotal = [" + TransLimitCrModel.TransTotal + "] Rows.");
                _log.WriteLog("- TransCancelTotal = [" + TransLimitCrModel.TransCancelTotal + "] Rows.");

            }
            catch (Exception Ex)
            {
                ReturnMsg = Ex.Message;
                return false;
            }

            return true;
        }

        private bool Search_TransEod(ref string ReturnMsg, ref InterfaceTransLimitCrModel TransLimitCrModel)
        {
            try
            {
                BaseParameterModel parameter = new BaseParameterModel();
                parameter.ProcedureName = "RP_Interface_Trans_CR_Eod_List_Proc";
                parameter.Parameters.Add(new Field { Name = "asof_date", Value = TransLimitCrModel.AsOfDate });
                parameter.Parameters.Add(new Field { Name = "recorded_by", Value = TransLimitCrModel.create_by });
                parameter.ResultModelNames.Add("InterfaceTransCrResultModel");
                parameter.ResultModelNames.Add("InterfaceCollCrResultModel");
                parameter.Paging = new PagingModel() { PageNumber = 1, RecordPerPage = 99999 };
                parameter.Orders = new List<OrderByModel>();
                ResultWithModel rwm = _uow.ExecDataProc(parameter);

                if (!rwm.Success)
                {
                    throw new Exception("[" + rwm.RefCode + "] " + rwm.Message);
                }

                DataSet ds = (DataSet)rwm.Data;
                DataTable dtGroupTrans = ds.Tables[0];
                DataTable dtGroupColl = ds.Tables[1];

                // Step 1 : Filter Trans
                DataView Dv = new DataView(dtGroupTrans);
                Dt_Trans = new DataTable();
                Dv.RowFilter = "trans_status <> 'Cancel'";
                Dt_Trans = Dv.ToTable();

                // Step 2 : Filter TransCancel
                Dt_TransCancel = new DataTable();
                Dv.RowFilter = "trans_status = 'Cancel'";
                Dt_TransCancel = Dv.ToTable();

                // Step 3 : Filter Coll
                Dt_Coll = dtGroupColl;

                TransLimitCrModel.TransTotal = Dt_Trans.Rows.Count;
                TransLimitCrModel.TransCancelTotal = Dt_TransCancel.Rows.Count;
                TransLimitCrModel.TransSuccess = 0;
                TransLimitCrModel.TransFail = 0;

                _log.WriteLog("- TransTotal = [" + TransLimitCrModel.TransTotal + "] Rows.");
                _log.WriteLog("- TransCancelTotal = [" + TransLimitCrModel.TransCancelTotal + "] Rows.");

            }
            catch (Exception Ex)
            {
                ReturnMsg = Ex.Message;
                return false;
            }

            return true;
        }

        private class SearchEntity
        {
            public string ChannelId { get; set; }
            public string RefId { get; set; }
            public string TransDate { get; set; }
            public string TransTime { get; set; }
            public string Mode { get; set; }
            public string TransNo { get; set; }
            public string DealNo { get; set; }
            public string InstCpde { get; set; }
            public string DeskBook { get; set; }
            public string TransType { get; set; }
            public string CounterPartyCIF { get; set; }

            public bool RespTransStatus { get; set; }
        }
        private bool InterfaceCR_ModeSearch(ref string ReturnMsg, InterfaceTransLimitCrModel TransLimitCrModel, ref SearchEntity SearchEnt, string TransNo)
        {
            try
            {
                WS_CR_Trasaction_REPOSoapClient WsCR = new WS_CR_Trasaction_REPOSoapClient(WS_CR_Trasaction_REPOSoapClient.EndpointConfiguration.WS_CR_Trasaction_REPOSoap);

                string StrGuid = Guid.NewGuid().ToString();
                SearchEnt.RefId = StrGuid;
                SearchEnt.TransDate = DateTime.Now.ToString("yyyyMMdd");
                SearchEnt.TransTime = DateTime.Now.ToString("HH:mm:ss");
                SearchEnt.Mode = "04";
                SearchEnt.TransNo = TransNo;
                SearchEnt.DealNo = string.Empty;
                SearchEnt.InstCpde = string.Empty;
                SearchEnt.DeskBook = string.Empty;
                SearchEnt.TransType = string.Empty;
                SearchEnt.CounterPartyCIF = string.Empty;

                WsCR.Endpoint.Address = new EndpointAddress(TransLimitCrModel.ServiceUrl);
                WsCR.Endpoint.Binding.SendTimeout = TimeSpan.FromMilliseconds(TransLimitCrModel.ServiceTimeOut);

                TRS_TransInfo_ResSearchEntity resSearchEntity = WsCR.SearchRepoTransactionAsync(
                    TransLimitCrModel.ChannelId,
                    SearchEnt.RefId,
                    SearchEnt.TransDate,
                    SearchEnt.TransTime,
                    TransLimitCrModel.RegisterCode,
                    SearchEnt.Mode,
                    SearchEnt.TransNo,
                    SearchEnt.DealNo,
                    SearchEnt.InstCpde,
                    SearchEnt.DeskBook,
                    SearchEnt.TransType,
                    SearchEnt.CounterPartyCIF
                ).Result.Body.SearchRepoTransactionResult;

                if (resSearchEntity.return_code != "0")
                {
                    throw new Exception("[" + resSearchEntity.return_code + "]" + resSearchEntity.return_message);
                }

                if (resSearchEntity.TradeInfo.Length > 0)
                {
                    SearchEnt.RespTransStatus = true;

                }
                else
                {
                    SearchEnt.RespTransStatus = false;
                }
            }
            catch (Exception ex)
            {
                ReturnMsg = ex.Message;
                return false;
            }

            return true;
        }
        private bool Set_TransInfoEntity(ref string ReturnMsg, ref InterfaceTransLimitCrModel TransLimitCrModel, ref TRS_TransInfo_Repo_ReqInsertTradeInfoEntity TradeInfoEnt, ref List<TRS_TransInfo_Repo_ReqInsertCollateralInfoEntity> List_CollEnt, int Index)
        {
            try
            {
                TransLimitCrModel.RefNo = Dt_Trans.Rows[Index]["ref_no"].ToString();
                TradeInfoEnt.tran_no = Dt_Trans.Rows[Index]["tran_no"].ToString();
                TradeInfoEnt.deal_no = Dt_Trans.Rows[Index]["deal_no"].ToString();
                TradeInfoEnt.trade_date = Dt_Trans.Rows[Index]["trade_date"].ToString();
                TradeInfoEnt.draft = Convert.ToInt32(Dt_Trans.Rows[Index]["draft"].ToString());
                TradeInfoEnt.year_basic = Convert.ToInt32(Dt_Trans.Rows[Index]["year_basic"].ToString());
                TradeInfoEnt.authorized_level = Dt_Trans.Rows[Index]["authorized_level"].ToString();
                TradeInfoEnt.settlement_date = Dt_Trans.Rows[Index]["settlement_date"].ToString();
                TradeInfoEnt.period = Dt_Trans.Rows[Index]["period"].ToString();
                TradeInfoEnt.maturity_date = Dt_Trans.Rows[Index]["maturity_date"].ToString();
                TradeInfoEnt.purchase_price = Convert.ToDecimal(Dt_Trans.Rows[Index]["purchase_price"].ToString());
                TradeInfoEnt.repoint_rate = Convert.ToDecimal(Dt_Trans.Rows[Index]["repoint_rate"].ToString());
                TradeInfoEnt.interest_amount = Convert.ToDecimal(Dt_Trans.Rows[Index]["interest_amount"].ToString());
                TradeInfoEnt.repurchase_price = Convert.ToDecimal(Dt_Trans.Rows[Index]["repurchase_price"].ToString());
                TradeInfoEnt.dealer_id = Dt_Trans.Rows[Index]["dealer_id"].ToString();
                TradeInfoEnt.report_time = Dt_Trans.Rows[Index]["report_time"].ToString();
                TradeInfoEnt.withholding_tax_amt = Convert.ToDecimal(Dt_Trans.Rows[Index]["withholding_tax_amt"].ToString());
                TradeInfoEnt.business_date = Dt_Trans.Rows[Index]["business_date"].ToString();
                TradeInfoEnt.comment_aries = Dt_Trans.Rows[Index]["comment_aries"].ToString();
                TradeInfoEnt.trans_purpose = Dt_Trans.Rows[Index]["trans_purpose"].ToString();
                TradeInfoEnt.inst_code = Dt_Trans.Rows[Index]["inst_code"].ToString();
                TradeInfoEnt.payment_method = Dt_Trans.Rows[Index]["payment_method"].ToString();
                TradeInfoEnt.desk_book = Dt_Trans.Rows[Index]["desk_book"].ToString();
                TradeInfoEnt.inst_type = Dt_Trans.Rows[Index]["inst_type"].ToString();
                TradeInfoEnt.trans_type = Dt_Trans.Rows[Index]["trans_type"].ToString();
                TradeInfoEnt.margin_payment_method_id = Dt_Trans.Rows[Index]["margin_payment_method_id"].ToString();
                TradeInfoEnt.portfolio = Dt_Trans.Rows[Index]["portfolio"].ToString();
                TradeInfoEnt.repoint_team = Dt_Trans.Rows[Index]["repoint_team"].ToString();
                TradeInfoEnt.currency_code = Dt_Trans.Rows[Index]["currency_code"].ToString();
                TradeInfoEnt.counter_party_cif = Dt_Trans.Rows[Index]["counter_party_cif"].ToString();
                TradeInfoEnt.repo_user_id = Convert.ToInt32(Dt_Trans.Rows[Index]["repo_user_id"].ToString());
                TradeInfoEnt.bilateral_contract_no = Dt_Trans.Rows[Index]["bilateral_contract_no"].ToString();
                TradeInfoEnt.termination_value = Convert.ToDecimal(Dt_Trans.Rows[Index]["termination_value"].ToString());
                TradeInfoEnt.termination_date = Dt_Trans.Rows[Index]["termination_date"].ToString();
                TradeInfoEnt.termination_flag = Dt_Trans.Rows[Index]["termination_flag"].ToString();
                TradeInfoEnt.commit_by = Dt_Trans.Rows[Index]["commit_by"].ToString();
                TradeInfoEnt.approve_by = Dt_Trans.Rows[Index]["approve_by"].ToString();
                TradeInfoEnt.process_swift_id = Dt_Trans.Rows[Index]["process_swift_id"].ToString();
                TradeInfoEnt.remark_cancel = Dt_Trans.Rows[Index]["remark_cancel"].ToString();
                TradeInfoEnt.cancel_status = Dt_Trans.Rows[Index]["cancel_status"].ToString();
                TradeInfoEnt.cancel_date = Dt_Trans.Rows[Index]["cancel_date"].ToString();
                TradeInfoEnt.balance_flag = Dt_Trans.Rows[Index]["balance_flag"].ToString();
                TradeInfoEnt.withholding_tax = Convert.ToDecimal(Dt_Trans.Rows[Index]["withholding_tax"].ToString());
                TradeInfoEnt.override_flag = Dt_Trans.Rows[Index]["override_flag"].ToString();
                TradeInfoEnt.deal_state = Dt_Trans.Rows[Index]["deal_state"].ToString();
                TradeInfoEnt.deal_status = Dt_Trans.Rows[Index]["deal_status"].ToString();
                TradeInfoEnt.trans_create_date = Dt_Trans.Rows[Index]["trans_create_date"].ToString();
                TradeInfoEnt.trans_create_by = Dt_Trans.Rows[Index]["trans_create_by"].ToString();
                TradeInfoEnt.trans_update_date = Dt_Trans.Rows[Index]["trans_update_date"].ToString();
                TradeInfoEnt.trans_update_by = Dt_Trans.Rows[Index]["trans_update_by"].ToString();
                TradeInfoEnt.verify_by = Dt_Trans.Rows[Index]["verify_by"].ToString();
                TradeInfoEnt.req_terminate_date = Dt_Trans.Rows[Index]["req_terminate_date"].ToString();
                TradeInfoEnt.approve_date = Dt_Trans.Rows[Index]["approve_date"].ToString();
                TradeInfoEnt.verify_date = Dt_Trans.Rows[Index]["verify_date"].ToString();
                TradeInfoEnt.commit_date = Dt_Trans.Rows[Index]["commit_date"].ToString();
                TradeInfoEnt.cancel_by = Dt_Trans.Rows[Index]["cancel_by"].ToString();
                TradeInfoEnt.cancel_group_by = Dt_Trans.Rows[Index]["cancel_group_by"].ToString();
                TradeInfoEnt.terminate_by = Dt_Trans.Rows[Index]["terminate_by"].ToString();
                TradeInfoEnt.allow_date = Dt_Trans.Rows[Index]["allow_date"].ToString();
                TradeInfoEnt.fee_amt1 = Convert.ToDecimal(Dt_Trans.Rows[Index]["fee_amt1"].ToString());
                TradeInfoEnt.fee_amt2 = Convert.ToDecimal(Dt_Trans.Rows[Index]["fee_amt2"].ToString());
                TradeInfoEnt.fee_amt3 = Convert.ToDecimal(Dt_Trans.Rows[Index]["fee_amt3"].ToString());
                TradeInfoEnt.fund_cost = Convert.ToDecimal(Dt_Trans.Rows[Index]["fund_cost"].ToString());
                TradeInfoEnt.market_date_rate_date = Dt_Trans.Rows[Index]["market_date_rate_date"].ToString();
                TradeInfoEnt.interest_type_code = Dt_Trans.Rows[Index]["interest_type_code"].ToString();
                TradeInfoEnt.interest_spread = Convert.ToDecimal(Dt_Trans.Rows[Index]["interest_spread"].ToString());
                TradeInfoEnt.interest_total = Convert.ToDecimal(Dt_Trans.Rows[Index]["interest_total"].ToString());
                TradeInfoEnt.threshold_amt = Convert.ToDecimal(Dt_Trans.Rows[Index]["threshold_amt"].ToString());
                TradeInfoEnt.transaction_exposure = Convert.ToDecimal(Dt_Trans.Rows[Index]["transaction_exposure"].ToString());
                TradeInfoEnt.cash_margin = Convert.ToDecimal(Dt_Trans.Rows[Index]["cash_margin"].ToString());
                TradeInfoEnt.int_cash_margin = Convert.ToDecimal(Dt_Trans.Rows[Index]["int_cash_margin"].ToString());
                TradeInfoEnt.margin_balance = Convert.ToDecimal(Dt_Trans.Rows[Index]["margin_balance"].ToString());
                TradeInfoEnt.cpty_swift_code = Dt_Trans.Rows[Index]["cpty_swift_code"].ToString();
                TradeInfoEnt.cpty_tax_id = Dt_Trans.Rows[Index]["cpty_tax_id"].ToString();
                TradeInfoEnt.cpty_juristic_id = Dt_Trans.Rows[Index]["cpty_juristic_id"].ToString();
                TradeInfoEnt.cpty_org_id = Dt_Trans.Rows[Index]["cpty_org_id"].ToString();
                TradeInfoEnt.exposure_s = Convert.ToDecimal(Dt_Trans.Rows[Index]["exposure_s"].ToString());
                TradeInfoEnt.haircut_s = Convert.ToDecimal(Dt_Trans.Rows[Index]["haircut_s"].ToString());
                TradeInfoEnt.card_profile_no = Dt_Trans.Rows[Index]["card_profile_no"].ToString();
                TradeInfoEnt.customer_type_code = Dt_Trans.Rows[Index]["customer_type_code"].ToString();
                TradeInfoEnt.ref1 = Dt_Trans.Rows[Index]["ref1"].ToString();
                TradeInfoEnt.ref2 = Dt_Trans.Rows[Index]["ref2"].ToString();
                TradeInfoEnt.ref3 = Dt_Trans.Rows[Index]["ref3"].ToString();
                TradeInfoEnt.ref4 = Dt_Trans.Rows[Index]["ref4"].ToString();
                TradeInfoEnt.ref5 = Dt_Trans.Rows[Index]["ref5"].ToString();
                TradeInfoEnt.ref6 = Dt_Trans.Rows[Index]["ref6"].ToString();
                TradeInfoEnt.ref7 = Dt_Trans.Rows[Index]["ref7"].ToString();
                TradeInfoEnt.ref8 = Dt_Trans.Rows[Index]["ref8"].ToString();
                TradeInfoEnt.ref9 = Dt_Trans.Rows[Index]["ref9"].ToString();
                TradeInfoEnt.ref10 = Dt_Trans.Rows[Index]["ref10"].ToString();

                DataView dv = new DataView(Dt_Coll);
                dv.RowFilter = "tran_no = '" + Dt_Trans.Rows[Index]["tran_no"] + "'";
                DataTable dtTrsnsColl = dv.ToTable();

                for (int j = 0; j < dtTrsnsColl.Rows.Count; j++)
                {
                    TRS_TransInfo_Repo_ReqInsertCollateralInfoEntity CollEnt = new TRS_TransInfo_Repo_ReqInsertCollateralInfoEntity();

                    CollEnt.coll_id = dtTrsnsColl.Rows[j]["coll_id"].ToString();
                    CollEnt.tran_no = dtTrsnsColl.Rows[j]["tran_no"].ToString();
                    CollEnt.dirty_price = Convert.ToDecimal(dtTrsnsColl.Rows[j]["dirty_price"].ToString());
                    CollEnt.clean_price = Convert.ToDecimal(dtTrsnsColl.Rows[j]["clean_price"].ToString());
                    CollEnt.purchase_unit = Convert.ToDecimal(dtTrsnsColl.Rows[j]["purchase_unit"].ToString());
                    CollEnt.par_value = Convert.ToDecimal(dtTrsnsColl.Rows[j]["par_value"].ToString());
                    CollEnt.sec_market_value = Convert.ToDecimal(dtTrsnsColl.Rows[j]["sec_market_value"].ToString());
                    CollEnt.cash_amount = Convert.ToDecimal(dtTrsnsColl.Rows[j]["cash_amount"].ToString());
                    CollEnt.ytm = Convert.ToDecimal(dtTrsnsColl.Rows[j]["ytm"].ToString());
                    CollEnt.dirty_price_after_hc = Convert.ToDecimal(dtTrsnsColl.Rows[j]["dirty_price_after_hc"].ToString());
                    CollEnt.termination_value = Convert.ToDecimal(dtTrsnsColl.Rows[j]["termination_value"].ToString());
                    CollEnt.withholding_tax_amt = Convert.ToDecimal(dtTrsnsColl.Rows[j]["withholding_tax_amt"].ToString());
                    CollEnt.repoint_amount = Convert.ToDecimal(dtTrsnsColl.Rows[j]["repoint_amount"].ToString());
                    CollEnt.security_code = dtTrsnsColl.Rows[j]["security_code"].ToString();
                    CollEnt.security_type = dtTrsnsColl.Rows[j]["security_type"].ToString();
                    CollEnt.sec_eod_mtm = Convert.ToDecimal(dtTrsnsColl.Rows[j]["sec_eod_mtm"].ToString());
                    CollEnt.collateral_c = Convert.ToDecimal(dtTrsnsColl.Rows[j]["collateral_c"].ToString());
                    CollEnt.bondtradedate = dtTrsnsColl.Rows[j]["BondTradeDate"].ToString();
                    CollEnt.bondvaldate = dtTrsnsColl.Rows[j]["BondValDate"].ToString();
                    CollEnt.bondmatdate = dtTrsnsColl.Rows[j]["BondMatDate"].ToString();
                    CollEnt.bondccy = dtTrsnsColl.Rows[j]["BondCurrency"].ToString();
                    CollEnt.ref1 = dtTrsnsColl.Rows[j]["ref1"].ToString();
                    CollEnt.ref2 = dtTrsnsColl.Rows[j]["ref2"].ToString();
                    CollEnt.ref3 = dtTrsnsColl.Rows[j]["ref3"].ToString();
                    CollEnt.ref4 = dtTrsnsColl.Rows[j]["ref4"].ToString();
                    CollEnt.ref5 = dtTrsnsColl.Rows[j]["ref5"].ToString();
                    CollEnt.ref6 = dtTrsnsColl.Rows[j]["ref6"].ToString();
                    CollEnt.ref7 = dtTrsnsColl.Rows[j]["ref7"].ToString();
                    CollEnt.ref8 = dtTrsnsColl.Rows[j]["ref8"].ToString();
                    CollEnt.ref9 = dtTrsnsColl.Rows[j]["ref9"].ToString();
                    CollEnt.ref10 = dtTrsnsColl.Rows[j]["ref10"].ToString();

                    List_CollEnt.Add(CollEnt);
                }
            }
            catch (Exception Ex)
            {
                ReturnMsg = Ex.Message;
                return false;
            }

            return true;
        }
        private bool InterfaceCR_ModeInsert(ref InterfaceTransLimitCrModel.ResponsTransLimitCr RespTrans, ref TRS_TransInfo_Repo_HeaderEntity ReqTrans, InterfaceTransLimitCrModel TransLimitCrModel, TRS_TransInfo_Repo_ReqInsertTradeInfoEntity TradeInfoEnt, List<TRS_TransInfo_Repo_ReqInsertCollateralInfoEntity> List_CollEnt)
        {
            try
            {
                WS_CR_Trasaction_REPOSoapClient WsCR = new WS_CR_Trasaction_REPOSoapClient(WS_CR_Trasaction_REPOSoapClient.EndpointConfiguration.WS_CR_Trasaction_REPOSoap);
                ReqTrans = new TRS_TransInfo_Repo_HeaderEntity();

                // Step 1 : Set HeaderEnt

                ReqTrans.ref_no = TransLimitCrModel.RefNo;
                ReqTrans.chanel_id = TransLimitCrModel.ChannelId;
                ReqTrans.trans_date = DateTime.Now.ToString("yyyyMMdd");
                ReqTrans.trans_time = DateTime.Now.ToString("HH:mm:ss");
                ReqTrans.mode = "01"; //Mode Insert
                ReqTrans.register_code = TransLimitCrModel.RegisterCode;

                List<TRS_TransInfo_Repo_ReqInsertTradeInfoEntity> List_TradeInfoEnt = new List<TRS_TransInfo_Repo_ReqInsertTradeInfoEntity>();
                List_TradeInfoEnt.Add(TradeInfoEnt);

                _log.WriteLog("- Action = " + RespTrans.Action);
                _log.WriteLog("- ref_no = " + ReqTrans.ref_no);
                _log.WriteLog("- trans_date = " + ReqTrans.trans_date);
                _log.WriteLog("- trans_time = " + ReqTrans.trans_time);
                _log.WriteLog("- mode = " + ReqTrans.mode);

                // Step 3 : Call Service
                WsCR.Endpoint.Address = new EndpointAddress(TransLimitCrModel.ServiceUrl);
                WsCR.Endpoint.Binding.SendTimeout = TimeSpan.FromMilliseconds(TransLimitCrModel.ServiceTimeOut);
                TRS_TransInfo_Repo_ResInsertEntity resInsertEntity = WsCR.UpdateRepoTransactionAsync(ReqTrans, List_TradeInfoEnt.ToArray(), List_CollEnt.ToArray()).Result.Body.UpdateRepoTransactionResult;

                RespTrans.ReturnCode = resInsertEntity.ReturnCode;
                RespTrans.ReturnMsg = resInsertEntity.Message;

                if (resInsertEntity.ReturnCode != "0")
                {
                    if (resInsertEntity.ins_trade != null)
                    {
                        RespTrans.ReturnMsg += " " + resInsertEntity.ins_trade[0].RetDescription;
                    }
                    return false;
                }

                _log.WriteLog("- ReturnCode = " + RespTrans.ReturnCode);
                _log.WriteLog("- ReturnMsg = " + RespTrans.ReturnMsg);
            }
            catch (Exception ex)
            {
                RespTrans.ReturnCode = "999";
                RespTrans.ReturnMsg = ex.Message;
                return false;
            }

            return true;
        }
        private bool InterfaceCR_ModeUpdate(ref InterfaceTransLimitCrModel.ResponsTransLimitCr RespTrans, ref TRS_TransInfo_Repo_HeaderEntity ReqTrans, InterfaceTransLimitCrModel TransLimitCrModel, TRS_TransInfo_Repo_ReqInsertTradeInfoEntity TradeInfoEnt, List<TRS_TransInfo_Repo_ReqInsertCollateralInfoEntity> List_CollEnt)
        {
            try
            {
                WS_CR_Trasaction_REPOSoapClient WsCR = new WS_CR_Trasaction_REPOSoapClient(WS_CR_Trasaction_REPOSoapClient.EndpointConfiguration.WS_CR_Trasaction_REPOSoap);
                ReqTrans = new TRS_TransInfo_Repo_HeaderEntity();

                // Step 1 : Set HeaderEnt
                ReqTrans.ref_no = TransLimitCrModel.RefNo;
                ReqTrans.chanel_id = TransLimitCrModel.ChannelId;
                ReqTrans.trans_date = DateTime.Now.ToString("yyyyMMdd");
                ReqTrans.trans_time = DateTime.Now.ToString("HH:mm:ss");
                ReqTrans.mode = "02"; //Mode Update
                ReqTrans.register_code = TransLimitCrModel.RegisterCode;

                List<TRS_TransInfo_Repo_ReqInsertTradeInfoEntity> List_TradeInfoEnt = new List<TRS_TransInfo_Repo_ReqInsertTradeInfoEntity>();
                List_TradeInfoEnt.Add(TradeInfoEnt);

                _log.WriteLog("- Action = " + RespTrans.Action);
                _log.WriteLog("- ref_no = " + ReqTrans.ref_no);
                _log.WriteLog("- trans_date = " + ReqTrans.trans_date);
                _log.WriteLog("- trans_time = " + ReqTrans.trans_time);
                _log.WriteLog("- mode = " + ReqTrans.mode);

                // Step 2 : Call Service
                WsCR.Endpoint.Address = new EndpointAddress(TransLimitCrModel.ServiceUrl);
                WsCR.Endpoint.Binding.SendTimeout = TimeSpan.FromMilliseconds(TransLimitCrModel.ServiceTimeOut);
                TRS_TransInfo_Repo_ResInsertEntity resInsertEntity = WsCR.UpdateRepoTransactionAsync(ReqTrans, List_TradeInfoEnt.ToArray(), List_CollEnt.ToArray()).Result.Body.UpdateRepoTransactionResult;

                RespTrans.ReturnCode = resInsertEntity.ReturnCode;
                RespTrans.ReturnMsg = resInsertEntity.Message;

                if (resInsertEntity.ReturnCode != "0")
                {
                    if (resInsertEntity.ins_trade != null)
                    {
                        RespTrans.ReturnMsg += " " + resInsertEntity.ins_trade[0].RetDescription;
                    }
                    return false;
                }

                _log.WriteLog("- ReturnCode = " + RespTrans.ReturnCode);
                _log.WriteLog("- ReturnMsg = " + RespTrans.ReturnMsg);

            }
            catch (Exception ex)
            {
                RespTrans.ReturnCode = "999";
                RespTrans.ReturnMsg = ex.Message;
                return false;
            }

            return true;
        }
        private bool InterfaceCR_ModeCancel(ref InterfaceTransLimitCrModel.ResponsTransLimitCr RespTrans, ref TRS_TransInfo_Repo_HeaderEntity ReqTrans, InterfaceTransLimitCrModel TransLimitCrModel, string TransNo)
        {
            try
            {
                WS_CR_Trasaction_REPOSoapClient WsCR = new WS_CR_Trasaction_REPOSoapClient(WS_CR_Trasaction_REPOSoapClient.EndpointConfiguration.WS_CR_Trasaction_REPOSoap);
                ReqTrans = new TRS_TransInfo_Repo_HeaderEntity();

                ReqTrans.ref_no = TransLimitCrModel.RefNo;
                ReqTrans.chanel_id = TransLimitCrModel.ChannelId;
                ReqTrans.trans_date = DateTime.Now.ToString("yyyyMMdd");
                ReqTrans.trans_time = DateTime.Now.ToString("HH:mm:ss");
                ReqTrans.mode = "03"; //Mode Cancel
                ReqTrans.register_code = TransLimitCrModel.RegisterCode;

                _log.WriteLog("- Action = " + RespTrans.Action);
                _log.WriteLog("- ref_no = " + ReqTrans.ref_no);
                _log.WriteLog("- trans_date = " + ReqTrans.trans_date);
                _log.WriteLog("- trans_time = " + ReqTrans.trans_time);
                _log.WriteLog("- mode = " + ReqTrans.mode);

                WsCR.Endpoint.Address = new EndpointAddress(TransLimitCrModel.ServiceUrl);
                WsCR.Endpoint.Binding.SendTimeout = TimeSpan.FromMilliseconds(TransLimitCrModel.ServiceTimeOut);
                TRS_TransInfo_Repo_ResDeleteEntity resDeleteEntity = WsCR.DeleteRepoTransactionAsync(ReqTrans, TransNo, "", "", "", "", "").Result.Body.DeleteRepoTransactionResult;

                RespTrans.ReturnCode = resDeleteEntity.ResponseCode;
                RespTrans.ReturnMsg = resDeleteEntity.ResponseDesc;

                if (resDeleteEntity.ResponseCode != "0")
                {
                    return false;
                }

                _log.WriteLog("- ReturnCode = " + RespTrans.ReturnCode);
                _log.WriteLog("- ReturnMsg = " + RespTrans.ReturnMsg);

            }
            catch (Exception ex)
            {
                RespTrans.ReturnCode = "999";
                RespTrans.ReturnMsg = ex.Message;
                return false;
            }

            return true;
        }
        private ResultWithModel UpdateTransLog(InterfaceTransLimitCrModel.ResponsTransLimitCr res, InterfaceTransLimitCrModel model)
        {
            ResultWithModel rwm = new ResultWithModel();
            try
            {
                BaseParameterModel parameter = new BaseParameterModel();
                parameter.ProcedureName = "RP_Interface_Trans_CR_Update_Proc";
                parameter.Parameters.Add(new Field { Name = "recorded_by", Value = model.create_by });
                parameter.Parameters.Add(new Field { Name = "ref_no", Value = model.RefNo });
                parameter.Parameters.Add(new Field { Name = "trans_no", Value = res.TransNo });
                parameter.Parameters.Add(new Field { Name = "action", Value = res.Action });
                parameter.Parameters.Add(new Field { Name = "response_code", Value = res.ReturnCode });
                parameter.Parameters.Add(new Field { Name = "response_message", Value = res.ReturnMsg });
                parameter.ResultModelNames.Add("InterfaceTransCrResultModel");
                rwm = _uow.ExecNonQueryProc(parameter);

                if (!rwm.Success)
                {
                    throw new Exception("[" + rwm.RefCode + "] " + rwm.Message);
                }
            }
            catch (Exception ex)
            {
                rwm.Message = ex.Message;
                rwm.Success = false;
            }

            return rwm;
        }
    }
}