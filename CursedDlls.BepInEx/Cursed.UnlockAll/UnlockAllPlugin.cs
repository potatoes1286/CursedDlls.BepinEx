using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using FistVR;
using HarmonyLib;
using RUST.Steamworks;
using Steamworks;
using UnityEngine;

[assembly: AssemblyVersion("1.4")]
namespace Cursed.UnlockAll
{
    [BepInPlugin("dll.cursed.unlockall", "CursedDlls - Unlock All Items", "1.4")]
    public class UnlockAllPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> _overwriteRewardsTxt;

        private void Awake()
        {
            _overwriteRewardsTxt = Config.Bind("General", "OverwriteRewardsTxt", false,
                "Overwrites the contents of Rewards.txt with every unlocked object. Even if this is false, however, all reward items will show in the Item Spawner.");

            Harmony.CreateAndPatchAll(typeof(UnlockAllPlugin));
        }

        //this broke on Update 100 Alpha 4. difference between A3 and A4 IM is a bunch of changes to support
        //the new IS, but it throws some exception when this is involved, so i removed it and repalced it.
        #region OldAddEveryObj - Breaks!
        
        /*[HarmonyPatch(typeof(IM), "GenerateItemDBs")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> GenerateItemDBsTranspiler(IEnumerable<CodeInstruction> instrs)
        {
            return new CodeMatcher(instrs).MatchForward(true, new CodeMatch(OpCodes.Ldstr, "ItemSpawnerIDs"), new CodeMatch(OpCodes.Call))
                .Advance(1)
                .Insert(
                    new CodeInstruction(OpCodes.Ldloc_0),
                    Transpilers.EmitDelegate<Func<ItemSpawnerID[], FVRObject[], ItemSpawnerID[]>>(
                        (spawnerIds, fvrObjects) =>
                        {
                            Debug.Log("0");
                            var objects = new List<FVRObject>(fvrObjects);
                            objects.Reverse();
                            var extSpawnerIds = new List<ItemSpawnerID>(spawnerIds);
                            foreach (var itemSpawnerId in spawnerIds)
                            {
                                if (itemSpawnerId != null)
                                {
                                    if (itemSpawnerId.MainObject != null)
                                        objects.Remove(itemSpawnerId.MainObject);
                                    if (itemSpawnerId.SecondObject != null)
                                        objects.Remove(itemSpawnerId.SecondObject);
                                }
                            }
                            Debug.Log("1");
                            foreach (var fvrObject in objects)
                            {
                                if (fvrObject != null)
                                {
                                    Debug.Log("2");
                                    var itemId = ScriptableObject.CreateInstance<ItemSpawnerID>();
                                    itemId.DisplayName = fvrObject.DisplayName;
                                    itemId.SubHeading = fvrObject.ItemID;
                                    itemId.Category = ItemSpawnerID.EItemCategory.Misc;
                                    itemId.SubCategory = ItemSpawnerID.ESubCategory.Backpack;
                                    itemId.ItemID = fvrObject.ItemID;
                                    itemId.MainObject = fvrObject;
                                    itemId.Secondaries = new ItemSpawnerID[0];
                                    itemId.UsesHugeSpawnPad = true;
                                    extSpawnerIds.Add(itemId);
                                    Debug.Log("3");
                                }
                            }
                            Debug.Log("4");
                            return extSpawnerIds.ToArray();
                        }))
                .InstructionEnumeration();
        }/**/
        
        #endregion

        [HarmonyPatch(typeof(IM), "GenerateItemDBs")]
        [HarmonyPrefix]
        public static bool AddUncategorizedSubCategory(IM __instance)
        {
            ItemSpawnerCategoryDefinitions.SubCategory subcat = new ItemSpawnerCategoryDefinitions.SubCategory
            {
                Subcat = ItemSpawnerID.ESubCategory.None,
                DisplayName = "UNCATEGORIZED",
                DoesDisplay_Sandbox = true,
                DoesDisplay_Unlocks = true,
                Sprite = null
            };

            List<ItemSpawnerCategoryDefinitions.SubCategory> subcats = __instance.CatDefs.Categories[6].Subcats.ToList();
            subcats.Add(subcat);
            __instance.CatDefs.Categories[6].Subcats = subcats.ToArray();
            
            return true;
        }
        
