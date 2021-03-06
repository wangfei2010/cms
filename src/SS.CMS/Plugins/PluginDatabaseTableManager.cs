﻿using Datory;
using SS.CMS.Framework;
using SS.CMS.Plugins.Impl;

namespace SS.CMS.Plugins
{
    public static class PluginDatabaseTableManager
    {
        public static async void SyncTable(ServiceImpl service)
        {
            if (service.DatabaseTables == null || service.DatabaseTables.Count <= 0) return;

            var db = new Database(WebConfigUtils.DatabaseType, WebConfigUtils.ConnectionString);

            foreach (var tableName in service.DatabaseTables.Keys)
            {
                var tableColumns = service.DatabaseTables[tableName];
                if (tableColumns == null || tableColumns.Count == 0) continue;

                if (!await db.IsTableExistsAsync(tableName))
                {
                    await db.CreateTableAsync(tableName, tableColumns);
                }
                else
                {
                    await db.AlterTableAsync(tableName, tableColumns);
                }
            }
        }
    }
}
