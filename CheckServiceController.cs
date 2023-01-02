using GM.Model.Common;
using GM.Model.Static;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Net;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class CheckServiceController : ControllerBase
    {
        [HttpPost]
        [Route("ConnectionOpen")]
        public ResultWithModel ConnectionOpen(RpConfigModel model)
        {
            ResultWithModel rwm = new ResultWithModel();
            try
            {
                rwm.Success = true;
                rwm.Message = CheckService(model.item_value);
            }
            catch (Exception ex)
            {
                rwm.Success = false;
                rwm.Message = ex.Message;
            }
            return rwm;
        }

        private string CheckService(string site)
        {
            try
            {
                //get
                using (WebClient client = new WebClient())
                {
                    if (site.StartsWith("https://"))
                        ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, errors) => true;

                    using (client.OpenRead(site))
                    {
                        return "Online";
                    }
                }

            }
            catch(Exception ex)
            {
                try
                {
                    //post
                    using (WebClient client = new WebClient())
                    {
                        if (site.StartsWith("https://"))
                            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, errors) => true;

                        client.UploadString(site, string.Empty);
                        return "Online";
                    }

                }
                catch (WebException we) 
                {

                    if (we.Response != null && ((System.Net.HttpWebResponse)we.Response).StatusCode == HttpStatusCode.BadRequest) 
                    {
                        return "Online";
                    }
                    else
                    {
                        return "Offline";
                    }
                }
                catch (Exception ux)
                {
                    return "Offline";
                }
            }
        }
    }
}