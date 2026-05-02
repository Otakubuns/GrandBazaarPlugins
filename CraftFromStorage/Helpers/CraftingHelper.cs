using System;
using System.Linq;
using BokuMono;
using BokuMono.Data;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Collections.Generic;
using UnityEngine;

namespace CraftFromStorage.Helpers;

public static class CraftingHelper
{
    /*
     * This function will check if a recipe is craftable from storage and bag
     * @param itemMasterData - the recipe to check(holds ingredient info only, not the recipe info)
     * returns true if it is
     */
    public static bool IsCraftable(IRequiredItemMasterData itemMasterData)
    {
        var masterDataManager = MasterDataManager.Instance;
        if (masterDataManager == null) return false;

        for (var i = 0; i < itemMasterData.RequiredItemCount; i++)
        {
            var itemData = itemMasterData.RequiredItemList._items[i].ToString().Split(',');
            var itemId = itemMasterData.GetRequiredItemId(i);
            var stack = int.Parse(itemData[1].Trim(' ', ')'));
            var category = itemMasterData.RequiredItemTypeList._items[i];

            if (itemId == 0 || stack == 0) continue;

            var totalAmount = 0;
            switch (category)
            {
                case RequiredItemType.Item:
                    totalAmount = CountInAllStorages(x =>
                        x != null && x.ItemId == itemId);

                    // Added check for irrefular and rare items(giant crops)
                    if (totalAmount < stack)
                    {
                        if (masterDataManager.CropMaster.TryGetIrregularId(itemId, out var irregularId))
                            totalAmount += CountInAllStorages(x => x != null && x.ItemId == irregularId);
                        else if (masterDataManager.FarmAnimalMaster.TryGetRareId(itemId, out var rareId))
                            totalAmount += CountInAllStorages(x => x != null && x.ItemId == rareId);
                    }

                    break;

                case RequiredItemType.Category:
                    totalAmount = CountInAllStorages(x =>
                        x != null && x.Category == itemId);
                    break;

                case RequiredItemType.Group:
                    itemMasterData.GroupMaster.TryGetGroupData(itemId, out var groupData);

                    foreach (var requiredItem in groupData.RequiredItemIdList)
                    {
                        if (requiredItem == 0) continue;

                        totalAmount += CountInAllStorages(x => x != null && x.ItemId == requiredItem);

                        if (masterDataManager.CropMaster.TryGetIrregularId(requiredItem, out var irregularId))
                            totalAmount += CountInAllStorages(x => x != null && x.ItemId == irregularId);
                        else if (masterDataManager.FarmAnimalMaster.TryGetRareId(requiredItem, out var rareId))
                            totalAmount += CountInAllStorages(x => x != null && x.ItemId == rareId);
                    }

                    break;
            }

            if (totalAmount < stack)
                return false;
        }

        return true;
    }

    /*
     * This function will return the amount of an item in storage only
     * @param itemId - the item to check
     * @param itemType - the type of item to check (item, category, group)
     * @param groupMaster - the group master data to use if checking a group(shouldn't be but just in case)
     */
    public static int GetStorageAmount(uint itemId, RequiredItemType itemType, IRequiredItemGroupMaster groupMaster)
    {
        switch (itemType)
        {
            case RequiredItemType.Item:
                return CountInStorage(x => x.ItemId == itemId);

            case RequiredItemType.Category:
                return CountInStorage(x => x.Category == itemId);

            case RequiredItemType.Group:
                if (!groupMaster.TryGetGroupData(itemId, out var groupData) ||
                    groupData == null) return 0;

                foreach (var requiredItem in groupData.RequiredItemIdList)
                {
                    if (requiredItem == 0) continue;

                    var totalGroupAmount = CountInStorage(x => x.ItemId == requiredItem);
                    return totalGroupAmount;
                }

                break;
            default:
                return 0;
        }

        return 0;
    }

    /*
     * This function will return the amount of multiple items in storage only
     * The improved version of GetStorageAmount using itemIds only
     * @param itemIds - the list of item ids to check
     * returns the total amount found(for text display)
     */
    public static int GetStorageAmount(List<uint> itemIds)
    {
        var masterDataManager = MasterDataManager.Instance;
        var totalAmount = 0;
        foreach (var itemId in itemIds)
        {
            totalAmount += CountInStorage(x => x != null && x.ItemId == itemId);

            if (masterDataManager.CropMaster.TryGetIrregularId(itemId, out var irregularId))
            {
                totalAmount += CountInStorage(x => x != null && x.ItemId == irregularId);
            }
            else if (masterDataManager.FarmAnimalMaster.TryGetRareId(itemId, out var rareId))
            {
                totalAmount += CountInStorage(x => x != null && x.ItemId == rareId);
            }
        }

        return totalAmount;
    }

    /*
     * This function will return the amount of an item in a specific storage
     * @param storage - the storage to check
     * @param itemId - the item to check
     * returns the total amount found
     */
    private static int GetAmount(StorageManager storage, uint itemId)
    {
        return storage.itemDatas
            .Where(x => x.ItemId == itemId)
            .Sum(x => x.Stack);
    }

