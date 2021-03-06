﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SS.CMS.Abstractions;
using SS.CMS.Core;
using SS.CMS.Framework;
using SS.CMS.Packaging;
using SS.CMS.Plugins.Impl;

namespace SS.CMS.Plugins
{
    /// <summary>
    /// The entry for managing SiteServer plugins
    /// </summary>
    public static class PluginManager
    {
        private static class PluginManagerCache
        {
            private const string CacheKey = "SS.CMS.Plugin.PluginCache";

            private static async Task<SortedList<string, PluginInstance>> LoadAsync()
            {
                var dict = new SortedList<string, PluginInstance>();

                Thread.Sleep(2000);

                try
                {
                    var pluginsPath = WebUtils.PluginsPath;
                    if (!Directory.Exists(pluginsPath))
                    {
                        return dict;
                    }

                    var directoryNames = DirectoryUtils.GetDirectoryNames(pluginsPath);
                    foreach (var directoryName in directoryNames)
                    {
                        if (StringUtils.StartsWith(directoryName, ".") || StringUtils.EqualsIgnoreCase(directoryName, "packages")) continue;
                        
                        var pluginInfo = await ActivePluginAsync(directoryName);
                        if (pluginInfo != null)
                        {
                            dict[directoryName] = pluginInfo;
                        }
                    }

#if DEBUG
                    PluginDebugger.Instance.Run();
#endif
                }
                catch (Exception ex)
                {
                    await DataProvider.ErrorLogRepository.AddErrorLogAsync(ex, "载入插件时报错");
                }

                return dict;
            }

            private static async Task<PluginInstance> ActivePluginAsync(string directoryName)
            {
                PackageMetadata metadata = null;
                string errorMessage;

                try
                {
                    metadata = PackageUtils.GetPackageMetadataFromPluginDirectory(directoryName, out errorMessage);

                    var dllDirectoryPath = WebUtils.GetPluginDllDirectoryPath(directoryName);
                    if (string.IsNullOrEmpty(dllDirectoryPath))
                    {
                        throw new Exception($"插件可执行文件 {directoryName}.dll 不存在");
                    }

                    //foreach (var filePath in DirectoryUtils.GetFilePaths(DirectoryUtils.GetDirectoryPath(metadata.ExecuteFilePath)))
                    //{

                    //    if (!StringUtils.EqualsIgnoreCase(PathUtils.GetExtension(filePath), ".dll")) continue;
                    //    var fileName = PathUtils.GetFileName(filePath);
                    //    if (StringUtils.EqualsIgnoreCase(fileName, PathUtils.GetFileName(metadata.ExecuteFilePath))) continue;
                    //    if (FileUtils.IsFileExists(PathUtils.Combine(WebConfigUtils.PhysicalApplicationPath, "Bin", fileName))) continue;
                    //    Assembly.Load(File.ReadAllBytes(filePath));
                    //}
                    //var assembly = Assembly.Load(File.ReadAllBytes(metadata.ExecuteFilePath));

                    //metadata.GetDependencyGroups()

                    CopyDllsToBin(metadata.Id, dllDirectoryPath);

                    //var assembly = Assembly.Load(File.ReadAllBytes(PathUtils.Combine(WebConfigUtils.PhysicalApplicationPath, "Bin", PathUtils.GetFileName(metadata.ExecuteFilePath))));

                    Assembly assembly;
                    try
                    {
                        assembly = Assembly.Load(metadata.Id);  // load the dll from bin directory
                    }
                    catch
                    {
                        assembly = Assembly.Load(File.ReadAllBytes(PathUtils.Combine(dllDirectoryPath, $"{metadata.Id}.dll")));
                    }

                    var type = assembly.GetExportedTypes().FirstOrDefault(exportedType => typeof(PluginBase).IsAssignableFrom(exportedType));

                    //var type = assembly.GetTypes().First(o => o.IsClass && !o.IsAbstract && o.IsSubclassOf(typeof(PluginBase)));

                    return await ActiveAndAddAsync(metadata, type);
                }
                catch (Exception ex)
                {
                    errorMessage = ex.Message;
                    await DataProvider.ErrorLogRepository.AddErrorLogAsync(ex, $"插件加载：{directoryName}");
                }

                return new PluginInstance(directoryName, metadata, errorMessage);
            }

