using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace DiscordManagementTool.Services;

public class ProxyService
{
    private readonly HttpClient _httpClient;

    public ProxyService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

    public async Task<bool> ValidateProxyAsync(string proxy, string protocol = "http")
    {
        try
        {
            string formattedProxy = proxy.Trim();
            string? username = null;
            string? password = null;
            var parts = formattedProxy.Split(':');
            if (parts.Length == 4)
            {
                formattedProxy = $"{parts[0]}:{parts[1]}";
                username = parts[2];
                password = parts[3];
            }

            string prefix = protocol.ToLower() switch { "socks4" => "socks4://", "socks5" => "socks5://", _ => "http://" };
            string finalProxy = formattedProxy.Contains("://") ? formattedProxy : prefix + formattedProxy;
            var uri = new Uri(finalProxy);
            var webProxy = new System.Net.WebProxy(uri);

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var userInfoParts = uri.UserInfo.Split(':');
                if (userInfoParts.Length == 2) webProxy.Credentials = new System.Net.NetworkCredential(userInfoParts[0], userInfoParts[1]);
            }
            else if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                webProxy.Credentials = new System.Net.NetworkCredential(username, password);
            }

            var handler = new HttpClientHandler { Proxy = webProxy, UseProxy = true };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync("https://discord.com/api/v9/gateway");
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<List<string>> ScrapeFreeProxiesAsync(string protocol = "http")
    {
        var allProxies = new HashSet<string>();
        var sources = new List<string> {
            "https://api.proxyscrape.com/v2/?request=displayproxies&protocol=http&timeout=10000&country=all&ssl=all&anonymity=all",
            "https://raw.githubusercontent.com/TheSpeedX/PROXY-List/master/http.txt",
            "https://raw.githubusercontent.com/monosans/proxy-list/main/proxies/http.txt"
        };

        var tasks = sources.Select(async url => {
            try {
                var response = await _httpClient.GetStringAsync(url);
                if (string.IsNullOrWhiteSpace(response)) return;
                var lines = response.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines) {
                    string proxy = line.Trim();
                    if (!string.IsNullOrEmpty(proxy) && !proxy.Contains(" ") && !proxy.Contains("<")) {
                        lock (allProxies) allProxies.Add(proxy);
                    }
                }
            } catch { }
        });

        await Task.WhenAll(tasks);
        return allProxies.ToList();
    }
}
