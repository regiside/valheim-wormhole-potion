using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using Jotunn;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using SoftReferenceableAssets;
using UnityEngine;

namespace RegisideWormholePotion
{
    [BepInPlugin("com.jotunn.ValheimTeleportPlugin", "Teleport to Player (Jotunn)", "1.0.2")]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]

    public class RegisideWormholePotion : BaseUnityPlugin
    {
        public const string PluginGUID = "com.jotunn.RegisideWormholePotion";
        public const string PluginName = "RegisideWormholePotion";
        public const string PluginVersion = "0.0.1";

        // Custom status effect
        private CustomStatusEffect WormholeEffect;

        private const string WormholeEffectName = "WormholeEffect";
        private const string WormholePotionName = "$item_wormholePotion";
        private static ItemDrop.ItemData s_pendingPotion;

        private CustomLocalization Localization;

        private readonly Harmony harmony = new Harmony("com.jotunn.ValheimTeleportPlugin");

        private void Awake()
        {
            AddStatusEffects();
            PrefabManager.OnVanillaPrefabsAvailable += AddClonedItems;
            AddLocalizations();

            harmony.PatchAll();

            Jotunn.Logger.LogInfo("RegisideWormholePotion is loaded");
        }

        void OnDestroy()
        {
            harmony.UnpatchSelf();
            PrefabManager.OnVanillaPrefabsAvailable -= AddClonedItems;
        }

        private void AddLocalizations()
        {
            Localization = LocalizationManager.Instance.GetLocalization();

            Localization.AddTranslation("English", new Dictionary<string, string>
            {
                {"item_wormholePotion", "Potion of Frith’s Bond v1"},
                {"item_wormholePotion_desc", "This elixir binds the threads of fate between companions. With a single sip, the distance melts away, and you are carried to the side of your shield-brothers and sisters, no matter how far the battle has taken them."},
                {"wormhole_effect", "Frith’s Bond"},
                {"wormhole_effect_desc", "Teleport to the location of a chosen ally. Cannot be used within realms barred by the gods."},
                {"wormhole_effectstart", "Choose a friend to be reunited with."},
                {"wormhole_effectstop", "Wormhole effect removed."},
            });
        }

        //Function to check if player can teleport.
        private static bool inventoryCanPortal(Inventory inventory)
        {
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (!item.m_shared.m_teleportable)
                {
                    return false;
                }
            }

            return true;
        }

        // Add new status effects
        private void AddStatusEffects()
        {
            StatusEffect effect = ScriptableObject.CreateInstance<StatusEffect>();
            effect.name = "WormholeEffect";
            effect.m_name = "$wormhole_effect";
            effect.m_tooltip = "$wormhole_effect_desc";
            effect.m_startMessageType = MessageHud.MessageType.Center;
            effect.m_startMessage = "$wormhole_effectstart";
            effect.m_stopMessageType = MessageHud.MessageType.Center;

            WormholeEffect = new CustomStatusEffect(effect, fixReference: false);
            ItemManager.Instance.AddStatusEffect(WormholeEffect);
        }

        //Add Wormhole potion item.
        private void AddClonedItems()
        {
            ItemConfig wormholePotionConfig = new ItemConfig
            {
                Name = "$item_wormholePotion",
                Description = "$item_wormholePotion_desc",
                CraftingStation = CraftingStations.Cauldron,
                MinStationLevel = 2
            };

            wormholePotionConfig.AddRequirement(new RequirementConfig("MushroomYellow", 10));
            wormholePotionConfig.AddRequirement(new RequirementConfig("Raspberry", 3));
            wormholePotionConfig.AddRequirement(new RequirementConfig("Fish2", 1)); // pike

            CustomItem wormholePotion = new CustomItem("WormholePotion", "MeadPoisonResist", wormholePotionConfig);

            GameObject wormholePotionPrefab = wormholePotion.ItemPrefab;
            if (wormholePotionPrefab != null)
            {
                ItemDrop itemDrop = wormholePotionPrefab.GetComponent<ItemDrop>();
                if (itemDrop != null)
                {
                    itemDrop.m_itemData.m_shared.m_food = 0;
                    itemDrop.m_itemData.m_shared.m_foodStamina = 0;
                    itemDrop.m_itemData.m_shared.m_foodBurnTime = 0;
                    itemDrop.m_itemData.m_shared.m_consumeStatusEffect = WormholeEffect.StatusEffect;
                }
            }

            ItemManager.Instance.AddItem(wormholePotion);

            PrefabManager.OnVanillaPrefabsAvailable -= AddClonedItems;
        }

        // Use the same synchronized public-player data that Valheim uses for map pins.
        [HarmonyPatch(typeof(Minimap), "OnMapLeftClick")]
        private static class Minimap_OnMapLeftClick_Patch
        {
            private static bool Prefix(Minimap __instance)
            {
                Player localPlayer = Player.m_localPlayer;

                if (localPlayer == null || !localPlayer.m_seman.HaveStatusEffect(WormholeEffectName.GetHashCode()))
                {
                    return true;
                }

                Vector3 clickPosition = __instance.ScreenToWorldPoint(Input.mousePosition);

                if (!TryFindClosestPublicPlayer(__instance, clickPosition, out ZNet.PlayerInfo targetPlayer))
                {
                    Jotunn.Logger.LogInfo("No public player marker found near the clicked position.");
                    MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "No player found nearby.", 0, (Sprite)null, false);
                    return false;
                }

                Inventory inventory = localPlayer.m_inventory;
                if (s_pendingPotion == null || !inventory.GetAllItems().Contains(s_pendingPotion))
                {
                    Jotunn.Logger.LogWarning("The pending wormhole potion is no longer in the player's inventory.");
                    MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "The wormhole potion is no longer in your inventory.", 0, (Sprite)null, false);
                    s_pendingPotion = null;
                    localPlayer.m_seman.RemoveStatusEffect(WormholeEffectName.GetHashCode());
                    __instance.SetMapMode(Minimap.MapMode.Small);
                    return false;
                }

                if (!localPlayer.TeleportTo(targetPlayer.m_position, localPlayer.transform.rotation, true))
                {
                    Jotunn.Logger.LogWarning($"Teleport to {targetPlayer.m_name} was rejected; the potion was not consumed.");
                    MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "The wormhole cannot open yet. Try again in a moment.", 0, (Sprite)null, false);
                    return false;
                }

                inventory.RemoveItem(s_pendingPotion, 1);
                s_pendingPotion = null;

                string networkRole = ZNet.instance != null && ZNet.instance.IsServer() ? "host" : "client";
                Jotunn.Logger.LogInfo($"Teleport started to {targetPlayer.m_name} ({targetPlayer.m_characterID}) at {targetPlayer.m_position} as {networkRole}.");
                Jotunn.Logger.LogInfo($"Removed one {WormholePotionName} from inventory.");

                __instance.SetMapMode(Minimap.MapMode.Small);

                return false;
            }

            private static bool TryFindClosestPublicPlayer(Minimap minimap, Vector3 clickPosition, out ZNet.PlayerInfo closestPlayer)
            {
                closestPlayer = default(ZNet.PlayerInfo);
                if (ZNet.instance == null)
                {
                    return false;
                }

                var publicPlayers = new List<ZNet.PlayerInfo>();
                ZNet.instance.GetOtherPublicPlayers(publicPlayers);

                // This mirrors the zoom-aware radius Valheim uses when interacting with map pins.
                float selectionRadius = minimap.m_removeRadius * minimap.m_largeZoom * 2f;
                float closestDistance = selectionRadius;
                bool found = false;
                Vector2 clickXZ = new Vector2(clickPosition.x, clickPosition.z);

                foreach (ZNet.PlayerInfo playerInfo in publicPlayers)
                {
                    if (playerInfo.m_characterID == ZDOID.None)
                    {
                        continue;
                    }

                    Vector2 playerXZ = new Vector2(playerInfo.m_position.x, playerInfo.m_position.z);
                    float distance = Vector2.Distance(clickXZ, playerXZ);

                    if (distance <= closestDistance)
                    {
                        closestDistance = distance;
                        closestPlayer = playerInfo;
                        found = true;
                    }
                }

                return found;
            }
        }

        // Patch the Player.ConsumeItem method
        [HarmonyPatch(typeof(Player), "ConsumeItem")]
        public static class Player_ConsumeItem_Patch
        {
            public static bool Prefix(Player __instance, Inventory inventory, ItemDrop.ItemData item, bool checkWorldLevel = false)
            {
                if (item == null || item.m_shared.m_name != WormholePotionName)
                {
                    return true;
                }

                if (__instance != Player.m_localPlayer)
                {
                    return true;
                }

                if (s_pendingPotion != null || __instance.m_seman.HaveStatusEffect(WormholeEffectName.GetHashCode()))
                {
                    MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "Choose a player on the map or close the map to cancel.", 0, (Sprite)null, false);
                    return false;
                }

                if (!inventoryCanPortal(inventory))
                {
                    MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "The weight of the world binds you. Try dropping your ores.", 0, (Sprite)null, false);
                    return false;
                }

                if (ZNet.instance == null || Minimap.instance == null)
                {
                    Jotunn.Logger.LogWarning("The network or minimap is not ready for wormhole targeting.");
                    return false;
                }

                var publicPlayers = new List<ZNet.PlayerInfo>();
                ZNet.instance.GetOtherPublicPlayers(publicPlayers);
                if (publicPlayers.Count == 0)
                {
                    MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "No players are currently sharing their map position.", 0, (Sprite)null, false);
                    return false;
                }

                StatusEffect statusEffect = ObjectDB.instance.GetStatusEffect(WormholeEffectName.GetHashCode());
                StatusEffect wormholeEffect = statusEffect == null ? null : __instance.m_seman.AddStatusEffect(statusEffect);
                if (wormholeEffect == null)
                {
                    Jotunn.Logger.LogWarning("Failed to apply wormhole effect; the potion was not consumed.");
                    return false;
                }

                s_pendingPotion = item;
                Jotunn.Logger.LogInfo("Wormhole targeting began.");

                if (__instance.m_zanim != null)
                {
                    Jotunn.Logger.LogInfo("Playing 'eat' animation.");
                    __instance.m_zanim.SetTrigger("eat");
                }
                else
                {
                    Jotunn.Logger.LogWarning("Player animation controller (m_zanim) is null!");
                }

                if (__instance.m_consumeItemEffects != null)
                {
                    __instance.m_consumeItemEffects.Create(__instance.transform.position, Quaternion.identity);
                }
                else
                {
                    Jotunn.Logger.LogWarning("Consume item effects not found!");
                }

                if (InventoryGui.instance != null && InventoryGui.IsVisible())
                {
                    InventoryGui.instance.Hide();
                }

                Minimap.instance.SetMapMode(Minimap.MapMode.Large);

                return false;
            }
        }

        //Patch the Minimap function
        [HarmonyPatch(typeof(Minimap), "SetMapMode")]
        public static class Minimap_SetMapMode_Patch
        {
            public static void Postfix(Minimap.MapMode mode)
            {
                if (mode == Minimap.MapMode.Large)
                {
                    return;
                }

                s_pendingPotion = null;

                Player localPlayer = Player.m_localPlayer;
                if (localPlayer == null)
                {
                    Jotunn.Logger.LogWarning("Local player not found.");
                    return;
                }

                if (localPlayer.m_seman.HaveStatusEffect(WormholeEffectName.GetHashCode()))
                {
                    localPlayer.m_seman.RemoveStatusEffect(WormholeEffectName.GetHashCode());
                    Jotunn.Logger.LogInfo("Wormhole effect removed by map close.");
                }
            }
        }
    }
}
