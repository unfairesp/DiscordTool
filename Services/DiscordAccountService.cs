using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Text;

namespace DiscordManagementTool.Services;

public class DiscordAccountService
{
    private readonly HttpClient _httpClient;

    public DiscordAccountService()
    {
        _httpClient = new HttpClient();
    }

    public async Task<string> GetTokenMetadataAsync(string token)
    {
        try
        {
            var user = await GetRequestAsync("https://discord.com/api/v9/users/@me", token);
            if (user == null) return "";

            var sb = new StringBuilder();
            string username = user["username"]?.ToString() ?? "Unknown";
            string discriminator = user["discriminator"]?.ToString() ?? "0000";
            string id = user["id"]?.ToString() ?? "Unknown";
            string email = user["email"]?.ToString() ?? "No Email";
            string phone = user["phone"]?.ToString() ?? "No Phone";
            bool mfa = user["mfa_enabled"]?.Value<bool>() ?? false;
            int nitro = user["premium_type"]?.Value<int>() ?? 0;

            sb.AppendLine($"User: {username}#{discriminator} ({id})");
            sb.AppendLine($"Email: {email}");
            sb.AppendLine($"Phone: {phone}");
            sb.AppendLine($"2FA: {(mfa ? "Enabled" : "Disabled")}");
            sb.AppendLine($"Nitro: {GetNitroName(nitro)}");

            // Get Billing Info
            var billing = await GetRequestAsync("https://discord.com/api/v9/users/@me/billing/payment-sources", token) as JArray;
            if (billing != null && billing.Count > 0)
            {
                sb.AppendLine($"Billing: {billing.Count} method(s) found");
            }

            // Get Friends count
            var friends = await GetRequestAsync("https://discord.com/api/v9/users/@me/relationships", token) as JArray;
            if (friends != null)
            {
                sb.AppendLine($"Friends: {friends.Count}");
            }

            sb.AppendLine($"Token: {token}");
            return sb.ToString();
        }
        catch
        {
            return "";
        }
    }

    private async Task<JToken?> GetRequestAsync(string url, string token)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", token);
            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                return JToken.Parse(await response.Content.ReadAsStringAsync());
            }
        }
        catch { }
        return null;
    }

    private string GetNitroName(int type)
    {
        return type switch
        {
            1 => "Nitro Classic",
            2 => "Nitro Boost",
            3 => "Nitro Basic",
            _ => "None"
        };
    }
}