            private static void CopyDllsToBin(string pluginId, string pluginDllDirectoryPath)
            {
                foreach (var filePath in DirectoryUtils.GetFilePaths(pluginDllDirectoryPath))
                {
                    if (!StringUtils.EqualsIgnoreCase(PathUtils.GetExtension(filePath), ".dll")) continue;

                    var fileName = PathUtils.GetFileName(filePath);
                    var binFilePath = PathUtils.Combine(WebConfigUtils.PhysicalApplicationPath, "Bin", fileName);

                    if (!FileUtils.IsFileExists(binFilePath))
                    {
                        FileUtils.MoveFile(filePath, binFilePath, false);
                    }
                    else if (StringUtils.EqualsIgnoreCase(fileName, pluginId + ".dll"))
                    {
                        if (FileUtils.ComputeHash(filePath) != FileUtils.ComputeHash(binFilePath))
                        {
                            FileUtils.MoveFile(filePath, binFilePath, true);
                        }
                    }
                }
            }

            private static async Task<PluginInstance> ActiveAndAddAsync(PackageMetadata metadata, Type type)
            {
                if (metadata == null || type == null) return null;

                if (StringUtils.EqualsIgnoreCase(metadata.Id, "SS.Home")) return null;

                var s = Stopwatch.StartNew();

                //var plugin = (IPlugin)Activator.CreateInstance(type);

                var plugin = (PluginBase)Activator.CreateInstance(type);
                plugin.Initialize(metadata);

                var service = new ServiceImpl(metadata);

                plugin.Startup(service);

                await PluginContentTableManager.SyncContentTableAsync(service);
                PluginDatabaseTableManager.SyncTable(service);

                return await PluginInstance.GetAsync(metadata, service, plugin, s.ElapsedMilliseconds);
            }

            public static void Clear()
            {
                CacheUtils.Remove(CacheKey);
            }

            public static async Task<SortedList<string, PluginInstance>> GetPluginSortedListAsync()
            {
                var retVal = CacheUtils.Get<SortedList<string, PluginInstance>>(CacheKey);
                if (retVal != null) return retVal;

                retVal = CacheUtils.Get<SortedList<string, PluginInstance>>(CacheKey);
                if (retVal == null)
                {
                    retVal = await LoadAsync();
                    CacheUtils.InsertHours(CacheKey, retVal, 24);
                }

                return retVal;
            }
        }

        private static List<PluginInstance> _pluginInfoListRunnable;

        public static async Task LoadPluginsAsync(string applicationPhysicalPath)
        {
            var config = await DataProvider.ConfigRepository.GetAsync();

            //Context.Initialize(new EnvironmentImpl(WebConfigUtils.DatabaseType, WebConfigUtils.ConnectionString, WebConfigUtils.HomeDirectory, WebConfigUtils.AdminDirectory, WebConfigUtils.PhysicalApplicationPath, config.GetApiUrl()), new ApiCollectionImpl
            //{
            //    AdminApi = AdminApi.Instance,
            //    ConfigApi = ConfigApi.Instance,
            //    ContentApi = ContentApi.Instance,
            //    ChannelApi = ChannelApi.Instance,
            //    ParseApi = ParseApi.Instance,
            //    PluginApi = PluginApi.Instance,
            //    SiteApi = SiteApi.Instance,
            //    UserApi = UserApi.Instance,
            //    UtilsApi = UtilsApi.Instance
            //});

            _pluginInfoListRunnable = await GetPluginInfoListRunnableAsync();
        }

        public static void ClearCache()
        {
            PluginManagerCache.Clear();
        }

        public static async Task<IPackageMetadata> GetMetadataAsync(string pluginId)
        {
            var dict = await PluginManagerCache.GetPluginSortedListAsync();
            PluginInstance pluginInfo;
            if (dict.TryGetValue(pluginId, out pluginInfo))
            {
                return pluginInfo.Plugin;
            }
            return null;
        }

        public static async Task<bool> IsExistsAsync(string pluginId)
        {
            if (string.IsNullOrEmpty(pluginId)) return false;

            var dict = await PluginManagerCache.GetPluginSortedListAsync();

            return dict.ContainsKey(pluginId);
        }

