﻿using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SS.CMS.Abstractions;
using SS.CMS.Abstractions.Dto.Result;
using SS.CMS.Core;
using SS.CMS.Core.Office;
using SS.CMS.Framework;
using SS.CMS.Web.Extensions;

namespace SS.CMS.Web.Controllers.Admin.Cms.Library
{
    [Route("admin/cms/library/libraryLayerWord")]
    public partial class LibraryLayerWordController : ControllerBase
    {
        private const string Route = "";
        private const string RouteUpload = "actions/upload";

        private readonly IAuthManager _authManager;

        public LibraryLayerWordController(IAuthManager authManager)
        {
            _authManager = authManager;
        }

        [HttpPost, Route(RouteUpload)]
        public async Task<ActionResult<UploadResult>> Upload([FromBody]UploadRequest request)
        {
            var auth = await _authManager.GetAdminAsync();
            if (!auth.IsAdminLoggin ||
                !await auth.AdminPermissions.HasSitePermissionsAsync(request.SiteId,
                    Constants.SitePermissions.Library))
            {
                return Unauthorized();
            }

            var site = await DataProvider.SiteRepository.GetAsync(request.SiteId);

            if (request.File == null)
            {
                return this.Error("请选择有效的文件上传");
            }

            var fileName = Path.GetFileName(request.File.FileName);

            var sExt = PathUtils.GetExtension(fileName);
            if (!StringUtils.EqualsIgnoreCase(sExt, ".doc") && !StringUtils.EqualsIgnoreCase(sExt, ".docx") && !StringUtils.EqualsIgnoreCase(sExt, ".wps"))
            {
                return this.Error("文件只能是 Word 格式，请选择有效的文件上传!");
            }

            var filePath = PathUtility.GetTemporaryFilesPath(fileName);
            DirectoryUtils.CreateDirectoryIfNotExists(filePath);
            request.File.CopyTo(new FileStream(filePath, FileMode.Create));

            var url = await PageUtility.GetSiteUrlByPhysicalPathAsync(site, filePath, true);

            return new UploadResult
            {
                Name = fileName,
                Url = url
            };
        }

        [HttpPost, Route(Route)]
        public async Task<ActionResult<StringResult>> Submit([FromBody] SubmitRequest request)
        {
            var auth = await _authManager.GetAdminAsync();

            if (!auth.IsAdminLoggin ||
                !await auth.AdminPermissions.HasSitePermissionsAsync(request.SiteId,
                    Constants.SitePermissions.Library))
            {
                return Unauthorized();
            }

            var site = await DataProvider.SiteRepository.GetAsync(request.SiteId);
            if (site == null) return this.Error("无法确定内容对应的站点");

            var builder = new StringBuilder();
            foreach (var fileName in request.FileNames)
            {
                if (string.IsNullOrEmpty(fileName)) continue;

                var filePath = PathUtility.GetTemporaryFilesPath(fileName);
                var (_, wordContent) = await WordManager.GetWordAsync(site, false, request.IsClearFormat, request.IsFirstLineIndent, request.IsClearFontSize, request.IsClearFontFamily, request.IsClearImages, filePath);
                wordContent = await ContentUtility.TextEditorContentDecodeAsync(site, wordContent, true);
                builder.Append(wordContent);
                FileUtils.DeleteFileIfExists(filePath);
            }

            return new StringResult
            {
                Value = builder.ToString()
            };
        }
    }
}
