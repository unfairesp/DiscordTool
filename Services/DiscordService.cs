using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DiscordManagementTool.Models;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace DiscordManagementTool.Services;

public class DiscordService
{
    private HttpClient _httpClient = null!;
    private HttpClientHandler _handler = null!;
    private List<string> _proxyList = new();
    private int _currentProxyIndex = 0;
    private bool _autoRotate = false;
    private string _proxyType = "HTTP";
    private int _globalDelay = 0;
    private int _jitter = 0;
    private bool _rotateUA = false;
    private bool _stealthMode = false;
    private static readonly Random _random = new();

    private static readonly string[] _userAgents = {
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/121.0"
    };

    public DiscordService()
    {
        InitializeClient();
    }

    private WebProxy? ParseProxy(string proxyUrl)
    {
        if (string.IsNullOrEmpty(proxyUrl)) return null;
        try
        {
            string formattedProxy = proxyUrl.Trim();
            string? username = null;
            string? password = null;
            var parts = formattedProxy.Split(':');
            if (parts.Length == 4)
            {
                formattedProxy = $"{parts[0]}:{parts[1]}";
                username = parts[2];
                password = parts[3];
            }
            if (!formattedProxy.Contains("://"))
            {
                string prefix = _proxyType.ToUpper() switch { "SOCKS4" => "socks4://", "SOCKS5" => "socks5://", _ => "http://" };
                formattedProxy = prefix + formattedProxy;
            }
            var uri = new Uri(formattedProxy);
            var proxy = new WebProxy(uri);
            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var userInfoParts = uri.UserInfo.Split(':');
                if (userInfoParts.Length == 2) proxy.Credentials = new System.Net.NetworkCredential(userInfoParts[0], userInfoParts[1]);
            }
            else if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                proxy.Credentials = new System.Net.NetworkCredential(username, password);
            }
            return proxy;
        }
        catch { return null; }
    }

    private void InitializeClient(string? proxyUrl = null)
    {
        _handler = new HttpClientHandler();
        var proxy = ParseProxy(proxyUrl ?? "");
        if (proxy != null) { _handler.Proxy = proxy; _handler.UseProxy = true; }
        _httpClient = new HttpClient(_handler);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", _userAgents[0]);
        _httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
        _httpClient.DefaultRequestHeaders.Add("X-Discord-Locale", "en-US");
    }

    public void SetProxy(string? proxyUrl) => InitializeClient(proxyUrl);
    public void SetProxyList(List<string> proxies, bool autoRotate, string proxyType = "HTTP")
    {
        _proxyList = proxies; _autoRotate = autoRotate; _proxyType = proxyType; _currentProxyIndex = 0;
        SetProxy(_proxyList.Any() ? _proxyList[0] : null);
    }
    public bool HasProxiesApplied() => _proxyList.Any() && _handler.UseProxy;
    public void ToggleAutoRotate(bool enabled) => _autoRotate = enabled;
    public void UpdateBypassSettings(int delay, int jitter, bool rotateUA, bool stealth)
    {
        _globalDelay = delay; _jitter = jitter; _rotateUA = rotateUA; _stealthMode = stealth;
    }

    public async Task<string> CheckIpAsync()
    {
        try { 
            string ip = (await _httpClient.GetStringAsync("https://api.ipify.org")).Trim();
            return _handler.UseProxy ? $"{ip} (Proxy Active)" : $"{ip} (NO PROXY)";
        } catch { return "Error fetching IP"; }
    }

    private async Task<HttpResponseMessage> SendRequestAsync(HttpMethod method, string url, string? token, object? payload = null)
    {
        try {
            if (_globalDelay > 0 || _jitter > 0) {
                int delay = _globalDelay + (_jitter > 0 ? _random.Next(0, _jitter) : 0);
                await Task.Delay(delay);
            }
            var request = new HttpRequestMessage(method, url);
            if (_rotateUA) request.Headers.UserAgent.ParseAdd(_userAgents[_random.Next(_userAgents.Length)]);
            if (!string.IsNullOrEmpty(token)) request.Headers.TryAddWithoutValidation("Authorization", token);
            if (_stealthMode) request.Headers.TryAddWithoutValidation("X-Super-Properties", GetSuperProperties());
            if (payload != null) {
                string json = payload is JToken tokenObj ? tokenObj.ToString() : JsonConvert.SerializeObject(payload);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }
            var response = await _httpClient.SendAsync(request);
            if (response.StatusCode == (System.Net.HttpStatusCode)429 && _autoRotate && _proxyList.Any()) {
                RotateProxy(); return await SendRequestAsync(method, url, token, payload);
            }
            return response;
        } catch { return new HttpResponseMessage(HttpStatusCode.InternalServerError); }
    }

    private void RotateProxy() {
        if (!_proxyList.Any()) return;
        _currentProxyIndex = (_currentProxyIndex + 1) % _proxyList.Count;
        SetProxy(_proxyList[_currentProxyIndex]);
    }

    private string GetSuperProperties() {
        var props = new JObject { ["os"] = "Windows", ["browser"] = "Discord Client", ["release_channel"] = "stable", ["client_version"] = "1.0.9011" };
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(props.ToString()));
    }

    public async Task<DiscordAccount?> ValidateTokenAsync(string token)
    {
        var response = await SendRequestAsync(HttpMethod.Get, "https://discord.com/api/v9/users/@me", token);
        if (!response.IsSuccessStatusCode) return new DiscordAccount { Token = token, IsValid = false, Status = "Invalid" };
        var json = JObject.Parse(await response.Content.ReadAsStringAsync());
        var acc = new DiscordAccount {
            Token = token, Username = json["username"]?.ToString() ?? "Unknown", Id = json["id"]?.ToString() ?? "",
            IsValid = true, Status = "Valid", Email = json["email"]?.ToString(), Phone = json["phone"]?.ToString()
        };
        return acc;
    }

    public async Task<bool> UpdateProfileAsync(string token, JObject profileData) {
        var resp = await SendRequestAsync(new HttpMethod("PATCH"), "https://discord.com/api/v9/users/@me", token, profileData);
        return resp.IsSuccessStatusCode;
    }

    public async Task<(bool success, string message)> JoinServerAsync(string token, string inviteCode) {
        var resp = await SendRequestAsync(HttpMethod.Post, $"https://discord.com/api/v9/invites/{inviteCode}", token, new JObject());
        return (resp.IsSuccessStatusCode, await resp.Content.ReadAsStringAsync());
    }

    public async Task<bool> LeaveServerAsync(string token, string guildId) {
        var resp = await SendRequestAsync(HttpMethod.Delete, $"https://discord.com/api/v9/users/@me/guilds/{guildId}", token);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateStatusAsync(string token, string status, string statusText) {
        var payload = new JObject {
            ["custom_status"] = new JObject { ["text"] = statusText },
            ["status"] = status
        };
        var resp = await SendRequestAsync(new HttpMethod("PATCH"), "https://discord.com/api/v9/users/@me/settings", token, payload);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> SetHypeSquadAsync(string token, int houseId) {
        var payload = new JObject { ["house_id"] = houseId };
        var resp = await SendRequestAsync(HttpMethod.Post, "https://discord.com/api/v9/hypesquad/online", token, payload);
        return resp.IsSuccessStatusCode;
    }

    public async Task<JObject?> GetFullAccountInfoAsync(string token) {
        var resp = await SendRequestAsync(HttpMethod.Get, "https://discord.com/api/v9/users/@me", token);
        if (!resp.IsSuccessStatusCode) return null;
        return JObject.Parse(await resp.Content.ReadAsStringAsync());
    }
}
