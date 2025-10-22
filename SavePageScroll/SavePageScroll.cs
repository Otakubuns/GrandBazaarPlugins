using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.UnityEngine;
using BokuMono;
using UnityEngine;
using KeyCode = BepInEx.Unity.IL2CPP.UnityEngine.KeyCode;

namespace SavePageScroll;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class SavePageScroll : BasePlugin
{
    internal static new ManualLogSource Log;
    private static ConfigEntry<int> scrollAmount;

    public override void Load()
    {
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        scrollAmount = Config.Bind("General", "Scroll Amount", 10, "Number of save slots to scroll with Page Up/Down keys.");
        AddComponent<UpdateBehaviour>();
    }

    public class UpdateBehaviour : MonoBehaviour
    {
        private float lastKeyPressTime = 0f;
        private const float keyPressCooldown = 0.2f;

        private void Update()
        {
            try
            {
                if (Time.time - lastKeyPressTime < keyPressCooldown) return;

                if (!Input.GetKeyInt(KeyCode.PageDown) && !Input.GetKeyInt(KeyCode.PageUp)) return;
                if (!UIAccessor.Instance) return;
                if (UIAccessor.Instance.CurrentUIKey != UILoadKey.SaveLoad) return;

                // Yes this is expensive invocation but it's only when page up/down is pressed & on the save/load page
                var uiSaveLoadPage = FindObjectOfType<UISaveLoadPage>(true);
                var scrollObject = uiSaveLoadPage.GetComponentInChildren<UIGroupCommonScroll>(true);

                if (Input.GetKeyInt(KeyCode.PageDown))
                {
                    var idToScroll = scrollObject.CurrentId + scrollAmount.Value;
                    if (scrollObject.CurrentId + 10 > scrollObject.ChildCount)
                        idToScroll = scrollObject.ChildCount - 1;

                    var scrollPosition = scrollObject.GetScrollPosition(idToScroll);
                    scrollObject.SetScrollPosition(scrollPosition, true);
                    scrollObject.currentID = idToScroll;
                }
                else if (Input.GetKeyInt(KeyCode.PageUp))
                {
                    var idToScroll = scrollObject.CurrentId - scrollAmount.Value;
                    if (scrollObject.CurrentId - 10 < 0) idToScroll = 0;

                    var scrollPosition = scrollObject.GetScrollPosition(idToScroll);
                    scrollObject.SetScrollPosition(scrollPosition, true);
                    scrollObject.currentID = idToScroll;
                }

                lastKeyPressTime = Time.time;
            }
            catch (System.Exception e)
            {
                // ignored (if the uikey isn't set but the class is it can be an exception)
            }
        }
    }
}