        [HarmonyPatch(typeof(IM), "GenerateItemDBs")]
        [HarmonyPostfix]
        public static void AddEveryNonShowID(IM __instance)
        {
            var fvrObjects = Resources.LoadAll<FVRObject>("ObjectIDs");
                var spawnerIds = Resources.LoadAll<ItemSpawnerID>("ItemSpawnerIDs");
                var objects = new List<FVRObject>(fvrObjects);
                objects.Reverse();
                var extSpawnerIds = new List<ItemSpawnerID>();
                foreach (var itemSpawnerId in spawnerIds)
                {
                    if (itemSpawnerId != null)
                    {
                        if (itemSpawnerId.MainObject != null)
                            objects.Remove(itemSpawnerId.MainObject);
                        if (itemSpawnerId.SecondObject != null)
                            objects.Remove(itemSpawnerId.SecondObject);
                    }
                }

                foreach (var fvrObject in objects)
                {
                    if (fvrObject != null)
                    {
                        var itemId = ScriptableObject.CreateInstance<ItemSpawnerID>();
                        itemId.DisplayName = fvrObject.DisplayName;
                        itemId.SubHeading = fvrObject.ItemID;
                        itemId.Category = ItemSpawnerID.EItemCategory.Misc;
                        itemId.SubCategory = ItemSpawnerID.ESubCategory.None;
                        itemId.ItemID = fvrObject.ItemID + "_uncat";
                        itemId.MainObject = fvrObject;
                        itemId.Secondaries = new ItemSpawnerID[0];
                        itemId.UsesLargeSpawnPad = true;
                        extSpawnerIds.Add(itemId);
                    }
                }
                            
                /*if (ids[i].IsDisplayedInMainEntry) continue;
                ids[i].IsDisplayedInMainEntry = true;
                ids[i].Category = ItemSpawnerID.EItemCategory.Misc;
                ids[i].SubCategory = ItemSpawnerID.ESubCategory.Backpack;
                ids[i].IsReward = false;
                ids[i].ItemID += "_uncat";*/

                foreach (var id in extSpawnerIds)
                {
                    if (__instance.SpawnerIDDic.ContainsKey(id.ItemID))
                    {
                        Debug.Log("Oh shit, duplicate of:" + id.ItemID + " on " + id.name);
                    }
                    else
                    {
                        __instance.SpawnerIDDic.Add(id.ItemID, id);
                    }

                    if (__instance.CategoryDic.ContainsKey(id.Category))
                    {
                        __instance.CategoryDic[id.Category].Add(id);
                    }
                    else
                    {
                        List<ItemSpawnerID> list2 = new List<ItemSpawnerID>();
                        list2.Add(id);
                        __instance.CategoryDic.Add(id.Category, list2);
                    }
                    if (__instance.SubCategoryDic.ContainsKey(id.SubCategory))
                    {
                        __instance.SubCategoryDic[id.SubCategory].Add(id);
                    }
                    else
                    {
                        List<ItemSpawnerID> list3 = new List<ItemSpawnerID>();
                        list3.Add(id);
                        __instance.SubCategoryDic.Add(id.SubCategory, list3);
                    }
                }
        }

        [HarmonyPatch(typeof(RewardUnlocks), nameof(RewardUnlocks.IsRewardUnlocked), typeof(string))]
        [HarmonyPrefix]
        public static bool IsRewardUnlockedPrefix(RewardUnlocks __instance, ref bool __result, string ID)
        {
            if (__instance.Rewards.Contains(ID) && _overwriteRewardsTxt.Value)
            {
                __instance.Rewards.Add(ID);
                using (var writer = ES2Writer.Create("Rewards.txt"))
                {
                    __instance.SaveToFile(writer);
                }
            }

            __result = true;
            return false;
        }

        [HarmonyPatch(typeof(RewardUnlocks), nameof(RewardUnlocks.IsRewardUnlocked), typeof(ItemSpawnerID))]
        [HarmonyPrefix]
        public static bool IsRewardUnlockedPrefix(RewardUnlocks __instance, ref bool __result, ItemSpawnerID ID)
        {
            __result = __instance.IsRewardUnlocked(ID.ItemID);
            return false;
        }

        /*
		 * Skiddie prevention
		 */
        [HarmonyPatch(typeof(HighScoreManager), nameof(HighScoreManager.UpdateScore), new Type[] { typeof(string), typeof(int), typeof(Action<int, int>) })]
        [HarmonyPrefix]
        public static bool HSM_UpdateScore()
        {
            return false;
        }
    }
}