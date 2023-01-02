using System;
using GM.CommonLibs.Common;
using GM.DataAccess.UnitOfWork;
using GM.Model.Common;
using GM.Model.ExternalInterface;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class InterfaceObligateController : ControllerBase
    {
        private readonly IUnitOfWork _uow;
        private static LogFile _log;

        public InterfaceObligateController(IUnitOfWork uow)
        {
            _uow = uow;
            _log = new LogFile(_uow.GetConfiguration, "InterfaceObligate");
        }

        [HttpPost]
        [Route("InterfaceObligate")]
        public RequestObligateModel.ResultObligate InterfaceObligate(RequestObligateModel model)
        {
            RequestObligateModel.ResultObligate res = new RequestObligateModel.ResultObligate();
            ServiceInOutReqModel serviceInOutReq = new ServiceInOutReqModel();
            DateTime now = DateTime.Now;

            var requestJsonData = JsonConvert.SerializeObject(model, new IsoDateTimeConverter() { DateTimeFormat = "dd/MM/yyyy HH:mm:ss" });
            //insert log in out
            serviceInOutReq.guid = model.Header.ref_id;
            serviceInOutReq.svc_req = requestJsonData;
            serviceInOutReq.svc_type = "IN";
            serviceInOutReq.module_name = "InterfaceObligateController";
            serviceInOutReq.action_name = "InterfaceObligate";
            serviceInOutReq.ref_id = model.Header.ref_id;
            serviceInOutReq.status = "0";
            serviceInOutReq.status_desc = "Success";
            serviceInOutReq.create_by = model.Body.create_by;
            _uow.External.ServiceInOutReq.Add(serviceInOutReq);

            try
            {
                //Create RP_Obligate
                BaseParameterModel parameter = new BaseParameterModel();
                parameter.ProcedureName = "RP_Interface_Obligate_List_Proc";
                parameter.Parameters.Add(new Field { Name = "obligate_id", Value = model.Body.obligate_id });
                parameter.Parameters.Add(new Field { Name = "system_name", Value = model.Body.system_name });
                parameter.Parameters.Add(new Field { Name = "instrument_id", Value = model.Body.instrument_id });
                parameter.Parameters.Add(new Field { Name = "port", Value = model.Body.port });
                parameter.Parameters.Add(new Field { Name = "start_obligate_date", Value = model.Body.start_obligate_date });
                parameter.Parameters.Add(new Field { Name = "expire_obligate_date", Value = model.Body.expire_obligate_date });
                parameter.Parameters.Add(new Field { Name = "obligate_unit", Value = model.Body.obligate_unit });
                parameter.Parameters.Add(new Field { Name = "approve_state", Value = model.Body.approve_state });
                parameter.Parameters.Add(new Field { Name = "create_date", Value = model.Body.create_date });
                parameter.Parameters.Add(new Field { Name = "recorded_by", Value = model.Body.create_by });

                ResultWithModel rwm = _uow.ExecNonQueryProc(parameter);

                if (!rwm.Success)
                {
                    throw new Exception("Error InterfaceObligate() : " + "[" + "-999" + "] " + rwm.Message);
                }

            }
            catch (Exception ex)
            {
                //Result
                _log.WriteLog("Error InterfaceObligate() : " + ex.Message);

                res.channel_id = model.Header.channel_id;
                res.ref_id = model.Header.ref_id;
                res.response_date = now.ToString("yyyyMMdd");
                res.response_time = now.ToString("HH:MM:ss.FFF");
                res.mode = "1";
                res.response_code = "-999";
                res.response_message = ex.Message;
                res.total_number = "1";
                res.content_type = "application/json";

                //update log in out
                var ResultJsonData = JsonConvert.SerializeObject(res, new IsoDateTimeConverter() { DateTimeFormat = "dd/MM/yyyy HH:mm:ss" });
                serviceInOutReq.svc_res = ResultJsonData;
                serviceInOutReq.guid = res.ref_id;
                serviceInOutReq.ref_id = res.ref_id;
                serviceInOutReq.status = res.response_code;
                serviceInOutReq.status_desc = res.response_code == "0" ? "Success" : "Not Success";
                _uow.External.ServiceInOutReq.Update(serviceInOutReq);

                return res;
            }
            //Result
            res.channel_id = model.Header.channel_id;
            res.ref_id = model.Header.ref_id;
            res.response_date = now.ToString("yyyyMMdd");
            res.response_time = now.ToString("HH:MM:ss.FFF");
            res.mode = "1";
            res.response_code = "0";
            res.response_message = "Success";
            res.total_number = "1";
            res.content_type = "application/json";

            //update log in out
            var json = JsonConvert.SerializeObject(res, new IsoDateTimeConverter() { DateTimeFormat = "dd/MM/yyyy HH:mm:ss" });
            serviceInOutReq.svc_res = json;
            serviceInOutReq.guid = res.ref_id;
            serviceInOutReq.ref_id = res.ref_id;
            serviceInOutReq.status = "0";
            serviceInOutReq.status_desc = "Success";
            _uow.External.ServiceInOutReq.Update(serviceInOutReq);

            return res;
        }
    }
}