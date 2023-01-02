using GM.Model.Common;
using GM.Model.Static;
using GM.SFTPUtility;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GM.Service.ExternalInterface.Controllers
{
   [Route("[controller]")]   
   [ApiController]  
   public class CheckSFTPController : ControllerBase  
   {
      [HttpPost]   
      [Route("ConnectionOpen")]   
      public ResultWithModel ConnectionOpen(List<RpConfigModel> model)   
      {
            ResultWithModel rwm = new ResultWithModel();   
            try
            {
                SftpUtility objectSftp = new SftpUtility();    
                string strMsg = string.Empty;
                SftpEntity sftpEnt = new SftpEntity();
                sftpEnt.RemoteServerName = model.FirstOrDefault(a => a.item_code == "IP")?.item_value;
                sftpEnt.RemotePort = model.FirstOrDefault(a => a.item_code == "PORT")?.item_value;
                sftpEnt.RemoteUserName = model.FirstOrDefault(a => a.item_code == "USER")?.item_value;
                sftpEnt.RemotePassword = model.FirstOrDefault(a => a.item_code == "PASSWORD")?.item_value;
                sftpEnt.RemoteSshHostKeyFingerprint = model.FirstOrDefault(a => a.item_code == "SSHHOSTKEYFINGERPRINT")?.item_value;
                sftpEnt.RemoteSshPrivateKeyPath = model.FirstOrDefault(a => a.item_code == "SSHPRIVATEKEYPATH")?.item_value;
                sftpEnt.NoOfFailRetry = Convert.ToInt32(model.FirstOrDefault(a => a.item_code == "FAIL_RETRY")?.item_value);
                sftpEnt.RemoteServerPath = model.FirstOrDefault(a => a.item_code == "PATH_SFTP")?.item_value;

                if (objectSftp.CheckConnectionSFTP(ref strMsg, sftpEnt))
                {
                    rwm.Success = true;
                    rwm.Message = "Online";
                }
                else
                {
                    rwm.Success = false;
                    rwm.Message = strMsg;
                }

            }
            catch (Exception ex)
            {
                rwm.Success = false;
                rwm.Message = ex.Message;
            }
            return rwm;
        }
    }
}