﻿using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Http;
using Datory;
using Datory.Utils;
using SiteServer.Abstractions;
using SiteServer.Abstractions.Dto;
using SiteServer.API.Context;
using SiteServer.CMS.Core;
using SiteServer.CMS.Framework;
using SiteServer.CMS.Repositories;

namespace SiteServer.API.Controllers.Pages.Cms.Channels
{
    public partial class PagesChannelsController
    {
        [HttpGet, Route(RouteGet)]
        public async Task<ChannelResult> Get(int siteId, int channelId)
        {
            var auth = await AuthenticatedRequest.GetAuthAsync();
            if (!auth.IsAdminLoggin ||
                !await auth.AdminPermissionsImpl.HasSitePermissionsAsync(siteId,
                    Constants.SitePermissions.Channels))
            {
                return Request.Unauthorized<ChannelResult>();
            }

            var site = await DataProvider.SiteRepository.GetAsync(siteId);
            if (site == null) return Request.NotFound<ChannelResult>();

            var channel = await DataProvider.ChannelRepository.GetAsync(channelId);

            var linkTypes = PageUtility.GetLinkTypeSelects();
            var taxisTypes = new List<Select<string>>
            {
                new Select<string>(TaxisType.OrderByTaxisDesc),
                new Select<string>(TaxisType.OrderByTaxis),
                new Select<string>(TaxisType.OrderByAddDateDesc),
                new Select<string>(TaxisType.OrderByAddDate)
            };

            var styles = new List<Style>();
            foreach (var style in await DataProvider.TableStyleRepository.GetChannelStyleListAsync(channel))
            {
                styles.Add(new Style
                {
                    Id = style.Id,
                    AttributeName = style.AttributeName,
                    DisplayName = style.DisplayName,
                    InputType = style.InputType.GetValue(),
                    Rules = TranslateUtils.JsonDeserialize<IEnumerable<TableStyleRule>>(style.RuleValues),
                    Items = style.Items
                });

                if (style.InputType == InputType.Image ||
                    style.InputType == InputType.Video ||
                    style.InputType == InputType.File)
                {
                    site.Set(EditorManager.GetCountName(style), site.Get(EditorManager.GetCountName(style), 0));
                }
                else if (style.InputType == InputType.CheckBox ||
                         style.InputType == InputType.SelectMultiple)
                {
                    var list = Utilities.GetStringList(site.Get(style.AttributeName,
                        string.Empty));
                    site.Set(style.AttributeName, list);
                }
            }

            return new ChannelResult
            {
                Channel = channel,
                LinkTypes = linkTypes,
                TaxisTypes = taxisTypes,
                Styles = styles
            };
        }

