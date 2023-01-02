using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using GM.CommonLibs.Helper;
using GM.Model.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace GM.Service.ExternalInterface.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class FileServiceController : ControllerBase
    {
        private static string[] DEFUALT_FOLDER = { "logs", "UploadFiles" };
        private readonly IHostingEnvironment _env;

        public FileServiceController(IHostingEnvironment env)
        {
            _env = env;
        }

        [HttpGet]
        [Route("GetHeadList")]
        public ResultWithModel HeadList()
        {
            ResultWithModel rwm = new ResultWithModel();
            List<FileServiceModel> fileModel = new List<FileServiceModel>();

            try
            {
                var contentRoot = _env.ContentRootPath;
                var dirNames = from fullDirname in Directory.EnumerateDirectories(contentRoot)
                    select fullDirname.Substring(fullDirname.LastIndexOf(Path.DirectorySeparatorChar) + 1);

                foreach (var dir in dirNames.Where(a => DEFUALT_FOLDER.Contains(a)))
                {
                    FileServiceModel model = new FileServiceModel();
                    model.key = dir;
                    model.title = dir;
                    model.lazy = true;
                    model.folder = true;
                    fileModel.Add(model);
                }

                rwm.Message = "Success";
                rwm.Success = true;
            }
            catch (Exception ex)
            {
                rwm.Message = ex.Message;
                rwm.Success = false;
            }
            
            DataSet dsResult = new DataSet();
            DataTable dt = fileModel.ToDataTable();
            dt.TableName = "FileServiceResultModel";
            dsResult.Tables.Add(dt);
            rwm.Data = dsResult;
            rwm.RefCode = 0;
            rwm.Serverity = "Low";
            return rwm;
        }

        [HttpGet]
        [Route("GetNodeList")]
        public ResultWithModel NodeList(string path)
        {
            ResultWithModel rwm = new ResultWithModel();
            List<FileServiceModel> fileModel = new List<FileServiceModel>();

            try
            {
                var contentRoot = _env.ContentRootPath;
                var searchPath = Path.Combine(contentRoot, path);

                var dirNames = from fullDirname in Directory.EnumerateDirectories(searchPath)
                    select fullDirname.Substring(fullDirname.LastIndexOf(Path.DirectorySeparatorChar) + 1);

                foreach (var dir in dirNames)
                {
                    FileServiceModel model = new FileServiceModel();
                    model.key = path + "/" + dir;
                    model.title = dir;
                    model.lazy = true;
                    model.folder = true;
                    fileModel.Add(model);
                }

                var filenames = from fullFilename in Directory.EnumerateFiles(searchPath, "*")
                    select Path.GetFileName(fullFilename);

                foreach (string file in filenames)
                {
                    FileServiceModel model = new FileServiceModel();
                    model.key = path + "/" + file;
                    model.title = file;
                    model.lazy = false;
                    model.folder = false;
                    fileModel.Add(model);
                }

                rwm.Message = "Success";
                rwm.Success = true;
            }
            catch (Exception ex)
            {
                rwm.Message = ex.Message;
                rwm.Success = false;
            }

            DataSet dsResult = new DataSet();
            DataTable dt = fileModel.ToDataTable();
            dt.TableName = "FileServiceResultModel";
            dsResult.Tables.Add(dt);
            rwm.Data = dsResult;
            rwm.RefCode = 0;
            rwm.Serverity = "Low";
            return rwm;
        }

        [HttpGet]
        [Route("DownloadFile")]
        public FileStream DownloadFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var contentRoot = _env.ContentRootPath;
            var pathFile = Path.Combine(contentRoot, path);

            if (!System.IO.File.Exists(pathFile))
            {
                return null;
            }

            var fileStream = new FileStream(pathFile, FileMode.Open, FileAccess.Read);
            return fileStream;
        }
    }
}