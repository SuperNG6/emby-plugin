using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;

public class EmbyActorHeadshotProvider : IRemoteMetadataProvider<Person, PersonLookupInfo>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<EmbyActorHeadshotProvider> _logger;

    public EmbyActorHeadshotProvider(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache, ILogger<EmbyActorHeadshotProvider> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "Emby Actor Headshot Provider";

    public async Task<MetadataResult<Person>> GetMetadata(PersonLookupInfo info, CancellationToken cancellationToken)
    {
        if (info == null || string.IsNullOrEmpty(info.Name))
        {
            return new MetadataResult<Person> { HasMetadata = false };
        }

        var cacheKey = $"actor-headshot-{info.Name}";
        if (_memoryCache.TryGetValue(cacheKey, out MetadataResult<Person> cachedResult))
        {
            return cachedResult;
        }

        var httpClient = _httpClientFactory.CreateClient();
        var url = $"https://api.example.com/actor/{Uri.EscapeDataString(info.Name)}/image";
        _logger.LogInformation("Fetching headshot for {ActorName} from {Url}", info.Name, url);

        try
        {
            var response = await httpClient.GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var imageUrl = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = new MetadataResult<Person>
                {
                    Item = new Person { Name = info.Name, PrimaryImagePath = imageUrl },
                    HasMetadata = true
                };
                _memoryCache.Set(cacheKey, result, TimeSpan.FromHours(1));
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching actor headshot for {ActorName}", info.Name);
        }

        return new MetadataResult<Person> { HasMetadata = false };
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken) =>
        _httpClientFactory.CreateClient().GetAsync(url, cancellationToken);
}
