﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SS.CMS.Abstractions;
using SS.CMS.Abstractions.Dto.Request;
using SS.CMS.Abstractions.Dto.Result;
using SS.CMS.Framework;

namespace SS.CMS.Web.Controllers.Admin.Cms.Contents
{
    public partial class ContentsRecycleController
    {
        [HttpPost, Route(RouteColumns)]
        public async Task<ActionResult<BoolResult>> Columns([FromBody] ColumnsRequest request)
        {
            var auth = await _authManager.GetAdminAsync();
            if (!auth.IsAdminLoggin ||
                !await auth.AdminPermissions.HasSitePermissionsAsync(request.SiteId,
                    Constants.SitePermissions.ContentsRecycle))
            {
                return Unauthorized();
            }

            var site = await DataProvider.SiteRepository.GetAsync(request.SiteId);
            if (site == null) return NotFound();

            site.RecycleListColumns = request.AttributeNames;

            await DataProvider.SiteRepository.UpdateAsync(site);

            return new BoolResult
            {
                Value = true
            };
        }

        public class ColumnsRequest : SiteRequest
        {
            public List<string> AttributeNames { get; set; }
        }
    }
}
