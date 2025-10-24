using System;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using BokuMono.Data;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CraftFromStorage;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class CraftFromStorage : BasePlugin
{
    private static ManualLogSource _log;

    public override void Load()
    {
        // Plugin startup logic
        _log = Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        Harmony.CreateAndPatchAll(typeof(CraftingPatch));
    }

    private class CraftingPatch
    {
        /*
         * This patch is to add the extra text showing how many items are in storage under the bag amount for cooking
         */
        [HarmonyPatch(typeof(UICookingPage), "InitPage")]
        [HarmonyPostfix]
        private static void PatchCookingUI(UICookingPage __instance)
        {
            var cookingItemSelector = __instance.cookingDetail.requiredItemSelector;

            if (cookingItemSelector == null) return;

            // Checking if the ui is already patched
            if (cookingItemSelector.requiredItemIcon._items[0].transform.FindChild("HaveStorageStackBG") !=
                null) return;

            // Scales the box to fit the storage area
            var itemSelectorScale = cookingItemSelector.GetComponent<RectTransform>();
            itemSelectorScale.sizeDelta = new Vector2(itemSelectorScale.sizeDelta.x, itemSelectorScale.sizeDelta.y + 90);
            cookingItemSelector.transform.position += new Vector3(0, 0.07f, 0);

            // Moves the icon and category up slightly to compensate for the space taken by the storage text
            __instance.cookingDetail.icon.transform.position += new Vector3(0, 0.04f, 0);
            __instance.cookingDetail.categoryDetail.transform.position += new Vector3(0, 0.10f, 0);

            // Group(Cooking)
            // Creating new icons for the storage amount text by copying the original ui element and placing underneath
            CraftingUI.AddStorageStackBg(cookingItemSelector.requiredItemIcon);

            // Move the "Adapt Recipe lower to align under the extra storage text
            var arrangeParent = __instance.cookingDetail.requiredItemSelector.transform.Find("ArrangeBG");

            if (arrangeParent == null) return;
            
            // Scale the arrange text and the background image tto fit new text
            var arrangeRectTransform = arrangeParent.GetComponent<RectTransform>();
            arrangeRectTransform.sizeDelta += new Vector2(0, 50f);
            arrangeRectTransform.anchoredPosition += new Vector2(0, -25f);
            var adaptRectTransform = arrangeParent.GetChild(0).GetComponent<RectTransform>();
            adaptRectTransform.anchoredPosition += new Vector2(0, -25f);
        }

        /*
         * This patch is to add the extra text showing how many items are in storage under the bag amount
         */
        [HarmonyPatch(typeof(UIWindmillCraftingPage), "InitPage")]
        [HarmonyPostfix]
        private static void CreateStorageStack(UIWindmillCraftingPage __instance)
        {
            // this is the ui element of holding the 5 icons and item amount
            var requiredItemSelector = __instance.windmillCraftingDetail.requiredItemSelector;

            if (requiredItemSelector == null) return;

            //just check the first one to see if its patched
            if (requiredItemSelector.requiredItemIcon._items[0].transform.FindChild("HaveStorageStackBG") !=
                null) return;

            // Scales the box to fit the storage area
            var requiredItemsScale = requiredItemSelector.GetComponent<RectTransform>();
            requiredItemsScale.sizeDelta = new Vector2(requiredItemsScale.sizeDelta.x, requiredItemsScale.sizeDelta.y + 90);
            requiredItemSelector.transform.position += new Vector3(0, 0.07f, 0);

            // Group(Windmill) holds the 5 icons and amount
            var groupGameObject = requiredItemSelector.transform.GetChild(1).gameObject;
            var groupScale = groupGameObject.GetComponent<RectTransform>();
            groupScale.anchoredPosition = new Vector2(groupScale.anchoredPosition.x, groupScale.anchoredPosition.y + 45);

            __instance.windmillCraftingDetail.icon.transform.position += new Vector3(0, 0.04f, 0);
            __instance.windmillCraftingDetail.categoryDetail.transform.position += new Vector3(0, 0.15f, 0);
            
            // Creating 5 new icons for the storage amount text by copying the original ui element and placing underneath
            CraftingUI.AddStorageStackBg(requiredItemSelector.requiredItemIcon);
        }

        /*
         * This patch makes the recipe icons to be craftable if the items are in storage
         * Does not allow crafting from storage yet, just makes the icon highlighted and clickable
         */
        [HarmonyPatch(typeof(RequiredItemMaster), "IsEnough")]
        [HarmonyPostfix]
        public static void PatchRecipeMask(StorageManager __0,
            IRequiredItemMasterData __1,
            int countOffset, ref bool __result)
        {
            if (__result) return; // if already true no real need to check

            __result = CraftingHelper.IsCraftable(__1);
        }

        /*
         * This patch is to update the text showing how many items are in storage next to the ingredient icons
         * Its patching this when a new recipe is selected
         */
        [HarmonyPatch(typeof(UIRequiredItemDetail), "SetRecipeData")]
        [HarmonyPostfix]
        private static void OnSetRecipe(UIRequiredItemDetail __instance, IRequiredItemMasterData requiredItemData)
        {
            if (__instance == null) return;
            var inventoryManager = ManagedSingleton<InventoryManager>.Instance;
            if (inventoryManager == null) return;

            var requiredItemSelector = __instance.requiredItemSelector;
            if (requiredItemSelector == null) return;

            for (var i = 0; i < requiredItemSelector.requiredItemIcon._size; i++)
            {
                var textMesh = requiredItemSelector.requiredItemIcon._items[i].transform.FindChild("HaveStorageStackBG")
                    .GetChild(0).GetComponent<LocalizedTextMeshPro>();

                if (textMesh == null) continue;

                // if the required item data is null, clear it so when clicking on an unlearned recipe it doesn't show old data
                if (requiredItemData == null)
                {
                    textMesh.text = "";
                    continue;
                }

                var itemData = requiredItemData.RequiredItemList._items[i].ToString().Split(',');
                var itemId = uint.Parse(itemData[0].Trim('(', ' '));
                var stack = int.Parse(itemData[1].Trim(' ', ')'));
                var category = requiredItemData.RequiredItemTypeList._items[i];

                if (itemId == 0 || stack == 0)
                {
                    textMesh.text = "";
                    continue;
                }

                var storageAmount = CraftingHelper.GetStorageAmount(itemId, category, requiredItemData.GroupMaster);

                // if amount is over 999 just show 999+
                if (storageAmount > 999)
                {
                    textMesh.text = "999+";
                    continue;
                }

                textMesh.text = storageAmount.ToString();
            }
        }

        // IDEA -> user clicks on item to craft -> pop up amount to craft -> starts crafting it(grabs from storage first then bag)

        //OK TWO APPRAOCHES:
        // 1. have it have the ui for items from stoage(would require alot more work)
        // 2. if they have the item, just popup the amount and then when clicked craft then grab the items then from both bag & storage
        
        /*
         * This patch is to open the craft count dialog when clicking on a windmill recipe
         */
        [HarmonyPatch(typeof(UIWindmillCraftingPage), "OnDecide")]
        [HarmonyPostfix]
        private static void OnCraftDecide(UIWindmillCraftingPage __instance, RecipeIconData __0)
        {
            var requiredItemData = __instance.windmillCraftingDetail.requiredItemSelector;
            var storage = InventoryManager.Instance;
            __instance.windmillCraftingDetail.requiredItemSelector.TrySelectRequiredItem(0, storage.HouseStorage,
                storage.HouseStorage.itemDatas[0], out var remData);
        }
        //     var craftData = __0.WindmillCraftData;
        //
        //
        //     var accessor = UIAccessor.Instance;
        //     
        //
        //
        //     // //Make this open up just the craft count dialog
        //     //  try
        //     //  {
        //     //      UIAccessor.Instance.RequestOpenMenu(UILoadKey.WindmillCraftCountDialog, __0, true);
        //     //  }
        //     //  catch (Exception e)
        //     //  {
        //     //      return true;
        //     //  }
        //     // //Will change this later to add cooking but for now just windmill
        //     // if (__0.RecipeType != RecipeMasterType.Windmill) return true;
        //     //
        //     // if (!IsCraftable(__0.WindmillCraftData.requiredItemList)) return true;
        //     //
        //     // var uiData = __0.WindmillCraftData;
        //     // var uiAccessor = UIAccessor.Instance;
        //     // SoundManager.Instance.Play(3001, SoundManager.Category.Se, 0, false, 1f);
        //     // uiAccessor.RequestOpenMenu(UILoadKey.RequiredItemSelect, uiData, true);
        //     return true;
        // }

        //UIRequiredItemSelectPage.Decide -> When someone selects an item to add to the items
    }
}