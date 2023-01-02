using System;
using GM.CommonLibs.Common;
using GM.DataAccess.UnitOfWork;
using GM.Model.Common;
using GM.Model.ExternalInterface;
using Microsoft.AspNetCore.Mvc;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class InternalPLController : ControllerBase
    {
        private readonly IUnitOfWork _uow;
        private static LogFile _log;

        public InternalPLController(IUnitOfWork uow)
        {
            _uow = uow;
            _log = new LogFile(_uow.GetConfiguration, "InternalPL");
        }

        [HttpPost]
        [Route("GetList")]
        public ResultWithModel GetList(InternalPLModel model)
        {
            BaseParameterModel parameter = new BaseParameterModel();
            parameter.ProcedureName = "RP_Trans_Liquidity_List_Proc";
            parameter.Parameters.Add(new Field { Name = "asof_date_from", Value = model.asof_date_from });
            parameter.Parameters.Add(new Field { Name = "asof_date_to", Value = model.asof_date_to });
            return _uow.ExecDataProc(parameter);
        }

        [HttpPost]
        [Route("ReRunPL")]
        public ResultWithModel ReRunPL(InternalPLModel model)
        {
            ResultWithModel rwm = new ResultWithModel();

            try
            {
                _log.WriteLog("Start InternalPL ==========");
                _log.WriteLog(" - asof_date_from = " + model.asof_date_from.Date);
                _log.WriteLog(" - asof_date_to = " + model.asof_date_to.Date);

                int end = (int)(model.asof_date_to - model.asof_date_from).TotalDays;

                _log.WriteLog(" - TotalDays = " + end);

                for (int start=0; start<=end; start++)
                {
                    DateTime asOfDate = model.asof_date_from.AddDays(start);

                    BaseParameterModel parameter = new BaseParameterModel();
                    parameter.ProcedureName = "RP_Gen_Pl_Proc";
                    parameter.Parameters.Add(new Field { Name = "asof_date", Value = asOfDate });
                    parameter.ResultModelNames.Add("InternalPLResultModel");
                    rwm = _uow.ExecNonQueryProc(parameter);

                    if (!rwm.Success)
                    {
                        throw new Exception(" asOfDate : " + asOfDate.Date + " [" + rwm.RefCode + "] " + rwm.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                rwm.RefCode = -999;
                rwm.Message = ex.Message;
                rwm.Serverity = "high";
                rwm.Success = false;
                _log.WriteLog("Error InternalPL() : " + ex.Message);
            }
            finally
            {
                _log.WriteLog("End InternalPL ==========");
            }

            rwm.Data = model;
            return rwm;
        }

    }
}