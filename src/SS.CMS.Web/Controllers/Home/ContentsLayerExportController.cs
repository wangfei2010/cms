﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SS.CMS.Abstractions;
using SS.CMS.Abstractions.Dto.Request;
using SS.CMS.Core;
using SS.CMS.Core.Office;
using SS.CMS.Framework;
using SS.CMS.Plugins;
using SS.CMS.Core.Serialization;

namespace SS.CMS.Web.Controllers.Home
{
    [Route("home/contentsLayerExport")]
    public partial class ContentsLayerExportController : ControllerBase
    {
        private const string Route = "";

        private readonly IAuthManager _authManager;

        public ContentsLayerExportController(IAuthManager authManager)
        {
            _authManager = authManager;
        }

        [HttpGet, Route(Route)]
        public async Task<ActionResult<GetResult>> Get([FromQuery] ChannelRequest request)
        {
            var auth = await _authManager.GetUserAsync();
            if (!auth.IsUserLoggin ||
                !await auth.UserPermissions.HasChannelPermissionsAsync(request.SiteId, request.ChannelId, Constants.ChannelPermissions.ContentView))
            {
                return Unauthorized();
            }

            var site = await DataProvider.SiteRepository.GetAsync(request.SiteId);
            if (site == null) return NotFound();

            var channel = await DataProvider.ChannelRepository.GetAsync(request.ChannelId);
            if (channel == null) return NotFound();

            var columns = await ColumnsManager.GetContentListColumnsAsync(site, channel, ColumnsManager.PageType.Contents);

            var (isChecked, checkedLevel) = await CheckManager.GetUserCheckLevelAsync(auth.AdminPermissions, site, request.SiteId);
            var checkedLevels = CheckManager.GetCheckedLevels(site, isChecked, checkedLevel, true);

            return new GetResult
            {
                Columns = columns,
                CheckedLevels = checkedLevels,
                CheckedLevel = checkedLevel
            };
        }

        [HttpPost, Route(Route)]
        public async Task<ActionResult<SubmitResult>> Submit([FromBody] SubmitRequest request)
        {
            var auth = await _authManager.GetUserAsync();

            var downloadUrl = string.Empty;

            if (!auth.IsUserLoggin ||
                !await auth.UserPermissions.HasChannelPermissionsAsync(request.SiteId, request.ChannelId, Constants.ChannelPermissions.ChannelEdit))
            {
                return Unauthorized();
            }

            var site = await DataProvider.SiteRepository.GetAsync(request.SiteId);
            if (site == null) return NotFound();

            var channel = await DataProvider.ChannelRepository.GetAsync(request.ChannelId);
            if (channel == null) return NotFound();

            var columns = await ColumnsManager.GetContentListColumnsAsync(site, channel, ColumnsManager.PageType.Contents);
            var pluginIds = PluginContentManager.GetContentPluginIds(channel);
            var pluginColumns = await PluginContentManager.GetContentColumnsAsync(pluginIds);

            var contentInfoList = new List<Content>();
            var ccIds = await DataProvider.ContentRepository.GetSummariesAsync(site, channel, true);
            var count = ccIds.Count();

            var pages = Convert.ToInt32(Math.Ceiling((double)count / site.PageSize));
            if (pages == 0) pages = 1;

            if (count > 0)
            {
                for (var page = 1; page <= pages; page++)
                {
                    var offset = site.PageSize * (page - 1);
                    var limit = site.PageSize;
                    var pageCcIds = ccIds.Skip(offset).Take(limit).ToList();

                    var sequence = offset + 1;

                    foreach (var channelContentId in pageCcIds)
                    {
                        var contentInfo = await DataProvider.ContentRepository.GetAsync(site, channelContentId.ChannelId, channelContentId.Id);
                        if (contentInfo == null) continue;

                        if (!request.IsAllCheckedLevel)
                        {
                            var checkedLevel = contentInfo.CheckedLevel;
                            if (contentInfo.Checked)
                            {
                                checkedLevel = site.CheckContentLevel;
                            }
                            if (!request.CheckedLevelKeys.Contains(checkedLevel))
                            {
                                continue;
                            }
                        }

                        if (!request.IsAllDate)
                        {
                            if (contentInfo.AddDate < request.StartDate || contentInfo.AddDate > request.EndDate)
                            {
                                continue;
                            }
                        }

                        contentInfoList.Add(await ColumnsManager.CalculateContentListAsync(sequence++, site, request.ChannelId, contentInfo, columns, pluginColumns));
                    }
                }

                if (contentInfoList.Count > 0)
                {
                    if (request.ExportType == "zip")
                    {
                        var fileName = $"{channel.ChannelName}.zip";
                        var filePath = PathUtility.GetTemporaryFilesPath(fileName);
                        var exportObject = new ExportObject(site, auth.AdminId);
                        contentInfoList.Reverse();
                        if (exportObject.ExportContents(filePath, contentInfoList))
                        {
                            downloadUrl = PageUtils.GetTemporaryFilesUrl(fileName);
                        }
                    }
                    else if (request.ExportType == "excel")
                    {
                        var fileName = $"{channel.ChannelName}.csv";
                        var filePath = PathUtility.GetTemporaryFilesPath(fileName);
                        await ExcelObject.CreateExcelFileForContentsAsync(filePath, site, channel, contentInfoList, request.ColumnNames);
                        downloadUrl = PageUtils.GetTemporaryFilesUrl(fileName);
                    }
                }
            }

            return new SubmitResult
            {
                Value = downloadUrl,
                IsSuccess = !string.IsNullOrEmpty(downloadUrl)
            };
        }
    }
}
