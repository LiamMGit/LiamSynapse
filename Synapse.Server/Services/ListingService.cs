using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Synapse.Networking.Models;

namespace Synapse.Server.Services;

public interface IListingService
{
    public Listing Listing { get; }

    public string[] GameVersion { get; }

    public Task Refresh();
}

public class ListingService : IListingService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<ListingService> _log;
    private readonly string _uri;

    private bool _refreshing;

    public ListingService(ILogger<ListingService> log, IConfiguration config, IHttpClientFactory httpFactory)
    {
        _log = log;
        _httpFactory = httpFactory;
        string fullUrl = config.GetRequiredSection("Listing").Get<string>() ?? throw new InvalidOperationException();
        int id = fullUrl.LastIndexOf('/');
        _uri = fullUrl[(id + 1)..];
        try
        {
            Refresh().Wait();
        }
        catch (Exception)
        {
            // ignored
        }
    }

    public Listing Listing { get; private set; } = new();

    public string[] GameVersion { get; private set; } = [];

    public async Task Refresh()
    {
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;
        HttpClient client = _httpFactory.CreateClient("listing");
        try
        {
            Listing listing = await _httpFactory.CreateClient("listing").GetFromJsonAsync<Listing>(_uri) ??
                              throw new InvalidOperationException("Deserialize returned null");
            Listing = listing;
            GameVersion = listing.GameVersion.Split(',');
        }
        catch (Exception e)
        {
            _log.LogError(e, "Failed to retrieve listing from [{Url}]", client.BaseAddress + _uri);
        }

        _refreshing = false;
    }
}
