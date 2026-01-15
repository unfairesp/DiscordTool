using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DiscordManagementTool.Services;

public class NetworkInfoService
{
    private readonly HttpClient _client = new HttpClient();

    public async Task<JObject?> GetNetworkDetailsAsync()
    {
        try
        {
            string response = await _client.GetStringAsync("https://ipapi.co/json/");
            return JObject.Parse(response);
        }
        catch { return null; }
    }

    public async Task<string> GetPublicIpAsync()
    {
        try
        {
            return await _client.GetStringAsync("https://api.ipify.org");
        }
        catch { return "Unknown IP"; }
    }
}
