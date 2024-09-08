using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Synapse.Server.Models;

namespace Synapse.Server.Services;

public interface IAuthService
{
    public Task<bool> Authenticate(string token, Platform platform, string id);
}

public class AuthService : IAuthService
{
    private const string STEAM_APP_ID = "620980";
    private readonly IHttpClientFactory _httpFactory;

    private readonly ILogger<AuthService> _log;
    private readonly bool _oculusEnabled;
    private readonly bool _steamEnabled;

    private readonly string? _steamUri;

    private readonly bool _testEnabled;

    public AuthService(ILogger<AuthService> log, IConfiguration config, IHttpClientFactory httpFactory)
    {
        _log = log;
        _httpFactory = httpFactory;
        IConfigurationSection eventSection = config.GetSection("Auth");
        IConfigurationSection test = eventSection.GetSection("Test");
        IConfigurationSection steam = eventSection.GetSection("Steam");
        IConfigurationSection oculus = eventSection.GetSection("Oculus");
        _testEnabled = test.GetSection("Enabled").Get<bool>();
        _steamEnabled = steam.GetSection("Enabled").Get<bool>();
        _oculusEnabled = oculus.GetSection("Enabled").Get<bool>();

        string? steamApiKey = steam.GetSection("APIKey").Get<string>();
        if (steamApiKey != null)
        {
            _steamUri = $"ISteamUserAuth/AuthenticateUserTicket/v1?appid={STEAM_APP_ID}&key={steamApiKey}&ticket=";
        }
    }

    public async Task<bool> Authenticate(string token, Platform platform, string id)
    {
        try
        {
            switch (platform)
            {
                case Platform.Test:
                    return _testEnabled;

                case Platform.Steam:
                    if (!_steamEnabled || _steamUri == null)
                    {
                        return false;
                    }

                    HttpClient client = _httpFactory.CreateClient("steam");
                    JsonObject? json = await client.GetFromJsonAsync<JsonObject>(_steamUri + token);
                    JsonNode? param = json?["response"]?["params"];
                    JsonNode? steamid = param?["steamid"];
                    JsonNode? result = param?["result"];
                    bool success = result?.GetValue<string>() == "OK" && steamid?.GetValue<string>() == id;
                    if (!success)
                    {
                        _log.LogError(
                            "Failed to authenticate Steam user with token [{Token}], and id [{Id}]; received API response [{Json}]",
                            token,
                            id,
                            json?.ToString() ?? "N/A");
                    }

                    return success;

                case Platform.OculusRift:
                    return _oculusEnabled; // xd

                default:
                    return false;
            }
        }
        catch (Exception e)
        {
            _log.LogCritical(e, "Exception while authenticating");
            return false;
        }
    }
}
