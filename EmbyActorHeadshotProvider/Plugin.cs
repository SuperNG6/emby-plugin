using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using System;

namespace EmbyActorHeadshotProvider
{
    /// <summary>
    /// 插件主类，Emby 在启动时会扫描并加载该 DLL 中的 BasePlugin 实现
    /// </summary>
    public class Plugin : BasePlugin<PluginConfiguration>
    {
        /// <summary>
        /// 插件名称
        /// </summary>
        public override string Name => "Actor Headshot Provider";

        /// <summary>
        /// 插件唯一标识符（请保持唯一性）
        /// </summary>
        public override Guid Id => new Guid("1a826edb-16c2-4978-a154-a87b8713f099");

        /// <summary>
        /// 插件描述信息
        /// </summary>
        public override string Description => "Provides actor headshots from local network source";

        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            // 将当前插件实例保存，方便其他类（如 ActorImageProvider）获取配置信息
            Instance = this;
        }

        /// <summary>
        /// 静态插件实例
        /// </summary>
        public static Plugin Instance { get; private set; }
    }
}
