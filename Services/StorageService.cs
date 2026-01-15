using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using DiscordManagementTool.Models;

namespace DiscordManagementTool.Services;

public class StorageService
{
    private readonly string _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "accounts.dat");

    public void SaveAccounts(List<DiscordAccount> accounts)
    {
        try
        {
            var json = JsonConvert.SerializeObject(accounts);
            var data = Encoding.UTF8.GetBytes(json);
            var encryptedData = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(_filePath, encryptedData);
        }
        catch { }
    }

    public List<DiscordAccount> LoadAccounts()
    {
        if (!File.Exists(_filePath)) return new List<DiscordAccount>();
        try
        {
            var encryptedData = File.ReadAllBytes(_filePath);
            var data = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(data);
            return JsonConvert.DeserializeObject<List<DiscordAccount>>(json) ?? new List<DiscordAccount>();
        }
        catch { return new List<DiscordAccount>(); }
    }
}
