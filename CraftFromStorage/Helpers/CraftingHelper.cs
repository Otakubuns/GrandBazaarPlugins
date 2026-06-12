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
    /// Checks if a recipe is craftable from storage and bag. Similar to vanilla IsEnough.
    /// </summary>
    /// <param name="itemMasterData">The recipe to check, which holds ingredient information only.</param>
    /// <returns>Returns true if the recipe can be crafted using the available items in storage and bag.</returns>
    public static bool IsCraftable(IRequiredItemMasterData itemMasterData)
    {
        var masterDataManager = MasterDataManager.Instance;
        if (masterDataManager == null) return false;

        var bagClone = BagItemStorageManager.Clone(InventoryManager.Instance.BagItemStorage);
        var houseClone = CloneHouseStorage(InventoryManager.Instance.HouseStorage);
        if (bagClone == null || houseClone == null) return false;

        for (var i = 0; i < itemMasterData.RequiredItemCount; i++)
        {
            var itemData = itemMasterData.RequiredItemList._items[i].ToString().Trim('(', ')');
            var parts = itemData.Split(',');
            if (parts.Length < 4) return false;

            var itemId = itemMasterData.GetRequiredItemId(i);
            if (!int.TryParse(parts[1].Trim(), out var stack)) stack = 0;
            if (!int.TryParse(parts[2].Trim(), out var quality)) quality = 0;
            if (!int.TryParse(parts[3].Trim(), out var freshness)) freshness = 0;
            var category = itemMasterData.RequiredItemTypeList._items[i];

            if (itemId == 0 || stack == 0) continue;

            switch (category)
            {
                case RequiredItemType.Item:
                    // For rotten items
                    if (itemId == 999999)
                    {
                        // fertilizer: any corrupted(rotten) item counts
                        var rottenRemainder = stack;
                        foreach (var storage in new StorageManager[] {bagClone, houseClone})
                        {
                            for (var slot = 0; slot < storage.ItemDatas.Length && rottenRemainder > 0; slot++)
                            {
                                var item = storage.ItemDatas[slot];
                                if (item == null || !item.IsCorruption) continue;
                                StorageTryUse(storage, slot, rottenRemainder, out rottenRemainder);
                            }
                        }

                        if (rottenRemainder > 0) return false;
                        break;
                    }

                    var checkItem = ItemData.Create(itemId, stack, quality, freshness);
                    if (checkItem.IsTool)
                    {
                        if (!InventoryManager.Instance.BagToolStorage.IsHaved(checkItem.ItemId)) return false;
                        break;
                    }

                    if (!IsHavedCombined(checkItem, bagClone, houseClone, true))
                    {
                        var variantId = GetVariantId(itemId);
                        if (variantId == 0)
                        {
                            return false;
                        }

                        checkItem = ItemData.Create(variantId, stack, quality, freshness);
                        if (!IsHavedCombined(checkItem, bagClone, houseClone, true))
                            return false;
                    }

                    UseStorage(checkItem, bagClone);
                    if (checkItem.Stack > 0)
                        UseStorage(checkItem, houseClone);
                    break;

                case RequiredItemType.Category:
                    // I dont think i've come across category being used but here just in case
                    var categoryRemaining = stack;
                    foreach (var storage in new StorageManager[] {bagClone, houseClone})
                    {
                        for (var slot = 0; slot < storage.ItemDatas.Length && categoryRemaining > 0; slot++)
                        {
                            var item = storage.ItemDatas[slot];
                            if (item == null) continue;
                            if (item.Category != (short) itemId) continue;
                            if (!item.IsConditions(item.ItemId, quality, freshness)) continue;
                            StorageTryUse(storage, slot, categoryRemaining, out categoryRemaining);
                        }
                    }

                    if (categoryRemaining > 0) return false;
                    break;
                case RequiredItemType.Group:
                    if (!itemMasterData.GroupMaster.TryGetGroupData(itemId, out var groupData) || groupData == null)
                        return false;
                    var groupRemaining = stack;
                    foreach (var requiredItem in groupData.RequiredItemIdList)
                    {
                        if (requiredItem == 0 || groupRemaining <= 0) break;

                        foreach (var storage in new StorageManager[] {bagClone, houseClone})
                        {
                            for (var slot = 0; slot < storage.ItemDatas.Length && groupRemaining > 0; slot++)
                            {
                                var item = storage.ItemDatas[slot];
                                if (item == null || item.ItemId != requiredItem) continue;
                                if (!item.IsConditions(item.ItemId, quality, freshness)) continue;
                                StorageTryUse(storage, slot, groupRemaining, out groupRemaining);
                            }
                        }

                        if (groupRemaining > 0)
                        {
                            var variantId = GetVariantId(requiredItem);
                            if (variantId != 0)
                                foreach (var storage in new StorageManager[] {bagClone, houseClone})
                                {
                                    for (var slot = 0; slot < storage.ItemDatas.Length && groupRemaining > 0; slot++)
                                    {
                                        var item = storage.ItemDatas[slot];
                                        if (item == null || item.ItemId != variantId) continue;
                                        if (!item.IsConditions(item.ItemId, quality, freshness)) continue;
                                        StorageTryUse(storage, slot, groupRemaining, out groupRemaining);
                                    }
                                }
                        }
                    }

                    if (groupRemaining > 0)
                        return false;

                    break;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets the id of the rare or irregular variant of an item. (ex. tomato can be giant tomato)
    /// </summary>
    /// <param name="itemId">the id of the item to check for rare or irregular.</param>
    /// <returns>id of irregular or rare item. 0 if there isn't one.</returns>
    private static uint GetVariantId(uint itemId)
    {
        if (MasterDataManager.Instance.CropMaster.TryGetIrregularId(itemId, out var irregularId))
            return irregularId;
        return MasterDataManager.Instance.FarmAnimalMaster.TryGetRareId(itemId, out var rareId) ? rareId : 0;
    }

    /// <summary>
    /// Counts the total amount of items across all storages (bag, house storage, tool storage) that match the condition.
    /// </summary>
    /// <param name="predicate">The condition(s) to search for.</param>
    /// <returns>Total amount combined in all storages.</returns>
    public static int CountInAllStorages(Func<ItemData, bool> predicate)
    {
        var inventoryManager = ManagedSingleton<InventoryManager>.Instance;
        return inventoryManager.BagItemStorage.itemDatas
                   .Where(predicate).Sum(x => x.Stack)
               + inventoryManager.HouseStorage.itemDatas
                   .Where(predicate).Sum(x => x.Stack);
    }

    /// <summary>
    /// Will return the amount of an item in storage only. (house storage)
    /// </summary>
    /// <param name="predicate">The condition(s) to search for.</param>
    /// <returns>The total amount of items in all storages.</returns>
    private static int CountInHouseStorage(Func<ItemData, bool> predicate)
    {
        var inventoryManager = ManagedSingleton<InventoryManager>.Instance;
        return inventoryManager.HouseStorage.itemDatas
            .Where(predicate).Sum(x => x.Stack);
    }

    /// <summary>
    /// Gets max craft amount, copying vanilla approach, with HouseStorage added.
    /// </summary>
    /// <param name="requiredItemList">List of items required to craft.</param>
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
    /// Check if there are any other ingredients in other "tabs" for adapt(arrange) recipe.
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
            // While 1 is usually the amount needed for adapt recipe I am making sure to use the stack amount in requireditemdata
            var amountNeeded = requiredItems[i]?.Stack;
            if (ids == null) continue;
            foreach (var id in ids)
            {
                var isThereItem = CountInHouseStorage(x => x != null &&
                                                           x.IsConditions(id, requiredItems[i].Quality,
                                                               requiredItems[i].Freshness));
                if (isThereItem >= amountNeeded) isEnough = false;
            }
        }

        return isEnough;
    }

    /// <summary>
    /// An implementation of the original games TryUse, reduces the items amount by the parameter.
    /// </summary>
    /// <param name="storage">The storage manager to use from.</param>
    /// <param name="slot">The item slot/index.</param>
    /// <param name="stack">The amount to reduce by.</param>
    /// <param name="remaining">The amount remaining after reducing.</param>
    /// <returns>True if the Item was successfully reduced.</returns>
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
    /// Clone for houseStorage as there isn't one in vanilla code.
    /// </summary>
    /// <param name="storage">The HouseStorage to clone.</param>
    /// <returns>Cloned HouseStorageManager.</returns>
    public static HouseStorageManager CloneHouseStorage(HouseStorageManager storage)
    {
        var clone = new HouseStorageManager();
        var original = storage.ItemDatas;

        for (var i = 0; i < original.Length; i++)
            clone.ItemDatas[i] = original[i] != null ? ItemData.Clone(original[i]) : null;

        return clone;
    }


    /// <summary>
    /// Vanilla recreation of TryCraft to allow more than just ItemBagStorage, reduces the stack of items in cloned storage until it cannot craft anymore.
    /// </summary>
    /// <param name="requiredItemList">The list of items used in the recipe.</param>
    /// <param name="bagClone">Cloned BagItemStorageManager. If not cloned, it will consume the items.</param>
    /// <param name="houseClone">Cloned HouseStorageManager. If not cloned, it will consume the items.</param>
    /// <param name="count">The amount currently craftable(increments each time it's successfully craftable).</param>
    /// <returns>The amount of times the item is craftable, always starting at 1.</returns>
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
    /// Modded implementation of the original games Use, was having issues with stack reducing.
    /// </summary>
    /// <param name="data">The Item to look for to reduce.</param>
    /// <param name="storage">The storage to reduce the item from.</param>
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
    /// Find the index slot of an item, with quality and freshness
    /// </summary>
    /// <param name="itemId">ItemID to look for.</param>
    /// <param name="quality">Quality of the item(often times -1)</param>
    /// <param name="freshness">Freshness of the item(often times -1)</param>
    /// <param name="storage">The storage to look for the item.</param>
    /// <param name="startIndex">The starting index (optional).</param>
    /// <param name="endIndex">The ending index (optional).</param>
    /// <returns>Slot index, -1 if not.</returns>
    private static int FindItemSlot(uint itemId, int quality, int freshness, StorageManager storage, int startIndex = 0,
        int endIndex = -1)
    {
        var end = endIndex == -1 ? storage.itemDatas.Count : endIndex;
        for (var i = startIndex; i < end; i++)
        {
            var item = storage.itemDatas[i];
            if (item == null) continue;

            if (item.IsConditions(itemId, quality, freshness))
                return i;
            
            var variantId = GetVariantId(itemId);
            if (variantId != 0 && item.IsConditions(variantId, quality, freshness))
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Allows finding item slot by custom predicate, used for rotten food search.
    /// </summary>
    /// <param name="predicate">The condition(s) to search for.</param>
    /// <param name="storage">The storage to look for the item.</param>
    /// <param name="startIndex">The starting index (optional).</param>
    /// <param name="endIndex">The ending index (optional).</param>
    /// <returns>Slot index, -1 if not.</returns>
    private static int FindItemSlot(Func<ItemData, bool> predicate, StorageManager storage, int startIndex = 0,
        int endIndex = -1)
    {
        var end = endIndex == -1 ? storage.itemDatas.Count : endIndex;
        for (var i = startIndex; i < end; i++)
        {
            var item = storage.itemDatas[i];
            if (item != null && predicate(item))
                return i;
        }

        return -1;
    }

    /// <summary>
    /// A modded approach to vanilla's IsHaved, checks if the combined amount of matching items in both storages is enough.
    /// </summary>
    /// <param name="data">The item to check for, including the required stack amount.</param>
    /// <param name="primary">The primary storage to check (bag or house).</param>
    /// <param name="secondary">The secondary storage to also check (bag or house).</param>
    /// <param name="useConditions">Use IsConditions instead of IsMatch.</param>
    /// <returns>True if the combined total from both storages is more or equal to required stack amount.</returns>
    private static bool IsHavedCombined(ItemData data, StorageManager primary, StorageManager secondary,
        bool useConditions = false)
    {
        var primaryCount = GetMatchingCount(data, primary, useConditions);
        var secondaryCount = GetMatchingCount(data, secondary, useConditions);
        return (primaryCount + secondaryCount) >= data.Stack;
    }

    /// <summary>
    /// Gets the count of matching items in both storage(matching both quality and rotten status).
    /// </summary>
    /// <param name="data">ItemData to search for.</param>
    /// <param name="storage">Storage to look through for item.</param>
    /// <param name="useConditions">Use IsConditions instead of IsMatch.</param>
    /// <returns>The amount of that item in the storage.</returns>
    private static int GetMatchingCount(ItemData data, StorageManager storage, bool useConditions = false)
    {
        var total = 0;
        var items = storage.ItemDatas;
        if (items == null) return 0;
        foreach (var item in items)
        {
            if (item == null) continue;
            if (useConditions ? item.IsConditions(data.ItemId, data.Quality, data.FreshnessValue) : item.IsMatch(data))
                total += item.Stack;
        }

        return total;
    }

    /// <summary>
    /// Finds the next page for item needed, if it isn't on the same page.
    /// </summary>
    /// <param name="uiRequiredItemSelectPage">Current UIRequiredItemSelectPage.</param>
    /// <param name="pageNumber">The current page number.</param>
    /// <returns>The page number the item was found on. -1 if not found or on same page.</returns>
    public static int FindNextItemPage(UIRequiredItemSelectPage uiRequiredItemSelectPage,
        int pageNumber)
    {
        var slot = uiRequiredItemSelectPage.curDetail.requiredItemSelector.selectIndex;
        var itemSelector = uiRequiredItemSelectPage.curDetail.requiredItemSelector.requiredItems[slot];
        if (itemSelector == null)
            return -1;

        if (itemSelector.SelectItemStack == itemSelector.Stack)
            return -1;

        var quality = itemSelector.Quality;
        var freshness = itemSelector.Freshness;
        foreach (var id in itemSelector.ids)
        {
            //Rotten check
            if (id == 999999)
            {
                var rottenSlot = FindItemSlot(x => x.IsCorruption, InventoryManager.Instance.BagItemStorage);
                if (rottenSlot != -1)
                    return 0;

                rottenSlot = FindItemSlot(x => x.IsCorruption, InventoryManager.Instance.HouseStorage);
                if (rottenSlot != -1)
                    return GetStoragePageNumber(rottenSlot);

                continue;
            }

            if (IsItemOnCurrentPage(pageNumber, id, quality, freshness)) continue;

            var newSlot = FindItemSlot(id, quality, freshness, InventoryManager.Instance.BagItemStorage);
            if (newSlot != -1) return 0;

            newSlot = FindItemSlot(id, quality, freshness, InventoryManager.Instance.HouseStorage);
            if (newSlot != -1) return GetStoragePageNumber(newSlot);
        }

        return -1;
    }

    /// <summary>
    /// Gets the page number for the storage slot, storage has 32 slots and starts at 0.
    /// </summary>
    /// <param name="slot">The number the item is at</param>
    /// <returns>The page number</returns>
    private static int GetStoragePageNumber(int slot)
    {
        return (slot / 32) + 1;
    }

    /// <summary>
    /// Checks if the item is on the current page.
    /// </summary>
    /// <param name="pageNumber">The current storage page.</param>
    /// <param name="itemId">The itemId to check.</param>
    /// <param name="quality">Quality of the item(often times -1)</param>
    /// <param name="freshness">Freshness of the item(often times -1)</param>
    /// <returns>True if an item was found on the page. False if not.</returns>
    private static bool IsItemOnCurrentPage(int pageNumber, uint itemId, int quality, int freshness)
    {
        try
        {
            //0 is bag storage so check bag anything more is housestorage
            if (pageNumber == 0)
            {
                if (itemId == 999999)
                    return FindItemSlot(x => x.IsCorruption, InventoryManager.Instance.BagItemStorage) != -1;
                return FindItemSlot(itemId, quality, freshness, InventoryManager.Instance.BagItemStorage) != -1;
            }

            var index = pageNumber > 0 ? (pageNumber - 1) * 32 : 0;
            var endIndex = Math.Min(index + 32, InventoryManager.Instance.HouseStorage.itemDatas.Count);

            if (itemId != 999999)
                return FindItemSlot(itemId, quality, freshness, InventoryManager.Instance.HouseStorage, index, endIndex) != -1;

            return FindItemSlot(x => x.IsCorruption, InventoryManager.Instance.HouseStorage, index, endIndex) != -1;
        }
        catch (Exception ex)
        {
            CraftFromStorage._log.LogError("Error checking current page items: " + ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Replicates the count logic used for crafting but using HouseStorage, counts and reduces from storage.
    /// </summary>
    /// <param name="cloneHouseStorage">Cloned HouseManager, if using non-cloned it will get rid of the items.</param>
    /// <param name="requiredItemData">RequiredItem data to loop through.</param>
    /// <returns>The amount in storage.</returns>
    public static int CountAndConsumeFromStorage(HouseStorageManager cloneHouseStorage,
        RequiredItemData requiredItemData)
    {
        var stack = requiredItemData.Stack;
        var freshness = requiredItemData.Freshness;
        var quality = requiredItemData.Quality;
        var type = requiredItemData.Type;
        var ids = requiredItemData.ids;
        var count = 0;

        switch (type)
        {
            case RequiredItemType.Item:
            {
                var itemId = ids._items[0];
                if (itemId == 999999)
                {
                    foreach (var item in cloneHouseStorage.ItemDatas)
                    {
                        if (item == null || !item.IsCorruption) continue;
                        count += item.Stack;
                        if (stack > 0) item.Reduce(stack, out stack);
                    }
                }
                else
                {
                    var variantId = GetVariantId(itemId);
                    foreach (var item in cloneHouseStorage.ItemDatas)
                    {
                        if (item == null) continue;
                        if (item.IsConditions(itemId, quality, freshness))
                        {
                            count += item.Stack;
                            if (stack > 0) item.Reduce(stack, out stack);
                        }
                        else if (variantId != 0 && item.IsConditions(variantId, quality, freshness))
                        {
                            count += item.Stack;
                            if (stack > 0) item.Reduce(stack, out stack);
                        }
                    }
                }

                break;
            }
            case RequiredItemType.Category:
            {
                var categoryId = ids._items[0];
                foreach (var item in cloneHouseStorage.ItemDatas)
                {
                    if (item == null) continue;
                    if (item.Category != (short) categoryId) continue;
                    if (!item.IsConditions(item.ItemId, quality, freshness)) continue;
                    count += item.Stack;
                    if (stack > 0) item.Reduce(stack, out stack);
                }

                break;
            }
            case RequiredItemType.Group:
            {
                for (var g = 0; g < ids._size; g++)
                {
                    var itemId = ids._items[g];
                    var variantId = GetVariantId(itemId);
                    for (var i = 0; i < cloneHouseStorage.CurrentCapacity; i++)
                    {
                        var item = cloneHouseStorage.ItemDatas[i];
                        if (item == null) continue;
                        if (item.IsConditions(itemId, quality, freshness))
                        {
                            count += item.Stack;
                            if (stack > 0) item.Reduce(stack, out stack);
                        }
                        else if (variantId != 0 && item.IsConditions(variantId, quality, freshness))
                        {
                            count += item.Stack;
                            if (stack > 0) item.Reduce(stack, out stack);
                        }
                    }
                }

                break;
            }
        }

        return count;
    }
}