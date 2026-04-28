using System;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using BokuMono.Data;
using BokuMono.UI;
using CraftFromStorage.Helpers;
using HarmonyLib;
using Il2CppSystem.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CraftFromStorage;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class CraftFromStorage : BasePlugin
{
    public static ManualLogSource _log;

    public override void Load()
    {
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
         */
        [HarmonyPatch(typeof(RequiredItemMaster), "IsEnough")]
        [HarmonyPostfix]
        public static void PatchRecipeMask(StorageManager __0, IRequiredItemMasterData recipe, int countOffset,
            ref bool __result)
        {
            if (__result) return; // if already true no real need to check
            
            __result = CraftingHelper.IsCraftable(recipe);
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

        //Commented out as AddGamePadInputCallback works
        // [HarmonyPatch(typeof(Input), "GetKeyDown", new Type[] {typeof(UnityEngine.KeyCode)})]
        // [HarmonyPostfix]
        // private static void PatchInputStorageTab(bool __result, UnityEngine.KeyCode key)
        // {
        //     if (!__result) return;
        //
        //     if (key != KeyCode.Q && key != KeyCode.E) return;
        //
        //     if (UIAccessor.Instance == null) return;
        //
        //     UILoadKey currentKey;
        //
        //     // Trying to grab it sometimes comes with "NullException" if too early so catching it to avoid the errors
        //     try {
        //         currentKey = UIAccessor.Instance.CurrentUIKey;
        //     }
        //     catch {
        //         return;
        //     }
        //
        //     if (currentKey != UILoadKey.RequiredItemSelect) return;
        //
        //     switch (key)
        //     {
        //         case KeyCode.Q:
        //         {
        //             _log.LogInfo("Q pressed");
        //             var uiIconKey = Resources.FindObjectsOfTypeAll<UIKeyIcon>()
        //                 .FirstOrDefault(storage => storage.name == "UIKeyStorageSelectItemLeft");
        //
        //             uiIconKey?.BokuMono_UI_ITouchable_InputTouch(ITouchable.InputState.Down, Vector2.zero, null);
        //             break;
        //         }
        //         case KeyCode.E:
        //         {
        //             var uiIconKey = Resources.FindObjectsOfTypeAll<UIKeyIcon>()
        //                 .FirstOrDefault(storage => storage.name == "UIKeyStorageSelectItemRight");
        //
        //             uiIconKey?.BokuMono_UI_ITouchable_InputTouch(ITouchable.InputState.Down, Vector2.zero, null);
        //             break;
        //         }
        //     }


        //Note: Arrange is adapt in the code/objects
        [HarmonyPatch(typeof(UIRequiredItemIcon))]
        [HarmonyPatch("Setup", new Type[] {
                typeof(BagItemStorageManager), typeof(RequiredItemData), typeof(int), typeof(UIRequiredItemIcon.State)
            })]
        [HarmonyPostfix]
        static void PatchIconSetup(UIRequiredItemIcon __instance, BagItemStorageManager cloneBagMgr,
            RequiredItemData requiredItemData, int quality, UIRequiredItemIcon.State state)
        {
            if (state != UIRequiredItemIcon.State.Normal ||
                UIAccessor.Instance.CurrentUIKey != UILoadKey.RequiredItemSelect) return;
            
            var itemSelectedString = requiredItemData.IconId.Replace("item_icon_", "");

            //item_icon_110201
            if (!uint.TryParse(itemSelectedString, out var itemSelectedID)) return;
            
            var amount = CraftingHelper.CountInAllStorages(data => data.ItemId == itemSelectedID);
            __instance.havedStackText.text = amount.ToString();
        }

        [HarmonyPatch(typeof(UIRequiredItemSelectPage), "SetShortCutFocus")]
        [HarmonyPrefix]
        private static bool PatchAdaptRecipeSelection(UIRequiredItemSelectPage __instance)
        {
            var detail = __instance.curDetail;
            if (detail == null)
                return true;

            if (!detail.IsSelectionCompletedRequiredItem() ||
                __instance.shortCutCursorIndex >= 0) return true;

            return !CraftingHelper.HaveAdaptItemsInStorage(InventoryManager.Instance.HouseStorage.itemDatas,
                __instance.curDetail);
        }

        /**
         * This patch overrides when the user clicks on an item to reference the storage(if the user is a storage tab)
         * Nothing else is overridden except the slot index and storage type
         */
        [HarmonyPatch(typeof(UIRequiredItemSelectPage), "Decide")]
        [HarmonyPrefix]
        private static void PatchItemSelectStorage(UIRequiredItemSelectPage __instance, ref int slot, ItemIconData data,
            ControllableUI ctt, ref StorageManager storage)
        {
            // If index is 0 or less its bag so return
            if (__instance.SelectedParcelIndex <= 0) return;

            //This is to grab the right item from storage since the index will start at 0 on new tabs
            var index = __instance.SelectedParcelIndex - 1;
            slot = (index * 32) + slot;

            storage = InventoryManager.Instance.HouseStorage;
        }

        /*
         * This patch adds keyboard and gamepad input for tab navigation
         * It still registers it without SteamInput
         */
        [HarmonyPatch(typeof(BaseInputManager<InputManager>), "AddGamePadButtonCallback")]
        [HarmonyPostfix]
        private static void PatchGamePadMenuNavigation(InputManager __instance, GamePadButton gamePadButton,
            InputSource inputSource)
        {
            if (UIAccessor.Instance == null) return;

            UILoadKey currentKey;

            try
            {
                currentKey = UIAccessor.Instance.CurrentUIKey;
            }
            catch
            {
                return;
            }

            if (currentKey != UILoadKey.RequiredItemSelect) return;

            if (gamePadButton == GamePadButton.L_Down)
            {
                var uiIconKey = Resources.FindObjectsOfTypeAll<UIKeyIcon>()
                    .FirstOrDefault(storage => storage.name == "UIKeyStorageSelectItemLeft");

                uiIconKey?.BokuMono_UI_ITouchable_InputTouch(ITouchable.InputState.Down, Vector2.zero, null);
            }

            if (gamePadButton == GamePadButton.R_Down)
            {
                var uiIconKey = Resources.FindObjectsOfTypeAll<UIKeyIcon>()
                    .FirstOrDefault(storage => storage.name == "UIKeyStorageSelectItemRight");

                uiIconKey?.BokuMono_UI_ITouchable_InputTouch(ITouchable.InputState.Down, Vector2.zero, null);
            }
        }


        /*
         * This patch adds the logic and animation + music for tab switching
         */
        [HarmonyPatch(typeof(BaseInputManager<InputManager>))]
        [HarmonyPatch("ReserveInput",
            new Type[] {typeof(InputSource), typeof(InputKeyBind.InputActionSet), typeof(GamePadButton)})]
        [HarmonyPostfix]
        public static void PatchTabButtons(BaseInputManager<InputManager> __instance,
            InputKeyBind.InputActionSet actionSet, GamePadButton gamePadButton)
        {
            if (UIAccessor.Instance.CurrentUIKey != UILoadKey.RequiredItemSelect ||
                actionSet != InputKeyBind.InputActionSet.Menu) return;

            var uiStorage = Resources.FindObjectsOfTypeAll<UIStorageGroup>()
                .FirstOrDefault(storage =>
                    storage.gameObject.name == "UIBagStorageGroup");

            if (uiStorage == null)
            {
                _log.LogError("UIBagStorageGroup not found in HideAndDontSave scene!");
                return;
            }

            var uiPageMarkAmount = MaxPageAmount();

            switch (gamePadButton)
            {
                // Right move(next tab)
                case GamePadButton.R_Down or GamePadButton.ZR_Down:
                {
                    uiStorage.parentPage.SelectedParcelIndex += 1;

                    if (uiStorage.parentPage.SelectedParcelIndex >= uiPageMarkAmount)
                        uiStorage.parentPage.SelectedParcelIndex = 0;

                    //Play animation and sound
                    uiStorage.pageMarkList.PlayRPushAnimation();
                    uiStorage.pageMarkList.PageChange(uiStorage.parentPage.SelectedParcelIndex);
                    SoundManager.Instance?.Play(3051, SoundManager.Category.Se, 0, false, 1);
                    uiStorage.transform.FindChild("UIHeaderTab(Clone)").GetChild(0)
                        .GetComponent<UIDOTweenAnimationManager>().PlayOnce(1);
                    uiStorage.parentPage.OnRefresh();
                    break;
                }
                // Left move(back tab)
                case GamePadButton.ZL_Down or GamePadButton.L_Down:
                {
                    uiStorage.parentPage.SelectedParcelIndex -= 1;

                    if (uiStorage.parentPage.SelectedParcelIndex < 0)
                        uiStorage.parentPage.SelectedParcelIndex = uiPageMarkAmount - 1;

                    //Play animation and sound
                    uiStorage.pageMarkList.PlayLPushAnimation();
                    uiStorage.pageMarkList.PageChange(uiStorage.parentPage.SelectedParcelIndex);
                    SoundManager.Instance?.Play(3051, SoundManager.Category.Se, 0, false, 1);
                    uiStorage.transform.FindChild("UIHeaderTab(Clone)").GetChild(0)
                        .GetComponent<UIDOTweenAnimationManager>().PlayOnce(0);
                    uiStorage.parentPage.OnRefresh();
                    break;
                }
            }
        }

        //TODO: text for the amount of items needs to be corrected. Other than that all that now needs to be worked one i
        /**
         * This patch adds the tabs UI(uipagemarklist) for switching and also UIButtons for moving between tabs
         */
        [HarmonyPatch(typeof(UIStorageGroup), "OnShow")]
        [HarmonyPostfix]
        private static void PatchItemSelectPage(UIStorageGroup __instance)
        {
            if (__instance.curUIKey != UILoadKey.RequiredItemSelect) return;
            if (__instance.name != "UIBagStorageGroup") __instance.name = "UIBagStorageGroup";

            if (__instance.pageMarkList != null)
            {
                __instance.pageMarkList.Setup(MaxPageAmount(), false, false);
                __instance.pageMarkList.PageChange(0);
                return;
            }

            // This is cloning the UIStorageGroup(House) page marklist from the hideanddontsave scene(used in actual code to instantiate it)

            var uiStorage = Resources.FindObjectsOfTypeAll<UIStorageGroup>()
                .FirstOrDefault(storage =>
                    storage.gameObject.scene.name == null &&
                    storage.gameObject.name == "UIStorageGroup(House)");

            if (uiStorage == null)
            {
                _log.LogError("UIStorageGroup(House) not found in HideAndDontSave scene!");
                return;
            }

            var uiPageMarkList = uiStorage.pageMarkList;

            if (uiPageMarkList == null)
            {
                _log.LogError("UIPageMarkList not found in UIStorageGroup(House)!");
                return;
            }

            var clone = Object.Instantiate(uiPageMarkList);
            clone.transform.SetParent(__instance.transform, false);

            __instance.pageMarkList = clone;
            __instance.pageMarkList.Setup(MaxPageAmount(), true, false);
            __instance.pageMarkList.PageChange(0);
            __instance.pageMarkList.SetName("UIStoragePageMarkList");

            //This will be used to check if on bag or storage. 0 is bag 1+ is storage
            __instance.parentPage.SelectedParcelIndex = 0;

            //uiHeaderTab has the left and right buttons for tabs
            var uiHeader = uiStorage.gameObject.transform.FindChild("UIHeaderTab");

            var headerClone = Object.Instantiate(uiHeader);
            Object.Destroy(headerClone.FindChild("HeaderPlate").gameObject);
            headerClone.transform.SetParent(__instance.transform, false);

            //Rename the UiKeys so its easier to find them
            headerClone.GetChild(0).FindChild("UIKeyArrowLeft").FindChild("UIKeyIcon")
                .SetName("UIKeyStorageSelectItemLeft");
            headerClone.GetChild(0).FindChild("UIKeyArrowRight").FindChild("UIKeyIcon")
                .SetName("UIKeyStorageSelectItemRight");
        }

        //GetGroupDataList -> RequiredItemDetail.CanSelectRequiredItem -> RequiredItemSelector.CanSelectRequiredItem -> ItemData$$IsConditions

        /*
         * This patch is how the games replaces the uiicons with the actual itemdata in the storage
         * It calls ItemDataListOverride to do most of the logic for abstraction
         */
        [HarmonyPatch(typeof(UIRequiredItemSelectPage), "GetGroupDataList")]
        [HarmonyPostfix]
        private static void PatchRequiredItemSelectPage(UIRequiredItemSelectPage __instance, StorageType storageType,
            ref Il2CppSystem.Collections.Generic.List<ItemIconData> __result)
        {
            var pageCount = __instance.SelectedParcelIndex;

            ItemDataListOverride(__result, pageCount, __instance);
            //TODO: see about moving tabs to the correct item for easier user experience
            for (var i = 0; i < __result.Count; i++)
            {
                if (__result._items[i].Data.IsBlank) continue;

                var icon = __result._items[i];

                if (icon.errorIds != null && icon.errorIds.Count != 0) continue;

                __instance.shortCutCursorIndex = i;
                break;
            }

            __instance.bagGroup.SetFocus(__instance.shortCutCursorIndex);
        }
    }

    /**
     * ItemDataListOverride - Takes the IconDataList and replaces it one by one with items from either bag or storage
     */
    private static void ItemDataListOverride(Il2CppSystem.Collections.Generic.List<ItemIconData> itemList,
        int pageNumber, UIRequiredItemSelectPage itemSelectPage)
    {
        var isBag = pageNumber == 0;
        // Change the text of the storage header to either Bag or Storage for user experience 
        var headerText =
            itemSelectPage.bagGroup.gameObject.transform.FindChild("HeaderText")
                .GetComponent<LocalizedTextMeshPro>();

        headerText?.SetText(isBag ? "Bag" : "Storage");

        var bagStorage = InventoryManager.Instance.BagItemStorage;
        var houseStorage = InventoryManager.Instance.HouseStorage;

        if (!isBag)
        {
            var index = (pageNumber - 1) * 32;
            var originalIndex = 0;
            for (var i = index; i < (index + 32); i++)
            {
                var item = itemList._items[originalIndex];

                var sourceItem = houseStorage.itemDatas[i];

                item.state = IconData.State.Normal;
                item.errorIds?.Clear();

                OverrideItemData(item, sourceItem, itemSelectPage);
                originalIndex++;
            }
        }
        else
        {
            for (var i = 0; i < 32; i++)
            {
                var item = itemList._items[i];

                var sourceItem = bagStorage.itemDatas[i];

                if (i >= bagStorage.CurrentCapacity)
                {
                    item.state = IconData.State.LockItem;
                    continue;
                }

                item.state = IconData.State.Normal;
                item.errorIds?.Clear();

                OverrideItemData(item, sourceItem, itemSelectPage);
            }
        }
    }

    private static void OverrideItemData(ItemIconData originalItem, ItemData newItem,
        UIRequiredItemSelectPage itemSelectPage)
    {
        originalItem.Data = newItem;

        var isEmpty = newItem?.Master == null;

        if (isEmpty)
        {
            originalItem.errorIds?.Clear();
            return;
        }

        var canSelect = itemSelectPage.curDetail.CanSelectRequiredItem(originalItem.Data);

        if (!canSelect)
        {
            originalItem.errorIds ??= new Il2CppSystem.Collections.Generic.List<StateErrorTextId>();
            if (!originalItem.errorIds.Contains(StateErrorTextId.StateError_1030))
                originalItem.errorIds.Add(StateErrorTextId.StateError_1030);
        }
        else
        {
            originalItem.errorIds?.Clear();
        }
    }

    private static int MaxPageAmount()
    {
        var storageTabs = (int) Math.Ceiling((double) (24 * InventoryManager.Instance.HouseStorage.ParcelCount) / 32);

        return storageTabs + 1;
    }


    /*
     * This patch is to remove the items from storage when crafting. This might not be to needed after patching tryselecteditme
     */
    // [HarmonyPatch(typeof(CookingManager), "Cooking")]
    // [HarmonyPostfix]
    // private static void PatchCookingManager(CookingManager __instance, IRecipeMasterData recipe,
    //     int count, int qualityValue, List<SlotItemData> useItemList)
    // {
    //     try
    //     {
    //         foreach (var useItemData in useItemList)
    //         {
    //             if (useItemData == null || useItemData.Storage.GetType() == typeof(BagItemStorageManager)) continue;
    //             CraftingHelper.RemoveItemsFromStorage(useItemData, recipe, count);
    //         }
    //     }
    //     catch (Exception e)
    //     {
    //         _log.LogError($"Error in PatchCookingManager: {e.Message}\n{e.StackTrace}");
    //     }
    // }

    // // This does the crafting thing(keep it commented for now as i want thte item seleciton to be manual)
    // // [HarmonyPatch(typeof(UIRequiredItemSelectPage), "OnShow")]
    // // [HarmonyPostfix]
    // private static void TestingCountPage(UIRequiredItemSelectPage __instance)
    // {
    //     try
    //     {
    //         var requiredItemData = __instance.curDetail.requiredItemSelector;
    //         if (requiredItemData == null) return;
    //         var storage = InventoryManager.Instance;
    //
    //         for (var i = 0; i < requiredItemData.requiredItems.Count; i++)
    //         {
    //             var requiredItem = requiredItemData.requiredItems[i];
    //             if (requiredItem == null) continue;
    //
    //             //check if any ids match required item ids
    //             var itemData = storage.HouseStorage.itemDatas
    //                 .FirstOrDefault(x => requiredItem.ids._items.Contains(x.ItemId));
    //
    //             // Clone the itemData to avoid modifying the original directly
    //             var clonedItemData = ItemData.Clone(itemData);
    //
    //             var slot = storage.HouseStorage.itemDatas.IndexOf(itemData);
    //             requiredItemData.TrySelectRequiredItem(slot, storage.HouseStorage, clonedItemData,
    //                 out var tempItemData);
    //             storage.HouseStorage.itemDatas[slot] = tempItemData;
    //
    //             try
    //             {
    //                 var uiRequiredItemIcon = __instance.curDetail.requiredItemSelector.requiredItemIcon._items[i];
    //                 // This will update the icon
    //                 //bug: on first open it will make it blacked out still
    //                 // uiRequiredItemIcon.OnUpdate(UIRequiredItemIcon.State.Normal);
    //                 // __instance.curDetail.requiredItemSelector.OnUpdate();
    //                 uiRequiredItemIcon.havedStackText.text = tempItemData.Stack.ToString();
    //             }
    //             catch (Exception e)
    //             {
    //                 _log.LogError($"Error getting uiRequiredItemIcon: {e.Message}\n{e.StackTrace}");
    //                 continue;
    //             }
    //         }
    //
    //         var maxCount =
    //             CraftingHelper.GetMaxCraftAmount(__instance.curDetail.requiredItemSelector.requiredItems);
    //         var craftQuality = requiredItemData.GetQualityValue(true);
    //
    //         _log.LogInfo($"Max Craft Amount: {maxCount}, Craft Quality: {craftQuality}");
    //         var mdm = MasterDataManager.Instance;
    //         var dialogData = mdm.DialogMaster.GetData(100000);
    //
    //         if (__instance.curRecipeMasterData.RecipeType == RecipeMasterType.Cooking)
    //         {
    //             var cookingDialogData = __instance.GetCookingDialogData(__instance.curRecipeMasterData, dialogData,
    //                 maxCount, craftQuality);
    //
    //             UIAccessor.Instance.RequestOpenDialog(UILoadKey.CookingCountDialog, cookingDialogData);
    //         }
    //         else if (__instance.curRecipeMasterData.RecipeType == RecipeMasterType.Windmill)
    //         {
    //             craftQuality = requiredItemData.GetQualityValue(false);
    //             var windmillDialogData = __instance.GetWindmillCraftDialogData(__instance.curRecipeMasterData,
    //                 dialogData, maxCount, craftQuality);
    //             UIAccessor.Instance.RequestOpenDialog(UILoadKey.WindmillCraftCountDialog, windmillDialogData);
    //         }
    //     }
    //     catch (Exception e)
    //     {
    //         _log.LogError($"Error in OnCookingDecide: {e.Message}\n{e.StackTrace}");
    //     }
    // }
}