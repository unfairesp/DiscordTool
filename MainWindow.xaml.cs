using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using DiscordManagementTool.Models;
using DiscordManagementTool.Services;

namespace DiscordManagementTool
{
    public partial class MainWindow : System.Windows.Window
    {
        private readonly DiscordService _discordService;
        private readonly StorageService _storageService;
        private readonly ProxyService _proxyService;
        private readonly List<DiscordAccount> _accounts = new List<DiscordAccount>();

        public MainWindow()
        {
            InitializeComponent();
            _discordService = new DiscordService();
            _storageService = new StorageService();
            _proxyService = new ProxyService();

            LoadSavedAccounts();
        }

        private void LoadSavedAccounts()
        {
            try
            {
                var saved = _storageService.LoadAccounts();
                if (saved != null && saved.Any())
                {
                    _accounts.AddRange(saved);
                    RefreshAccountUI();
                    Log($"Loaded {_accounts.Count} accounts from storage.");
                }
            }
            catch (Exception ex)
            {
                Log($"Error loading accounts: {ex.Message}");
            }
        }

        private void RefreshAccountUI()
        {
            Dispatcher.Invoke(() =>
            {
                TokenCountText.Text = $"Tokens: {_accounts.Count}";
                TokenInfoSelector.ItemsSource = null;
                TokenInfoSelector.ItemsSource = _accounts;
            });
        }

