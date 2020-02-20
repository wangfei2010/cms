﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using SiteServer.Abstractions;
using SiteServer.Abstractions.Dto.Result;
using SiteServer.API.Context;
using SiteServer.CMS.Core;
using SiteServer.CMS.Framework;

namespace SiteServer.API.Controllers.Pages.Cms.Contents
{
    [RoutePrefix("pages/cms/contents/contentsLayerDelete")]
    public partial class PagesContentsLayerDeleteController : ApiController
    {
        private const string Route = "";

        private readonly ICreateManager _createManager;

        public PagesContentsLayerDeleteController(ICreateManager createManager)
        {
            _createManager = createManager;
        }

        [HttpGet, Route(Route)]
        public async Task<GetResult> Get([FromUri] GetRequest request)
        {
            var auth = await AuthenticatedRequest.GetAuthAsync();
            if (!auth.IsAdminLoggin ||
                !await auth.AdminPermissionsImpl.HasSitePermissionsAsync(request.SiteId,
                    Constants.SitePermissions.Contents) ||
                !await auth.AdminPermissionsImpl.HasChannelPermissionsAsync(request.SiteId, request.ChannelId, Constants.ChannelPermissions.ContentDelete))
            {
                return Request.Unauthorized<GetResult>();
            }

            var site = await DataProvider.SiteRepository.GetAsync(request.SiteId);
            if (site == null) return Request.NotFound<GetResult>();

            var summaries = ContentUtility.ParseSummaries(request.ChannelContentIds);

            var contents = new List<Content>();
            foreach (var summary in summaries)
            {
                var channel = await DataProvider.ChannelRepository.GetAsync(summary.ChannelId);
                var content = await DataProvider.ContentRepository.GetAsync(site, channel, summary.Id);
                if (content == null) continue;

                var pageContent = new Content(content.ToDictionary());
                pageContent.Set(ContentAttribute.CheckState, CheckManager.GetCheckState(site, content));
                contents.Add(pageContent);
            }

            return new GetResult
            {
                Contents = contents
            };
        }

        [HttpPost, Route(Route)]
        public async Task<BoolResult> Submit([FromBody] SubmitRequest request)
        {
            var auth = await AuthenticatedRequest.GetAuthAsync();
            if (!auth.IsAdminLoggin ||
                !await auth.AdminPermissionsImpl.HasSitePermissionsAsync(request.SiteId,
                    Constants.SitePermissions.Contents) ||
                !await auth.AdminPermissionsImpl.HasChannelPermissionsAsync(request.SiteId, request.ChannelId, Constants.ChannelPermissions.ContentDelete))
            {
                return Request.Unauthorized<BoolResult>();
            }

            var site = await DataProvider.SiteRepository.GetAsync(request.SiteId);
            if (site == null) return Request.NotFound<BoolResult>();

            var summaries = ContentUtility.ParseSummaries(request.ChannelContentIds);

            if (!request.IsRetainFiles)
            {
                foreach (var summary in summaries)
                {
                    await _createManager.DeleteContentAsync(site, summary.ChannelId, summary.Id);
                }
            }

            if (summaries.Count == 1)
            {
                var summary = summaries[0];

                var content = await DataProvider.ContentRepository.GetAsync(site, summary.ChannelId, summary.Id);
                if (content != null)
                {
                    await auth.AddSiteLogAsync(request.SiteId, summary.ChannelId, summary.Id, "删除内容",
                        $"栏目:{await DataProvider.ChannelRepository.GetChannelNameNavigationAsync(request.SiteId, summary.ChannelId)},内容标题:{content.Title}");
                }
            }
            else
            {
                await auth.AddSiteLogAsync(request.SiteId, "批量删除内容",
                    $"栏目:{await DataProvider.ChannelRepository.GetChannelNameNavigationAsync(request.SiteId, request.ChannelId)},内容条数:{summaries.Count}");
            }

            foreach (var distinctChannelId in summaries.Select(x => x.ChannelId).Distinct())
            {
                var distinctChannel = await DataProvider.ChannelRepository.GetAsync(distinctChannelId);
                var contentIdList = summaries.Where(x => x.ChannelId == distinctChannelId)
                    .Select(x => x.Id).ToList();
                await DataProvider.ContentRepository.RecycleContentsAsync(site, distinctChannel, contentIdList, auth.AdminId);

                await _createManager.TriggerContentChangedEventAsync(request.SiteId, distinctChannelId);
            }

            return new BoolResult
            {
                Value = true
            };
        }
    }
}