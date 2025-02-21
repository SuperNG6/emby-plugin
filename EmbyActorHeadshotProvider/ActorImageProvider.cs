using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;
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
    public class Plugin : BasePlugin
    {
        public override string Name => "Actor Headshot Provider";
        public override Guid Id => new Guid("1a826edb-16c2-4978-a154-a87b8713f099");
        public override string Description => "Provides actor headshots from local network source";
        
        public Plugin(IApplicationPaths applicationPaths) : base(applicationPaths)
        {
            Instance = this;
        }
        
        public static Plugin Instance { get; private set; }
    }

    public class ActorImageProvider : IRemoteImageProvider, IHasOrder
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ActorImageProvider> _logger;
        private readonly IMemoryCache _cache;
        private const string BASE_URL = "http://127.0.0.1";
        private const string FILETREE_URL = "/Filetree.json";
        private const string CONTENT_PATH = "/Content/";
        private const string CACHE_KEY = "ActorMappingCache";
        private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromMinutes(30);

        public string Name => "Local Actor Headshot Provider";
        public int Order => 1;  // 优先级较高
        
        public ActorImageProvider(
            IHttpClientFactory httpClientFactory,
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

        private class ActorMapping
        {
            public Dictionary<string, Dictionary<string, string>> Content { get; set; }
        }

        private async Task<ActorMapping> GetActorMappingAsync(CancellationToken cancellationToken)
        {
            if (_cache.TryGetValue(CACHE_KEY, out ActorMapping mapping))
            {
                return mapping;
            }

            try
            {
                using var client = _httpClientFactory.CreateClient();
                var response = await client.GetStringAsync($"{BASE_URL}{FILETREE_URL}", cancellationToken);
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                mapping = JsonSerializer.Deserialize<ActorMapping>(response, options);
                
                _cache.Set(CACHE_KEY, mapping, CACHE_DURATION);
                return mapping;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching actor mapping from {Url}", $"{BASE_URL}{FILETREE_URL}");
                return new ActorMapping { Content = new Dictionary<string, Dictionary<string, string>>() };
            }
        }

        private string NormalizeActorName(string name)
        {
            return name.Replace(" ", "").ToLowerInvariant();
        }

        private (string category, string mappedImage)? FindActorImage(
            ActorMapping mapping, 
            string actorName)
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
                        // 移除时间戳参数
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

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var person = item as Person;
            if (person == null) return Array.Empty<RemoteImageInfo>();

            try
            {
                var mapping = await GetActorMappingAsync(cancellationToken);
                var result = FindActorImage(mapping, person.Name);
                
                if (result.HasValue)
                {
                    var (category, mappedImage) = result.Value;
                    var imageUrl = $"{BASE_URL}{CONTENT_PATH}{category}/{mappedImage}";
                    
                    _logger.LogInformation(
                        "Found image for actor {ActorName}: {ImageUrl}", 
                        person.Name, 
                        imageUrl);

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
                
                _logger.LogWarning(
                    "No image found for actor {ActorName}", 
                    person.Name);
                    
                return Array.Empty<RemoteImageInfo>();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error getting image for actor {ActorName}",
                    person.Name);
                    
                return Array.Empty<RemoteImageInfo>();
            }
        }
    }

    // 插件配置
    public class PluginConfiguration
    {
        public string BaseUrl { get; set; } = "http://127.0.0.1";
        public int CacheDurationMinutes { get; set; } = 30;
        public bool EnableLogging { get; set; } = true;
    }
}