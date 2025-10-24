
using UnityEngine;
using BokuMono;
using Il2CppSystem.Collections.Generic;

namespace CraftFromStorage;

public class CraftingUI
{
    public static void AddStorageStackBg(List<UIRequiredItemIcon> requiredItemSelector)
    {
        foreach (var uiRequiredItemIcon in requiredItemSelector)
        {
            var bagUiElement = uiRequiredItemIcon.transform.FindChild("HavedStackBG").gameObject;
            var storageStackBg = Object.Instantiate(bagUiElement, uiRequiredItemIcon.transform, true);
            storageStackBg.name = "HaveStorageStackBG";
            storageStackBg.transform.position = bagUiElement.transform.position + new Vector3(0, -0.07f, 0);
        }
    }
}