        public static async Task<List<PluginInstance>> GetPluginInfoListRunnableAsync()
        {
            var dict = await PluginManagerCache.GetPluginSortedListAsync();
            return dict.Values.Where(pluginInfo => pluginInfo.Plugin != null).ToList();
        }

        public static async Task<List<PluginInstance>> GetAllPluginInfoListAsync()
        {
            var dict = await PluginManagerCache.GetPluginSortedListAsync();
            return dict.Values.ToList();
        }

        public static async Task<List<PluginInstance>> GetEnabledPluginInfoListAsync<T>() where T : PluginBase
        {
            var dict = await PluginManagerCache.GetPluginSortedListAsync();
            return
                dict.Values.Where(
                        pluginInfo =>
                            pluginInfo.Plugin != null && !pluginInfo.IsDisabled &&
                            pluginInfo.Plugin is T
                    )
                    .ToList();
        }

        public static async Task<List<ServiceImpl>> GetServicesAsync()
        {
            var dict = await PluginManagerCache.GetPluginSortedListAsync();

            return dict.Values.Where(
                pluginInfo =>
                    pluginInfo.Plugin != null && !pluginInfo.IsDisabled
            ).Select(pluginInfo => pluginInfo.Service).ToList();
        }

        public static async Task<PluginInstance> GetPluginInfoAsync(string pluginId)
        {
            if (string.IsNullOrEmpty(pluginId)) return null;

            var dict = await PluginManagerCache.GetPluginSortedListAsync();

            return dict.TryGetValue(pluginId, out var pluginInfo) ? pluginInfo : null;
        }

        public static async Task<PluginInstance> GetPluginInfoAsync<T>() where T : PluginBase
        {
            var dict = await PluginManagerCache.GetPluginSortedListAsync();
            return dict.Values.Where(instance => instance.Plugin is T).FirstOrDefault(instance => instance.IsRunnable && !instance.IsDisabled);
        }

        public static async Task<Dictionary<string, string>> GetPluginIdAndVersionDictAsync()
        {
            var dict = await PluginManagerCache.GetPluginSortedListAsync();

            var retVal = new Dictionary<string, string>();

            foreach (var pluginId in dict.Keys)
            {
                var pluginInfo = dict[pluginId];
                if (pluginInfo.Metadata != null)
                {
                    retVal[pluginId] = pluginInfo.Metadata.Version;
                }
                else
                {
                    retVal[pluginId] = string.Empty;
                }
            }

            return retVal;
        }

        public static List<string> PackagesIdAndVersionList
        {
            get
            {
                var packagesPath = WebUtils.GetPackagesPath();
                DirectoryUtils.CreateDirectoryIfNotExists(packagesPath);
                return DirectoryUtils.GetDirectoryNames(packagesPath).ToList();
            }
        }

        public static async Task<PluginBase> GetPluginAsync(string pluginId)
        {
            if (string.IsNullOrEmpty(pluginId)) return null;

            var dict = await PluginManagerCache.GetPluginSortedListAsync();

            PluginInstance pluginInfo;
            if (dict.TryGetValue(pluginId, out pluginInfo))
            {
                return pluginInfo.Plugin;
            }
            return null;
        }

        public static async Task<PluginInstance> GetEnabledPluginInfoAsync<T>(string pluginId) where T : PluginBase
        {
            if (string.IsNullOrEmpty(pluginId)) return null;

            var dict = await PluginManagerCache.GetPluginSortedListAsync();

            var isGet = dict.TryGetValue(pluginId, out var pluginInfo);
            if (isGet && pluginInfo.Plugin != null && !pluginInfo.IsDisabled &&
                pluginInfo.Plugin is T)
            {
                return pluginInfo;
            }
            return null;
        }

        public static async Task<List<PluginInstance>> GetEnabledPluginInfoListAsync<T1, T2>()
        {
            var dict = await PluginManagerCache.GetPluginSortedListAsync();

            return dict.Values.Where(
                            pluginInfo =>
                                pluginInfo.Plugin != null && !pluginInfo.IsDisabled &&
                                (pluginInfo.Plugin is T1 || pluginInfo.Plugin is T2)
                        )
                        .ToList();
        }

