using GM.CommonLibs.Common;
using GM.CommonLibs.Helper;
using GM.DataAccess.UnitOfWork;
using GM.Model.Common;
using GM.Model.ExternalInterface;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class InternalJobController : ControllerBase
    {
        private readonly IUnitOfWork _uow;
        private static LogFile _log;

        public InternalJobController(IUnitOfWork uow)
        {
            _uow = uow;
            _log = new LogFile(_uow.GetConfiguration, "InternalJob");
        }

        [HttpPost]
        [Route("InternalBatchJobEod")]
        public ResultWithModel InternalBatchJobEod(InternalJobModel InternalJobModel)
        {
            ResultWithModel rwm = new ResultWithModel();

            try
            {
                _log.WriteLog("Start InternalBatchJobEod ==========");

                if (!DateTime.TryParseExact(InternalJobModel.AsofDate, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var asOfDate))
                {
                    throw new Exception("AsofDate is not correct");
                }

                BaseParameterModel parameter = new BaseParameterModel();
                parameter.ProcedureName = "GM_Batch_Run_Job_EOD";
                parameter.Parameters.Add(new Field { Name = "From_Business_date", Value = asOfDate });
                parameter.Parameters.Add(new Field { Name = "To_Business_date", Value = asOfDate });
                rwm = _uow.ExecNonQueryProc(parameter);

                if (!rwm.Success)
                {
                    throw new Exception("[" + rwm.RefCode + "] " + rwm.Message);
                }

            }
            catch (Exception ex)
            {
                rwm.RefCode = -999;
                rwm.Message = ex.Message;
                rwm.Serverity = "high";
                rwm.Success = false;
                _log.WriteLog("Error InternalBatchJobEod() : " + ex.Message);
            }
            finally
            {
                _log.WriteLog("End InternalBatchJobEod ==========");
            }

            DataSet dsResult = new DataSet();
            DataTable dt = new List<InternalJobModel> { InternalJobModel }.ToDataTable<InternalJobModel>();
            dt.TableName = "InternalJobResultModel";
            dsResult.Tables.Add(dt);
            rwm.Data = dsResult;
            return rwm;
        }

        [HttpPost]
        [Route("InternalBatchJobEndOfDay")]
        public ResultWithModel InternalBatchJobEndOfDay(InternalJobModel InternalJobModel)
        {
            ResultWithModel rwm = new ResultWithModel();

            try
            {
                _log.WriteLog("Start InternalBatchJobEndOfDay ==========");
                
                BaseParameterModel parameter = new BaseParameterModel();
                parameter.ProcedureName = "GM_Batch_End_of_day";

                if (!string.IsNullOrEmpty(InternalJobModel.AsofDate))
                {
                    if (!DateTime.TryParseExact(InternalJobModel.AsofDate, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var asOfDate))
                    {
                        throw new Exception("AsofDate is not correct");
                    }
                    parameter.Parameters.Add(new Field { Name = "From_Business_date", Value = asOfDate });
                    parameter.Parameters.Add(new Field { Name = "To_Business_date", Value = asOfDate });
                }
                else
                {
                    parameter.Parameters.Add(new Field { Name = "From_Business_date", Value = DBNull.Value });
                    parameter.Parameters.Add(new Field { Name = "To_Business_date", Value = DBNull.Value });
                }
                
                parameter.ResultModelNames.Add("InternalJobResultModel");
                rwm = _uow.ExecNonQueryProc(parameter);

                if (rwm.Success)
                {
                    _log.WriteLog("result = Success");
                }
                else
                {
                    throw new Exception("[" + rwm.RefCode + "] " + rwm.Message);
                }
            }
            catch (Exception ex)
            {
                rwm.RefCode = -999;
                rwm.Message = ex.Message;
                rwm.Serverity = "high";
                rwm.Success = false;
                _log.WriteLog("Error InternalBatchJobEndOfDay() : " + ex.Message);
            }
            finally
            {
                _log.WriteLog("End InternalBatchJobEndOfDay ==========");
            }

            DataSet dsResult = new DataSet();
            DataTable dt = new List<InternalJobModel> { InternalJobModel }.ToDataTable<InternalJobModel>();
            dt.TableName = "InternalJobResultModel";
            dsResult.Tables.Add(dt);
            rwm.Data = dsResult;
            return rwm;
        }
    }
}