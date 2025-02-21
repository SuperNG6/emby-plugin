using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EmbyActorHeadshotProvider
{
    /// <summary>
    /// 实现 IRemoteImageProvider 接口，为 Emby 提供演员头像图片。
    /// 该实现通过访问配置中的 Filetree.json 获取演员映射信息，然后根据演员名称返回对应图片 URL。
    /// </summary>
    public class ActorImageProvider : IRemoteImageProvider, IHasOrder
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ActorImageProvider> _logger;
        private readonly IMemoryCache _cache;
        private const string CACHE_KEY = "ActorMappingCache";

        public string Name => "Local Actor Headshot Provider";

        /// <summary>
        /// 设置较高优先级，确保 Emby 优先使用该图片提供者
        /// </summary>
        public int Order => 1;

        public ActorImageProvider(IHttpClientFactory httpClientFactory,
                                  ILogger<ActorImageProvider> logger,
                                  IMemoryCache cache)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _cache = cache;
        }

        public bool Supports(BaseItem item) => item is Person;

        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            return new[] { ImageType.Primary };
        }

        /// <summary>
        /// 内部类，与 Filetree.json 格式对应，解析后的结构为：Content -> 分类 -> 演员文件名 : 映射的图片文件（可能包含时间戳参数）
        /// </summary>
        private class ActorMapping
        {
            public Dictionary<string, Dictionary<string, string>> Content { get; set; }
        }

        /// <summary>
        /// 获取演员映射信息，支持缓存
        /// </summary>
        private async Task<ActorMapping> GetActorMappingAsync(CancellationToken cancellationToken)
        {
            if (_cache.TryGetValue(CACHE_KEY, out ActorMapping mapping))
            {
                return mapping;
            }

            var config = Plugin.Instance.Configuration;
            var fileTreeUrl = $"{config.BaseUrl.TrimEnd('/')}{config.FileTreePath}";

            try
            {
                using var client = _httpClientFactory.CreateClient();
                var response = await client.GetStringAsync(fileTreeUrl, cancellationToken);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                mapping = JsonSerializer.Deserialize<ActorMapping>(response, options);

                _cache.Set(CACHE_KEY, mapping,
                    TimeSpan.FromMinutes(config.CacheDurationMinutes));

                if (config.EnableDetailedLogging)
                {
                    _logger.LogInformation("Actor mapping cache updated from {Url}", fileTreeUrl);
                }

                return mapping;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching actor mapping from {Url}", fileTreeUrl);
                return new ActorMapping { Content = new Dictionary<string, Dictionary<string, string>>() };
            }
        }

        /// <summary>
        /// 统一格式化演员名称，去掉空格并转换为小写，便于比较
        /// </summary>
        private string NormalizeActorName(string name)
        {
            return name?.Replace(" ", "").ToLowerInvariant() ?? string.Empty;
        }

        /// <summary>
        /// 遍历所有分类，查找与传入演员名称匹配的记录（匹配时考虑带或不带扩展名）
        /// </summary>
        private (string category, string mappedImage)? FindActorImage(ActorMapping mapping, string actorName)
        {
            var normalizedName = NormalizeActorName(actorName);

            foreach (var category in mapping.Content)
            {
                foreach (var actor in category.Value)
                {
                    var possibleNames = new[]
                    {
                        NormalizeActorName(actor.Key),
                        NormalizeActorName(System.IO.Path.GetFileNameWithoutExtension(actor.Key))
                    };

                    if (possibleNames.Any(n => n == normalizedName))
                    {
                        // 如果图片文件名中包含查询参数（例如 ?t=...），则去掉
                        var mappedImage = actor.Value;
                        var queryIndex = mappedImage.IndexOf('?');
                        if (queryIndex > 0)
                        {
                            mappedImage = mappedImage.Substring(0, queryIndex);
                        }
                        return (category.Key, mappedImage);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 根据 BaseItem（Person）中的 Name 获取演员头像图片 URL，返回给 Emby 供前端显示
        /// </summary>
        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var person = item as Person;
            if (person == null) return Array.Empty<RemoteImageInfo>();

            try
            {
                var config = Plugin.Instance.Configuration;
                var mapping = await GetActorMappingAsync(cancellationToken);
                var result = FindActorImage(mapping, person.Name);

                if (result.HasValue)
                {
                    var (category, mappedImage) = result.Value;
                    var imageUrl = $"{config.BaseUrl.TrimEnd('/')}{config.ContentPath}{category}/{mappedImage}";

                    if (config.EnableDetailedLogging)
                    {
                        _logger.LogInformation("Found image for actor {ActorName}: {ImageUrl}", person.Name, imageUrl);
                    }

                    return new[]
                    {
                        new RemoteImageInfo
                        {
                            ProviderName = Name,
                            Url = imageUrl,
                            Type = ImageType.Primary,
                            DateModified = DateTime.UtcNow
                        }
                    };
                }

                _logger.LogWarning("No image found for actor {ActorName}", person.Name);
                return Array.Empty<RemoteImageInfo>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting image for actor {ActorName}", person.Name);
                return Array.Empty<RemoteImageInfo>();
            }
        }
    }
}