        public static async Task<List<PluginBase>> GetEnabledPluginMetadatasAsync<T>() where T : PluginBase
        {
            var dict = await PluginManagerCache.GetPluginSortedListAsync();

            return dict.Values.Where(
                        pluginInfo =>
                            pluginInfo.Plugin != null && !pluginInfo.IsDisabled &&
                            pluginInfo.Plugin is T
                    ).Select(pluginInfo => pluginInfo.Plugin).ToList();
        }

        public static async Task<IPackageMetadata> GetEnabledPluginMetadataAsync<T>(string pluginId) where T : PluginBase
        {
            if (string.IsNullOrEmpty(pluginId)) return null;

            var dict = await PluginManagerCache.GetPluginSortedListAsync();

            var isGet = dict.TryGetValue(pluginId, out var pluginInfo);
            if (isGet && pluginInfo.Plugin != null && !pluginInfo.IsDisabled &&
                pluginInfo.Plugin is T)
            {
                return pluginInfo.Plugin;
            }
            return null;
        }

        public static async Task<T> GetEnabledFeatureAsync<T>(string pluginId) where T : PluginBase
        {
            if (string.IsNullOrEmpty(pluginId)) return default(T);

            var dict = await PluginManagerCache.GetPluginSortedListAsync();

            PluginInstance pluginInfo;
            var isGet = dict.TryGetValue(pluginId, out pluginInfo);
            if (isGet && pluginInfo.Plugin != null && !pluginInfo.IsDisabled &&
                pluginInfo.Plugin is T)
            {
                return (T)pluginInfo.Plugin;
            }
            return default(T);
        }

        public static async Task<List<T>> GetEnabledFeaturesAsync<T>() where T : PluginBase
        {
            var dict = await PluginManagerCache.GetPluginSortedListAsync();

            var pluginInfos = dict.Values.Where(
                        pluginInfo =>
                            pluginInfo.Plugin != null && !pluginInfo.IsDisabled &&
                            pluginInfo.Plugin is T
                    )
                    .ToList();
            return pluginInfos.Select(pluginInfo => (T)pluginInfo.Plugin).ToList();
        }

        public static async Task<ServiceImpl> GetServiceAsync(string pluginId)
        {
            if (string.IsNullOrEmpty(pluginId)) return null;

            foreach (var service in await GetServicesAsync())
            {
                if (StringUtils.EqualsIgnoreCase(service.PluginId, pluginId))
                {
                    return service;
                }
            }

            return null;
        }



        //public static List<ContentModelInfo> GetAllContentModels(SiteInfo siteInfo)
        //{
        //    var cacheName = nameof(GetAllContentModels) + siteInfo.Id;
        //    var contentModels = GetCache<List<ContentModelInfo>>(cacheName);
        //    if (contentModels != null) return contentModels;

        //    contentModels = new List<ContentModelInfo>();

        //    foreach (var pluginInfo in GetEnabledPluginInfoLists<IContentModel>())
        //    {
        //        var model = pluginInfo.Plugin as IContentModel;

        //        if (model == null) continue;

        //        var tableName = siteInfo.AuxiliaryTableForContent;
        //        var tableType = EAuxiliaryTableType.BackgroundContent;
        //        if (model.ContentTableColumns != null && model.ContentTableColumns.Count > 0)
        //        {
        //            tableName = pluginInfo.Id;
        //            tableType = EAuxiliaryTableType.Custom;
        //        }

        //        contentModels.Add(new ContentModelInfo(
        //            pluginInfo.Id,
        //            pluginInfo.Id,
        //            $"插件：{pluginInfo.Metadata.DisplayName}",
        //            tableName,
        //            tableType,
        //            PageUtils.GetPluginDirectoryUrl(pluginInfo.Id, pluginInfo.Metadata.Icon))
        //        );
        //    }

        //    SetCache(cacheName, contentModels);

        //    return contentModels;
        //}



        //public static List<ContentModelInfo> GetAllContentModels(SiteInfo siteInfo)
        //{
        //    var cacheName = nameof(GetAllContentModels) + siteInfo.Id;
        //    var contentModels = GetCache<List<ContentModelInfo>>(cacheName);
        //    if (contentModels != null) return contentModels;

        //    contentModels = new List<ContentModelInfo>();

        //    foreach (var pluginInfo in GetEnabledPluginInfoLists<IContentModel>())
        //    {
        //        var model = pluginInfo.Plugin as IContentModel;

        //        if (model == null) continue;

