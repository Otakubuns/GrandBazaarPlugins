using System;
using System.Linq;
using BokuMono;
using BokuMono.Data;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Collections.Generic;

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

        for (int i = 0; i < itemMasterData.RequiredItemCount; i++)
        {
            var itemData = itemMasterData.RequiredItemList._items[i].ToString().Split(',');
            var itemId = uint.Parse(itemData[0].Trim('(', ' '));
            var stack = int.Parse(itemData[1].Trim(' ', ')'));
            var category = itemMasterData.RequiredItemTypeList._items[i];

            if (itemId == 0 || stack == 0) continue;

            var totalAmount = 0;
            switch (category)
            {
                case RequiredItemType.Item:
                    totalAmount = CountInAllStorages(x =>
                        x.ItemId == itemId);

                    // Added check for irrefular and rare items(giant crops)
                    if (totalAmount < stack)
                    {
                        if (masterDataManager.CropMaster.TryGetIrregularId(itemId, out var irregularId))
                            totalAmount += CountInAllStorages(x => x.ItemId == irregularId);
                        else if (masterDataManager.FarmAnimalMaster.TryGetRareId(itemId, out var rareId))
                            totalAmount += CountInAllStorages(x => x.ItemId == rareId);
                    }

                    break;

                case RequiredItemType.Category:
                    totalAmount = CountInAllStorages(x =>
                        x.Category == itemId);
                    break;

                case RequiredItemType.Group:
                    if (!itemMasterData.GroupMaster.TryGetGroupData(itemId, out var groupData) ||
                        groupData == null) continue;

                    foreach (var requiredItem in groupData.RequiredItemIdList)
                    {
                        if (requiredItem == 0) continue;

                        totalAmount = CountInAllStorages(x => x.ItemId == requiredItem);

                        if (masterDataManager.CropMaster.TryGetIrregularId(requiredItem, out var irregularId))
                            totalAmount += CountInAllStorages(x => x.ItemId == irregularId);
                        else if (masterDataManager.FarmAnimalMaster.TryGetRareId(requiredItem, out var rareId))
                            totalAmount += CountInAllStorages(x => x.ItemId == rareId);
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
            totalAmount += CountInStorage(x => x.ItemId == itemId);

            if (masterDataManager.CropMaster.TryGetIrregularId(itemId, out var irregularId))
            {
                totalAmount += CountInStorage(x => x.ItemId == irregularId);
            }
            else if (masterDataManager.FarmAnimalMaster.TryGetRareId(itemId, out var rareId))
            {
                totalAmount += CountInStorage(x => x.ItemId == rareId);
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
    private static int CountInAllStorages(Func<ItemData, bool> predicate)
    {
        var inventoryManager = ManagedSingleton<InventoryManager>.Instance;
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
            var craftableAmounts = Array.Empty<int>();
            // ok so grab all the items and there stacks and divide it by the amount needed and then at the end just check for lowest amount and thats amount to craft
            foreach (var item in requiredItemList)
            {
                if (item == null) continue;
                foreach (var selectedItem in item.SelectedItems)
                {
                    if (selectedItem.ItemId == 0) continue;
                    // make sure that if it is an adapt recipe that it doesnt use that to get max craft amount(since its optional)
                    CraftFromStorage._log.LogInfo($"Getting craftable amount for itemId: {selectedItem.ItemId}");
                    var storageAmount = GetAmount(selectedItem.Storage, selectedItem.ItemId);

                    selectedItem.Storage.itemDatas.ToList().ForEach(x =>
                    {
                        if (x.ItemId == 0 || x.Stack == 0) return;
                        CraftFromStorage._log.LogInfo($"Storage ItemId: {x.ItemId} Stack: {x.Stack}");
                    });
                    CraftFromStorage._log.LogInfo(
                        $"Found {storageAmount} in storage for itemId: {selectedItem.ItemId} with stack needed: {item.Stack}");
                    var maxCraftable = storageAmount / item.Stack;
                    CraftFromStorage._log.LogInfo($"Max craftable for itemId {selectedItem.ItemId} is {maxCraftable}");
                    craftableAmounts = craftableAmounts.Append(maxCraftable).ToArray();
                }
            }
            
            CraftFromStorage._log.LogInfo($"Craftable amounts: {string.Join(", ", craftableAmounts)}");

            return craftableAmounts.Length > 0 ? craftableAmounts.Min() : 1;
        }
        catch (Exception)
        {
            return 1;
        }
    }

    public static void RemoveItemsFromStorage(SlotItemData useItemData, IRecipeMasterData recipe, int count)
    {
        var houseStorage = ManagedSingleton<InventoryManager>.Instance.HouseStorage;
        var amountToRemove = useItemData.Stack * count - useItemData.Stack;

        houseStorage.itemDatas[useItemData.Slot]?.Reduce(amountToRemove, out _);
    }
}