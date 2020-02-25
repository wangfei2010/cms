﻿using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SS.CMS.Abstractions;
using SS.CMS.Api.Preview;
using SS.CMS.Framework;
using SS.CMS.Web.Extensions;

namespace SS.CMS.Web.Controllers.Admin.Cms.Editor
{
    public partial class EditorController
    {
        [HttpPost, Route(RoutePreview)]
        public async Task<ActionResult<PreviewResult>> Preview([FromBody] PreviewRequest request)
        {
            var auth = await _authManager.GetAdminAsync();
            if (!auth.IsAdminLoggin ||
                !await auth.AdminPermissions.HasSitePermissionsAsync(request.SiteId,
                    Constants.SitePermissions.Contents) ||
                !await auth.AdminPermissions.HasChannelPermissionsAsync(request.SiteId, request.ChannelId,
                    Constants.ChannelPermissions.ContentAdd))
            {
                return Unauthorized();
            }

            var site = await DataProvider.SiteRepository.GetAsync(request.SiteId);
            var channel = await DataProvider.ChannelRepository.GetAsync(request.ChannelId);

            if (site == null)
            {
                return this.Error("指定的站点不存在");
            }
            if (channel == null)
            {
                return this.Error("指定的栏目不存在");
            }

            //var styleList = await TableStyleManager.GetContentStyleListAsync(site, channel);

            //var dict = BackgroundInputTypeParser.SaveAttributesAsync(site, styleList, request.Content, ContentAttribute.AllAttributes.Value);
            //var contentInfo = new Content(dict)
            //{
            //    ChannelId = channelId,
            //    SiteId = siteId,
            //    AddUserName = AuthRequest.AdminName,
            //    LastEditUserName = AuthRequest.AdminName,
            //    LastEditDate = DateTime.Now
            //};

            var content = request.Content;
            content.SiteId = site.Id;
            content.ChannelId = channel.Id;
            content.AdminId = auth.AdminId;
            content.Checked = true;

            content.Id = await DataProvider.ContentRepository.InsertPreviewAsync(site, channel, content);

            return new PreviewResult()
            {
                Url = ApiRoutePreview.GetContentPreviewUrl(request.SiteId, request.ChannelId, request.ContentId, content.Id)
            };

            ////contentInfo.GroupNameCollection = ControlUtils.SelectedItemsValueToStringCollection(CblContentGroups.Items);
            //var tagCollection = ContentTagUtils.ParseTagsString(form["TbTags"]);

            //contentInfo.Title = form["TbTitle"];
            //var formatString = TranslateUtils.ToBool(form[ContentAttribute.Title + "_formatStrong"]);
            //var formatEm = TranslateUtils.ToBool(form[ContentAttribute.Title + "_formatEM"]);
            //var formatU = TranslateUtils.ToBool(form[ContentAttribute.Title + "_formatU"]);
            //var formatColor = form[ContentAttribute.Title + "_formatColor"];
            //var theFormatString = ContentUtility.GetTitleFormatString(formatString, formatEm, formatU, formatColor);
            //contentInfo.Set(ContentAttribute.GetFormatStringAttributeName(ContentAttribute.Title), theFormatString);
            ////foreach (ListItem listItem in CblContentAttributes.Items)
            ////{
            ////    var value = listItem.Selected.ToString();
            ////    var attributeName = listItem.Value;
            ////    contentInfo.Set(attributeName, value);
            ////}
            ////contentInfo.LinkUrl = TbLinkUrl.Text;
            //contentInfo.AddDate = TranslateUtils.ToDateTime(form["TbAddDate"]);
            //contentInfo.Checked = false;
            //contentInfo.Tags = TranslateUtils.ObjectCollectionToString(tagCollection, " ");

            //foreach (var service in PluginManager.GetServicesAsync())
            //{
            //    try
            //    {
            //        service.OnContentFormSubmit(new ContentFormSubmitEventArgs(siteId, channelId, contentInfo.Id, TranslateUtils.ToDictionary(form), contentInfo));
            //    }
            //    catch (Exception ex)
            //    {
            //        LogUtils.AddErrorLogAsync(service.PluginId, ex, nameof(IService.ContentFormSubmit));
            //    }
            //}

            //contentInfo.Id = DataProvider.ContentRepository.InsertPreviewAsync(site, channelInfo, contentInfo);

            //return new
            //{
            //    previewUrl = ApiRoutePreview.GetContentPreviewUrl(siteId, channelId, contentId, contentInfo.Id)
            //};
        }

        public class PreviewRequest
        {
            public int SiteId { get; set; }
            public int ChannelId { get; set; }
            public int ContentId { get; set; }
            public Content Content { get; set; }
        }

        public class PreviewResult
        {
            public string Url { get; set; }
        }
    }
}