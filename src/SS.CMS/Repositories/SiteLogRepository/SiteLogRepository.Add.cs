﻿using System;
using System.Threading.Tasks;
using SS.CMS.Abstractions;
using SS.CMS;
using SS.CMS.Core;

namespace SS.CMS.Repositories
{
    public partial class SiteLogRepository
    {
        public async Task AddSiteLogAsync(int siteId, int channelId, int contentId, Administrator adminInfo, string action, string summary)
        {
            var config = await _configRepository.GetAsync();
            if (!config.IsLogSite) return;

            if (siteId <= 0)
            {
                await _logRepository.AddAdminLogAsync(adminInfo, action, summary);
            }
            else
            {
                try
                {
                    await DeleteIfThresholdAsync();

                    if (!string.IsNullOrEmpty(action))
                    {
                        action = WebUtils.MaxLengthText(action, 250);
                    }
                    if (!string.IsNullOrEmpty(summary))
                    {
                        summary = WebUtils.MaxLengthText(summary, 250);
                    }
                    if (channelId < 0)
                    {
                        channelId = -channelId;
                    }

                    var siteLogInfo = new SiteLog
                    {
                        Id = 0,
                        SiteId = siteId,
                        ChannelId = channelId,
                        ContentId = contentId,
                        AdminId = adminInfo.Id,
                        IpAddress = string.Empty,
                        AddDate = DateTime.Now,
                        Action = action,
                        Summary = summary
                    };

                    await InsertAsync(siteLogInfo);

                    await _administratorRepository.UpdateLastActivityDateAsync(adminInfo);
                }
                catch (Exception ex)
                {
                    await _errorLogRepository.AddErrorLogAsync(ex);
                }
            }
        }
    }
}