    /*
     * This function will return the amount of an item in all storages(bag, house storage, tool storage)
     * @param predicate - the predicate to use to find the item(the itemid, category, groupId)
     * returns the total amount found(used in checking if craftable)
     */
    public static int CountInAllStorages(Func<ItemData, bool> predicate)
    {
        var inventoryManager = ManagedSingleton<InventoryManager>.Instance;
        // var storageAmount = inventoryManager.HouseStorage.itemDatas
        //     .Where(predicate).Sum(x => x.Stack);
        //
        // var bagAmount = inventoryManager.BagItemStorage.itemDatas
        //     .Where(predicate).Sum(x => x.Stack);
        //
        // var bagToolAmount = inventoryManager.BagToolStorage.itemDatas
        //     .Where(predicate).Sum(x => x.Stack);
        //
        // CraftFromStorage._log.LogInfo($"Storage Amount: {storageAmount}\nBag Amount: {bagAmount}\nTool Amount: {bagToolAmount}\n==========");
        //
        return inventoryManager.BagItemStorage.itemDatas
                   .Where(predicate).Sum(x => x.Stack)
               + inventoryManager.HouseStorage.itemDatas
                   .Where(predicate).Sum(x => x.Stack)
               + inventoryManager.BagToolStorage.itemDatas
                   .Where(predicate).Sum(x => x.Stack);
    }

    /*
     * This function will return the amount of an item in storage only(house storage and tool storage)
     * @param predicate - the predicate to use to find the item(the itemid, category, groupId)
     * returns the total amount found(used in getting storage amount so that's why bag storage is excluded)
     */
    private static int CountInStorage(Func<ItemData, bool> predicate)
    {
        var inventoryManager = ManagedSingleton<InventoryManager>.Instance;
        return inventoryManager.HouseStorage.itemDatas
                   .Where(predicate).Sum(x => x.Stack)
               + inventoryManager.BagToolStorage.itemDatas
                   .Where(predicate).Sum(x => x.Stack);
    }

    public static ItemData GetItemDataFromStorage(List<uint> currentRequiredItem, int stack)
    {
        var houseStorage = ManagedSingleton<InventoryManager>.Instance.HouseStorage;
        foreach (var itemData in houseStorage.itemDatas)
        {
            if (currentRequiredItem.Contains(itemData.ItemId) && itemData.Stack >= stack)
            {
                return itemData;
            }
        }

        var toolStorage = ManagedSingleton<InventoryManager>.Instance.BagToolStorage;
        return toolStorage.itemDatas.FirstOrDefault(itemData =>
            currentRequiredItem.Contains(itemData.ItemId) && itemData.Stack >= stack);
    }

    public static int GetMaxCraftAmount(Il2CppReferenceArray<RequiredItemData> requiredItemList)
    {
        try
        {
            var required = new System.Collections.Generic.Dictionary<(bool isBag, int slot), int>();
            
            foreach (var item in requiredItemList)
            {
                if (item == null) continue;
                
                foreach (var selectedItem in item.SelectedItems)
                {
                    if (selectedItem.ItemId == 0) continue;
                    
                    var key = (
                        selectedItem.Storage is BagItemStorageManager,
                        selectedItem.Slot
                    );

                    required[key] = required.TryGetValue(key, out var current)
                        ? current + item.Stack
                        : item.Stack;
                }
            }

            if (required.Count == 0) return 1;

            var minCraftable = int.MaxValue;

            foreach (var (key, needed) in required)
            {
                var (isBag, slot) = key;

                var storageAmount = isBag
                    ? InventoryManager.Instance.BagItemStorage.GetStack(slot)
                    : InventoryManager.Instance.HouseStorage.GetStack(slot);

                var max = storageAmount / needed;

                if (max < minCraftable)
                    minCraftable = max;
            }
            
            return minCraftable == int.MaxValue ? 1 : minCraftable;
        }
        catch (Exception ex)
        {
            CraftFromStorage._log.LogError($"Crafting Error: {ex}");
            return 1;
        }
    }

    /*
     * Check if there are any other ingredients in other "tabs" for adapt recipe
     */
    public static bool HaveAdaptItemsInStorage(UIRequiredItemDetail curDetail)
    {
        var isEnough = true;
        // Done this way to be dynamic
        var count = curDetail.requiredItemSelector.requiredItems.Count;
        for (var i = count - 2; i < count; i++)
        {
            var ids = curDetail.requiredItemSelector.requiredItems[i]?.ids;
            // While 1 is usually the amount needed for apart recipe i am making sure to use the stack amount in requireditemdata
            var amountNeeded = curDetail.requiredItemSelector.requiredItems[i]?.Stack;
            if (ids == null) continue;
            foreach (var id in ids)
            {
                var isThereItem = CountInStorage(x => x.ItemId == id);
                if (isThereItem >= amountNeeded) isEnough = false;
            }
        }

        return isEnough;
    }

    public static bool StorageTryUse(StorageManager storage, int slot, int stack, out int remaining)
    {
        remaining = stack;

        var items = storage.ItemDatas;
        if (items == null) return false;

        if (slot < 0 || slot >= items.Length) return false;

        var targetItem = items[slot];

        if (targetItem == null || targetItem.ItemId == 0) return false;

        return targetItem.Reduce(stack, out remaining);
    }
}