﻿using System.Collections.Generic;
using SS.CMS.Abstractions;
using SS.CMS.Abstractions.Dto.Request;

namespace SS.CMS.Web.Controllers.Home
{
    public partial class ContentsLayerCutController
    {
        public class GetRequest : ChannelRequest
        {
            public List<int> ContentIds { get; set; }
        }

        public class GetResult
        {
            public List<IDictionary<string, object>> Value { get; set; }
            public List<object> Sites { get; set; }
            public List<object> Channels { get; set; }
            public Site Site { get; set; }
        }

        public class GetChannelsResult
        {
            public List<object> Channels { get; set; }
        }

        public class SubmitRequest : ChannelRequest
        {
            public List<int> ContentIds { get; set; }
            public int TargetSiteId { get; set; }
            public int TargetChannelId { get; set; }
        }
    }
}
