﻿using System;
using System.Collections.Generic;
using Datory;
using Newtonsoft.Json;
using SS.CMS.Framework;

namespace SS.CMS.Cli.Updater.Tables
{
    public partial class TableLog
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("userName")]
        public string UserName { get; set; }

        [JsonProperty("ipAddress")]
        public string IpAddress { get; set; }

        [JsonProperty("addDate")]
        public DateTimeOffset AddDate { get; set; }

        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("summary")]
        public string Summary { get; set; }
    }

    public partial class TableLog
    {
        public const string OldTableName = "bairong_Log";

        public static ConvertInfo Converter => new ConvertInfo
        {
            NewTableName = NewTableName,
            NewColumns = NewColumns,
            ConvertKeyDict = ConvertKeyDict,
            ConvertValueDict = ConvertValueDict
        };

        private static readonly string NewTableName = DataProvider.LogRepository.TableName;

        private static readonly List<TableColumn> NewColumns = DataProvider.LogRepository.TableColumns;

        private static readonly Dictionary<string, string> ConvertKeyDict = null;

        private static readonly Dictionary<string, string> ConvertValueDict = null;
    }
}
