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

        // Define the wormHoleActive variable
        //public static bool wormHoleActive { get; private set; }

        private CustomLocalization Localization;

        private readonly Harmony harmony = new Harmony("com.jotunn.ValheimTeleportPlugin");

        private void Awake()
        {
            // Load, create and init your custom mod stuff
            AddStatusEffects();

            PrefabManager.OnVanillaPrefabsAvailable += AddClonedItems; 

            AddLocalizations();
            

            harmony.PatchAll();

            Jotunn.Logger.LogInfo("RegisideWormholePotion is loaded");

        }

        void OnDestroy()
        {
            harmony.UnpatchSelf();
            //PrefabManager.OnVanillaPrefabsAvailable -= AddClonedItems; //suggested by chat gpt.
        }

        private void AddLocalizations()
        {
            Localization = LocalizationManager.Instance.GetLocalization();

            Localization.AddTranslation("English", new Dictionary<string, string>
            {
                {"item_wormholePotion", "Potion of Frith’s Bond"},
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
            // Get the player's inventory
            //Inventory inventory = player.GetInventory();

            // Check each item in the inventory
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (!item.m_shared.m_teleportable) // If the item is not teleportable
                {
                    return false; // Player is carrying unportalable items
                }
            }

            return true; // All items are teleportable
        }

        // Add new status effects
        private void AddStatusEffects()
        {
            StatusEffect effect = ScriptableObject.CreateInstance<StatusEffect>();
            effect.name = "WormholeEffect";
            effect.m_name = "$wormhole_effect";
            //effect.m_icon = AssetUtils.LoadSpriteFromFile("RegisideWormholePotion/assets/reee.png");
            effect.m_tooltip = "$wormhole_effect_desc";
            effect.m_startMessageType = MessageHud.MessageType.Center;
            effect.m_startMessage = "$wormhole_effectstart";
            effect.m_stopMessageType = MessageHud.MessageType.Center;
            effect.m_stopMessage = "$wormhole_effectstop";

            WormholeEffect = new CustomStatusEffect(effect, fixReference: false);  // We dont need to fix refs here, because no mocks were used
            ItemManager.Instance.AddStatusEffect(WormholeEffect);
        }

        //Add Wormhole potion item.
        private void AddClonedItems()
        {
            // Create and add a custom item based on MeadPoisonResist
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

            // Modify the item properties to remove mead attributes
            GameObject wormholePotionPrefab = wormholePotion.ItemPrefab;
            if (wormholePotionPrefab != null)
            {
                ItemDrop itemDrop = wormholePotionPrefab.GetComponent<ItemDrop>();
                if (itemDrop != null)
                {
                    // Reset the food properties
                    itemDrop.m_itemData.m_shared.m_food = 0;
                    itemDrop.m_itemData.m_shared.m_foodStamina = 0;
                    itemDrop.m_itemData.m_shared.m_foodBurnTime = 0;

                    // Add our custom status effect to it
                    itemDrop.m_itemData.m_shared.m_consumeStatusEffect = WormholeEffect.StatusEffect;

                    // Optionally clear the status effect if it's related to mead
                    //itemDrop.m_itemData.m_shared.m_consumeStatusEffect = null;


                }
            }

            ItemManager.Instance.AddItem(wormholePotion);

            PrefabManager.OnVanillaPrefabsAvailable -= AddClonedItems;
        }

        private static void SetPlayerVisibility(Player player, bool isVisible)
        {
            foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = isVisible;
            }
        }

        // On map Left Click patch...
        [HarmonyPatch(typeof(Minimap), "OnMapLeftClick")]
        private static class Minimap_OnMapLeftClick_Patch
        {
            private static bool Prefix(Minimap __instance)
            {

                // Get the local player
                Player localPlayer = Player.m_localPlayer;

                // Check if wormhole effect is active
                if (Player.m_localPlayer != null && (!Player.m_localPlayer.m_seman.HaveStatusEffect("WormholeEffect".GetHashCode())))
                {
                    return true; //Proceed with default behavior, player doesn't have the wormhole effect applied.
                }

                // Get the mouse click position in the game world
                Vector3 clickPosition = __instance.ScreenToWorldPoint(Input.mousePosition);


                // Find the closest player to the clicked position
                Player targetPlayer = FindClosestPlayer(clickPosition);
                if (targetPlayer == null)
                {
                    Jotunn.Logger.LogInfo("No player found near the clicked position.");
                    MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "No player found nearby.",0, (Sprite)null, false);
                    return true; //Proceed with default behavior, player not found.
                }

                // Hide the player's model before teleporting
                //SetPlayerVisibility(localPlayer, false);

                // Teleport to the target player's position
                localPlayer.TeleportTo(targetPlayer.transform.position, localPlayer.transform.rotation, true); // takes you to closest player to click.
                //localPlayer.TeleportTo(clickPosition, localPlayer.transform.rotation, true); // take you to clicked position for debugging.
                
                Jotunn.Logger.LogInfo($"Teleported to {targetPlayer.GetPlayerName()}");


                //Remove potion from inventory...

                // Access the player's inventory
                Inventory inventory = localPlayer.m_inventory;

                // Find the specific item (e.g., Wormhole Potion)
                string itemName = "$item_wormholePotion";
                ItemDrop.ItemData item = inventory.GetItem(itemName);

                if (item != null)
                {
                    // Remove one instance of the item
                    inventory.RemoveItem(item, 1);
                    Jotunn.Logger.LogInfo($"Removed one {itemName} from inventory.");
                }
                else
                {
                    Jotunn.Logger.LogInfo($"Item {itemName} not found in inventory.");
                }

                //start rendering character model again.
                //SetPlayerVisibility(localPlayer, true);

                // Set the map mode to small
                if (Minimap.instance != null)
                {
                    Minimap.instance.SetMapMode(Minimap.MapMode.Small);
                }

                return false; // Skip default map click behavior
            }

            private static Player FindClosestPlayer(Vector3 position)
            {
                const float maxDistance = 60f; // Maximum distance in meters
                //const float maxDistance = float.MaxValue; // unlimited range from click.


                float closestDistance = maxDistance;
                Player closestPlayer = null;

                foreach (Player otherPlayer in Player.GetAllPlayers())
                {
                    //log all players.
                    Jotunn.Logger.LogInfo(otherPlayer.GetPlayerName());
                    

                    if (otherPlayer == Player.m_localPlayer) continue; // Ignore the local player


                    float distance = Vector3.Distance(position, otherPlayer.transform.position);
                    
                    Jotunn.Logger.LogInfo(otherPlayer.GetPlayerName() + " Distance: " + distance);// Log the distance.

                    //set return closest players location.
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestPlayer = otherPlayer;
                    }
                }

                if (closestPlayer != null)
                {
                    Jotunn.Logger.LogInfo($"Closest Player is: {closestPlayer.GetPlayerName()}");
                }
                return closestPlayer;
            }
        }


        // Patch the Player.ConsumeItem method
        [HarmonyPatch(typeof(Player), "ConsumeItem")]
        public static class Player_ConsumeItem_Patch
        {
            public static bool Prefix(Player __instance, Inventory inventory, ItemDrop.ItemData item, bool checkWorldLevel = false)
            {
                if (item == null || item.m_shared.m_name != "$item_wormholePotion")
                {
                    return true; // Allow default behavior for other items
                }



                //check if local player can portal

                if (!inventoryCanPortal(inventory))
                {
                    MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "The weight of the world binds you. Try dropping your ores.", 0, (Sprite)null, false);
                    return false; // cancel the item consume.
                }

                // Trigger the consume animation
                if (__instance.m_zanim != null)
                {
                    Jotunn.Logger.LogWarning("Playing 'eat' animation.");
                    __instance.m_zanim.SetTrigger("eat"); // Triggers the "eat" animation
                }
                else
                {
                    Jotunn.Logger.LogWarning("Player animation controller (m_zanim) is null!");
                }

                // Play eating sound
                if (__instance.m_consumeItemEffects != null)
                {
                    __instance.m_consumeItemEffects.Create(__instance.transform.position, Quaternion.identity);
                }
                else
                {
                    Jotunn.Logger.LogWarning("Consume item effects not found!");
                }

                // Apply wormhole status to player.
                // Retrieve the status effect from ObjectDB
                StatusEffect statusEffect = ObjectDB.instance.GetStatusEffect("WormholeEffect".GetHashCode());

                // Add the custom status effect to the player
                StatusEffect wormholeEffect = __instance.m_seman.AddStatusEffect(statusEffect);
                if (wormholeEffect != null)
                {
                    Jotunn.Logger.LogInfo("Wormhole effect applied.");
                }
                else
                {
                    Jotunn.Logger.LogWarning("Failed to apply wormhole effect.");
                }


                // Log custom behavior for wormhole potion
                Jotunn.Logger.LogInfo("began wormhole potion");

                // Close the player's inventory UI
                if (InventoryGui.instance != null && InventoryGui.IsVisible())  // Accessing the InventoryGui
                {
                    InventoryGui.instance.Hide();  // Using the instance to call Hide()
                }

                // Open the map...
                if (Minimap.instance != null)
                {
                    Minimap.instance.SetMapMode(Minimap.MapMode.Large);
                }

                // Display a message to the player
                //MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "Choose an ally to be reunited with.");

                // Prevent default behavior for this item
                return false;
            }


        }




        //Patch the Minimap funciton
        [HarmonyPatch(typeof(Minimap), "SetMapMode")]
        public static class Minimap_SetMapMode_Patch
        {
            public static void Postfix(Minimap.MapMode mode)
            {
                // Check if the map was opened...
                if (mode == Minimap.MapMode.Large)
                {
                    return; //if so, ignore the rest.
                }

                // Check if the wormHoleActive is true
                //if (RegisideWormholePotion.wormHoleActive) {...}//old check.

                //Check if player has wormhole effect.

                // Get the local player
                Player localPlayer = Player.m_localPlayer;
                if (localPlayer == null)
                {
                    Jotunn.Logger.LogWarning("Local player not found.");
                    return;
                }

                if (Player.m_localPlayer.m_seman.HaveStatusEffect("WormholeEffect".GetHashCode()))
                {
                    // Set wormHoleActive to false
                    //RegisideWormholePotion.wormHoleActive = false;

                    // Remove wormhole effect from player.
                    localPlayer.m_seman.RemoveStatusEffect("WormholeEffect".GetHashCode());
                    Jotunn.Logger.LogInfo("Wormhole effect removed by map close.");

                }
            }
        }

    }
}