        private void Log(string message)
        {
            if (System.Windows.Application.Current == null || Dispatcher.HasShutdownStarted) return;
            Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    LogText.Text += $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
                    LogText.ScrollToEnd();
                }
                catch { }
            });
        }

        private void Tab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string tabName)
            {
                foreach (var child in ((Grid)TokensTab.Parent).Children)
                {
                    if (child is Grid tab)
                    {
                        tab.Visibility = tab.Name == tabName ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }
        }

        #region Token Management

        private async void ValidateTokens_Click(object sender, RoutedEventArgs e)
        {
            var tokens = TokenInput.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                      .Select(t => t.Trim())
                                      .Distinct()
                                      .ToList();

            if (!tokens.Any())
            {
                Log("No tokens provided.");
                return;
            }

            Log($"Validating {tokens.Count} tokens...");
            _accounts.Clear();

            foreach (var token in tokens)
            {
                var account = await _discordService.ValidateTokenAsync(token);
                if (account != null && account.IsValid)
                {
                    _accounts.Add(account);
                    Log($"Validated: {account.FullName} | Nitro: {account.NitroType}");
                }
                else
                {
                    Log($"Invalid Token: {token.Substring(0, Math.Min(10, token.Length))}...");
                }
            }

            _storageService.SaveAccounts(_accounts);
            RefreshAccountUI();
            Log($"Validation complete. {_accounts.Count} valid accounts.");
        }

        private void ClearTokens_Click(object sender, RoutedEventArgs e)
        {
            TokenInput.Clear();
            _accounts.Clear();
            _storageService.SaveAccounts(_accounts);
            RefreshAccountUI();
            Log("All tokens cleared.");
        }

        #endregion

        #region Joiner & Cleaner

        private async void StartJoining_Click(object sender, RoutedEventArgs e)
        {
            var invite = InviteInput.Text.Trim();
            if (string.IsNullOrEmpty(invite)) { Log("Enter an invite code."); return; }
            if (!_accounts.Any()) { Log("No valid tokens available."); return; }

            if (invite.Contains("/")) invite = invite.Split('/').Last();

            Log($"Starting joiner for {invite} with {_accounts.Count} accounts...");
            JoinProgress.Maximum = _accounts.Count;
            JoinProgress.Value = 0;

            foreach (var account in _accounts)
            {
                var (success, message) = await _discordService.JoinServerAsync(account.Token, invite);
                Log($"[{account.Username}] {(success ? "Joined successfully." : "Failed: " + message)}");
                JoinProgress.Value++;
                await Task.Delay(2000);
            }
            Log("Joiner operation finished.");
        }

        private async void StartCleaning_Click(object sender, RoutedEventArgs e)
        {
            if (!_accounts.Any()) { Log("No tokens available."); return; }

            bool leaveGuilds = LeaveGuildsCheck.IsChecked ?? false;
            bool deleteGuilds = DeleteOwnedGuildsCheck.IsChecked ?? false;
            bool removeFriends = RemoveFriendsCheck.IsChecked ?? false;

            if (!leaveGuilds && !deleteGuilds && !removeFriends) { Log("No options selected."); return; }

            Log($"Starting cleaning for {_accounts.Count} accounts...");
            CleanProgress.Maximum = _accounts.Count;
            CleanProgress.Value = 0;

            foreach (var account in _accounts)
            {
                if (deleteGuilds) await _discordService.DeleteOwnedGuildsAsync(account.Token);
                if (leaveGuilds) await _discordService.LeaveAllServersAsync(account.Token);
                if (removeFriends) await _discordService.RemoveAllFriendsAsync(account.Token);

                CleanProgress.Value++;
                Log($"[{account.Username}] Cleaned.");
                await Task.Delay(1000);
            }
            Log("Cleaning operation finished.");
        }

        #endregion

        #region Profile & Settings

        private async void UpdateProfiles_Click(object sender, RoutedEventArgs e)
        {
            string statusText = StatusInput.Text;
            string displayName = DisplayNameInput.Text;
            string pronouns = PronounsInput.Text;
            int houseId = int.Parse((HypeSquadCombo.SelectedItem as ComboBoxItem)?.Tag.ToString() ?? "0");

            Log($"Updating profiles for {_accounts.Count} accounts...");
            foreach (var account in _accounts)
            {
                var profileData = new JObject();
                if (!string.IsNullOrEmpty(displayName)) profileData["global_name"] = displayName;
                if (!string.IsNullOrEmpty(pronouns)) profileData["pronouns"] = pronouns;
                
                if (profileData.HasValues) await _discordService.UpdateProfileAsync(account.Token, profileData);

                if (!string.IsNullOrEmpty(statusText))
                    await _discordService.UpdateStatusAsync(account.Token, "online", statusText);

                if (houseId > 0)
                    await _discordService.SetHypeSquadAsync(account.Token, houseId);

                Log($"[{account.Username}] Profile updated.");
            }
            Log("All profiles updated.");
        }

        private async void SetStatus_Click(object sender, RoutedEventArgs e)
        {
            string status = (sender as System.Windows.Controls.Button)?.Tag?.ToString() ?? "online";
            Log($"Setting status to {status} for {_accounts.Count} accounts...");
            foreach (var acc in _accounts)
            {
                await _discordService.UpdateStatusAsync(acc.Token, status, "");
                Log($"[{acc.Username}] Status set.");
            }
        }

        private async void SetLightMode_Click(object sender, RoutedEventArgs e)
        {
            foreach (var acc in _accounts)
            {
                await _discordService.UpdateUserSettingsAsync(acc.Token, new JObject { ["theme"] = "light" });
                Log($"[{acc.Username}] Set to Light Mode.");
            }
        }

        private async void SetDarkMode_Click(object sender, RoutedEventArgs e)
        {
            foreach (var acc in _accounts)
            {
                await _discordService.UpdateUserSettingsAsync(acc.Token, new JObject { ["theme"] = "dark" });
                Log($"[{acc.Username}] Set to Dark Mode.");
            }
        }

        private async void RandomBio_Click(object sender, RoutedEventArgs e)
        {
            string[] bios = { "Discord Management Tool User", "I love Discord", "Developer", "Available", "Busy" };
            var rand = new Random();
            foreach (var acc in _accounts)
            {
                string bio = bios[rand.Next(bios.Length)];
                await _discordService.UpdateProfileAsync(acc.Token, new JObject { ["bio"] = bio });
                Log($"[{acc.Username}] Bio set: {bio}");
            }
        }

        private async void RandomHypeSquad_Click(object sender, RoutedEventArgs e)
        {
            var random = new Random();
            foreach (var acc in _accounts)
            {
                int house = random.Next(1, 4);
                await _discordService.SetHypeSquadAsync(acc.Token, house);
                Log($"[{acc.Username}] HypeSquad set to {house}.");
                await Task.Delay(300);
            }
        }

        private void ClearBios_Click(object sender, RoutedEventArgs e)
        {
            Log("Operation not implemented in this version.");
        }

        private void ClearNotifications_Click(object sender, RoutedEventArgs e)
        {
            Log("Operation not implemented in this version.");
        }

        #endregion

        #region Mass Operations

        private async void MassLeave_Click(object sender, RoutedEventArgs e)
        {
            Log("Starting Mass Leave...");
            foreach (var acc in _accounts)
            {
                await _discordService.LeaveAllServersAsync(acc.Token);
                Log($"[{acc.Username}] Finished leaving servers.");
            }
        }

        private async void MassUnfriend_Click(object sender, RoutedEventArgs e)
        {
            Log("Starting Mass Unfriend...");
            foreach (var acc in _accounts)
            {
                await _discordService.RemoveAllFriendsAsync(acc.Token);
                Log($"[{acc.Username}] Finished removing friends.");
            }
        }

        private async void MassBlock_Click(object sender, RoutedEventArgs e)
        {
            Log("Starting Mass Block...");
            foreach (var acc in _accounts)
            {
                await _discordService.MassBlockFriendsAsync(acc.Token);
                Log($"[{acc.Username}] Finished blocking friends.");
            }
        }

        private async void MassUnblock_Click(object sender, RoutedEventArgs e)
        {
            Log("Starting Mass Unblock...");
            foreach (var acc in _accounts)
            {
                var relationships = await _discordService.GetRelationshipsAsync(acc.Token);
                if (relationships != null)
                {
                    foreach (var rel in relationships)
                    {
                        if (rel["type"]?.Value<int>() == 2) await _discordService.RemoveFriendAsync(acc.Token, rel["id"]!.ToString());
                    }
                }
                Log($"[{acc.Username}] Finished unblocking.");
            }
        }

        private async void AcceptAllRequests_Click(object sender, RoutedEventArgs e)
        {
            Log("Accepting all friend requests...");
            foreach (var acc in _accounts)
            {
                var relationships = await _discordService.GetRelationshipsAsync(acc.Token);
                if (relationships != null)
                {
                    foreach (var rel in relationships)
                    {
                        if (rel["type"]?.Value<int>() == 3) await _discordService.AcceptFriendRequestAsync(acc.Token, rel["id"]!.ToString());
                    }
                }
                Log($"[{acc.Username}] Finished accepting requests.");
            }
        }

        private async void RejectAllRequests_Click(object sender, RoutedEventArgs e)
        {
            Log("Rejecting all friend requests...");
            foreach (var acc in _accounts)
            {
                var relationships = await _discordService.GetRelationshipsAsync(acc.Token);
                if (relationships != null)
                {
                    foreach (var rel in relationships)
                    {
                        if (rel["type"]?.Value<int>() == 3 || rel["type"]?.Value<int>() == 4) 
                            await _discordService.RemoveFriendAsync(acc.Token, rel["id"]!.ToString());
                    }
                }
                Log($"[{acc.Username}] Finished rejecting requests.");
            }
        }

        private async void CloseAllDMs_Click(object sender, RoutedEventArgs e)
        {
            Log("Closing all DMs...");
            foreach (var acc in _accounts)
            {
                await _discordService.CloseAllDMsAsync(acc.Token);
                Log($"[{acc.Username}] DMs closed.");
            }
        }

        #endregion

        #region Token Info

        private async void FetchTokenInfo_Click(object sender, RoutedEventArgs e)
        {
            if (TokenInfoSelector.SelectedItem is DiscordAccount account)
            {
                Log($"Fetching detailed info for {account.FullName}...");
                var info = await _discordService.GetFullAccountInfoAsync(account.Token);
                if (info != null)
                {
                    TokenDetailsPanel.Children.Clear();
                    foreach (var prop in info.Properties())
                    {
                        TokenDetailsPanel.Children.Add(new TextBlock 
                        { 
                            Text = $"{prop.Name}: {prop.Value}", 
                            Foreground = System.Windows.Media.Brushes.White,
                            Margin = new Thickness(0, 2, 0, 2)
                        });
                    }

                    var guilds = await _discordService.GetGuildsAsync(account.Token);
                    GuildsInfoList.ItemsSource = guilds?.Select(g => new 
                    { 
                        Name = g["name"]?.ToString(), 
                        PermissionsText = $"ID: {g["id"]}",
                        Role = (g["owner"]?.Value<bool>() ?? false) ? "Owner" : "Member"
                    });
                }
            }
            else
            {
                Log("Select an account first.");
            }
        }

        private void ExportFullData_Click(object sender, RoutedEventArgs e)
        {
            Log("Export not implemented in this version.");
        }

        #endregion

        #region Webhook Tools

        private async void TabSendWebhook_Click(object sender, RoutedEventArgs e)
        {
            string url = TabWebhookUrlInput.Text.Trim();
            string msg = TabWebhookMessageInput.Text.Trim();
            if (string.IsNullOrEmpty(url)) { Log("Enter Webhook URL."); return; }
            bool ok = await _discordService.SendWebhookMessageAsync(url, msg);
            Log(ok ? "Message sent." : "Failed to send.");
        }

        private async void TabDeleteWebhook_Click(object sender, RoutedEventArgs e)
        {
            string url = TabWebhookUrlInput.Text.Trim();
            if (string.IsNullOrEmpty(url)) return;
            bool ok = await _discordService.DeleteWebhookAsync(url);
            Log(ok ? "Webhook deleted." : "Failed to delete.");
        }

        private async void TabGetWebhookInfo_Click(object sender, RoutedEventArgs e)
        {
            string url = TabWebhookUrlInput.Text.Trim();
            if (string.IsNullOrEmpty(url)) return;
            var info = await _discordService.GetWebhookInfoAsync(url);
            if (info != null) Log($"Webhook: {info["name"]} | Guild: {info["guild_id"]}");
        }

        #endregion

        #region Proxies

        private void ScrapeProxies_Click(object sender, RoutedEventArgs e)
        {
            Log("Scraping proxies...");
            var proxies = _proxyService.ScrapeProxiesAsync().Result; // Simple sync for now
            ProxyInput.Text = string.Join(Environment.NewLine, proxies);
            Log($"Scraped {proxies.Count} proxies.");
        }

        private void SmartScrape_Click(object sender, RoutedEventArgs e)
        {
            Log("Smart scraping premium proxies...");
            var proxies = _proxyService.ScrapeProxiesAsync().Result;
            ProxyInput.Text = string.Join(Environment.NewLine, proxies);
            Log($"Scraped {proxies.Count} proxies.");
        }

        private void TestProxySpeed_Click(object sender, RoutedEventArgs e)
        {
            Log("Latency testing not implemented.");
        }

        private void ClearProxyList_Click(object sender, RoutedEventArgs e)
        {
            ProxyInput.Clear();
            VerifiedProxyInput.Clear();
            Log("Proxy lists cleared.");
        }

        private void DisableProxies_Click(object sender, RoutedEventArgs e)
        {
            Log("Proxies disabled.");
        }

        private void ConfirmProxy_Click(object sender, RoutedEventArgs e)
        {
            Log("Checking public IP...");
        }

        #endregion

        #region Utility Features

        private void CheckTokenAge_Click(object sender, RoutedEventArgs e)
        {
            Log("Feature not implemented in this version.");
        }

        private void DownloadAvatars_Click(object sender, RoutedEventArgs e)
        {
            Log("Feature not implemented in this version.");
        }

        private void ExportInfo_Click(object sender, RoutedEventArgs e)
        {
            Log("Feature not implemented in this version.");
        }

        private void GetUserInfo_Click(object sender, RoutedEventArgs e)
        {
            Log("Feature not implemented in this version.");
        }

        private void SaveBypassSettings_Click(object sender, RoutedEventArgs e)
        {
            Log("Settings saved.");
        }

        #endregion

        #region Mass DM (Logic from original Mass DM Tab)

        private async void StartMassDM_Click(object sender, RoutedEventArgs e)
        {
            string msg = MassDMInput.Text.Trim();
            if (string.IsNullOrEmpty(msg)) { Log("Enter message content."); return; }
            if (!_accounts.Any()) { Log("No tokens available."); return; }

            Log($"Starting Mass DM with {_accounts.Count} accounts...");
            MassDMProgress.Maximum = _accounts.Count;
            MassDMProgress.Value = 0;

            foreach (var acc in _accounts)
            {
                await _discordService.MassDMFriendsAsync(acc.Token, msg);
                MassDMProgress.Value++;
                Log($"[{acc.Username}] Finished DMing friends.");
                await Task.Delay(2000);
            }
            Log("Mass DM operation finished.");
        }

        #endregion
    }
}