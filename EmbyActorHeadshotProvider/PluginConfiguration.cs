using MediaBrowser.Model.Plugins;

namespace EmbyActorHeadshotProvider
{
    /// <summary>
    /// 插件配置，允许用户在 Emby 管理后台进行配置
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// 插件基础 URL（例如 http://127.0.0.1）
        /// </summary>
        public string BaseUrl { get; set; }

        /// <summary>
        /// Filetree.json 文件路径
        /// </summary>
        public string FileTreePath { get; set; }

        /// <summary>
        /// 图片内容路径，最终 URL 形式为：BaseUrl + ContentPath + 分类 + "/" + 文件名
        /// </summary>
        public string ContentPath { get; set; }

        /// <summary>
        /// 缓存时长（分钟）
        /// </summary>
        public int CacheDurationMinutes { get; set; }

        /// <summary>
        /// 是否启用详细日志（便于调试）
        /// </summary>
        public bool EnableDetailedLogging { get; set; }

        public PluginConfiguration()
        {
            // 默认配置
            BaseUrl = "http://127.0.0.1";
            FileTreePath = "/Filetree.json";
            ContentPath = "/Content/";
            CacheDurationMinutes = 30;
            EnableDetailedLogging = false;
        }
    }
}
