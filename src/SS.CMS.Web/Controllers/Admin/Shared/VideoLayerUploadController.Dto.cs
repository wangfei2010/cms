﻿using Microsoft.AspNetCore.Http;
using SS.CMS.Abstractions.Dto.Request;

namespace SS.CMS.Web.Controllers.Admin.Shared
{
    public partial class VideoLayerUploadController
    {
        public class UploadRequest : SiteRequest
        {
            public IFormFile File { set; get; }
        }

        public class UploadResult
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public string Url { get; set; }
        }
    }
}
