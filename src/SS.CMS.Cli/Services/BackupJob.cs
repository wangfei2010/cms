﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datory;
using Datory.Utils;
using Mono.Options;
using SS.CMS.Abstractions;
using SS.CMS.Cli.Core;
using SS.CMS.Framework;
using Microsoft.Extensions.DependencyInjection;

namespace SS.CMS.Cli.Services
{
    public class BackupJob
    {
        public const string CommandName = "backup";

        public static async Task Execute(IJobContext context)
        {
            var application = CliUtils.Provider.GetService<BackupJob>();
            await application.RunAsync(context);
        }

        private string _directory;
        private string _configFile;
        private List<string> _includes;
        private List<string> _excludes;
        private int _maxRows;
        private bool _isHelp;

        private readonly OptionSet _options;

        public BackupJob()
        {
            _options = new OptionSet() {
                { "d|directory=", "指定保存备份文件的文件夹名称",
                    v => _directory = v },
                { "c|config-file=", "指定配置文件Web.config路径或文件名",
                    v => _configFile = v },
                { "includes=", "指定需要备份的表，多个表用英文逗号隔开，默认备份所有表",
                    v => _includes = v == null ? null : Utilities.GetStringList(v) },
                { "excludes=", "指定需要排除的表，多个表用英文逗号隔开",
                    v => _excludes = v == null ? null : Utilities.GetStringList(v) },
                { "max-rows=", "指定需要备份的表的最大行数",
                    v => _maxRows = v == null ? 0 : TranslateUtils.ToInt(v) },
                { "h|help",  "命令说明",
                    v => _isHelp = v != null }
            };
        }

        public static void PrintUsage()
        {
            Console.WriteLine("数据库备份: siteserver backup");
            var job = new BackupJob();
            job._options.WriteOptionDescriptions(Console.Out);
            Console.WriteLine();
        }

        public async Task RunAsync(IJobContext context)
        {
            if (!CliUtils.ParseArgs(_options, context.Args)) return;

            if (_isHelp)
            {
                PrintUsage();
                return;
            }

            var directory = _directory;
            if (string.IsNullOrEmpty(directory))
            {
                directory = $"backup/{DateTime.Now:yyyy-MM-dd}";
            }

            var treeInfo = new TreeInfo(directory);
            DirectoryUtils.CreateDirectoryIfNotExists(treeInfo.DirectoryPath);

            var webConfigPath = CliUtils.GetWebConfigPath(_configFile);
            if (!FileUtils.IsFileExists(webConfigPath))
            {
                await CliUtils.PrintErrorAsync($"系统配置文件不存在：{webConfigPath}！");
                return;
            }

            WebConfigUtils.Load(CliUtils.PhysicalApplicationPath, webConfigPath);

            if (string.IsNullOrEmpty(WebConfigUtils.ConnectionString))
            {
                await CliUtils.PrintErrorAsync($"{webConfigPath} 中数据库连接字符串 connectionString 未设置");
                return;
            }

            await Console.Out.WriteLineAsync($"数据库类型: {WebConfigUtils.DatabaseType.GetValue()}");
            await Console.Out.WriteLineAsync($"连接字符串: {WebConfigUtils.ConnectionString}");
            await Console.Out.WriteLineAsync($"备份文件夹: {treeInfo.DirectoryPath}");

            var (isConnectionWorks, errorMessage) = await WebConfigUtils.Database.IsConnectionWorksAsync();
            if (!isConnectionWorks)
            {
                await CliUtils.PrintErrorAsync($"数据库连接错误：{errorMessage}");
                return;
            }

            if (_excludes == null)
            {
                _excludes = new List<string>();
            }
            _excludes.Add("bairong_Log");
            _excludes.Add("bairong_ErrorLog");
            _excludes.Add("siteserver_ErrorLog");
            _excludes.Add("siteserver_Log");
            _excludes.Add("siteserver_Tracking");

            await Backup(_includes, _excludes, _maxRows, treeInfo);

            await CliUtils.PrintRowLineAsync();
            await Console.Out.WriteLineAsync($"恭喜，成功备份数据库至文件夹：{treeInfo.DirectoryPath}！");
        }

        public static async Task Backup(List<string> includes, List<string> excludes, int maxRows, TreeInfo treeInfo)
        {
            var database = DataProvider.DatabaseRepository.GetDatabase();
            var allTableNames = await database.GetTableNamesAsync();

            var tableNames = new List<string>();

            foreach (var tableName in allTableNames)
            {
                if (includes != null && !StringUtils.ContainsIgnoreCase(includes, tableName)) continue;
                if (StringUtils.ContainsIgnoreCase(excludes, tableName)) continue;
                if (StringUtils.ContainsIgnoreCase(tableNames, tableName)) continue;
                tableNames.Add(tableName);
            }

            await FileUtils.WriteTextAsync(treeInfo.TablesFilePath, TranslateUtils.JsonSerialize(tableNames));

            await CliUtils.PrintRowLineAsync();
            await CliUtils.PrintRowAsync("备份表名称", "总条数");
            await CliUtils.PrintRowLineAsync();

            foreach (var tableName in tableNames)
            {
                var repository = new Repository(WebConfigUtils.Database, tableName);
                var tableInfo = new TableInfo
                {
                    Columns = repository.TableColumns,
                    TotalCount = await repository.CountAsync(),
                    RowFiles = new List<string>()
                };

                if (maxRows > 0 && tableInfo.TotalCount > maxRows)
                {
                    tableInfo.TotalCount = maxRows;
                }

                await CliUtils.PrintRowAsync(tableName, tableInfo.TotalCount.ToString("#,0"));

                var identityColumnName = await WebConfigUtils.Database.AddIdentityColumnIdIfNotExistsAsync(tableName, tableInfo.Columns);

                if (tableInfo.TotalCount > 0)
                {
                    var current = 1;
                    if (tableInfo.TotalCount > CliUtils.PageSize)
                    {
                        var pageCount = (int) Math.Ceiling((double) tableInfo.TotalCount / CliUtils.PageSize);

                        using (var progress = new ProgressBar())
                        {
                            for (; current <= pageCount; current++)
                            {
                                progress.Report((double) (current - 1) / pageCount);

                                var fileName = $"{current}.json";
                                tableInfo.RowFiles.Add(fileName);
                                var offset = (current - 1) * CliUtils.PageSize;
                                var limit = tableInfo.TotalCount - offset < CliUtils.PageSize
                                    ? tableInfo.TotalCount - offset
                                    : CliUtils.PageSize;

                                var rows = DataProvider.DatabaseRepository.GetPageObjects(tableName, identityColumnName, offset,
                                    limit);

                                await FileUtils.WriteTextAsync(treeInfo.GetTableContentFilePath(tableName, fileName), TranslateUtils.JsonSerialize(rows));
                            }
                        }
                    }
                    else
                    {
                        var fileName = $"{current}.json";
                        tableInfo.RowFiles.Add(fileName);
                        var rows = DataProvider.DatabaseRepository.GetObjects(tableName);

                        await FileUtils.WriteTextAsync(treeInfo.GetTableContentFilePath(tableName, fileName), TranslateUtils.JsonSerialize(rows));
                    }
                }

                await FileUtils.WriteTextAsync(treeInfo.GetTableMetadataFilePath(tableName),
                    TranslateUtils.JsonSerialize(tableInfo));
            }
        }
    }
}
