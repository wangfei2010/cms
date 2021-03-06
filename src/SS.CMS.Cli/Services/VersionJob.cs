﻿using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Datory;
using Mono.Options;
using SS.CMS.Abstractions;
using SS.CMS.Cli.Core;
using SS.CMS.Framework;
using Microsoft.Extensions.DependencyInjection;

namespace SS.CMS.Cli.Services
{
    public class VersionJob
    {
        public const string CommandName = "version";

        public static async Task Execute(IJobContext context)
        {
            var application = CliUtils.Provider.GetService<VersionJob>();
            await application.RunAsync(context);
        }

        private string _configFile;
        private bool _isHelp;

        private readonly OptionSet _options;

        public VersionJob()
        {
            _options = new OptionSet {
                { "c|config-file=", "指定配置文件Web.config路径或文件名",
                    v => _configFile = v },
                { "h|help",  "命令说明",
                    v => _isHelp = v != null }
            };
        }

        public static void PrintUsage()
        {
            Console.WriteLine("显示当前版本: siteserver version");
            var job = new VersionJob();
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

            var version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            await Console.Out.WriteLineAsync($"SiteServer CLI 版本号: {version.Substring(0, version.Length - 2)}");
            await Console.Out.WriteLineAsync($"当前文件夹: {CliUtils.PhysicalApplicationPath}");
            await Console.Out.WriteLineAsync();

            var webConfigPath = CliUtils.GetWebConfigPath(_configFile);

            if (FileUtils.IsFileExists(webConfigPath))
            {
                WebConfigUtils.Load(CliUtils.PhysicalApplicationPath, webConfigPath);

                try
                {
                    var cmsVersion = FileVersionInfo.GetVersionInfo(PathUtils.Combine(CliUtils.PhysicalApplicationPath, "Bin", "SS.CMS.dll")).ProductVersion;
                    await Console.Out.WriteLineAsync($"SitServer CMS Version: {cmsVersion}");
                }
                catch
                {
                    // ignored
                }

                await Console.Out.WriteLineAsync($"数据库类型: {WebConfigUtils.DatabaseType.GetValue()}");
                await Console.Out.WriteLineAsync($"连接字符串: {WebConfigUtils.ConnectionString}");
                await Console.Out.WriteLineAsync($"连接字符串（加密）: {TranslateUtils.EncryptStringBySecretKey(WebConfigUtils.ConnectionString, WebConfigUtils.SecretKey)}");
            }
        }
    }
}
