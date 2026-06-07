using System;
using System.Linq;
using BokuMono;
using BokuMono.Data;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Collections.Generic;

namespace CraftFromStorage.Helpers;

/// <summary>
/// CraftingHelper is a utility class that provides methods to check if a recipe can be crafted & storage amounts.
/// </summary>
public static class CraftingHelper
{
    /// <summary>
    /// Checks if a recipe is craftable from storage and bag.
    /// </summary>
    /// <param name="itemMasterData">The recipe to check, which holds ingredient information only.</param>
    /// <returns>Returns true if the recipe can be crafted using the available items in storage and bag.</returns>
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
                        x != null && x.ItemId == itemId && x.IsCorruption == false);

                    // Added check for irrefular and rare items(giant crops)
                    if (totalAmount < stack)
                    {
                        if (masterDataManager.CropMaster.TryGetIrregularId(itemId, out var irregularId))
                            totalAmount += CountInAllStorages(x => x != null && x.ItemId == irregularId && x.IsCorruption == false);
                        else if (masterDataManager.FarmAnimalMaster.TryGetRareId(itemId, out var rareId))
                            totalAmount += CountInAllStorages(x => x != null && x.ItemId == rareId && x.IsCorruption == false);
                    }

                    break;

                case RequiredItemType.Category:
                    totalAmount = CountInAllStorages(x =>
                        x != null && x.Category == itemId && x.IsCorruption == false);
                    break;

                case RequiredItemType.Group:
                    itemMasterData.GroupMaster.TryGetGroupData(itemId, out var groupData);

                    foreach (var requiredItem in groupData.RequiredItemIdList)
                    {
                        if (requiredItem == 0) continue;

                        totalAmount += CountInAllStorages(x => x != null && x.ItemId == requiredItem && x.IsCorruption == false);

                        if (masterDataManager.CropMaster.TryGetIrregularId(requiredItem, out var irregularId))
                            totalAmount += CountInAllStorages(x => x != null && x.ItemId == irregularId && x.IsCorruption == false);
                        else if (masterDataManager.FarmAnimalMaster.TryGetRareId(requiredItem, out var rareId))
                            totalAmount += CountInAllStorages(x => x != null && x.ItemId == rareId && x.IsCorruption == false);
                    }

                    break;
            }

            if (totalAmount < stack)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Return the amount of multiple items in storage only.
    /// </summary>
    /// <param name="itemIds">List of item ids to check.</param>
    /// <returns>Total amount found.</returns>
    public static int GetStorageAmount(List<uint> itemIds)
    {
        var masterDataManager = MasterDataManager.Instance;
        var totalAmount = 0;
        foreach (var itemId in itemIds)
        {
            totalAmount += CountInStorage(x => x != null && x.ItemId == itemId && x.IsCorruption == false);

            if (masterDataManager.CropMaster.TryGetIrregularId(itemId, out var irregularId))
            {
                totalAmount += CountInStorage(x => x != null && x.ItemId == irregularId && x.IsCorruption == false);
            }
            else if (masterDataManager.FarmAnimalMaster.TryGetRareId(itemId, out var rareId))
            {
                totalAmount += CountInStorage(x => x != null && x.ItemId == rareId && x.IsCorruption == false);
            }
        }

        return totalAmount;
    }

    /// <summary>
    /// Return the amount of an item in specific storage
    /// </summary>
    /// <param name="storage">Storage type to search in.</param>
    /// <param name="itemId">ID of the item to search for.</param>
    /// <returns>Amount found.</returns>
    private static int GetAmount(StorageManager storage, uint itemId)
    {
        return storage.itemDatas
            .Where(x => x.ItemId == itemId)
            .Sum(x => x.Stack);
    }

    /// <summary>
    /// Counts the total amount of items across all storages (bag, house storage, tool storage) that match the condition.
    /// </summary>
    /// <param name="predicate">Condition to look for.</param>
    /// <returns>Total amount combined in all storages.</returns>
    public static int CountInAllStorages(Func<ItemData, bool> predicate)
    {
        var inventoryManager = ManagedSingleton<InventoryManager>.Instance;
        return inventoryManager.BagItemStorage.itemDatas
                   .Where(predicate).Sum(x => x.Stack)
               + inventoryManager.HouseStorage.itemDatas
                   .Where(predicate).Sum(x => x.Stack)
               + inventoryManager.BagToolStorage.itemDatas
                   .Where(predicate).Sum(x => x.Stack);
    }

    /// <summary>
    /// Will return the amount of an item in storage only. (house storage and tool storage)
    /// </summary>
    /// <param name="predicate">The condition to search for.</param>
    /// <returns>The total amount of items in all storages.</returns>
    private static int CountInStorage(Func<ItemData, bool> predicate)
    {
        var inventoryManager = ManagedSingleton<InventoryManager>.Instance;
        return inventoryManager.HouseStorage.itemDatas
                   .Where(predicate).Sum(x => x.Stack)
               + inventoryManager.BagToolStorage.itemDatas
                   .Where(predicate).Sum(x => x.Stack);
    }

    /// <summary>
    /// Gets the ItemData of an item in storage that matches the id and stack amount
    /// </summary>
    /// <param name="currentRequiredItem">List of required items ids to craft</param>
    /// <param name="stack">Amount needed to craft</param>
    /// <returns>ItemData of the found item</returns>
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

    /// <summary>
    /// Gets the maximum times a recipe can be crafted based on the required items and their amounts in storage and bag.
    /// </summary>
    /// <param name="requiredItemList">List of items required to craft</param>
    /// <returns>Maximum amount that can be crafted.</returns>
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
    
    /// <summary>
    /// Check if there are any other ingredients in other "tabs" for adapt(arrange) recipe
    /// </summary>
    /// <param name="requiredItems">The list of items the recipe needs</param>
    /// <returns>True if there is enough items(have no extra items)</returns>
    public static bool HaveAdaptItemsInStorage(Il2CppReferenceArray<RequiredItemData> requiredItems)
    {
        var isEnough = true;
        // Done this way to be dynamic
        var count = requiredItems.Count;
        for (var i = count - 2; i < count; i++)
        {
            var ids = requiredItems[i]?.ids;
            // While 1 is usually the amount needed for apart recipe i am making sure to use the stack amount in requireditemdata
            var amountNeeded = requiredItems[i]?.Stack;
            if (ids == null) continue;
            foreach (var id in ids)
            {
                var isThereItem = CountInStorage(x => x.ItemId == id && x.IsCorruption == false);
                if (isThereItem >= amountNeeded) isEnough = false;
            }
        }

        return isEnough;
    }

    /// <summary>
    /// An implementation of the original games TryUse, reduces the items amount by the parameter
    /// </summary>
    /// <param name="storage">The storage manager to use from.</param>
    /// <param name="slot">The item slot/index.</param>
    /// <param name="stack">The amount to reduce by.</param>
    /// <param name="remaining">The amount reamining after reducing.</param>
    /// <returns>True if the Item was successfully reduced</returns>
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