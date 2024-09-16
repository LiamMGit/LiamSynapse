using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Synapse.Networking.Models;

namespace Synapse.Server.Services;

public interface IListingService
{
    public Listing? Listing { get; }

    public string[] GameVersion { get; }
}

public class ListingService : IListingService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<ListingService> _log;
    private readonly string _uri;

    private Listing? _listing;
    private TaskCompletionSource<Listing?>? _refreshTcs;

    public ListingService(ILogger<ListingService> log, IConfiguration config, IHttpClientFactory httpFactory)
    {
        _log = log;
        _httpFactory = httpFactory;
        string fullUrl = config.GetRequiredSection("Listing").Get<string>() ?? throw new InvalidOperationException();
        int id = fullUrl.LastIndexOf('/');
        _uri = fullUrl[(id + 1)..];
        _ = GetListing().ContinueWith(n => _listing = n.Result);
    }

    public Listing? Listing => _listing ??= GetListing().Result;

    public string[] GameVersion { get; private set; } = [];

    private async Task<Listing?> GetListing()
    {
        if (_refreshTcs != null)
        {
            return await _refreshTcs.Task;
        }

        _refreshTcs = new TaskCompletionSource<Listing?>();
        HttpClient client = _httpFactory.CreateClient("listing");
        try
        {
            Listing listing = await client.GetFromJsonAsync<Listing>(_uri) ??
                              throw new InvalidOperationException("Deserialize returned null");
            GameVersion = listing.GameVersion.Split(',');
            _refreshTcs.SetResult(listing);
            return listing;
        }
        catch (Exception e)
        {
            _log.LogError(e, "Failed to retrieve listing from [{Url}]", client.BaseAddress + _uri);
            _refreshTcs.SetResult(null);
            return null;
        }
        finally
        {
            _refreshTcs = null;
        }
    }
}