        //        var links = new List<PluginContentLink>();
        //        if (model.ContentLinks != null)
        //        {
        //            links.AddRange(model.ContentLinks.Select(link => new PluginContentLink
        //            {
        //                Text = link.Text,
        //                Href = PageUtils.GetPluginDirectoryUrl(pluginInfo.Id, link.Href),
        //                Target = link.Target
        //            }));
        //        }
        //        var tableName = siteInfo.AuxiliaryTableForContent;
        //        var tableType = EAuxiliaryTableType.BackgroundContent;
        //        if (model.IsCustomContentTable && model.CustomContentTableColumns != null && model.CustomContentTableColumns.Count > 0)
        //        {
        //            tableName = pluginInfo.Id;
        //            tableType = EAuxiliaryTableType.Custom;
        //        }

        //        contentModels.Add(new ContentModelInfo(
        //            pluginInfo.Id,
        //            pluginInfo.Id,
        //            $"插件：{pluginInfo.Metadata.DisplayName}",
        //            tableName,
        //            tableType,
        //            PageUtils.GetPluginDirectoryUrl(pluginInfo.Id, pluginInfo.Metadata.Icon),
        //            links)
        //        );
        //    }

        //    SetCache(cacheName, contentModels);

        //    return contentModels;
        //}



        //public static Dictionary<string, Func<PluginRenderContext, string>> GetRenders()
        //{
        //    var renders = new Dictionary<string, Func<PluginRenderContext, string>>();

        //    var pluginInfoList = GetEnabledPluginInfoList<IRender>();
        //    if (pluginInfoList != null && pluginInfoList.Count > 0)
        //    {
        //        foreach (var pluginInfo in pluginInfoList)
        //        {
        //            var plugin = pluginInfo.Plugin as IRender;
        //            if (plugin?.Render != null)
        //            {
        //                renders.Add(pluginInfo.Metadata.Id, plugin.Render);
        //            }
        //            //if (!(pluginInfo.Plugin is IRender plugin)) continue;

        //            //if (plugin.Render != null)
        //            //{
        //            //    renders.Add(pluginInfo.Metadata.Id, plugin.Render);
        //            //}
        //        }
        //    }

        //    return renders;
        //}

        //public static List<Action<object, FileSystemEventArgs>> GetFileSystemChangedActions()
        //{
        //    var actions = new List<Action<object, FileSystemEventArgs>>();

        //    var plugins = GetEnabledFeatures<IFileSystem>();
        //    if (plugins != null && plugins.Count > 0)
        //    {
        //        foreach (var plugin in plugins)
        //        {
        //            if (plugin.FileSystemChanged != null)
        //            {
        //                actions.Add(plugin.FileSystemChanged);
        //            }
        //        }
        //    }

        //    return actions;
        //}

        

        //public static bool Install(string pluginId, string version, out string errorMessage)
        //{
        //    errorMessage = string.Empty;
        //    if (string.IsNullOrEmpty(pluginId)) return false;

        //    try
        //    {
        //        if (IsExists(pluginId))
        //        {
        //            errorMessage = $"插件 {pluginId} 已存在";
        //            return false;
        //        }
        //        var directoryPath = PathUtils.GetPluginPath(pluginId);
        //        DirectoryUtils.DeleteDirectoryIfExists(directoryPath);

        //        var zipFilePath = PathUtility.GetTemporaryFilesPath(pluginId + ".zip");
        //        FileUtils.DeleteFileIfExists(zipFilePath);

        //        var downloadUrl = $"http://download.siteserver.cn/plugins/{pluginId}/{version}/{pluginId}.zip";
        //        WebClientUtils.SaveRemoteFileToLocal(downloadUrl, zipFilePath);
                
        //        ZipUtils.UnpackFiles(zipFilePath, directoryPath);
        //        FileUtils.DeleteFileIfExists(zipFilePath);

        //        string dllDirectoryPath;
        //        var metadata = GetPluginMetadata(pluginId, out dllDirectoryPath, out errorMessage);
        //        if (metadata == null)
        //        {
        //            return false;
        //        }

        //        //SaveMetadataToJson(metadata);
        //    }
        //    catch (Exception ex)
        //    {
        //        errorMessage = ex.Message;
        //        return false;
        //    }

        //    return true;
        //}

