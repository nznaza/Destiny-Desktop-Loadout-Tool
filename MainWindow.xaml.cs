using DotNetBungieAPI;
using DotNetBungieAPI.DefinitionProvider.Sqlite;
using DotNetBungieAPI.Extensions;
using DotNetBungieAPI.Models;
using DotNetBungieAPI.Models.Authorization;
using DotNetBungieAPI.Models.Destiny;
using DotNetBungieAPI.Models.Destiny.Definitions.InventoryItems;
using DotNetBungieAPI.Models.Destiny.Responses;
using DotNetBungieAPI.Models.Exceptions;
using DotNetBungieAPI.Models.Requests;
using DotNetBungieAPI.Service.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using NAudio.Wave;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using static DestinyLoadoutTool.WindowUserMessage;



namespace DestinyLoadoutTool
{
    public class DisplayActivity(string name, uint hash)
    {
        public string? Name = name;
        public uint Hash = hash;
    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly WaveOutEvent outputDevice;
        private readonly Random RandGen = new Random();
        private readonly IEnumerable<UnmanagedMemoryStream> soundsDone;
        private readonly IEnumerable<UnmanagedMemoryStream> soundsReady;
        private readonly IEnumerable<UnmanagedMemoryStream> soundsError;
        private AuthorizationTokenData? currentToken;
        readonly string ManifestFolder = Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory)!, "Manifest");
        private readonly HttpClientHandler handler = new D2LTHttpClientHandler();
        private readonly HttpClient httpClient;
        private readonly IBungieClient client;
        private AuthorizationTokenData? authorizationToken;
        private readonly Task ManifestDownload;
        private readonly ServiceCollection services = new ServiceCollection();

        private ObservableCollection<D2LTLoadout> _DIMLoadouts = [];
        public ObservableCollection<D2LTLoadout> DIMLoadouts
        {
            get
            {
                return _DIMLoadouts;
            }
            set
            {
                _DIMLoadouts = value;
                UpdateLoadoutsForCharacter();
            }
        }

        public ObservableCollection<D2LTLoadout>? SelectedCharacterLoadout { get; set; }

        private DisplayCharacterItem? SelectedCharacter
        {
            get => characters.SelectedItem as DisplayCharacterItem;

        }
        private DisplayMembershipItem? SelectedMembership
        {
            get => profiles.SelectedItem as DisplayMembershipItem;
        }

        List<D2LTInventoryItem> Vault = [];
        Dictionary<long, List<D2LTInventoryItem>> CharacterInventories = [];
        private readonly bool TRY_TRANSFER_UNEQUIPPED = true;

        public event PropertyChangedEventHandler? PropertyChanged;

        List<D2LTInventoryItem>? CurrentCharacterInventory
        {
            get
            {
                return SelectedCharacter is not null ? CharacterInventories.GetValueOrDefault(SelectedCharacter.CharacterId) : null;
            }
        }


        public MainWindow()
        {
            InitializeComponent();
            try
            {
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.Debug()
                    .WriteTo.File("Log.txt", restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information)
                    .WriteTo.EventLog("D2LT", "Application", manageEventSource: true, restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Error)
                    .CreateLogger();
            }
            catch
            {
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.Debug()
                    .WriteTo.File("Log.txt", restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information)
                    .CreateLogger();

            }
            Serilog.Debugging.SelfLog.Enable(msg => Debug.WriteLine(msg));

            services.AddLogging(builder => builder.Services.AddSerilog());

            outputDevice = new WaveOutEvent();
            ResourceManager resourceManager = new ResourceManager(App.ResourceAssembly.GetName().Name + ".g", Assembly.GetExecutingAssembly());
            Dictionary<string, UnmanagedMemoryStream> resources = resourceManager.GetResourceSet(CultureInfo.CurrentUICulture, true, true)!.Cast<DictionaryEntry>().ToDictionary(r => r.Key.ToString()!, r => (UnmanagedMemoryStream)r.Value!); ;
            soundsDone = resources.Where(x => x.Key.ToString()!.StartsWith("resources/done")).Select(x => x.Value!);
            soundsReady = resources.Where(x => x.Key.ToString()!.StartsWith("resources/ready")).Select(x => x.Value!);
            soundsError = resources.Where(x => x.Key.ToString()!.StartsWith("resources/error")).Select(x => x.Value!);

            int clientID = 48057;
            string clientAPIKey = "4ced7f1d15e64219ab6cd4281fdc4838";
            httpClient = new HttpClient(handler);
            client = BungieApiBuilder.GetApiClient((config) =>
            {
                config.DotNetBungieApiHttpClient.ConfigureDefaultHttpClient(httpClientConfig => { httpClientConfig.UseExternalHttpClient(httpClient); });
                config.ClientConfiguration.UsedLocales.Add(BungieLocales.EN);
                config.ClientConfiguration.ClientId = clientID;
                config.ClientConfiguration.ApiKey = clientAPIKey;
                config.ClientConfiguration.ApplicationScopes = DotNetBungieAPI.Models.Applications.ApplicationScopes.ReadBasicUserProfile | DotNetBungieAPI.Models.Applications.ApplicationScopes.MoveEquipDestinyItems;
                config.DefinitionProvider.UseSqliteDefinitionProvider(settings => { settings.AutoUpdateManifestOnStartup = true; settings.FetchLatestManifestOnInitialize = true; settings.ManifestFolderPath = ManifestFolder; });

            }, null, services);
            LoginLink.NavigateUri = new Uri(client.ApiHttpClient.GetAuthLink(clientID, string.Empty));

            ManifestDownload = Task.Run(GetManifest);

            if (Settings.Default.Expires > DateTime.UtcNow)
            {
                authorizationToken = new AuthorizationTokenData() { AccessToken = Settings.Default.AccessToken };
                GetPlayerMemberships();
            }
            else
            {
                MessageBox.Show("Please Login again", "Login Expired", MessageBoxButton.OK, MessageBoxImage.Hand);
            }


            if (!Directory.Exists(ManifestFolder))
                Directory.CreateDirectory(ManifestFolder);

            string executingAssembly = Environment.ProcessPath!;
            bool isValid = URISchemeHandlerExistAndValid(executingAssembly);
            if (!isValid)
            {
                try
                {
                    URISchemeCreate(executingAssembly);
                }
                catch
                {
                    MessageBox.Show("URI Scheme handler could not be installed and is outdated or not present, Please register URI handler");
                }
            }

        }

        private static void URISchemeCreate(string executingAssembly)
        {
            RegistryKey key = Registry.ClassesRoot.CreateSubKey("d2lt");
            key.SetValue("", "URL:d2lt");
            key.SetValue("URL Protocol", "");

            key = key.CreateSubKey("shell");
            key = key.CreateSubKey("open");
            key = key.CreateSubKey("command");
            key.SetValue("", $"\"{executingAssembly}\" \"%1\"");
        }

        #region Internal Methods
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            HwndSource? hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            hwndSource?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if ((WindowMessage)msg == WindowMessage.WM_COPYDATA)
            {
                if (wParam == IntPtr.Zero)
                {
                    COPYDATASTRUCT cd = new COPYDATASTRUCT();
                    cd = (COPYDATASTRUCT)Marshal.PtrToStructure(lParam, typeof(COPYDATASTRUCT))!;
                    new Thread(async () =>
                    {
                        await ValidateProtocolToken(cd.lpData);
                    }).Start();
                }

            }

            return IntPtr.Zero;
        }

        private static bool URISchemeHandlerExistAndValid(string executingAssembly)
        {
            RegistryKey? URISubkey = Registry.ClassesRoot.OpenSubKey("d2lt", false);
            string? URLProtocol = URISubkey?.GetValue("URL Protocol")?.ToString();
            string? openCommand = URISubkey?.OpenSubKey("shell", false)?.OpenSubKey("open", false)?.OpenSubKey("command", false)?.GetValue("")?.ToString();
            return openCommand == $"\"{executingAssembly}\" \"%1\"" && URLProtocol != null;
        }

        #endregion

        #region User Internal Functions Buttons

        #region Bungie User Data
        public async Task ValidateProtocolToken(string authorizationCode)
        {
            authorizationCode = authorizationCode.Replace("d2lt:/?", "");


            string? code = HttpUtility.ParseQueryString(authorizationCode!).Get("code");
            string? error = HttpUtility.ParseQueryString(authorizationCode!).Get("error");
            string? error_description = HttpUtility.ParseQueryString(authorizationCode!).Get("error_description");
            if (string.IsNullOrEmpty(code) && string.IsNullOrEmpty(error))
                code = authorizationCode;
            if (!string.IsNullOrEmpty(error))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Topmost = true;
                    Focus();
                    Topmost = false;
                    MessageBox.Show(error_description, error, MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            else if (!string.IsNullOrEmpty(code))
            {
                await GetTokenAndLoadMemberships(code);
            }

        }

        private void LoginRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            // for .NET Core you need to add UseShellExecute = true
            // see https://learn.microsoft.com/dotnet/api/system.diagnostics.processstartinfo.useshellexecute#property-value
            ProcessStartInfo startInfo = new ProcessStartInfo(e.Uri.AbsoluteUri)
            {
                UseShellExecute = true
            };
            Process.Start(startInfo);
            e.Handled = true;
        }

        private void InstallProtocolButtonClick(object sender, RoutedEventArgs e)
        {
            using Process process = new Process();
            string? executingAssembly = Environment.ProcessPath;
            string exeLocation = $"\"\"{executingAssembly}\"\" \"\"%1\"\"";

            process.StartInfo.FileName = "cmd";
            var cmdCreateProtocol1 = $@"reg add ""HKEY_CLASSES_ROOT\d2lt"" /v """" /d ""URL:d2lt"" /f";
            var cmdCreateProtocol2 = $@"reg add ""HKEY_CLASSES_ROOT\d2lt"" /v ""URL Protocol"" /d """" /f";
            var cmdProtocolOpen = $@"reg add ""HKEY_CLASSES_ROOT\d2lt\shell\open\command"" /v """" /d ""{exeLocation}"" /f";
            var cmdEventCreate = $@"reg add ""HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\EventLog\Application\D2LT"" /v """" /d """" /f";

            process.StartInfo.Arguments = $"/c {cmdCreateProtocol1} && {cmdCreateProtocol2} && {cmdProtocolOpen} && {cmdEventCreate}";

            process.StartInfo.UseShellExecute = true;
            process.StartInfo.Verb = "runas";
            try
            {
                process.Start();
                process.WaitForExit();
                Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Debug()
                .WriteTo.File("Log.txt")
                .WriteTo.EventLog("D2LT", "Application", manageEventSource: true, restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Error)
                .CreateLogger();
            }
            catch { }
        }

        private async void ValidateCodeButtonClick(object sender, RoutedEventArgs e)
        {
            await GetTokenAndLoadMemberships(Code.Text);
            Code.Text = string.Empty;
        }
        #endregion

        #region HotSwap Buttons
        private void ToggleButtonClick(object s, RoutedEventArgs e)
        {
            ToggleButton sender = (ToggleButton)s;

            if (sender.IsChecked == true)
                EnableHotKeys();
            else
                DisableHotKeys();
        }

        private void EnableHotKeys()
        {
            GlobalHotKey.RegisterHotKey("F13", ExecuteSwap, 1);
            GlobalHotKey.RegisterHotKey("F14", ExecuteSwap, 2);
            GlobalHotKey.RegisterHotKey("F15", ExecuteSwap, 3);
            GlobalHotKey.RegisterHotKey("F16", ExecuteSwap, 4);

            UnmanagedMemoryStream sound = soundsReady.ElementAt(RandGen.Next(0, soundsReady.Count()));
            sound.Position = 0;
            outputDevice.Stop();
            outputDevice.Init(new Mp3FileReader(sound));
            outputDevice.Play();
        }

        private void DisableHotKeys()
        {
            GlobalHotKey.UnregisterAllHotKeys();
        }
        #endregion

        #region Loadouts
        private void ImportDIMLoadouts(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "DIM backup|*.json;",
                Title = "Select your DIM settings file"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using StreamReader streamReader = new StreamReader(dialog.OpenFile());

                    string DIM_json = streamReader.ReadToEnd();
                    JObject DIM_Data = JObject.Parse(DIM_json);

                    IList<JToken> results = [.. DIM_Data["loadouts"]!.Children()];
                    ManifestDownload.Wait();

                    ObservableCollection<D2LTLoadout> newDIMLoadouts = [];
                    foreach (JToken result in results)
                    {
                        DIMLoadout loadout = result.ToObject<DIMLoadout>()!;
                        if (loadout.DestinyVersion != 2)
                            continue;
                        newDIMLoadouts.Add(new D2LTLoadout(loadout));
                    }
                    DIMLoadouts = newDIMLoadouts;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); ;
                }
            }

        }
        #endregion

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region User-Data Visual Feedback
        private async void ProfilesSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SelectedMembership is not null)
            {
                await LoadCharacters();
            }
        }

        private void CharacterSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateLoadoutsForCharacter();
        }

        private void UpdateLoadoutsForCharacter()
        {
            if (SelectedCharacter is not null)
            {
                SelectedCharacterLoadout = new ObservableCollection<D2LTLoadout>(DIMLoadouts.Where(x => x.PlatformMembershipId == SelectedCharacter?.MembershipId && x.ClassType == SelectedCharacter?.Class).OrderByDescending(x => x.FullLoadout).ThenByDescending(x => x.ArmorLoadout).ThenByDescending(x => x.WeaponsLoadout).ThenBy(x => Regex.Replace(x.Name, @"\d+", m => m.Value.PadLeft(5, '0'))));
            }
            else
            {
                SelectedCharacterLoadout = null;
            }
            NotifyPropertyChanged(nameof(SelectedCharacterLoadout));
            FullLoadout.SortDirection = ListSortDirection.Descending;
        }

        private async Task LoadCharacters()
        {
            try
            {
                LoadingCharacters.Visibility = Visibility.Visible;
                CharacterSelection.IsEnabled = false;
                BungieResponse<DestinyProfileResponse>? destinyProfileResponse = await client.ApiAccess.Destiny2.GetProfile(SelectedMembership!.MembershipType, SelectedMembership!.MembershipId, [DestinyComponentType.Characters]);

                if (destinyProfileResponse?.Response.Characters != null)
                {
                    List<DisplayCharacterItem> charactersItems = destinyProfileResponse.Response.Characters.Data.Select(x => new DisplayCharacterItem { CharacterId = x.Value.CharacterId, MembershipId = SelectedMembership!.MembershipId, Class = x.Value.ClassType, Icon = new BitmapImage(new Uri(new Uri("https://www.bungie.net/"), x.Value.EmblemPath)), Name = $"{x.Value.RaceType} {x.Value.ClassType}", LastPlayed = x.Value.DateLastPlayed }).ToList();
                    characters.ItemsSource = charactersItems;
                    characters.SelectedIndex = charactersItems.IndexOf(charactersItems.MaxBy(x => x.LastPlayed)!);
                }
                LoadingCharacters.Visibility = Visibility.Hidden;
                CharacterSelection.IsEnabled = true;
            }
            catch
            {

            }
        }

        #endregion

        #region Bungie API Calls
        private async Task GetManifest()
        {
            await client.DefinitionProvider.Initialize();
            await client.DefinitionProvider.ReadToRepository(client.Repository);
            D2LTHelpers.ItemDefinitions = client.Repository.GetAll<DestinyInventoryItemDefinition>();
        }

        private async void GetPlayerMemberships()
        {
            try
            {
                BungieResponse<DotNetBungieAPI.Models.User.UserMembershipData> membershipData = await client.ApiAccess.User.GetMembershipDataById(Settings.Default.MembershipId, BungieMembershipType.All);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    List<DisplayMembershipItem> memberships = (membershipData.Response?.DestinyMemberships.Select(x => new DisplayMembershipItem() { Name = x.BungieGlobalDisplayName, MembershipId = x.MembershipId, Icon = new BitmapImage(new Uri(new Uri("https://www.bungie.net/"), x.IconPath)), MembershipType = x.MembershipType })!).ToList();
                    BungieMembershipType? LastSeenDisplayNameType = membershipData.Response?.DestinyMemberships.First().LastSeenDisplayNameType;

                    profiles.ItemsSource = memberships;
                    profiles.SelectedIndex = memberships.FindIndex(x => x.MembershipType == LastSeenDisplayNameType);
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
        }

        private async Task GetInventories()
        {
            try
            {
                var ProfileResponse = (await client.ApiAccess.Destiny2.GetProfile(SelectedMembership!.MembershipType, SelectedMembership.MembershipId, [DestinyComponentType.ProfileInventories, DestinyComponentType.CharacterEquipment, DestinyComponentType.CharacterInventories, DestinyComponentType.ItemInstances, DestinyComponentType.ItemSockets]));
                Vault = D2LTHelpers.GetD2LTInventoryItems(ProfileResponse.Response.ProfileInventory.Data.Items, ProfileResponse.Response.ItemComponents.Sockets.Data);
                CharacterInventories = ProfileResponse.Response.CharacterEquipment.Data
                    .ToDictionary(characterInventory => characterInventory.Key,
                                  characterInventory => D2LTHelpers.GetD2LTInventoryItems(characterInventory.Value.Items, ProfileResponse.Response.ItemComponents.Sockets.Data)
                                      .Concat(D2LTHelpers.GetD2LTInventoryItems(ProfileResponse.Response.CharacterInventories.Data[characterInventory.Key].Items, ProfileResponse.Response.ItemComponents.Sockets.Data))
                                      .ToList());


            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
        }

        private async Task<bool> GetAuthTokenFromCode(string code)
        {
            try
            {
                currentToken = await client.Authorization.GetAuthTokenAsync(code);
                authorizationToken = new AuthorizationTokenData() { AccessToken = currentToken.AccessToken };
                Settings.Default.AccessToken = currentToken.AccessToken;
                Settings.Default.Expires = currentToken.FirstReceived.AddSeconds(currentToken.ExpiresIn!);
                Settings.Default.RefreshToken = currentToken.RefreshToken;
                Settings.Default.RefreshExpires = currentToken.FirstReceived.AddSeconds(currentToken.RefreshExpiresIn!);
                Settings.Default.MembershipId = currentToken.MembershipId;
                Settings.Default.Save();
                return true;
            }
            catch (BungieNetAuthorizationErrorException ex)
            {
                MessageBox.Show(ex.Error!.ErrorDescription, ex.Error.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return false;
        }

        private async Task GetTokenAndLoadMemberships(string code)
        {
            if (await GetAuthTokenFromCode(code))
            {
                GetPlayerMemberships();
                MessageBox.Show("Logged in", "Token Acquired", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async Task<bool> ChangeInGameLoadout(int id, DisplayCharacterItem character, DisplayMembershipItem membership)
        {
            bool success = false;
            try
            {
                BungieResponse<int> result = await client.ApiAccess.Destiny2.EquipLoadout(new DestinyLoadoutActionRequest(id, character.CharacterId, membership.MembershipType), authorizationToken!);
                success = result.IsSuccessfulResponseCode;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return success;
        }

        private async Task TransferToCharacter(D2LTItem item, long Character)
        {
            var transferResult = await client.ApiAccess.Destiny2.TransferItem(new DestinyItemTransferRequest(item.Hash, 1, false, (long)item.InstanceId!, Character, SelectedMembership!.MembershipType), authorizationToken!);
            if (transferResult.ErrorCode == PlatformErrorCodes.Success)
            {
                var d2ltItem = Vault!.First(x => x.InstanceId == item.InstanceId);
                Vault.Remove(d2ltItem);
                CurrentCharacterInventory!.Add(d2ltItem);
            }
            else
            {
                Log.Debug(transferResult.ErrorCode.ToString());
                _ = Task.Run(() =>
                {
                    MessageBox.Show($"Error Transferring {item.Name}", transferResult.ErrorCode.ToString());
                });
            }
        }

        private async Task TransferToVault(D2LTInventoryItem item, long Character)
        {
            var transferResult = await client.ApiAccess.Destiny2.TransferItem(new DestinyItemTransferRequest(item.Hash, 1, true, (long)item.InstanceId!, Character, SelectedMembership!.MembershipType), authorizationToken!);
            if (transferResult.ErrorCode == PlatformErrorCodes.Success)
            {
                var d2ltItem = CurrentCharacterInventory!.First(x => x.InstanceId == item.InstanceId);
                CurrentCharacterInventory!.Remove(d2ltItem);
                Vault.Add(d2ltItem);
            }
            else
            {
                Log.Information($"Error Transferring {item.Name}", transferResult.ErrorCode.ToString());
                _ = Task.Run(() =>
                {
                    MessageBox.Show($"Error Transferring {item.Name}", transferResult.ErrorCode.ToString());
                });
            }
        }

        private async Task<bool> EquipPerk(D2LTItem equipment, DestinyInventoryItemDefinition perk, int Slot)
        {
            var item = CurrentCharacterInventory!.FirstOrDefault(x => x.InstanceId == equipment.InstanceId);
            bool result;

            if (item is null)
            {
                Log.Information($"({equipment.InstanceId}) is not in character");
                return false;
            }

            Log.Information($"{item.Name} ({equipment.InstanceId}) equipping {perk.DisplayProperties.Name} ({perk.Hash}) in slot {Slot}");
            if (item!.Plugs[Slot]!.Hash == perk.Hash)
            {
                return true;
            }
            var EquipModResult = await client.ApiAccess.Destiny2.InsertSocketPlugFree(new DestinyInsertPlugsFreeActionRequest() { ItemId = (long)equipment.InstanceId!, CharacterId = SelectedCharacter!.CharacterId, MembershipType = SelectedMembership!.MembershipType, Plug = new DestinyInsertPlugsRequestEntry(Slot, DestinySocketArrayType.Default, perk.Hash) }, authorizationToken!);
            if (EquipModResult.ErrorCode != PlatformErrorCodes.Success && EquipModResult.ErrorCode != PlatformErrorCodes.DestinySocketAlreadyHasPlug)
                Log.Warning(EquipModResult.ErrorCode.ToString());
            result = EquipModResult.ErrorCode == PlatformErrorCodes.Success || EquipModResult.ErrorCode == PlatformErrorCodes.DestinySocketAlreadyHasPlug;

            if (result)
            {
                CurrentCharacterInventory!.Remove(item);
                item.PlugsHashes[Slot] = perk.Hash;
                CurrentCharacterInventory!.Add(item);
            }

            return result;
        }
        #endregion

        #region Main functions 
        private async void ExecuteSwap(int id)
        {
            if (characters.SelectedItem is DisplayCharacterItem character && profiles.SelectedItem is DisplayMembershipItem membership)
            {
                UnmanagedMemoryStream sound;

                if (await ChangeInGameLoadout(id, character, membership))
                {
                    sound = soundsDone.ElementAt(RandGen.Next(0, soundsDone.Count()));
                }
                else
                {
                    sound = soundsError.ElementAt(RandGen.Next(0, soundsError.Count()));

                }
                sound.Position = 0;
                outputDevice.Stop();
                outputDevice.Init(new Mp3FileReader(sound));
                outputDevice.Play();
            }
        }

        private async void EquipLoadouts(object sender, RoutedEventArgs e)
        {
            if (SelectedCharacterLoadout is not null)
            {
                IEnumerable<D2LTLoadout> selectedLoadouts = SelectedCharacterLoadout.Where(x => x.Selected);

                var EquippedItems = selectedLoadouts.SelectMany(x => x.Equipped).DistinctBy(x => x.InstanceId);
                var EquippedKineticWeapons = EquippedItems.Where(x => x.Slot == CharacterEquippingBlockSlot.KineticWeapon);
                var EquippedEnergyWeapons = EquippedItems.Where(x => x.Slot == CharacterEquippingBlockSlot.EnergyWeapon);
                var EquippedPowerWeapons = EquippedItems.Where(x => x.Slot == CharacterEquippingBlockSlot.PowerWeapon);
                var EquippedHelmets = EquippedItems.Where(x => x.Slot == CharacterEquippingBlockSlot.Helmet);
                var EquippedGauntlets = EquippedItems.Where(x => x.Slot == CharacterEquippingBlockSlot.Gauntlet);
                var EquippedChests = EquippedItems.Where(x => x.Slot == CharacterEquippingBlockSlot.ChestArmor);
                var EquippedLegArmors = EquippedItems.Where(x => x.Slot == CharacterEquippingBlockSlot.LegArmor);
                var EquippedClassArmors = EquippedItems.Where(x => x.Slot == CharacterEquippingBlockSlot.ClassArmor);

                var UnequippedItems = selectedLoadouts.SelectMany(x => x.Unequipped).DistinctBy(x => x.InstanceId).ExceptBy(EquippedItems.Select(x => x.InstanceId), x => x.InstanceId);
                var UnequippedKineticWeapons = UnequippedItems.Where(x => x.Slot == CharacterEquippingBlockSlot.KineticWeapon);
                var UnequippedEnergyWeapons = UnequippedItems.Where(x => x.Slot == CharacterEquippingBlockSlot.EnergyWeapon);
                var UnequippedPowerWeapons = UnequippedItems.Where(x => x.Slot == CharacterEquippingBlockSlot.PowerWeapon);
                var UnequippedHelmets = UnequippedItems.Where(x => x.Slot == CharacterEquippingBlockSlot.Helmet);
                var UnequippedGauntlets = UnequippedItems.Where(x => x.Slot == CharacterEquippingBlockSlot.Gauntlet);
                var UnequippedChests = UnequippedItems.Where(x => x.Slot == CharacterEquippingBlockSlot.ChestArmor);
                var UnequippedLegArmors = UnequippedItems.Where(x => x.Slot == CharacterEquippingBlockSlot.LegArmor);
                var UnequippedClassArmors = UnequippedItems.Where(x => x.Slot == CharacterEquippingBlockSlot.ClassArmor);

                var TransferKineticWeapons = EquippedKineticWeapons.ToList();
                var TransferEnergyWeapons = EquippedEnergyWeapons.ToList();
                var TransferPowerWeapons = EquippedPowerWeapons.ToList();
                var TransferHelmets = EquippedHelmets.ToList();
                var TransferGauntlets = EquippedGauntlets.ToList();
                var TransferChests = EquippedChests.ToList();
                var TransferLegArmors = EquippedLegArmors.ToList();
                var TransferClassArmors = EquippedClassArmors.ToList();

                if (TransferKineticWeapons.Count > 10)
                {
                    MessageBox.Show("Too many Kinetic Weapons");
                    return;
                }
                if (TransferEnergyWeapons.Count > 10)
                {
                    MessageBox.Show("Too many Energy Weapons");
                    return;
                }
                if (TransferPowerWeapons.Count > 10)
                {
                    MessageBox.Show("Too many Power Weapons");
                    return;
                }
                if (TransferHelmets.Count > 10)
                {
                    MessageBox.Show("Too many Helmets");
                    return;
                }
                if (TransferGauntlets.Count > 10)
                {
                    MessageBox.Show("Too many Gauntlets");
                    return;
                }
                if (TransferChests.Count > 10)
                {
                    MessageBox.Show("Too many Chest Armors");
                    return;
                }
                if (TransferLegArmors.Count > 10)
                {
                    MessageBox.Show("Too many Leg Armors");
                    return;
                }
                if (TransferClassArmors.Count > 10)
                {
                    MessageBox.Show("Too many Class Armors");
                    return;
                }

                if (TRY_TRANSFER_UNEQUIPPED)
                {
                    if (EquippedKineticWeapons.Count() + UnequippedKineticWeapons.Count() <= 10)
                        TransferKineticWeapons.AddRange(UnequippedKineticWeapons);

                    if (EquippedEnergyWeapons.Count() + UnequippedEnergyWeapons.Count() <= 10)
                        TransferEnergyWeapons.AddRange(UnequippedEnergyWeapons);

                    if (EquippedPowerWeapons.Count() + UnequippedPowerWeapons.Count() <= 10)
                        TransferPowerWeapons.AddRange(UnequippedPowerWeapons);

                    if (EquippedHelmets.Count() + UnequippedHelmets.Count() <= 10)
                        TransferHelmets.AddRange(UnequippedHelmets);

                    if (EquippedGauntlets.Count() + UnequippedGauntlets.Count() <= 10)
                        TransferGauntlets.AddRange(UnequippedGauntlets);

                    if (EquippedChests.Count() + UnequippedChests.Count() <= 10)
                        TransferChests.AddRange(UnequippedChests);

                    if (EquippedLegArmors.Count() + UnequippedLegArmors.Count() <= 10)
                        TransferLegArmors.AddRange(UnequippedLegArmors);

                    if (EquippedClassArmors.Count() + UnequippedClassArmors.Count() <= 10)
                        TransferClassArmors.AddRange(UnequippedClassArmors);
                }

                List<D2LTItem> ItemsToTransfer = [];
                ItemsToTransfer.AddRange(TransferKineticWeapons);
                ItemsToTransfer.AddRange(TransferEnergyWeapons);
                ItemsToTransfer.AddRange(TransferPowerWeapons);
                ItemsToTransfer.AddRange(TransferHelmets);
                ItemsToTransfer.AddRange(TransferGauntlets);
                ItemsToTransfer.AddRange(TransferChests);
                ItemsToTransfer.AddRange(TransferLegArmors);
                ItemsToTransfer.AddRange(TransferClassArmors);


                await TransferItemsToCharacter(ItemsToTransfer);

                foreach (var loadout in selectedLoadouts)
                {
                    if (await EquipLoadout(loadout))
                    {
                        LoadingAll.Visibility = Visibility.Visible;
                        await EquipLoadoutMods(loadout);
                        LoadingAll.Visibility = Visibility.Hidden;
                        MessageBox.Show($"{loadout.Name} Equipped", "Loadout Equipped", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                    }
                }
            }

        }

        private async Task TransferItemsToCharacter(List<D2LTItem> ItemsToTransfer)
        {
            await GetInventories();

            var Unused = CurrentCharacterInventory!.ExceptBy(ItemsToTransfer.Select(x => x.InstanceId), x => x.InstanceId)!.Where(x => (x.TransferStatus & TransferStatuses.NotTransferrable) == 0).ToList();
            var ItemsInInventory = CurrentCharacterInventory!.IntersectBy(ItemsToTransfer.Select(x => x.InstanceId), x => x.InstanceId)!.Where(x => (x.TransferStatus & TransferStatuses.NotTransferrable) == 0).ToList();
            var YetToTransfer = ItemsToTransfer.ExceptBy(ItemsInInventory.Select(x => x.InstanceId), x => x.InstanceId).ToList();

            foreach (var item in Unused)
            {
                try
                {
                    if (item.TransferStatus == TransferStatuses.ItemIsEquipped)
                        continue;
                    await TransferToVault(item, SelectedCharacter!.CharacterId);
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message);
                    MessageBox.Show(ex.Message);
                    return;
                }

            }

            foreach (var item in YetToTransfer)
            {
                try
                {
                    await TransferToCharacter(item, SelectedCharacter!.CharacterId);
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message);
                    MessageBox.Show(ex.Message);
                    return;
                }
            }
        }

        private async Task<bool> EquipLoadout(D2LTLoadout loadout)
        {
            try
            {

                long[] yetToEquip = loadout.Equipped.Select(x => (long)x.InstanceId!).ExceptBy(SelectedCharacterLoadout!.SelectMany(x => x.Equipped).Select(x => x.InstanceId), x => x).ToArray();
                var equipResult = await client.ApiAccess.Destiny2.EquipItems(new DestinyItemSetActionRequest(yetToEquip, SelectedCharacter!.CharacterId, SelectedMembership!.MembershipType), authorizationToken!);
                if (equipResult.ErrorCode == PlatformErrorCodes.Success)
                {
                    return true;
                }
                else
                {
                    Log.Information(equipResult.ErrorCode.ToString());
                    _ = Task.Run(() =>
                    {
                        MessageBox.Show($"Error Equipping items for {loadout.Name}", equipResult.ErrorCode.ToString());
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                MessageBox.Show(ex.Message);
                return false;
            }
            return false;
        }

        private async Task EquipLoadoutMods(D2LTLoadout loadout)
        {
            try
            {
                foreach (var equipmentOverride in loadout.Equipped.Where(x => x.SocketOverrides is not null))
                {
                    foreach (var a in equipmentOverride.SocketOverrides!)
                    {
                        var transferResult = await EquipPerk(equipmentOverride, D2LTHelpers.ItemDefinitions.First(y => y.Hash == a.Value), (int)a.Key);
                    }
                    if (equipmentOverride.Slot == CharacterEquippingBlockSlot.Subclass)
                    {
                        var equipResult = await client.ApiAccess.Destiny2.EquipItem(new DestinyItemActionRequest((long)equipmentOverride.InstanceId!, SelectedCharacter!.CharacterId, SelectedMembership!.MembershipType), authorizationToken!);
                    }
                }
                
                HashSet<string> RaidMods = new HashSet<string>(loadout.Mods.Select(x => D2LTHelpers.ItemDefinitions.First(y => y.Hash == x).Plug.PlugCategoryIdentifier).Where(x => !StringPlugCategoryIdentifier.RelevantPlugs.Contains(x)));
                foreach (var raidMod in RaidMods)
                {
                    await EquipLoadoutArmorGenericMods(loadout, raidMod);
                }
                await EquipLoadoutArmorGenericMods(loadout, StringPlugCategoryIdentifier.Artifice);

                foreach (var equipment in loadout.Equipped)
                {
                    await UnequipModsCategory(equipment, StringPlugCategoryIdentifier.General);
                }

                await EquipModsSpecificSlot(loadout, CharacterEquippingBlockSlot.Helmet);
                await EquipModsSpecificSlot(loadout, CharacterEquippingBlockSlot.Gauntlet);
                await EquipModsSpecificSlot(loadout, CharacterEquippingBlockSlot.ChestArmor);
                await EquipModsSpecificSlot(loadout, CharacterEquippingBlockSlot.LegArmor);
                await EquipModsSpecificSlot(loadout, CharacterEquippingBlockSlot.ClassArmor);

                await EquipLoadoutArmorGenericMods(loadout, StringPlugCategoryIdentifier.General);

            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unhandled Exception");
                MessageBox.Show(ex.Message);
                return;
            }
        }

        private async Task<bool> UnequipModsCategory(D2LTItem equipment, string categoryIdentifier)
        {
            uint[] emptyPlugHashes = [235531041, // Activity Mod Socket (Activity Ghost Mod, enhancements.ghosts_activity)
                                    3545404847, // Activity Mod Socket (Activity Ghost Mod, enhancements.ghosts_activity_fake)
                                    1390587439, // Default Effect (ship.spawnfx)
                                    1608119540, // Default Emblem (Emblem, emblem.variant)
                                    702981643, // Default Ornament (Restore Defaults, armor_skins_empty)
                                    1959648454, // Default Ornament (exotic_all_skins)
                                    2931483505, // Default Ornament (exotic_all_skins)
                                    2325217837, // Default Shader (Restore Defaults, shader)
                                    4248210736, // Default Shader (Restore Defaults, shader)
                                    2794014115, // Default Weapon Effects (v420.plugs.weapons.masterworks.toggle.vfx)
                                    1675508353, // Economic Mod Socket (Economic Ghost Mod, enhancements.ghosts_economic)
                                    791435474, // Empty Activity Mod Socket (Deprecated Armor Mod, enhancements.activity)
                                    3074755706, // Empty Arrows Socket (crafting.recipes.empty_socket)
                                    2802541735, // Empty Aspect Socket (hunter.arc.aspects)
                                    518663192, // Empty Aspect Socket (hunter.prism.aspects)
                                    1715180370, // Empty Aspect Socket (hunter.shared.aspects)
                                    3875863236, // Empty Aspect Socket (hunter.solar.aspects)
                                    4037640975, // Empty Aspect Socket (hunter.strand.aspects)
                                    2801436041, // Empty Aspect Socket (hunter.void.aspects)
                                    2789698445, // Empty Aspect Socket (titan.arc.aspects)
                                    3635963100, // Empty Aspect Socket (titan.prism.aspects)
                                    321296654, // Empty Aspect Socket (titan.shared.aspects)
                                    3416473448, // Empty Aspect Socket (titan.solar.aspects)
                                    3207138885, // Empty Aspect Socket (titan.strand.aspects)
                                    662916127, // Empty Aspect Socket (titan.void.aspects)
                                    3472368310, // Empty Aspect Socket (warlock.arc.aspects)
                                    1080004479, // Empty Aspect Socket (warlock.prism.aspects)
                                    3819991001, // Empty Aspect Socket (warlock.shared.aspects)
                                    2352766955, // Empty Aspect Socket (warlock.solar.aspects)
                                    2164407902, // Empty Aspect Socket (warlock.strand.aspects)
                                    3834374608, // Empty Aspect Socket (warlock.void.aspects)
                                    1007199041, // Empty Barrels Socket (crafting.recipes.empty_socket)
                                    1527687869, // Empty Batteries Socket (crafting.recipes.empty_socket)
                                    2836298415, // Empty Blades Socket (crafting.recipes.empty_socket)
                                    3471922734, // Empty Bowstrings Socket (crafting.recipes.empty_socket)
                                    1498917124, // Empty Catalyst Socket (v400.empty.exotic.masterwork)
                                    1649663920, // Empty Catalyst Socket (v400.empty.exotic.masterwork)
                                    1961918267, // Empty Deepsight Socket (crafting.plugs.weapons.mods.extractors)
                                    253922071, // Empty Enhancement Socket (crafting.plugs.weapons.mods.enhancers)
                                    1826298670, // Empty Fragment Socket (shared.arc.fragments)
                                    3363787531, // Empty Fragment Socket (shared.arc.fragments)
                                    3251563851, // Empty Fragment Socket (shared.fragments)
                                    2808665197, // Empty Fragment Socket (shared.prism.fragments)
                                    3720092164, // Empty Fragment Socket (shared.prism.fragments)
                                    424005861, // Empty Fragment Socket (shared.solar.fragments)
                                    4205702044, // Empty Fragment Socket (shared.solar.fragments)
                                    330751742, // Empty Fragment Socket (shared.stasis.trinkets)
                                    1618645595, // Empty Fragment Socket (shared.strand.fragments)
                                    2111549310, // Empty Fragment Socket (shared.strand.fragments)
                                    770211541, // Empty Fragment Socket (shared.void.fragments)
                                    1372656116, // Empty Fragment Socket (shared.void.fragments)
                                    1372656117, // Empty Fragment Socket (shared.void.fragments)
                                    1219897208, // Empty Frames Socket (crafting.recipes.empty_socket)
                                    366474809, // Empty Grips Socket (crafting.recipes.empty_socket)
                                    1779961758, // Empty Guards Socket (crafting.recipes.empty_socket)
                                    1232390730, // Empty Hafts Socket (crafting.recipes.empty_socket)
                                    3057124503, // Empty Magazines Socket (crafting.recipes.empty_socket)
                                    3803329707, // Empty Magazines Socket (crafting.recipes.empty_socket)
                                    2909846572, // Empty Memento Socket (crafting.recipes.empty_socket)
                                    481675395, // Empty Mod Socket (General Armor Mod, deprecated)
                                    573150099, // Empty Mod Socket (Leg Armor Mod, deprecated)
                                    807186981, // Empty Mod Socket (Helmet Armor Mod, deprecated)
                                    1137289077, // Empty Mod Socket (Class Item Armor Mod, deprecated)
                                    1659393211, // Empty Mod Socket (Chest Armor Mod, deprecated)
                                    1844045567, // Empty Mod Socket (Arms Armor Mod, deprecated)
                                    4173924323, // Empty Mod Socket (Artifice Armor Mod, enhancements.artifice)
                                    4055462131, // Empty Mod Socket (Deep Stone Crypt Raid Mod, enhancements.raid_descent)
                                    706611068, // Empty Mod Socket (Garden of Salvation Raid Mod, enhancements.raid_garden)
                                    3738398030, // Empty Mod Socket (Vault of Glass Armor Mod, enhancements.raid_v520)
                                    2447143568, // Empty Mod Socket (Vow of the Disciple Raid Mod, enhancements.raid_v600)
                                    1728096240, // Empty Mod Socket (King's Fall Mod, enhancements.raid_v620)
                                    4144354978, // Empty Mod Socket (Root of Nightmares Armor Mod, enhancements.raid_v700)
                                    717667840, // Empty Mod Socket (Crota's End Mod, enhancements.raid_v720)
                                    4059283783, // Empty Mod Socket (Salvation's Edge Armor Mod, enhancements.raid_v800)
                                    720857, // Empty Mod Socket (Legacy Armor Mod, enhancements.season_forge)
                                    1180997867, // Empty Mod Socket (Nightmare Mod, enhancements.season_maverick)
                                    2620967748, // Empty Mod Socket (Legacy Armor Mod, enhancements.season_maverick)
                                    4106547009, // Empty Mod Socket (Legacy Armor Mod, enhancements.season_opulence)
                                    1679876242, // Empty Mod Socket (Last Wish Raid Mod, enhancements.season_outlaw)
                                    3625698764, // Empty Mod Socket (Legacy Armor Mod, enhancements.season_outlaw)
                                    1182150429, // Empty Mod Socket (Armor Mod, enhancements.universal)
                                    1835369552, // Empty Mod Socket (enhancements.universal)
                                    2600899007, // Empty Mod Socket (Armor Mod, enhancements.universal)
                                    3851138800, // Empty Mod Socket (Armor Mod, enhancements.universal)
                                    1285086138, // Empty Mod Socket (Arms Artifact Mod, enhancements.v2_arms)
                                    3820147479, // Empty Mod Socket (Arms Armor Mod, enhancements.v2_arms)
                                    1803434835, // Empty Mod Socket (Chest Armor Mod, enhancements.v2_chest)
                                    3965359154, // Empty Mod Socket (Chest Artifact Mod, enhancements.v2_chest)
                                    3200810407, // Empty Mod Socket (Class Item Armor Mod, enhancements.v2_class_item)
                                    4059708161, // Empty Mod Socket (Class Item Artifact Mod, enhancements.v2_class_item)
                                    1980618587, // Empty Mod Socket (General Armor Mod, enhancements.v2_general)
                                    787139317, // Empty Mod Socket (Helmet Artifact Mod, enhancements.v2_head)
                                    1078080765, // Empty Mod Socket (Helmet Armor Mod, enhancements.v2_head)
                                    79843948, // Empty Mod Socket (Leg Artifact Mod, enhancements.v2_legs)
                                    2269836811, // Empty Mod Socket (Leg Armor Mod, enhancements.v2_legs)
                                    144338558, // Empty Mod Socket (Weapon Mod, v400.weapon.mod_empty)
                                    2323986101, // Empty Mod Socket (Weapon Mod, v400.weapon.mod_empty)
                                    4024425661, // Empty Mod Socket (Armor Mod, v404.armor.fotl.masks.abyss.perks)
                                    51925409, // Empty Scopes Socket (crafting.recipes.empty_socket)
                                    1134447515, // Empty Stocks Socket (crafting.recipes.empty_socket)
                                    469511105, // Empty Traits Socket (crafting.recipes.empty_socket)
                                    2503665585, // Empty Traits Socket (crafting.recipes.empty_socket)
                                    819232495, // Empty Tubes Socket (crafting.recipes.empty_socket)
                                    4043342755, // Empty Weapon Level Boost Socket (crafting.plugs.weapons.mods.transfusers.level)
                                    4216349042, // Experience Mod Socket (Experience Ghost Mod, enhancements.ghosts_experience)
                                    2085536058, // Flickering Blessing Mod Socket (Flickering Blessing Destination Mod, schism_boons.destination_mods.efficiency)
                                    1656746282, // Locked Artifice Socket (Artifice Armor Mod, enhancements.artifice.exotic)
                                    3725942064, // Pale Blessing Mod Socket (Pale Blessing Destination Mod, schism_boons.destination_mods.playstyle)
                                    1692473496, // Protocol Socket (v700.weapons.mods.mission_avalon)
                                    760030801, // Tracking Mod Socket (Tracking Ghost Mod, enhancements.ghosts_tracking)
                                    ];
            var emptyPlugs = emptyPlugHashes.Select(x => D2LTHelpers.ItemDefinitions.First(y => y.Hash == x));
            var emptyPerk = emptyPlugs.First(x => x?.Plug.PlugCategoryIdentifier == categoryIdentifier && x?.Plug.EnabledRules.Count() > 0);

            int FirstSlot = CurrentCharacterInventory!.FirstOrDefault(x => x.InstanceId == equipment.InstanceId)?.Plugs.Select(x => x?.PlugCategoryIdentifier).ToList().IndexOf(categoryIdentifier) ?? -1;
            int MaxSlot = CurrentCharacterInventory!.FirstOrDefault(x => x.InstanceId == equipment.InstanceId)?.Plugs.Select(x => x?.PlugCategoryIdentifier).ToList().LastIndexOf(categoryIdentifier) ?? -1;
            if (FirstSlot == -1)
                return false;

            bool Unequiped = true;
            for (int Slot = FirstSlot; Slot <= MaxSlot; Slot++)
            {

                Unequiped &= await EquipPerk(equipment, emptyPerk, Slot);
            }
            return Unequiped;
        }

        private async Task EquipModsSpecificSlot(D2LTLoadout loadout, CharacterEquippingBlockSlot EquippingSlot)
        {
            var loadoutMods = loadout.Mods.Select(x => D2LTHelpers.ItemDefinitions.First(y => y.Hash == x));

            var equipment = loadout.Equipped.First(x => x.Slot == EquippingSlot);
            var slotMods = loadoutMods.Where(x => x.Plug.PlugCategoryIdentifier == StringPlugCategoryIdentifier.GetCategoryFromEquippingBlock(EquippingSlot));

            await UnequipModsCategory(equipment, StringPlugCategoryIdentifier.GetCategoryFromEquippingBlock(EquippingSlot));


            var item = CurrentCharacterInventory!.FirstOrDefault(x => x.InstanceId == equipment.InstanceId);
            int Slot = CurrentCharacterInventory!.First(x => x.InstanceId == equipment.InstanceId).Plugs.Select(x => x?.PlugCategoryIdentifier).ToList().IndexOf(StringPlugCategoryIdentifier.GetCategoryFromEquippingBlock(EquippingSlot));
            int slotOffsetEquip = 0;
            foreach (var mod in slotMods)
            {
                var transferResult = await EquipPerk(equipment, mod, Slot + slotOffsetEquip);
                slotOffsetEquip++;
            }
        }

        private async Task EquipLoadoutArmorGenericMods(D2LTLoadout loadout, string PlugIdentifier)
        {
            var generalMods = loadout.Mods.Where(x => D2LTHelpers.ItemDefinitions.First(y => y.Hash == x).Plug.PlugCategoryIdentifier == StringPlugCategoryIdentifier.General).ToArray();


            int i = 0;

            CharacterEquippingBlockSlot[] relevantSlots = [CharacterEquippingBlockSlot.Helmet,
                                                            CharacterEquippingBlockSlot.Gauntlet,
                                                            CharacterEquippingBlockSlot.ChestArmor,
                                                            CharacterEquippingBlockSlot.LegArmor,
                                                            CharacterEquippingBlockSlot.ClassArmor];

            foreach (var slot in relevantSlots)
            {
                var equipment = loadout.Equipped.FirstOrDefault(x => x.Slot == slot);
                if (equipment == null) continue;

                int slotIndex = CurrentCharacterInventory!.First(x => x.InstanceId == equipment.InstanceId).Plugs.Select(x => x?.PlugCategoryIdentifier).ToList().IndexOf(PlugIdentifier);

                if (i < generalMods.Length)
                {
                    var transferResult = await EquipPerk(equipment, D2LTHelpers.ItemDefinitions.First(y => y.Hash == generalMods[i]), slotIndex);
                    if (transferResult)
                        i++;
                }
            }
        }
        #endregion

    }


}
