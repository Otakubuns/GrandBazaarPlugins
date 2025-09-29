using System;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BokuMono;
using BokuMono.Data;
using BokuMono.FieldEvent;
using BokuMono.Utility;
using HarmonyLib;
using Il2CppSystem.Collections.Generic;
using Il2CppSystem.Text;
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
        
        private static bool _hasPatched;

        /*
         * This patch is to add the extra text showing how many items are in storage under the bag amount
         */
        [HarmonyPatch(typeof(UIWindmillCraftingPage), "InitPage")]
        [HarmonyPostfix]
        private static void CreateStorageStack(UIWindmillCraftingPage __instance)
        {
            //TODO: Add a hook for going to menu if this is wiped on main menu
            if (_hasPatched) return;
            // this is the ui elemet of holding the 5 icons and item amount
            var backgroundImage =
                GameObject.Find(
                    "UserInterface(Clone)/UICamera/UIMenuCanvas/UIMenuManager/Pages/UIWindmillCraftingPage(Clone)/UIWindmillCraftingScrollGroup/UIWindmillCraftingDetail/UIRequiredItemSelector(Windmill)/");


            if (backgroundImage == null) return;

            var groupGameObject = backgroundImage.transform.GetChild(1).gameObject;
            
            if(groupGameObject == null) return;

            var rectangle = backgroundImage.GetComponent<RectTransform>();
            rectangle.sizeDelta = new Vector2(rectangle.sizeDelta.x, rectangle.sizeDelta.y + 90);
            backgroundImage.transform.position += new Vector3(0, 0.07f, 0);

            var child = backgroundImage.transform.GetChild(1).GetComponent<RectTransform>();

            child.anchoredPosition = new Vector2(child.anchoredPosition.x, child.anchoredPosition.y + 45);

            var uiWindmillDetail = __instance.transform.GetChild(0).GetChild(3).gameObject;
            var icon = uiWindmillDetail.transform.GetChild(2).gameObject;
            icon.transform.position += new Vector3(0, 0.04f, 0);
            var category = uiWindmillDetail.transform.GetChild(3).gameObject;
            category.transform.position += new Vector3(0, 0.15f, 0);

            // Creating 5 new icons for the storage amount text by copying the original ui element and placing underneath
            for (var i = 0; i < 5; i++)
            {
                var detailObject = groupGameObject.transform.GetChild(i);
                var ui = detailObject.transform.GetChild(0).GetChild(3).gameObject;
                var storageStackBg = Object.Instantiate(ui, ui.transform.parent, true);
                storageStackBg.name = "HaveStorageStackBG";
                storageStackBg.transform.position = ui.transform.position + new Vector3(0, -0.07f, 0);
            }

            _hasPatched = true;
        }

        /*
         * This patch is to update the greyed out recipe icons if they are craftable from storage
         */
        [HarmonyPatch(typeof(UIRecipeIconContent), "UpdateContentUniqueData")]
        [HarmonyPostfix]
        private static void OnRecipeIconUpdate(UIRecipeIconContent __instance, UIData __0)
        {
            if (__instance == null || __instance.cacheData == null) return;
            
            // is in a try as some reason it will throw an error sometimes and i don't know exactly why so 
            try
            {
                if (__instance.cacheData.RecipeType != RecipeMasterType.Windmill) return;
            }
            catch (Exception e)
            {
                _hasPatched = false;
                return;
            }

            var storage = ManagedSingleton<InventoryManager>.Instance.HouseStorage;
            var bag = ManagedSingleton<InventoryManager>.Instance.BagItemStorage;

            if (storage == null || bag == null)
            {
                return;
            }

            // gonna need to have amountNeeded taken from storage when crafting
            __instance.mask.enabled = !IsCraftable(__instance.cacheData.WindmillCraftData.requiredItemList);
        }

        /*
         * This function will check if a recipe is craftable from storage and bag
         * returns true if it is
         */
        private static bool IsCraftable(List<Il2CppSystem.ValueTuple<uint, int, int, int>> ingredientList)
        {
            var inventoryManager = ManagedSingleton<InventoryManager>.Instance;
            if (inventoryManager == null) return false;
            var isCraftable = true;
            // this loop into each ingredient needed for the recipe
            foreach (var tuple in ingredientList)
            {
                // since it is boxed i am just going to cut up the tostring as im just looking for the itemid and stack
                // and also i dont know how to unbox it properly
                var itemString = tuple.ToString();
                var split = itemString.Split(',');
                var itemId = uint.Parse(split[0].Trim('(', ' '));
                var stack = int.Parse(split[1].Trim(' ', ')'));
                var amountNeeded = stack;


                if (itemId == 0 || stack == 0) continue;

                var bagStorage = inventoryManager.BagItemStorage.itemDatas
                    .Where(x => x.ItemId == itemId).Sum(x => x.Stack);
                var houseStorage = inventoryManager.HouseStorage.itemDatas
                    .Where(x => x.ItemId == itemId).Sum(x => x.Stack);
                var toolStorage = inventoryManager.BagToolStorage.itemDatas
                    .Where(x => x.ItemId == itemId).Sum(x => x.Stack);

                amountNeeded -= bagStorage + toolStorage;
                if (amountNeeded > 0) amountNeeded -= houseStorage;


                if (amountNeeded <= 0) continue;

                isCraftable = false;
                break;
            }

            return isCraftable;
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


            var counterForIcons = 0;
            // its never gonna be more than 1 as im now grabbing single ingredient
            foreach (var tuple in data.RequiredItemList)
            {
                var itemString = tuple.ToString();
                var split = itemString.Split(',');
                var itemId = uint.Parse(split[0].Trim('(', ' '));
                var stack = int.Parse(split[1].Trim(' ', ')'));
                LocalizedTextMeshPro textMesh;

                try
                {
                    textMesh = __instance.transform.GetChild(1).GetChild(counterForIcons).GetChild(0)
                        .FindChild("HaveStorageStackBG").GetChild(0).GetComponent<LocalizedTextMeshPro>();
                }
                catch (System.Exception e)
                {
                    counterForIcons++;
                    continue;
                }

                if (itemId == 0 || stack == 0)
                {
                    textMesh.text = "";
                    counterForIcons++;
                    continue;
                }


                var storageAmount = inventoryManager.HouseStorage.itemDatas
                    .Where(x => x.ItemId == itemId).Sum(x => x.Stack);

                // This patches the little check icon to show if you have enough in storage(or bag)
                if (storageAmount >= stack)
                {
                    __instance.requiredItemIcon._items[counterForIcons].checkIcon.enabled = true;
                }

                // if amount is over 999 just show 999+
                if (storageAmount > 999)
                {
                    textMesh.text = "999+";
                    counterForIcons++;
                    continue;
                }

                textMesh.text = storageAmount.ToString();
                counterForIcons++;
            }
        }
        
        // IDEA -> user clicks on item to craft -> pop up amount to craft -> starts crafting it(grabs from storage first then bag)
        
        
        //OK TWO APPRAOCHES:
        // 1. have it have the ui for items from stoage(would require alot more work)
        // 2. if they have the item, just popup the amount and then when clicked craft then grab the items then from both bag & storage

        [HarmonyPatch(typeof(UIWindmillCraftingPage), "OnDecide")]
        [HarmonyPrefix]
        private static bool OnCraftDecide(UIWindmillCraftingPage __instance, RecipeIconData __0)
        {
            var accessor = UIAccessor.Instance;
            
           

            
            
            // //Make this open up just the craft count dialog
            //  try
            //  {
            //      UIAccessor.Instance.RequestOpenMenu(UILoadKey.WindmillCraftCountDialog, __0, true);
            //  }
            //  catch (Exception e)
            //  {
            //      return true;
            //  }
            // //Will change this later to add cooking but for now just windmill
            // if (__0.RecipeType != RecipeMasterType.Windmill) return true;
            //
            // if (!IsCraftable(__0.WindmillCraftData.requiredItemList)) return true;
            //
            // var uiData = __0.WindmillCraftData;
            // var uiAccessor = UIAccessor.Instance;
            // SoundManager.Instance.Play(3001, SoundManager.Category.Se, 0, false, 1f);
            // uiAccessor.RequestOpenMenu(UILoadKey.RequiredItemSelect, uiData, true);
            return true;
        }

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

        //[HarmonyPatch(typeof(UIRequiredItemSelectPage), "OnDecide")]
        
        
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