        [HttpPut, Route(Route)]
        public async Task<List<int>> Edit([FromBody] PutRequest request)
        {
            var auth = await AuthenticatedRequest.GetAuthAsync();
            if (!auth.IsAdminLoggin ||
                !await auth.AdminPermissionsImpl.HasChannelPermissionsAsync(request.SiteId, request.Id, Constants.ChannelPermissions.ChannelEdit))
            {
                return Request.Unauthorized<List<int>>();
            }

            var site = await DataProvider.SiteRepository.GetAsync(request.SiteId);
            if (site == null) return Request.NotFound<List<int>>();

            if (string.IsNullOrEmpty(request.ChannelName))
            {
                return Request.BadRequest<List<int>>("栏目修改失败，必须填写栏目名称！");
            }

            var channel = await DataProvider.ChannelRepository.GetAsync(request.Id);
            if (!channel.IndexName.Equals(request.IndexName) && !string.IsNullOrEmpty(request.IndexName))
            {
                if (await DataProvider.ChannelRepository.IsIndexNameExistsAsync(request.SiteId, request.IndexName))
                {
                    return Request.BadRequest<List<int>>("栏目修改失败，栏目索引已存在！");
                }
            }

            if (!channel.FilePath.Equals(request.FilePath) && !string.IsNullOrEmpty(request.FilePath))
            {
                if (!DirectoryUtils.IsDirectoryNameCompliant(request.FilePath))
                {
                    return Request.BadRequest<List<int>>("栏目页面路径不符合系统要求！");
                }

                if (PathUtils.IsDirectoryPath(request.FilePath))
                {
                    request.FilePath = PageUtils.Combine(request.FilePath, "index.html");
                }

                if (await DataProvider.ChannelRepository.IsFilePathExistsAsync(request.SiteId, request.FilePath))
                {
                    return Request.BadRequest<List<int>>("栏目修改失败，栏目页面路径已存在！");
                }
            }

            if (!string.IsNullOrEmpty(request.ChannelFilePathRule))
            {
                var filePathRule = request.ChannelFilePathRule.Replace("|", string.Empty);
                if (!DirectoryUtils.IsDirectoryNameCompliant(filePathRule))
                {
                    return Request.BadRequest<List<int>>("栏目页面命名规则不符合系统要求！");
                }
                if (PathUtils.IsDirectoryPath(filePathRule))
                {
                    return Request.BadRequest<List<int>>("栏目页面命名规则必须包含生成文件的后缀！");
                }
            }

            if (!string.IsNullOrEmpty(request.ContentFilePathRule))
            {
                var filePathRule = request.ContentFilePathRule.Replace("|", string.Empty);
                if (!DirectoryUtils.IsDirectoryNameCompliant(filePathRule))
                {
                    return Request.BadRequest<List<int>>("内容页面命名规则不符合系统要求！");
                }
                if (PathUtils.IsDirectoryPath(filePathRule))
                {
                    return Request.BadRequest<List<int>>("内容页面命名规则必须包含生成文件的后缀！");
                }
            }

            var styles = await DataProvider.TableStyleRepository.GetChannelStyleListAsync(channel);
            foreach (var style in styles)
            {
                var value = request.Get(style.AttributeName, string.Empty);
                var inputType = style.InputType;
                if (inputType == InputType.TextEditor)
                {
                    value = await ContentUtility.TextEditorContentEncodeAsync(site, value);
                    value = UEditorUtils.TranslateToStlElement(value);
                }
                else if (inputType == InputType.Image ||
                         inputType == InputType.Video ||
                         inputType == InputType.File)
                {
                    var count = request.Get(EditorManager.GetCountName(style), 0);
                    channel.Set(EditorManager.GetCountName(style), count);
                    for (var n = 1; n <= count; n++)
                    {
                        channel.Set(EditorManager.GetExtendName(style, n), request.Get(EditorManager.GetExtendName(style, n), string.Empty));
                    }
                }

                if (inputType == InputType.CheckBox ||
                    style.InputType == InputType.SelectMultiple)
                {
                    var list = request.Get<IEnumerable<object>>(style.AttributeName);
                    channel.Set(style.AttributeName, Utilities.ToString(list));
                }
                else
                {
                    channel.Set(style.AttributeName, value);
                }

                if (inputType == InputType.Image || inputType == InputType.File || inputType == InputType.Video)
                {
                    var attributeName = ContentAttribute.GetExtendAttributeName(style.AttributeName);
                    channel.Set(attributeName, request.Get(attributeName, string.Empty));
                }
            }

            channel.ChannelName = request.ChannelName;
            channel.IndexName = request.IndexName;
            channel.GroupNames = request.GroupNames;
            channel.ImageUrl = request.ImageUrl;
            channel.Content = request.Content;
            channel.ChannelTemplateId = request.ChannelTemplateId;
            channel.ContentTemplateId = request.ContentTemplateId;
            channel.ContentModelPluginId = request.ContentModelPluginId;
            channel.ContentRelatedPluginIds = request.ContentRelatedPluginIdList;
            channel.LinkUrl = request.LinkUrl;
            channel.LinkType = request.LinkType;
            channel.DefaultTaxisType = request.DefaultTaxisType;
            channel.FilePath = request.FilePath;
            channel.ChannelFilePathRule = request.ChannelFilePathRule;
            channel.ContentFilePathRule = request.ContentFilePathRule;
            channel.Keywords = request.Keywords;
            channel.Description = request.Description;

            await DataProvider.ChannelRepository.UpdateAsync(channel);

            var expendedChannelIds = new List<int>
            {
                request.SiteId
            };
            if (!expendedChannelIds.Contains(channel.ParentId))
            {
                expendedChannelIds.Add(channel.ParentId);
            }

            return expendedChannelIds;
        }
    }
}