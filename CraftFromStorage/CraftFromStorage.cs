using System;

using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.UnityEngine;
using BokuMono;
using BokuMono.Data;
using BokuMono.FieldEvent;
using BokuMono.Utility;
using HarmonyLib;
using Il2CppSystem.Collections.Generic;
using Il2CppSystem.Text;
using UnityEngine;
using UnityEngine.UI;
using KeyCode = BepInEx.Unity.IL2CPP.UnityEngine.KeyCode;
using Logger = BepInEx.Logging.Logger;
using Math = System.Math;
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
            //UICookingDetail and go to the itemselector from there
            var cookingItemSelector = __instance.cookingDetail.requiredItemSelector;

            if (cookingItemSelector == null) return;

            //just check the first one to see if its patched
            if (cookingItemSelector.transform.GetChild(2).GetChild(0).GetChild(0).FindChild("HaveStorageStackBG") !=
                null) return;
            
            //TODO: cooking is different from windmill so need to adjust accordingly
            // var rectangle = cookingItemSelector.GetComponent<RectTransform>();
            // rectangle.sizeDelta = new Vector2(rectangle.sizeDelta.x, rectangle.sizeDelta.y + 90);
            // cookingItemSelector.transform.position += new Vector3(0, 0.07f, 0);

            // Group(Cooking)
            var groupGameObject = cookingItemSelector.transform.GetChild(2).gameObject;
            // var child = groupGameObject.GetComponent<RectTransform>();
            //
            // child.anchoredPosition = new Vector2(child.anchoredPosition.x, child.anchoredPosition.y + 45);

            var childCount = groupGameObject.transform.childCount;

            // Creating new icons for the storage amount text by copying the original ui element and placing underneath
            for (var i = 0; i < childCount; i++)
            {
                var detailObject = groupGameObject.transform.GetChild(i).gameObject;
                var ui = detailObject.transform.GetChild(0).GetChild(3).gameObject;
                var storageStackBg = Object.Instantiate(ui, ui.transform.parent, true);
                storageStackBg.name = "HaveStorageStackBG";
                storageStackBg.transform.position = ui.transform.position + new Vector3(0, -0.07f, 0);
            }
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
            if (requiredItemSelector.transform.GetChild(1).GetChild(0).GetChild(0).FindChild("HaveStorageStackBG") !=
                null) return;
            
             var rectangle = requiredItemSelector.GetComponent<RectTransform>();
             rectangle.sizeDelta = new Vector2(rectangle.sizeDelta.x, rectangle.sizeDelta.y + 90);
             requiredItemSelector.transform.position += new Vector3(0, 0.07f, 0);

            // Group(Windmill) holds the 5 icons and amount
            var groupGameObject = requiredItemSelector.transform.GetChild(1).gameObject;
            var child = groupGameObject.GetComponent<RectTransform>();
            child.anchoredPosition = new Vector2(child.anchoredPosition.x, child.anchoredPosition.y + 45);
            
            var uiDetail = __instance.transform.GetChild(0).GetChild(3).gameObject;
            var icon = uiDetail.transform.GetChild(2).gameObject;
            icon.transform.position += new Vector3(0, 0.04f, 0);
            var category = uiDetail.transform.GetChild(3).gameObject;
            category.transform.position += new Vector3(0, 0.15f, 0);

            //get amount of children
            var childCount = groupGameObject.transform.childCount;

            // Creating 5 new icons for the storage amount text by copying the original ui element and placing underneath
            for (var i = 0; i < childCount; i++)
            {
                var detailObject = groupGameObject.transform.GetChild(i).gameObject;
                var ui = detailObject.transform.GetChild(0).GetChild(3).gameObject;
                var storageStackBg = Object.Instantiate(ui, ui.transform.parent, true);
                storageStackBg.name = "HaveStorageStackBG";
                storageStackBg.transform.position = ui.transform.position + new Vector3(0, -0.07f, 0);
            }
        }


        /*
         * This patch makes the recipe icons to be craftable if the items are in storage
         * Does not allow crafting from storage yet, just makes the icon highlighted and clickable
         */
        [HarmonyPatch(typeof(RequiredItemMaster), "IsEnough")]
        [HarmonyPostfix]
        public static void PatchRecipeMask(BokuMono.StorageManager __0,
            BokuMono.Data.IRequiredItemMasterData __1,
            int countOffset, ref bool __result)
        {
            if (__result) return; // if already true no real need to check

            __result = CraftingHelper.IsCraftable(__1);
        }

        /*
         * This patch is to update the text showing how many items are in storage next to the ingredient icons
         * Its patching this when a new recipe is selected
         */
        [HarmonyPatch(typeof(UIRequiredItemSelector), "Setup")]
        [HarmonyPostfix]
        private static void OnRequiredItemSelect(UIRequiredItemSelector __instance, IRequiredItemMasterData data)
        {
            if (__instance == null) return;
            var inventoryManager = ManagedSingleton<InventoryManager>.Instance;
            if (inventoryManager == null) return;

            var childIndex = 1;

            if (__instance.name == "UIRequiredItemSelector(Cooking)")
            {
                childIndex = 2;
            }

            // this is the data holding the required items for the recipe (item id, stack)
            var childCount = __instance.transform.GetChild(childIndex).childCount;

            for (int i = 0; i < childCount; i++)
            {
                var itemData = data.RequiredItemList._items[i].ToString().Split(',');
                var itemId = uint.Parse(itemData[0].Trim('(', ' '));
                var stack = int.Parse(itemData[1].Trim(' ', ')'));
                var category = data.RequiredItemTypeList._items[i];

                LocalizedTextMeshPro textMesh;

                try
                {
                    textMesh = __instance.transform.GetChild(childIndex).GetChild(i).GetChild(0)
                        .FindChild("HaveStorageStackBG").FindChild("HavedStackText")
                        .GetComponent<LocalizedTextMeshPro>();
                }
                catch (Exception e)
                {
                    continue;
                }

                if (itemId == 0 || stack == 0)
                {
                    textMesh.text = "";
                    continue;
                }

                var storageAmount = CraftingHelper.GetStorageAmount(itemId, category, data.GroupMaster);

                // This patches the little check icon to show if you have enough in storage(or bag)
                if (storageAmount >= stack)
                {
                    __instance.requiredItemIcon._items[i].checkIcon.enabled = true;
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

        // IDEA -> user clicks on item to craft -> pop up amount to craft -> starts crafting it(grabs from storage first then bag)


        //OK TWO APPRAOCHES:
        // 1. have it have the ui for items from stoage(would require alot more work)
        // 2. if they have the item, just popup the amount and then when clicked craft then grab the items then from both bag & storage

        // This is called when the user clicks on the recipe to craft
        [HarmonyPatch(typeof(UIRequiredItemSelectPage), "OnShow")]
        [HarmonyPostfix]
        private static void OnShowRequiredItemSelect(UIRequiredItemSelectPage __instance)
        {
            if (__instance == null) return;


            // //requireditemdata the count will always be 5(even if tehre is only 1 item)
            // var recipeData = __instance.curRecipeMasterData;
            // var itemSelector = __instance.curDetail.requiredItemSelector;
            // //itemSelector.selectIndex = itemSelector.maxArrangeItemCount + itemSelector.maxRequiredItemCount;
            // __instance.OpenCraftDialog(__instance.curRecipeMasterData);
            // //currequireditemcount is how many different items are needed
            //
            // var selector = __instance.curDetail.requiredItemSelector;
            // var storage = ManagedSingleton<InventoryManager>.Instance; // or whatever you use
            //
            // foreach (var req in selector.RequiredItems)
            // {
            //     if (req == null) continue;
            //
            //     // Get all matching items from both bag and storage
            //     var matches = CraftingHelper.GetMatchingItemsFromAllStorages(req);
            // }


            // if (__instance.curRecipeMasterData == null) return;
            // try
            // {
            //     // __instance.SetActive(false);
            //     __instance.OpenCraftDialog(__instance.curRecipeMasterData);
            // }
            // catch (Exception e)
            // {
            //     return;
            // }
        }

        // [HarmonyPatch(typeof(UIWindmillCraftingPage), "OnDecide")]
        // [HarmonyPrefix]
        // private static bool OnCraftDecide(UIWindmillCraftingPage __instance, RecipeIconData __0)
        // {
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

        // [HarmonyPatch(typeof(UIWindmillCraftCountDialog), "GetUIPageData")]
        // [HarmonyPrefix]
        // private static bool OnCraftCountDecide(UIWindmillCraftCountDialog __instance, Il2CppSystem.Object __0)
        // {
        //     // if(__0 is not UIWindmillCraftCountDialogData uiData) return true;
        //     // try
        //     // {
        //     //     UIAccessor.Instance.RequestOpenMenu(UILoadKey.WindmillCraftCountDialog, uiData, true);
        //     // }
        //     // catch (Exception e)
        //     // {
        //     //     return true;
        //     // }
        // }


        //UIRequiredItemSelectPage.Decide -> When someone selects an item to add to the items
        //INIT IS AFTER GETUIPAGEDATA FOR SOME REASON


        // Commented out for now as i want to go a different approach atm but would love to have a whole new UI to select stuff from storage
        /*[HarmonyPatch(typeof(UIRequiredItemSelectPage), "InitPage")]
        [HarmonyPostfix]
        private static void OnGetUIPageData(UIRequiredItemSelectPage __instance)
        {
            if (__instance == null) return;

            if(__instance.curRecipeMasterData == null) return;
            try
            {
                // __instance.SetActive(false);
                // __instance.OpenCraftDialog(__instance.curRecipeMasterData);
            }
            catch (Exception e)
            {
                return;
            }

            //TODO: opening cooking will break this but wont crash

            //check if the

            // if (__0 is not WindmillCraftingMasterData uiData)
            // {
            //     return;
            // }

            //Doing it here as this is called before init
            // if (!_hasCraftingSelectPatch)
            // {
            //     //.transform.GetChild(0).GetChild(1) is cooking
            //     //TODO: will change this eventually to add cooking but for now just windmill
            //     var backgroundImage =
            //         __instance.transform.GetChild(0).GetChild(2).GetChild(2);
            //     var rectangle = backgroundImage.GetComponent<RectTransform>();
            //     rectangle.sizeDelta = new Vector2(rectangle.sizeDelta.x, rectangle.sizeDelta.y + 90);
            //     backgroundImage.transform.position += new Vector3(0, 0.07f, 0);
            //
            //     var child = backgroundImage.transform.GetChild(1).GetComponent<RectTransform>();
            //
            //     child.anchoredPosition = new Vector2(child.anchoredPosition.x, child.anchoredPosition.y + 45);
            //
            //     for (var i = 0; i < 5; i++)
            //     {
            //         var detailObject = backgroundImage.transform.GetChild(1).GetChild(i);
            //         var ui = detailObject.transform.GetChild(0).GetChild(3).gameObject;
            //         var storageStackBg = Object.Instantiate(ui, ui.transform.parent, true);
            //         storageStackBg.name = "HaveSelectStorageStackBG";
            //         storageStackBg.transform.position = ui.transform.position + new Vector3(0, -0.07f, 0);
            //     }
            //
            //     _hasCraftingSelectPatch = true;
            // }
            //
            // var inventoryManager = ManagedSingleton<InventoryManager>.Instance;
            // if (inventoryManager == null) return;


            // var counterForIcons = 0;
            // foreach (var tuple in uiData.RequiredItemList)
            // {
            //     var itemString = tuple.ToString();
            //     var split = itemString.Split(',');
            //     var itemId = uint.Parse(split[0].Trim('(', ' '));
            //     var stack = int.Parse(split[1].Trim(' ', ')'));
            //     LocalizedTextMeshPro textMesh;
            //
            //     try
            //     {
            //         textMesh = __instance.transform.GetChild(0).GetChild(2).GetChild(2).GetChild(1)
            //             .GetChild(counterForIcons).GetChild(0).FindChild("HaveSelectStorageStackBG").GetChild(0).GetComponent<LocalizedTextMeshPro>();
            //     }
            //     catch (System.Exception e)
            //     {
            //         counterForIcons++;
            //         continue;
            //     }
            //
            //     if (itemId == 0 || stack == 0)
            //     {
            //         textMesh.text = "";
            //         counterForIcons++;
            //         continue;
            //     }
            //
            //
            //     var storageAmount = inventoryManager.HouseStorage.itemDatas
            //         .Where(x => x.ItemId == itemId).Sum(x => x.Stack);
            //
            //     // if amount is over 999 just show 999+
            //     if (storageAmount > 999)
            //     {
            //         textMesh.text = "999+";
            //         counterForIcons++;
            //         continue;
            //     }
            //
            //     textMesh.text = storageAmount.ToString();
            //     counterForIcons++;
            // }
        }
                    */
    }
}