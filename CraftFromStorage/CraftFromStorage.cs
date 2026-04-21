using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using BokuMono.Data;
using CraftFromStorage.Helpers;
using HarmonyLib;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace CraftFromStorage;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class CraftFromStorage : BasePlugin
{
    public static ManualLogSource _log;

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
            itemSelectorScale.sizeDelta =
                new Vector2(itemSelectorScale.sizeDelta.x, itemSelectorScale.sizeDelta.y + 90);
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
            requiredItemsScale.sizeDelta =
                new Vector2(requiredItemsScale.sizeDelta.x, requiredItemsScale.sizeDelta.y + 90);
            requiredItemSelector.transform.position += new Vector3(0, 0.07f, 0);

            // Group(Windmill) holds the 5 icons and amount
            var groupGameObject = requiredItemSelector.transform.GetChild(1).gameObject;
            var groupScale = groupGameObject.GetComponent<RectTransform>();
            groupScale.anchoredPosition =
                new Vector2(groupScale.anchoredPosition.x, groupScale.anchoredPosition.y + 45);

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
        public static void PatchRecipeMask(StorageManager __0, IRequiredItemMasterData __1, int countOffset,
            ref bool __result)
        {
            if (__result) return; // if already true no real need to check

            __result = CraftingHelper.IsCraftable(__1);
        }

        // Iassume ill need to use this UiRequiredItemSelectPage.Decide

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
                LocalizedTextMeshPro textMesh;
                try
                {
                    textMesh = requiredItemSelector.requiredItemIcon._items[i].gameObject.transform
                        .FindChild("HaveStorageStackBG")
                        .GetChild(0).GetComponent<LocalizedTextMeshPro>();
                }
                catch (Exception e)
                {
                    continue;
                }

                if (textMesh == null) continue;

                // if the required item data is null, clear it so when clicking on an unlearned recipe it doesn't show old data
                if (requiredItemData == null || requiredItemSelector.requiredItems[i] == null)
                {
                    textMesh.text = "";
                    continue;
                }

                var itemIds = requiredItemSelector.requiredItems[i]?.ids;
                var stack = __instance.requiredItemSelector.requiredItems[i]!.Stack;

                if (itemIds!._size <= 0 || stack == 0)
                {
                    textMesh.text = "";
                    continue;
                }

                var storageAmount =
                    CraftingHelper.GetStorageAmount(itemIds);

                for (var j = 0; j < i; j++)
                {
                    try
                    {
                        if (j == i) continue;
                        var priorItemIds = requiredItemSelector.requiredItems[j]?.ids;
                        var priorItemStack = __instance.requiredItemSelector.requiredItems[j]!.Stack;

                        if (priorItemIds!._items.Where(id => id != 0)
                            .Any(priorItemId => itemIds._items.Contains(priorItemId)))
                        {
                            storageAmount = Math.Max(0, storageAmount - priorItemStack);
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        continue;
                    }
                }


                if (storageAmount >= stack)
                {
                    requiredItemSelector.requiredItemIcon._items[i].checkIcon.enabled = true;
                }

                // if amount is over 999 just show 999+
                if (storageAmount > 999)
                {
                    textMesh.text = "999+";
                    continue;
                }

                textMesh.text = storageAmount.ToString();
            }
        }
        
        // This makes whe an item is selected use the house(for now) needed to add pagination logic for switching from bag to house
        //TODO: Pagination logic here
        [HarmonyPatch(typeof(UIRequiredItemSelectPage), "Decide")]
        [HarmonyPrefix]
        private static void PatchItemSelectStorage(UIRequiredItemSelectPage __instance, int slot, ItemIconData data,
            ControllableUI ctt, ref StorageManager storage)
        {
            //TODO: see if the user on the bag or house storage page(and which one)
            storage = InventoryManager.Instance.HouseStorage;
        }
        
        //TODO: text for the amount of items needs to be corrected. Other than that all that now needs to be worked one i
        // 1. Pagination for bag page and multiplep ages of sotrage(if exists)
        // 2. make sure if item is slsected dfrom different pages they are returned correctly(needs to be checked when pagination done)

        //GetGroupDataList -> RequiredItemDetail.CanSelectRequiredItem -> RequiredItemSelector.CanSelectRequiredItem -> ItemData$$IsConditions
        [HarmonyPatch(typeof(UIRequiredItemSelectPage), "GetGroupDataList")]
        [HarmonyPostfix]
        private static void PatchRequiredItemSelectPage(UIRequiredItemSelectPage __instance, StorageType storageType,
            ref Il2CppSystem.Collections.Generic.List<ItemIconData> __result)
        {
            //TODO: Willl only replace the slots where items are. If slot is empty it ignores. Need to change that
            //TODO: its still focusing onthe original item thats valid(need to figure that out)

            //THIS WORKS NOW, i need to specifically now make sure im either doing all the checks now or i do a prefix and add extra stuff
            var counter = 0;

            foreach (var iconData in __result)
            {
                iconData.state = 0; 
                var houseItem = InventoryManager.Instance.HouseStorage.itemDatas[counter];

                // This is if the slot is empty(or locked)
                if (houseItem != null && houseItem.Master != null)
                {
                    // Overwrite the Data and force the state to 0 (Normal) 
                    // This 'unlocks' the slot visually.
                    iconData.Data = houseItem;

                    // 3. Re-validate for the UI
                    var canselect = __instance.curDetail.CanSelectRequiredItem(iconData.Data);

                    if (!canselect)
                    {
                        iconData.errorIds ??= new Il2CppSystem.Collections.Generic.List<StateErrorTextId>();
                        if (!iconData.errorIds.Contains(StateErrorTextId.StateError_1030))
                            iconData.errorIds.Add(StateErrorTextId.StateError_1030);
                    }
                    else
                    {
                        iconData.errorIds?.Clear();
                    }
                }
                counter++;
            }
            //_log.LogInfo($"Patching GetGroupDataList, full list: {result}");
        }


        /*
         * This patch is to remove the items from storage when crafting. This might not be to needed after patching tryselecteditme
         */
        [HarmonyPatch(typeof(CookingManager), "Cooking")]
        [HarmonyPostfix]
        private static void PatchCookingManager(CookingManager __instance, IRecipeMasterData recipe,
            int count, int qualityValue, List<SlotItemData> useItemList)
        {
            try
            {
                foreach (var useItemData in useItemList)
                {
                    if (useItemData == null || useItemData.Storage.GetType() == typeof(BagItemStorageManager)) continue;
                    CraftingHelper.RemoveItemsFromStorage(useItemData, recipe, count);
                }
            }
            catch (Exception e)
            {
                _log.LogError($"Error in PatchCookingManager: {e.Message}\n{e.StackTrace}");
            }
        }

        // This does the crafting thing(keep it commented for now as i want thte item seleciton to be manual)
        // [HarmonyPatch(typeof(UIRequiredItemSelectPage), "OnShow")]
        // [HarmonyPostfix]
        private static void TestingCountPage(UIRequiredItemSelectPage __instance)
        {
            try
            {
                var requiredItemData = __instance.curDetail.requiredItemSelector;
                if (requiredItemData == null) return;
                var storage = InventoryManager.Instance;

                for (var i = 0; i < requiredItemData.requiredItems.Count; i++)
                {
                    var requiredItem = requiredItemData.requiredItems[i];
                    if (requiredItem == null) continue;

                    //check if any ids match required item ids
                    var itemData = storage.HouseStorage.itemDatas
                        .FirstOrDefault(x => requiredItem.ids._items.Contains(x.ItemId));

                    // Clone the itemData to avoid modifying the original directly
                    var clonedItemData = ItemData.Clone(itemData);

                    var slot = storage.HouseStorage.itemDatas.IndexOf(itemData);
                    requiredItemData.TrySelectRequiredItem(slot, storage.HouseStorage, clonedItemData,
                        out var tempItemData);
                    storage.HouseStorage.itemDatas[slot] = tempItemData;

                    try
                    {
                        var uiRequiredItemIcon = __instance.curDetail.requiredItemSelector.requiredItemIcon._items[i];
                        //TODO: If i want to do UI  i would need to have something that keeps track on the ui if theyre on storage or bag
                        //TODO: Also getting object not set, so im doing something with this
                        // This will update the icon
                        //bug: on first open it will make it blacked out still
                        // uiRequiredItemIcon.OnUpdate(UIRequiredItemIcon.State.Normal);
                        // __instance.curDetail.requiredItemSelector.OnUpdate();
                        uiRequiredItemIcon.havedStackText.text = tempItemData.Stack.ToString();
                    }
                    catch (Exception e)
                    {
                        _log.LogError($"Error getting uiRequiredItemIcon: {e.Message}\n{e.StackTrace}");
                        continue;
                    }
                }

                //TODO: coming up as 1 for craft amount, probably due to my logic tbh. need to fix that
                var maxCount =
                    CraftingHelper.GetMaxCraftAmount(__instance.curDetail.requiredItemSelector.requiredItems);
                var craftQuality = requiredItemData.GetQualityValue(true);

                _log.LogInfo($"Max Craft Amount: {maxCount}, Craft Quality: {craftQuality}");
                var mdm = MasterDataManager.Instance;
                var dialogData = mdm.DialogMaster.GetData(100000);

                //Todo: check for cooking & windmill and use respective dialog
                if (__instance.curRecipeMasterData.RecipeType == RecipeMasterType.Cooking)
                {
                    //TODO: amount now shows but i need to figure out how its grabbing the amount in the original crafting cause its only grabbing the first amount
                    var cookingDialogData = __instance.GetCookingDialogData(__instance.curRecipeMasterData, dialogData,
                        maxCount, craftQuality);

                    UIAccessor.Instance.RequestOpenDialog(UILoadKey.CookingCountDialog, cookingDialogData);
                }
                else if (__instance.curRecipeMasterData.RecipeType == RecipeMasterType.Windmill)
                {
                    craftQuality = requiredItemData.GetQualityValue(false);
                    var windmillDialogData = __instance.GetWindmillCraftDialogData(__instance.curRecipeMasterData,
                        dialogData, maxCount, craftQuality);
                    UIAccessor.Instance.RequestOpenDialog(UILoadKey.WindmillCraftCountDialog, windmillDialogData);
                }
            }
            catch (Exception e)
            {
                _log.LogError($"Error in OnCookingDecide: {e.Message}\n{e.StackTrace}");
            }
        }

        //TODO: UIStorageGroup(UIBagGroup) -> UIItemIconContent -> Need to add storage


        //PROB A BETTER WAY TO DO IT I MIGHT HAVE FOUND
        //TODO: could keep a counter how many itrems replaced and use that have pages(but also comes with the issue of not knowing when to stop)
        [HarmonyPatch(typeof(UIItemIconContent), "UpdateContentUniqueData")]
        [HarmonyPrefix]
        private static void PatchItemIconContent(UIItemIconContent __instance, UIData data)
        {
            // try
            // {
            //     // Only override the item icon content if its in the storage selection UI, otherwise it will mess with the bag icons as well
            //     if (__instance.GetComponentInParent<UIStorageGroup>().curUIKey != UILoadKey.RequiredItemSelect) return;
            //
            //     // THIS does work but the icon still isnt clickable and also it still knows its the old item still, probably to do with clicking on the isdecide
            //     _log.LogInfo("Patching UIItemIconContent");
            //
            //     var iconData = data.GetData<ItemIconData>();
            //     if (iconData == null)
            //         return;
            //
            //     var item = InventoryManager.Instance.HouseStorage.itemDatas[1];
            //
            //     iconData.Data = item;
            // }
            // catch (Exception e)
            // {
            //     _log.LogError($"Error in PatchItemIconContent: {e.Message}\n{e.StackTrace}");
            // }
        }
    }

    //NOT sure this is needed as selecitem is the page as well. not sure yet tho
    [HarmonyPatch(typeof(UIStorageGroup), "OnShow")]
    [HarmonyPostfix]
    private static void PatchStorageUI(UIStorageGroup __instance)
    {
        try
        {
            if (__instance.curUIKey != UILoadKey.RequiredItemSelect) return;
            __instance.storageType = StorageType.House;

            var hsm = InventoryManager.Instance.HouseStorage;

            if (hsm != null)
            {
                // TODO: Pagination
            }
        }
        catch (Exception e)
        {
            _log.LogError($"Error in PatchStorageUI: {e.Message}\n{e.StackTrace}");
        }
    }
}