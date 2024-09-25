using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using ShopAPI;

namespace ShopExperienceOverload
{
    public class ShopExperienceOverload : BasePlugin
    {
        public override string ModuleName => "[SHOP] Experience Overload";
        public override string ModuleDescription => "";
        public override string ModuleAuthor => "E!N";
        public override string ModuleVersion => "v1.0.1";

        private IShopApi? SHOP_API;
        private const string CategoryName = "ExpOverload";
        public static JObject? JsonExpOverloads { get; private set; }
        private readonly PlayerExpOverload[] playerExpOverloads = new PlayerExpOverload[65];

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            SHOP_API = IShopApi.Capability.Get();
            if (SHOP_API == null) return;

            LoadConfig();
            InitializeShopItems();
            SetupTimersAndListeners();
        }

        private void LoadConfig()
        {
            string configPath = Path.Combine(ModuleDirectory, "../../configs/plugins/Shop/ExpOverload.json");
            if (File.Exists(configPath))
            {
                JsonExpOverloads = JObject.Parse(File.ReadAllText(configPath));
            }
        }

        private void InitializeShopItems()
        {
            if (JsonExpOverloads == null || SHOP_API == null) return;

            SHOP_API.CreateCategory(CategoryName, "Перегрузка опыта");

            var sortedItems = JsonExpOverloads
                .Properties()
                .Select(p => new { Key = p.Name, Value = (JObject)p.Value })
                .OrderBy(p => (int)p.Value["lvl"]!)
                .ToList();

            foreach (var item in sortedItems)
            {
                Task.Run(async () =>
                {
                    int itemId = await SHOP_API.AddItem(item.Key, (string)item.Value["name"]!, CategoryName, (int)item.Value["price"]!, (int)item.Value["sellprice"]!, (int)item.Value["duration"]!);
                    SHOP_API.SetItemCallbacks(itemId, OnClientBuyItem, OnClientSellItem, OnClientToggleItem);
                }).Wait();
            }
        }

        private void SetupTimersAndListeners()
        {
            AddTimer(1.0f, UpdatePlayerExpOverload, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT | CounterStrikeSharp.API.Modules.Timers.TimerFlags.STOP_ON_MAPCHANGE);
            RegisterListener<Listeners.OnClientDisconnect>(playerSlot => playerExpOverloads[playerSlot] = null!);
        }

        public HookResult OnClientBuyItem(CCSPlayerController player, int itemId, string categoryName, string uniqueName, int buyPrice, int sellPrice, int duration, int count)
        {
            if (TryGetItemLevel(uniqueName, out int level))
            {
                playerExpOverloads[player.Slot] = new PlayerExpOverload(level, itemId);
                UpdatePlayerExpOverload();
            }
            else
            {
                Logger.LogError($"{uniqueName} has invalid or missing 'lvl' in config!");
            }
            return HookResult.Continue;
        }

        public HookResult OnClientToggleItem(CCSPlayerController player, int itemId, string uniqueName, int state)
        {
            if (state == 1 && TryGetItemLevel(uniqueName, out int level))
            {
                playerExpOverloads[player.Slot] = new PlayerExpOverload(level, itemId);
            }
            else if (state == 0)
            {
                OnClientSellItem(player, itemId, uniqueName, 0);
            }
            UpdatePlayerExpOverload();
            return HookResult.Continue;
        }

        public HookResult OnClientSellItem(CCSPlayerController player, int itemId, string uniqueName, int sellPrice)
        {
            playerExpOverloads[player.Slot] = null!;
            if (player.InventoryServices != null)
            {
                player.InventoryServices.PersonaDataXpTrailLevel = 0;
                Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInventoryServices");
            }

            return HookResult.Continue;
        }

        private void UpdatePlayerExpOverload()
        {
            foreach (CCSPlayerController player in Utilities.GetPlayers().Where(x => x.IsValid && !x.IsBot && !x.IsHLTV))
            {
                if (player.InventoryServices != null && playerExpOverloads[player.Slot] != null)
                {
                    player.InventoryServices.PersonaDataXpTrailLevel = playerExpOverloads[player.Slot].ExpOverloadLvl;
                    Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInventoryServices");
                }
            }
        }

        private static bool TryGetItemLevel(string uniqueName, out int level)
        {
            level = 0;
            if (JsonExpOverloads != null && JsonExpOverloads.TryGetValue(uniqueName, out var obj) && obj is JObject jsonItem && jsonItem["lvl"] != null && jsonItem["lvl"]!.Type != JTokenType.Null)
            {
                level = (int)jsonItem["lvl"]!;
                return true;
            }
            return false;
        }

        public record class PlayerExpOverload(int ExpOverloadLvl, int ItemID);
    }
}
