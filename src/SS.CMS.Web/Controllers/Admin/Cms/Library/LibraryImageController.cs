﻿using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SS.CMS.Abstractions;
using SS.CMS.Abstractions.Dto.Result;
using SS.CMS.Core;
using SS.CMS.Framework;
using SS.CMS.Web.Extensions;

namespace SS.CMS.Web.Controllers.Admin.Cms.Library
{

    [Route("admin/cms/library/libraryImage")]
    public partial class LibraryImageController : ControllerBase
    {
        private const string Route = "";
        private const string RouteId = "{id}";
        private const string RouteList = "list";
        private const string RouteGroups = "groups";
        private const string RouteGroupId = "groups/{id}";

        private readonly IAuthManager _authManager;

        public LibraryImageController(IAuthManager authManager)
        {
            _authManager = authManager;
        }

        [HttpPost, Route(RouteList)]
        public async Task<ActionResult<QueryResult>> List([FromBody]QueryRequest req)
        {
            var auth = await _authManager.GetAdminAsync();

            if (!auth.IsAdminLoggin ||
                !await auth.AdminPermissions.HasSitePermissionsAsync(req.SiteId,
                    Constants.SitePermissions.Library))
            {
                return Unauthorized();
            }

            var groups = await DataProvider.LibraryGroupRepository.GetAllAsync(LibraryType.Image);
            groups.Insert(0, new LibraryGroup
            {
                Id = 0,
                GroupName = "全部图片"
            });
            var count = await DataProvider.LibraryImageRepository.GetCountAsync(req.GroupId, req.Keyword);
            var items = await DataProvider.LibraryImageRepository.GetAllAsync(req.GroupId, req.Keyword, req.Page, req.PerPage);

            return new QueryResult
            {
                Groups = groups,
                Count = count,
                Items = items
            };
        }

        [HttpPost, Route(Route)]
        public async Task<ActionResult<LibraryImage>> Create([FromBody]CreateRequest request)
        {
            var auth = await _authManager.GetAdminAsync();

            if (!auth.IsAdminLoggin ||
                !await auth.AdminPermissions.HasSitePermissionsAsync(request.SiteId,
                    Constants.SitePermissions.Library))
            {
                return Unauthorized();
            }

            var library = new LibraryImage
            {
                GroupId = request.GroupId
            };

            if (request.File == null)
            {
                return this.Error("请选择有效的文件上传");
            }

            var fileName = Path.GetFileName(request.File.FileName);

            if (!PathUtils.IsExtension(PathUtils.GetExtension(fileName), ".jpg", ".jpeg", ".bmp", ".gif", ".png", ".svg", ".webp"))
            {
                return this.Error("文件只能是图片格式，请选择有效的文件上传!");
            }

            var libraryFileName = PathUtils.GetLibraryFileName(fileName);
            var virtualDirectoryPath = PathUtils.GetLibraryVirtualDirectoryPath(UploadType.Image);
            
            var directoryPath = PathUtils.Combine(WebConfigUtils.PhysicalApplicationPath, virtualDirectoryPath);
            var filePath = PathUtils.Combine(directoryPath, libraryFileName);

            DirectoryUtils.CreateDirectoryIfNotExists(filePath);
            request.File.CopyTo(new FileStream(filePath, FileMode.Create));

            library.Title = fileName;
            library.Url = PageUtils.Combine(virtualDirectoryPath, libraryFileName);

            library.Id = await DataProvider.LibraryImageRepository.InsertAsync(library);

            return library;
        }

        [HttpPut, Route(RouteId)]
        public async Task<ActionResult<LibraryImage>> Update([FromBody] UpdateRequest request)
        {
            var auth = await _authManager.GetAdminAsync();

            if (!auth.IsAdminLoggin ||
                !await auth.AdminPermissions.HasSitePermissionsAsync(request.SiteId,
                    Constants.SitePermissions.Library))
            {
                return Unauthorized();
            }

            var lib = await DataProvider.LibraryImageRepository.GetAsync(request.Id);
            lib.Title = request.Title;
            lib.GroupId = request.GroupId;
            await DataProvider.LibraryImageRepository.UpdateAsync(lib);

            return lib;
        }

        [HttpDelete, Route(RouteId)]
        public async Task<ActionResult<BoolResult>> Delete([FromBody]DeleteRequest request)
        {
            var auth = await _authManager.GetAdminAsync();

            if (!auth.IsAdminLoggin ||
                !await auth.AdminPermissions.HasSitePermissionsAsync(request.SiteId,
                    Constants.SitePermissions.Library))
            {
                return Unauthorized();
            }

            await DataProvider.LibraryImageRepository.DeleteAsync(request.Id);

            return new BoolResult
            {
                Value = true
            };
        }

        [HttpPost, Route(RouteGroups)]
        public async Task<ActionResult<LibraryGroup>> CreateGroup([FromBody] GroupRequest group)
        {
            var auth = await _authManager.GetAdminAsync();

            if (!auth.IsAdminLoggin ||
                !await auth.AdminPermissions.HasSitePermissionsAsync(group.SiteId,
                    Constants.SitePermissions.Library))
            {
                return Unauthorized();
            }

            var libraryGroup = new LibraryGroup
            {
                Type = LibraryType.Image,
                GroupName = group.Name
            };
            libraryGroup.Id = await DataProvider.LibraryGroupRepository.InsertAsync(libraryGroup);

            return libraryGroup;
        }

        [HttpPut, Route(RouteGroupId)]
        public async Task<ActionResult<LibraryGroup>> RenameGroup([FromQuery]int id, [FromBody] GroupRequest group)
        {
            var auth = await _authManager.GetAdminAsync();

            if (!auth.IsAdminLoggin ||
                !await auth.AdminPermissions.HasSitePermissionsAsync(group.SiteId,
                    Constants.SitePermissions.Library))
            {
                return Unauthorized();
            }

            var libraryGroup = await DataProvider.LibraryGroupRepository.GetAsync(id);
            libraryGroup.GroupName = group.Name;
            await DataProvider.LibraryGroupRepository.UpdateAsync(libraryGroup);

            return libraryGroup;
        }

        [HttpDelete, Route(RouteGroupId)]
        public async Task<ActionResult<BoolResult>> DeleteGroup([FromBody]DeleteRequest request)
        {
            var auth = await _authManager.GetAdminAsync();

            if (!auth.IsAdminLoggin ||
                !await auth.AdminPermissions.HasSitePermissionsAsync(request.SiteId,
                    Constants.SitePermissions.Library))
            {
                return Unauthorized();
            }

            await DataProvider.LibraryGroupRepository.DeleteAsync(LibraryType.Image, request.Id);

            return new BoolResult
            {
                Value = true
            };
        }
    }
}
