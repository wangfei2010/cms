﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Datory.Utils;
using Microsoft.AspNetCore.Mvc;
using SS.CMS.Abstractions;
using SS.CMS.Abstractions.Dto.Request;
using SS.CMS.Abstractions.Dto.Result;
using SS.CMS.Framework;
using SS.CMS.Web.Extensions;

namespace SS.CMS.Web.Controllers.Admin.Cms.Settings
{
    [Route("admin/cms/settings/settingsCreateTrigger")]
    public partial class SettingsCreateTriggerController : ControllerBase
    {
        private const string Route = "";

        private readonly IAuthManager _authManager;

        public SettingsCreateTriggerController(IAuthManager authManager)
        {
            _authManager = authManager;
        }

        [HttpGet, Route(Route)]
        public async Task<ActionResult<GetResult>> List([FromQuery] SiteRequest request)
        {
            var auth = await _authManager.GetAdminAsync();
            if (!auth.IsAdminLoggin ||
                !await auth.AdminPermissions.HasSitePermissionsAsync(request.SiteId,
                    Constants.SitePermissions.ConfigCreateRule))
            {
                return Unauthorized();
            }

            var site = await DataProvider.SiteRepository.GetAsync(request.SiteId);
            if (site == null) return this.Error("无法确定内容对应的站点");

            var channel = await DataProvider.ChannelRepository.GetAsync(request.SiteId);
            var cascade = await DataProvider.ChannelRepository.GetCascadeAsync(site, channel, async summary =>
            {
                var count = await DataProvider.ContentRepository.GetCountAsync(site, summary);
                var entity = await DataProvider.ChannelRepository.GetAsync(summary.Id);

                var changeNames = new List<string>();
                var channelIdList = Utilities.GetIntList(entity.CreateChannelIdsIfContentChanged);
                foreach (var channelId in channelIdList)
                {
                    if (await DataProvider.ChannelRepository.IsExistsAsync(channelId))
                    {
                        changeNames.Add(await DataProvider.ChannelRepository.GetChannelNameNavigationAsync(request.SiteId, channelId));
                    }
                }

                return new
                {
                    entity.IndexName,
                    Count = count,
                    ChangeNames = changeNames,
                    entity.IsCreateChannelIfContentChanged,
                    CreateChannelIdsIfContentChanged = channelIdList,
                };
            });

            return new GetResult
            {
                Channel = cascade
            };
        }

        [HttpPut, Route(Route)]
        public async Task<ActionResult<BoolResult>> Submit([FromBody] SubmitRequest request)
        {
            var auth = await _authManager.GetAdminAsync();
            if (!auth.IsAdminLoggin ||
                !await auth.AdminPermissions.HasSitePermissionsAsync(request.SiteId,
                    Constants.SitePermissions.ConfigCreateRule))
            {
                return Unauthorized();
            }

            var site = await DataProvider.SiteRepository.GetAsync(request.SiteId);
            if (site == null) return this.Error("无法确定内容对应的站点");

            var channel = await DataProvider.ChannelRepository.GetAsync(request.ChannelId);

            channel.IsCreateChannelIfContentChanged = request.IsCreateChannelIfContentChanged;
            channel.CreateChannelIdsIfContentChanged = Utilities.ToString(request.CreateChannelIdsIfContentChanged);

            await DataProvider.ChannelRepository.UpdateAsync(channel);

            await auth.AddSiteLogAsync(request.SiteId, request.ChannelId, 0, "设置栏目变动生成页面", $"栏目:{channel.ChannelName}");

            return new BoolResult
            {
                Value = true
            };
        }
    }
}