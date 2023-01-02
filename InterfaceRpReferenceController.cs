using System;
using GM.DataAccess.UnitOfWork;
using GM.Model.Common;
using GM.Model.InterfaceRpReference;
using Microsoft.AspNetCore.Mvc;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class InterfaceRpReferenceController : ControllerBase
    {
        private readonly IUnitOfWork _uow;

        public InterfaceRpReferenceController(IUnitOfWork uow)
        {
            _uow = uow;
        }

        [HttpPost]
        [Route("ImportRpReference")]
        public ResultWithModel ImportRpReference(ReqRpReference model, string processDate, string settlementDay)
        {
            ResultWithModel res = new ResultWithModel();
            ResRpReference resRpRefRepo = new ResRpReference();
            DateTime asOfDate = ConvertStrToDate(processDate);

            try
            {
                _uow.BeginTransaction();

                // Step 1 : Deserialize StrJson
                //rpDetail = JsonConvert.DeserializeObject<ReqRpReference>(strJson);

                // Step 3 : Delete RpReferenceTbma(Temp)
                res = _uow.External.InterfaceRpReference.Remove(asOfDate, settlementDay);
                if (!res.Success)
                {
                    throw new Exception("Delete_RpReferenceTbma() : " + res.Message);
                }

                foreach (var reqRpReferenceList in model.datas.rp)
                {
                    // Step 4 : Insert RpReferenceTbma(Temp)
                    res = _uow.External.InterfaceRpReference.Add(reqRpReferenceList);
                    if (!res.Success)
                    {
                        throw new Exception("Insert_RpReferenceTbma() : [" + reqRpReferenceList.symbol + "] " + res.Message);
                    }
                }

                _uow.Commit();

                resRpRefRepo.response_code = "0";
                resRpRefRepo.response_message = "Import Reference Temp Success.";
            }
            catch (Exception ex)
            {
                _uow.Rollback();
                resRpRefRepo.response_code = "-999";
                resRpRefRepo.response_message = "Fail : " + ex.Message;
            }

            // Step 5 : Insert Into RpReference Select From RpReferenceTbma(Temp)
            if (resRpRefRepo.response_code == "0")
            {
                res = _uow.External.InterfaceRpReference.Import(asOfDate, settlementDay);
                if (!res.Success)
                {
                    resRpRefRepo.response_code = "-999";
                    resRpRefRepo.response_message = "Fail Insert_RpReference() : " + res.Message;
                }
                else
                {
                    resRpRefRepo.response_code = "0";
                    resRpRefRepo.response_message = "Import Reference Success.";
                }
            }

            resRpRefRepo.refcode = model.datas.ref_id;
            resRpRefRepo.count_data = model.datas.rp.Count.ToString();

            res.Data = resRpRefRepo;
            return res;
        }

        private static DateTime ConvertStrToDate(string strDate)
        {
            if (string.IsNullOrEmpty(strDate))
            {
                return DateTime.MinValue;
            }
            else
            {
                System.Globalization.DateTimeFormatInfo Format = new System.Globalization.DateTimeFormatInfo();
                string[] myDateTimePatterns = { "dd/MM/yyyy" };
                Format.SetAllDateTimePatterns(myDateTimePatterns, 'd');
                return DateTime.Parse(strDate, Format);
            }
        }
    }
}