using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using BokuMono;
using BokuMono.Data;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

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
        var usedItems = new Il2CppSystem.Collections.Generic.Dictionary<uint, int>();

        for (var i = 0; i < itemMasterData.RequiredItemCount; i++)
        {
            var itemData = itemMasterData.RequiredItemList._items[i].ToString().Split(',');
            if (itemData.Length < 2) continue;
            var itemId = itemMasterData.GetRequiredItemId(i);
            var stack = int.Parse(itemData[1].Trim(' ', ')'));
            var category = itemMasterData.RequiredItemTypeList._items[i];

            if (itemId == 0 || stack == 0) continue;

            switch (category)
            {
                case RequiredItemType.Item:
                    var remaining = stack - TakeFromStorage(itemId, stack, usedItems);
                    if (remaining > 0)
                    {
                        var variantId = GetVariantId(itemId);
                        if (variantId == 0 || TakeFromStorage(variantId, remaining, usedItems) < remaining)
                            return false;
                    }

                    break;

                case RequiredItemType.Category:
                    if (CountAvailable(itemId, usedItems) < stack) return false;
                    usedItems.TryGetValue(itemId, out var usedCat);
                    usedItems[itemId] = usedCat + stack;
                    break;

                case RequiredItemType.Group:
                    itemMasterData.GroupMaster.TryGetGroupData(itemId, out var groupData);
                    var groupRemaining = stack;

                    foreach (var requiredItem in groupData.RequiredItemIdList)
                    {
                        if (requiredItem == 0 || groupRemaining <= 0) continue;
                        groupRemaining -= TakeFromStorage(requiredItem, groupRemaining, usedItems);

                        if (groupRemaining <= 0) break;

                        var variantId = GetVariantId(requiredItem);
                        if (variantId != 0)
                            groupRemaining -= TakeFromStorage(variantId, groupRemaining, usedItems);
                    }

                    if (groupRemaining > 0) return false;
                    break;
            }
        }

        return true;
    }

    /// <summary>
    /// Counts the available amount of an item in all storages,using useItems to account for already used items
    /// </summary>
    /// <param name="itemId"></param>
    /// <param name="usedItems"></param>
    /// <returns></returns>
    private static int CountAvailable(uint itemId, Il2CppSystem.Collections.Generic.Dictionary<uint, int> usedItems)
    {
        usedItems.TryGetValue(itemId, out var used);
        return CountInAllStorages(x => x != null && x.ItemId == itemId && x.IsCorruption == false) - used;
    }

    /// <summary>
    /// A kind-of implementation of original TryUse, using a usedItems dictonary instead
    /// </summary>
    /// <param name="itemId"></param>
    /// <param name="amount"></param>
    /// <param name="usedItems"></param>
    /// <returns>the amount deducted, so it can be used later so duplicates aren't counted</returns>
    private static int TakeFromStorage(uint itemId, int amount,
        Il2CppSystem.Collections.Generic.Dictionary<uint, int> usedItems)
    {
        var available = CountAvailable(itemId, usedItems);
        var taken = Math.Min(available, amount);
        if (taken <= 0) return taken;
        usedItems.TryGetValue(itemId, out var used);
        usedItems[itemId] = used + taken;
        return taken;
    }

    /// <summary>
    /// Gets the ids of the rare or irregular variant of an item (ex. tomato can be giant tomato)
    /// </summary>
    /// <param name="itemId">the id of the item to check for rare & irregular</param>
    /// <returns>id of irregular or rare item. 0 if there isn't one</returns>
    private static uint GetVariantId(uint itemId)
    {
        if (MasterDataManager.Instance.CropMaster.TryGetIrregularId(itemId, out var irregularId))
            return irregularId;
        if (MasterDataManager.Instance.FarmAnimalMaster.TryGetRareId(itemId, out var rareId))
            return rareId;
        return 0;
    }


    /// <summary>
    /// Return the amount of multiple items in storage only.
    /// </summary>
    /// <param name="itemIds">List of item ids to check.</param>
    /// <returns>Total amount found.</returns>
    public static int GetStorageAmount(Il2CppSystem.Collections.Generic.List<uint> itemIds)
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
    public static ItemData GetItemDataFromStorage(Il2CppSystem.Collections.Generic.List<uint> currentRequiredItem,
        int stack)
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
    /// Gets max craft amount, copying vanilla approach with housestorage added
    /// </summary>
    /// <param name="requiredItemList">List of items required to craft</param>
    /// <returns>Maximum amount that can be crafted.</returns>
    public static int GetMaxCraftAmount(Il2CppReferenceArray<RequiredItemData> requiredItemList)
    {
        try
        {
            var bagClone = BagItemStorageManager.Clone(InventoryManager.Instance.BagItemStorage);
            var houseClone = CloneHouseStorage(InventoryManager.Instance.HouseStorage);

            return TryCraft(requiredItemList, bagClone, houseClone, 1);
        }
        catch (Exception ex)
        {
            CraftFromStorage._log.LogError("Error calculating max craft amount: " + ex.Message);
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
    /// <param name="remaining">The amount remaining after reducing.</param>
    /// <returns>True if the Item was successfully reduced</returns>
    public static bool StorageTryUse(StorageManager storage, int slot, int stack, out int remaining)
    {
        remaining = stack;

        var items = storage.ItemDatas;
        if (items == null) return false;

        if (slot < 0 || slot >= items.Length) return false;

        var targetItem = items[slot];
        if (targetItem == null) return false;

        return targetItem.Reduce(stack, out remaining);
    }

    /// <summary>
    /// Clone for houseStorage as there isn't one in vanilla code
    /// </summary>
    /// <param name="storage">the HouseSotrage to clone</param>
    /// <returns>cloned storage</returns>
    private static HouseStorageManager CloneHouseStorage(HouseStorageManager storage)
    {
        var clone = new HouseStorageManager();
        var original = storage.ItemDatas;

        for (var i = 0; i < original.Length; i++)
            clone.ItemDatas[i] = original[i] != null ? ItemData.Clone(original[i]) : null;

        return clone;
    }


    /// <summary>
    /// Vanilla recreation of TryCraft to allow more than just ItemBagStorage, reduces the stack of items in cloned storage until it cannot craft anymore
    /// </summary>
    /// <param name="requiredItemList"></param>
    /// <param name="bagClone"></param>
    /// <param name="houseClone"></param>
    /// <param name="count"></param>
    /// <returns>The amount of times the item was reduced, always starting at 1</returns>
    private static int TryCraft(Il2CppReferenceArray<RequiredItemData> requiredItemList, BagItemStorageManager bagClone,
        HouseStorageManager houseClone, int count)
    {
        foreach (var item in requiredItemList)
        {
            if (item == null || item.SelectItemStack == 0) continue;

            foreach (var selectedItem in item.SelectedItems)
            {
                var primaryStorage = selectedItem.Storage is BagItemStorageManager
                    ? (StorageManager) bagClone
                    : houseClone;

                var secondaryStorage = selectedItem.Storage is BagItemStorageManager
                    ? (StorageManager) houseClone
                    : bagClone;

                var primaryData = ItemData.Clone(selectedItem);

                if (!IsHavedCombined(primaryData, primaryStorage, secondaryStorage))
                    return count;

                UseStorage(primaryData, primaryStorage);
                if (primaryData.Stack > 0)
                    UseStorage(primaryData, secondaryStorage);
            }
        }
        
        return TryCraft(requiredItemList, bagClone, houseClone, count + 1);
    }

    /// <summary>
    /// Modded implementation of the original games Use, was having issues with stack reducing
    /// </summary>
    /// <param name="data"></param>
    /// <param name="storage"></param>
    public static void UseStorage(ItemData data, StorageManager storage)
    {
        var items = storage.ItemDatas;
        if (items == null) return;

        for (var i = 0; i < items.Length; i++)
        {
            if (data.Stack <= 0) break;
            if (items[i] == null || !items[i].IsMatch(data)) continue;

            StorageTryUse(storage, i, data.Stack, out var remaining);
            data.Stack = remaining;
        }
    }
    
    /// <summary>
    /// A modded approach to vanilla's IsHaved, checks if the combined amount of matching items in both storages is enough
    /// </summary>
    /// <param name="data">The item to check for, including the required stack amount.</param>
    /// <param name="primary">The primary storage to check (bag or house).</param>
    /// <param name="secondary">The secondary storage to also check (bag or house).</param>
    /// <returns>True if the combined total from both storages is more or equal to required stack amount.</returns>
    private static bool IsHavedCombined(ItemData data, StorageManager primary, StorageManager secondary)
    {
        var primaryCount = GetMatchingCount(data, primary);
        var secondaryCount = GetMatchingCount(data, secondary);
        return (primaryCount + secondaryCount) >= data.Stack;
    }

    /// <summary>
    /// Gets the count of matching items in both storage(matching both quality and rotten status)
    /// </summary>
    /// <param name="data"></param>
    /// <param name="storage"></param>
    /// <returns></returns>
    private static int GetMatchingCount(ItemData data, StorageManager storage)
    {
        var total = 0;
        var items = storage.ItemDatas;
        if (items == null) return 0;
        foreach (var item in items)
        {
            if (item == null) continue;
            if (item.IsMatch(data))
                total += item.Stack;
        }

        return total;
    }
}