        public static void Delete(string pluginId)
        {
            DirectoryUtils.DeleteDirectoryIfExists(WebUtils.GetPluginPath(pluginId));
            ClearCache();
        }

        public static async Task UpdateDisabledAsync(string pluginId, bool isDisabled)
        {
            var pluginInfo = await GetPluginInfoAsync(pluginId);
            if (pluginInfo != null)
            {
                pluginInfo.IsDisabled = isDisabled;
                await DataProvider.PluginRepository.UpdateIsDisabledAsync(pluginId, isDisabled);
                ClearCache();
            }
        }

        public static async Task UpdateTaxisAsync(string pluginId, int taxis)
        {
            var pluginInfo = await GetPluginInfoAsync(pluginId);
            if (pluginInfo != null)
            {
                pluginInfo.Taxis = taxis;
                await DataProvider.PluginRepository.UpdateTaxisAsync(pluginId, taxis);
                ClearCache();
            }
        }

        //public static PluginMetadata UpdateDatabase(string pluginId, string databaseType, string connectionString)
        //{
        //    var metadata = GetMetadata(pluginId);
        //    if (metadata != null)
        //    {
        //        if (WebConfigUtils.IsProtectData && !string.IsNullOrEmpty(databaseType))
        //        {
        //            databaseType = TranslateUtils.EncryptStringBySecretKey(databaseType);
        //        }
        //        if (WebConfigUtils.IsProtectData && !string.IsNullOrEmpty(connectionString))
        //        {
        //            connectionString = TranslateUtils.EncryptStringBySecretKey(connectionString);
        //        }
        //        metadata.DatabaseType = databaseType;
        //        metadata.ConnectionString = connectionString;
        //        SaveMetadataToJson(metadata);
        //    }
        //    return metadata;
        //}

        /// <summary>
        /// Parse plugin metadata in giving directories
        /// </summary>
        /// <returns></returns>
        //internal static PluginMetadata GetMetadataFromJson(string directoryPath)
        //{
        //    var configPath = Path.Combine(directoryPath, PluginConfigName);
        //    if (!File.Exists(configPath))
        //    {
        //        return null;
        //    }

        //    PluginMetadata metadata;
        //    try
        //    {
        //        metadata = JsonConvert.DeserializeObject<PluginMetadata>(File.ReadAllText(configPath));
        //        metadata.DirectoryPath = directoryPath;
        //    }
        //    catch
        //    {
        //        return null;
        //    }

        //    if (string.IsNullOrEmpty(metadata.Id))
        //    {
        //        return null;
        //    }

        //    return metadata;
        //}

        //private static PackageMetadata GetPluginMetadata(string directoryName, out string dllDirectoryPath, out string errorMessage)
        //{
        //    dllDirectoryPath = string.Empty;
        //    var nuspecPath = PathUtils.GetPluginNuspecPath(directoryName);
        //    if (!File.Exists(nuspecPath))
        //    {
        //        errorMessage = $"插件配置文件 {directoryName}.nuspec 不存在";
        //        return null;
        //    }
        //    dllDirectoryPath = PathUtils.GetPluginDllDirectoryPath(directoryName);
        //    if (string.IsNullOrEmpty(dllDirectoryPath))
        //    {
        //        errorMessage = $"插件可执行文件 {directoryName}.dll 不存在";
        //        return null;
        //    }

        //    PackageMetadata metadata;
        //    try
        //    {
        //        metadata = PackageUtils.GetPackageMetadata(nuspecPath);
        //    }
        //    catch(Exception ex)
        //    {
        //        errorMessage = ex.Message;
        //        return null;
        //    }

        //    if (string.IsNullOrEmpty(metadata.Id))
        //    {
        //        errorMessage = "插件配置文件不正确";
        //        return null;
        //    }

        //    errorMessage = string.Empty;
        //    return metadata;
        //}

        public static async Task<string> GetPluginIconUrlAsync(string pluginId)
        {
            foreach (var service in await GetServicesAsync())
            {
                if (service.PluginId == pluginId)
                {
                    return GetPluginIconUrl(service);
                }
            }
            return string.Empty;
        }

        public static string GetPluginIconUrl(ServiceImpl service)
        {
            var url = string.Empty;
            if (service.Metadata.IconUrl != null)
            {
                url = service.Metadata.IconUrl.ToString();
            }
            return url;
        }
    }
}
