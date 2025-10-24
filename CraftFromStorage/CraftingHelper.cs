using System;
using System.Linq;
using BokuMono;
using BokuMono.Data;
using UnityEngine;

namespace CraftFromStorage;

public class CraftingHelper
{
    /*
     * This function will check if a recipe is craftable from storage and bag
     * @param itemMasterData - the recipe to check(holds ingredient info only, not the recipe info)
     * returns true if it is
     */
    public static bool IsCraftable(IRequiredItemMasterData itemMasterData)
    {
        var inventoryManager = ManagedSingleton<InventoryManager>.Instance;

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
        //TODO: for categories, it seems like it uses seperate items for each item if they belong to the same category
        //(if its flowers itll treat mint and lavender seperately) probasbly just grab first items in bag and then seperate(or leave it as is idk)
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
}