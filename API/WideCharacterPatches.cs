using MUtility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using HarmonyLib;
using System.Collections;
using MonoMod.Cil;
using System.Reflection;
using System.Linq;

using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using MmCodes = Mono.Cecil.Cil.OpCodes;
using OpCodes = System.Reflection.Emit.OpCodes;
using Random = UnityEngine.Random;

namespace WideCharacterAPI
{
    [HarmonyPatch]
    public static class WideCharacterPatches
    {
        #region size setup and checks
        [HarmonyPatch(typeof(CharacterCombat), MethodType.Constructor, typeof(int), typeof(int), typeof(CharacterSO), typeof(bool), typeof(int), typeof(int), typeof(int), typeof(int), typeof(BaseWearableSO), typeof(WearableStaticModifiers), typeof(bool), typeof(string), typeof(bool))]
        [HarmonyPostfix]
        public static void IncreaseSize(CharacterCombat __instance)
        {
            if (__instance.Character is CharacterSOAdvanced ch)
            {
                __instance.Size = Mathf.Max(1, ch.size);
            }
        }

        [HarmonyPatch(typeof(CharacterCombat), nameof(CharacterCombat.TransformCharacter))]
        [HarmonyPostfix]
        public static void IncreaseSizeOnTransform(CharacterCombat __instance)
        {
            if (__instance.Character is CharacterSOAdvanced ch)
            {
                __instance.Size = Mathf.Max(1, ch.size);
            }
        }

        [HarmonyPatch(typeof(SlotsCombat), nameof(SlotsCombat.AddCharacterToSlot))]
        [HarmonyPostfix]
        public static void AddToMoreSlots(SlotsCombat __instance, IUnit character, int slotID)
        {
            if (character.Size > 1)
            {
                for (int i = 1; i < character.Size; i++)
                {
                    __instance.CharacterSlots[slotID + i].SetUnit(character);
                }
            }
        }

        [HarmonyPatch(typeof(CombatStats), nameof(CombatStats.TryTransformCharacter))]
        [HarmonyPrefix]
        public static bool MaybePreventTransform(CombatStats __instance, ref bool __result, int id, CharacterSO transformation)
        {
            if (transformation == null || transformation.Equals(null))
            {
                return true;
            }
            if (!__instance.Characters.TryGetValue(id, out var cc))
            {
                return true;
            }
            if (!cc.IsAlive)
            {
                return true;
            }

            if (transformation is CharacterSOAdvanced adv && adv.size > cc.Size)
            {
                var charslots = __instance.combatSlots.CharacterSlots;

                for (int i = cc.Size; i < adv.size; i++)
                {
                    var slothere = cc.SlotID + i;

                    if (slothere < 0 || slothere >= charslots.Length || charslots[slothere].HasUnit)
                    {
                        __result = false;
                        return false;
                    }
                }
            }

            return true;
        }

        [HarmonyPatch(typeof(CombatStats), nameof(CombatStats.TryTransformCharacter))]
        [HarmonyILManipulator]
        public static void UpdateSlots(ILContext ctx)
        {
            var cursor = new ILCursor(ctx);
            var sizeCacheLocal = cursor.DeclareLocal<int>();

            foreach (var m in cursor.MatchBefore(x => x.Calls(tc)))
            {
                cursor.Emit(MmCodes.Ldloc_0);
                cursor.Emit(MmCodes.Ldloca_S, sizeCacheLocal);
                cursor.Emit(MmCodes.Call, ss);
            }

            cursor.Index = 0;
            while (cursor.JumpToNext(x => x.Calls(tc)))
            {
                cursor.Emit(MmCodes.Ldarg_0);
                cursor.Emit(MmCodes.Ldloc_0);
                cursor.Emit(MmCodes.Ldloc_S, sizeCacheLocal);
                cursor.Emit(MmCodes.Call, csfs);
            }
        }

        public static bool SaveSize(bool _, CharacterCombat cc, out int size)
        {
            size = cc.Size;
            return _;
        }

        public static void ChangeSlotsForSize(CombatStats stats, CharacterCombat cc, int size)
        {
            var charslots = stats.combatSlots.CharacterSlots;

            if (size > cc.Size) // remove slots
            {
                for (int i = cc.Size; i < size; i++)
                {
                    var slothere = cc.SlotID + i;

                    if (slothere < 0 || slothere >= charslots.Length)
                    {
                        continue;
                    }

                    charslots[slothere].SetUnit(null);
                }
            }
            else if (size < cc.Size) // add slots
            {
                for (int i = size; i < cc.Size; i++)
                {
                    var slothere = cc.SlotID + i;

                    if (slothere < 0 || slothere >= charslots.Length)
                    {
                        continue;
                    }

                    charslots[slothere].SetUnit(cc);
                }
            }
        }

        [HarmonyPatch(typeof(SpawnCharacterAction), nameof(SpawnCharacterAction.Execute), MethodType.Enumerator)]
        [HarmonyILManipulator]
        public static void FixSizesAndStuff(ILContext ctx)
        {
            var cursor = new ILCursor(ctx);

            for (; cursor.JumpBeforeNext(x => x.Calls(gcfs)); cursor.JumpToNext(x => x.Calls(gcfs)))
            {
                cursor.Emit(MmCodes.Ldarg_0);
                cursor.Emit(MmCodes.Ldfld, sca_this);
                cursor.Emit(MmCodes.Call, correctsize);
            }

            cursor.Index = 0;
            while (cursor.JumpToNext(x => x.Calls(grcs)))
            {
                cursor.Emit(MmCodes.Ldarg_0);
                cursor.Emit(MmCodes.Ldfld, sca_this);
                cursor.Emit(MmCodes.Ldarg_0);
                cursor.Emit(MmCodes.Ldfld, sca_stats);
                cursor.Emit(MmCodes.Call, crs);
            }
        }

        public static int CorrectSize(int current, SpawnCharacterAction act)
        {
            if (act != null && act._character != null && act._character is CharacterSOAdvanced adv)
            {
                return adv.size;
            }
            return current;
        }

        public static int CorrectRandomSlot(int current, SpawnCharacterAction act, CombatStats stat)
        {
            if (act != null && act._character != null && act._character is CharacterSOAdvanced adv)
            {
                return stat.GetRandomCharacterSlotWithSize(adv.size);
            }
            return current;
        }
        #endregion

        #region targetting

        [HarmonyPatch(typeof(SlotsCombat), nameof(SlotsCombat.GetFrontOpponentSlotTargets))]
        [HarmonyPostfix]
        public static void AddMoreSlots(SlotsCombat __instance, List<TargetSlotInfo> __result, int originSlotID, bool isOriginCharacter)
        {
            if (isOriginCharacter)
            {
                var size = 1;
                if (originSlotID >= 0 && originSlotID < __instance.CharacterSlots.Length && __instance.CharacterSlots[originSlotID].HasUnit)
                {
                    size = __instance.CharacterSlots[originSlotID].Unit.Size;
                }
                if (size > 1)
                {
                    for (int i = 1; i < size; i++)
                    {
                        var enemyTargetSlot = __instance.GetEnemyTargetSlot(originSlotID, i);
                        if (enemyTargetSlot != null && !__result.Contains(enemyTargetSlot))
                        {
                            __result.Add(enemyTargetSlot);
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(SlotsCombat), nameof(SlotsCombat.GetAllySlotTarget))]
        [HarmonyILManipulator]
        public static void ChangeOffset(ILContext ctx)
        {
            var cursor = new ILCursor(ctx);

            foreach (var m in cursor.MatchBefore(x => x.Calls(gcts)))
            {
                cursor.Emit(MmCodes.Ldarg_0);
                cursor.Emit(MmCodes.Ldarg_1);
                cursor.Emit(MmCodes.Ldarg_2);

                cursor.Emit(MmCodes.Call, cso);
            }
        }

        [HarmonyPatch(typeof(SlotsCombat), nameof(SlotsCombat.GetOpponentSlotTarget))]
        [HarmonyILManipulator]
        public static void ChangeOffset2(ILContext ctx)
        {
            var cursor = new ILCursor(ctx);

            foreach (var m in cursor.MatchBefore(x => x.Calls(gets)))
            {
                cursor.Emit(MmCodes.Ldarg_0);
                cursor.Emit(MmCodes.Ldarg_1);
                cursor.Emit(MmCodes.Ldarg_2);

                cursor.Emit(MmCodes.Call, cso);
            }
        }

        public static int CorrectSizeOffset(int current, SlotsCombat slots, int originSlotID, int targetDirection)
        {
            if (originSlotID >= 0 && originSlotID < slots.CharacterSlots.Length)
            {
                var slothere = slots.CharacterSlots[originSlotID];

                if (slothere != null && slothere.HasUnit && slothere.Unit.Size > 1)
                {
                    return targetDirection + slothere.Unit.Size - 1;
                }
            }
            return current;
        }

        [HarmonyPatch(typeof(SlotsCombat), nameof(SlotsCombat.GetAllSelfSlots))]
        [HarmonyPostfix]
        public static void AddMoreSlotsSelf(SlotsCombat __instance, List<TargetSlotInfo> __result, int originSlotID, bool isOriginCharacter)
        {
            if (isOriginCharacter)
            {
                var size = 1;
                if (originSlotID >= 0 && originSlotID < __instance.CharacterSlots.Length && __instance.CharacterSlots[originSlotID].HasUnit)
                {
                    size = __instance.CharacterSlots[originSlotID].Unit.Size;
                }
                if (size > 1)
                {
                    for (int i = 1; i < size; i++)
                    {
                        var enemyTargetSlot = __instance.GetCharacterTargetSlot(originSlotID, i);
                        if (enemyTargetSlot != null && !__result.Contains(enemyTargetSlot))
                        {
                            __result.Add(enemyTargetSlot);
                        }
                    }
                }
            }
        }
        #endregion

        #region movement
        public static bool DoSwapLogicAndCBA(SlotsCombat slots, int firstguySlotId, int secondguySlotId, bool manualSwap)
        {
            try
            {
                var cslots = slots.CharacterSlots.Select(x => x.Unit).ToArray();
                var occupiedSlots = new Dictionary<int, IUnit>();
                secondguySidUpdateDict.Clear();

                var origFirstguySlotId = firstguySlotId;
                var origSecondguySlotId = secondguySlotId;

                //var slottext = string.Join(", ", cslots.Select(x => x != null ? x.Name : "Null"));

                //Debug.Log("---------------------");

                //Debug.Log(slottext);

                var firstguy = cslots[firstguySlotId];

                var firstguySize = 1;

                if (firstguy != null)
                {
                    firstguySize = firstguy.Size;

                    secondguySlotId -= firstguySlotId - firstguy.SlotID;
                    firstguySlotId = firstguy.SlotID;
                }

                var movingLeft = secondguySlotId < firstguySlotId;

                var secondguysMinSid = secondguySlotId;
                var secondguysMaxSid = secondguySlotId + firstguySize - 1;

                if (movingLeft)
                    secondguysMaxSid = Mathf.Min(secondguySlotId + firstguySize - 1, firstguySlotId);
                else
                    secondguysMinSid = Mathf.Max(secondguySlotId, firstguySlotId + firstguySize - 1);

                var offset = 0;

                if (movingLeft)
                {
                    var firstSecondguy = cslots[secondguysMinSid];

                    if (firstSecondguy != null)
                    {
                        offset = Mathf.Max(secondguySlotId + firstguySize - (firstSecondguy.SlotID - secondguysMinSid + firstguySlotId), 0);
                    }
                }
                else
                {
                    var lastSecondguy = cslots[secondguysMaxSid];

                    if (lastSecondguy != null)
                    {
                        offset = Mathf.Min(secondguySlotId - (lastSecondguy.SlotID + lastSecondguy.Size - secondguysMinSid + firstguySlotId), 0);
                    }
                }

                if (firstguy != null)
                {
                    for (int i = 0; i < firstguySize; i++)
                    {
                        cslots[firstguy.SlotID + i] = null;
                    }
                }

                var secondguySlots = new HashSet<int>();

                for (int i = secondguysMinSid; i <= secondguysMaxSid; i++)
                {
                    if (cslots[i] != null)
                    {
                        secondguySlots.Add(cslots[i].SlotID);
                    }
                }

                var secondguys = new List<IUnit>();

                foreach (var sid in secondguySlots)
                {
                    var guy = cslots[sid];

                    if (guy != null)
                    {
                        if (!guy.CanBeSwapped)
                        {
                            //Debug.Log("!!! guy CANNOT BE SWAPPPE ");
                            return false;
                        }

                        secondguys.Add(guy);

                        for (int i = 0; i < guy.Size; i++)
                        {
                            cslots[i + guy.SlotID] = null;
                        }
                    }
                }

                var sids = new List<int>();
                var ids = new List<int>();

                foreach (var guy in secondguys)
                {
                    for (int i = 0; i < guy.Size; i++)
                    {
                        var newslot = guy.SlotID - secondguysMinSid + firstguySlotId + i + offset;

                        if (cslots[newslot] != null)
                        {
                            var swapStack = new Stack<(int slotid, bool isFirstguySide, IUnit guy)>();

                            swapStack.Push((cslots[newslot].SlotID, false, cslots[newslot]));

                            while (swapStack.Count > 0)
                            {
                                (var slotid, var isFirstguySide, var swapguy) = swapStack.Pop();

                                if (!swapguy.CanBeSwapped)
                                {
                                    //Debug.Log("swap guy CANNOT BE SWAPPPE ");
                                    return false;
                                }

                                int newSwapSid;

                                if (!isFirstguySide)
                                {
                                    newSwapSid = slotid - firstguySlotId - offset + secondguysMinSid;
                                }
                                else
                                {
                                    newSwapSid = slotid + firstguySlotId + offset - secondguysMinSid;
                                }

                                var size = swapguy.Size;

                                for (var j = 0; j < size; j++)
                                {
                                    if (cslots[slotid + j] == swapguy)
                                    {
                                        cslots[slotid + j] = null;
                                    }

                                    if (cslots[newSwapSid + j] != null)
                                    {
                                        swapStack.Push((cslots[newSwapSid + j].SlotID, !isFirstguySide, cslots[newSwapSid + j]));
                                    }

                                    cslots[newSwapSid + j] = swapguy;
                                    occupiedSlots.Add(newSwapSid + j, swapguy);
                                }

                                ids.Add(swapguy.ID);
                                sids.Add(newSwapSid);
                            }
                        }

                        //Debug.Log($"newslot {newslot} for gUY {guy.Name}");

                        cslots[newslot] = guy;
                        occupiedSlots.Add(newslot, guy);

                        if (i == 0)
                        {
                            ids.Add(guy.ID);
                            sids.Add(newslot);
                        }
                    }
                }

                if (firstguy != null)
                {
                    realFirstGuySlotId = secondguySlotId;

                    for (int i = 0; i < firstguySize; i++)
                    {
                        var sid = secondguySlotId + i;

                        cslots[sid] = firstguy;
                        occupiedSlots.Add(sid, firstguy);
                    }

                    ids.Add(firstguy.ID);
                    sids.Add(secondguySlotId);
                }

                idsCache = ids.ToArray();
                slotIdsCache = sids.ToArray();

                foreach (var sid in sids)
                {
                    var guyhere = cslots[sid];

                    if (guyhere != null)
                    {
                        secondguySidUpdateDict[guyhere] = sid;
                    }
                }

                firstGuy = cslots[origSecondguySlotId];
                secondGuy = cslots[origFirstguySlotId];

                firstGuySlotId = origSecondguySlotId;
                secondGuySlotId = origFirstguySlotId;

                realFirstGuy = firstguy;

                firstguyPreChanges.Clear();
                firstguyPostChanges.Clear();

                secondguyPreChanges.Clear();
                secondguyPostChanges.Clear();

                for (int i = 0; i < cslots.Length; i++)
                {
                    if (cslots[i] != slots.CharacterSlots[i].Unit)
                    {
                        if (firstguy != null && cslots[i] == firstguy)
                        {
                            if (i < firstguySlotId)
                            {
                                firstguyPreChanges[i] = cslots[i];
                            }
                            else if (i > firstguySlotId)
                            {
                                firstguyPostChanges[i] = cslots[i];
                            }
                        }
                        else
                        {
                            if (i < secondguySlotId)
                            {
                                secondguyPreChanges[i] = cslots[i];
                            }
                            else if (i > secondguySlotId)
                            {
                                secondguyPostChanges[i] = cslots[i];
                            }
                        }
                    }
                }

                //slottext = string.Join(", ", cslots.Select(x => x != null ? x.Name : "Null"));

                //Debug.Log(slottext);
            }
            catch//(Exception ex)
            {
                //Debug.Log("shit " + ex);
                return false;
            }
            return true;
        }

        public static IUnit SetSecondGuy(IUnit _, SlotsCombat sc)
        {
            //Debug.Log("clearing!!!!!!!!");
            secondguysForPreUpdate.Clear();
            secondguysForPostUpdate.Clear();
            foreach (var change in secondguyPreChanges)
            {
                try
                {
                    var s = sc.CharacterSlots[change.Key];
                    var g = change.Value;
                    if (g != null && s.Unit != g && g != firstGuy && !secondguysForPostUpdate.Contains(g) && !secondguysForPreUpdate.Contains(g))
                    {
                        secondguysForPreUpdate.Add(g);
                    }
                    s.SetUnit(g);
                }
                catch (Exception ex)
                {
                    Debug.Log("uh oh " + ex);
                }
            }

            try
            {
                var s = sc.CharacterSlots[secondGuySlotId];
                var g = secondGuy;
                if (g != null && s.Unit != g && g != firstGuy && !secondguysForPostUpdate.Contains(g) && !secondguysForPreUpdate.Contains(g))
                {
                    secondguysForPreUpdate.Add(g);
                }
                s.SetUnit(g);
            }
            catch (Exception ex)
            {
                Debug.Log("uh oh " + ex);
            }

            return secondGuy;
        }

        public static IUnit SetFirstGuy(IUnit _, SlotsCombat sc)
        {
            foreach (var change in firstguyPreChanges)
            {
                try
                {
                    sc.CharacterSlots[change.Key].SetUnit(change.Value);
                }
                catch (Exception ex)
                {
                    Debug.Log("uh oh " + ex);
                }
            }
            return firstGuy;
        }

        public static void ApplySecondGuyPostChanges(SlotsCombat sc)
        {
            foreach (var change in secondguyPostChanges)
            {
                try
                {
                    var s = sc.CharacterSlots[change.Key];
                    var g = change.Value;
                    if (g != null && s.Unit != g && g != firstGuy && !secondguysForPostUpdate.Contains(g) && !secondguysForPreUpdate.Contains(g))
                    {
                        secondguysForPostUpdate.Add(g);
                    }
                    s.SetUnit(g);
                }
                catch (Exception ex)
                {
                    Debug.Log("uh oh " + ex);
                }
            }
        }

        public static void ApplyFirstGuyPostChanges(SlotsCombat sc)
        {
            foreach (var change in firstguyPostChanges)
            {
                try
                {
                    sc.CharacterSlots[change.Key].SetUnit(change.Value);
                }
                catch (Exception ex)
                {
                    Debug.Log("uh oh " + ex);
                }
            }
        }

        public static bool UpdateSecondGuys_Pre(bool _)
        {
            //Debug.Log("ih");
            foreach (var updateguy in secondguysForPreUpdate)
            {
                //Debug.Log("looking at " + updateguy.Name);
                if (updateguy != null && secondguySidUpdateDict.TryGetValue(updateguy, out var newSid))
                {
                    updateguy.SwappedTo(newSid);
                    //Debug.Log($"hi!!! {updateguy.Name} was swapped to slot {newSid}");
                }
            }
            return _;
        }

        public static bool UpdateSecondGuys_Post(bool _)
        {
            //Debug.Log("(post) ih");
            foreach (var updateguy in secondguysForPostUpdate)
            {
                //Debug.Log("(post) looking at " + updateguy.Name);
                if (updateguy != null && secondguySidUpdateDict.TryGetValue(updateguy, out var newSid))
                {
                    updateguy.SwappedTo(newSid);
                    //Debug.Log($"(post) hi!!! {updateguy.Name} was swapped to slot {newSid}");
                }
            }
            return _;
        }

        public static bool OrigFirstGuyUpdate(bool curr, bool mandatory)
        {
            //Debug.Log($"firstguy: {firstGuy.Name}, real firstguy: {realFirstGuy.Name}, firstguy slot id: {firstGuySlotId} and real sid {realFirstGuySlotId}");
            if (firstGuy != null && realFirstGuy != null && firstGuy == realFirstGuy && firstGuySlotId == realFirstGuySlotId)
            {
                return curr;
            }
            if (realFirstGuy != null)
            {
                if (mandatory)
                {
                    //Debug.Log($"firstguy {realFirstGuy}.SwappedTo({realFirstGuySlotId})");
                    realFirstGuy.SwappedTo(realFirstGuySlotId);
                }
                else
                {
                    //Debug.Log($"firstguy {realFirstGuy}.SwapTo({realFirstGuySlotId})");
                    realFirstGuy.SwapTo(realFirstGuySlotId);
                }
            }
            return false;
        }

        public static int[] ReplaceIds(int[] _)
        {
            return idsCache;
        }

        public static int[] ReplaceSIDs(int[] _)
        {
            return slotIdsCache;
        }

        public static bool DontUpdateSecondGuy(bool _)
        {
            return false;
        }

        [HarmonyPatch(typeof(SlotsCombat), nameof(SlotsCombat.SwapCharacters))]
        [HarmonyILManipulator]
        public static void FixSwap(ILContext ctx)
        {
            var cursor = new ILCursor(ctx);

            if (cursor.TryGotoNext(x => x.Calls(setunit)))
            {
                cursor.Emit(MmCodes.Ldarg_0);
                cursor.Emit(MmCodes.Call, sg);

                if (cursor.JumpToNext(x => x.Calls(setunit)))
                {
                    cursor.Emit(MmCodes.Ldarg_0);
                    cursor.Emit(MmCodes.Call, sgpc);
                }
            }

            if (cursor.TryGotoNext(x => x.Calls(setunit)))
            {
                cursor.Emit(MmCodes.Ldarg_0);
                cursor.Emit(MmCodes.Call, fg);

                if (cursor.JumpToNext(x => x.Calls(setunit)))
                {
                    cursor.Emit(MmCodes.Ldarg_0);
                    cursor.Emit(MmCodes.Call, fgpc);
                }
            }

            cursor.Index = 0;
            while (cursor.TryGotoNext(x => x.MatchStloc(1)))
            {
                cursor.Emit(MmCodes.Call, rid);

                cursor.JumpToNext(x => x.MatchStloc(1));
            }

            cursor.Index = 0;
            while (cursor.TryGotoNext(x => x.MatchStloc(2)))
            {
                cursor.Emit(MmCodes.Call, rsid);

                cursor.JumpToNext(x => x.MatchStloc(2));
            }

            cursor.Index = 0;
            if (cursor.JumpBeforeNext(x => x.OpCode == MmCodes.Brfalse_S, 7))
            {
                //Debug.Log("emitting 1");
                cursor.Emit(MmCodes.Call, usg_pre);
            }

            if (cursor.JumpToNext(x => x.MatchLdcI4(1)))
            {
                //Debug.Log("emitting 2");
                cursor.Emit(MmCodes.Call, usg_post);
            }

            cursor.Index = 0;
            if (cursor.JumpToNext(x => x.Calls(hu), 8))
            {
                cursor.Emit(MmCodes.Ldarg_3);
                cursor.Emit(MmCodes.Call, ofgu);
            }

            cursor.Index = 0;
            if (cursor.JumpToNext(x => x.Calls(hu), 9))
            {
                cursor.Emit(MmCodes.Call, dusg);
            }
        }

        [HarmonyPatch(typeof(SlotsCombat), nameof(SlotsCombat.SwapCharacters))]
        [HarmonyPrefix]
        public static bool DoPreLogic(SlotsCombat __instance, ref bool __result, int firstSlotID, int secondSlotID, bool isMandatory = false)
        {
            idsCache = new int[0];
            slotIdsCache = new int[0];
            if (!DoSwapLogicAndCBA(__instance, firstSlotID, secondSlotID, !isMandatory))
            {
                __result = false;
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(SlotsCombat), nameof(SlotsCombat.MassCharacterSwapping))]
        [HarmonyILManipulator]
        public static void FixMassSwap(ILContext ctx)
        {
            var cursor = new ILCursor(ctx);

            if (cursor.JumpBeforeNext(x => x.MatchLdcI4(0), 10))
            {
                cursor.Emit(MmCodes.Call, cl);
            }

            cursor.Index = 0;
            if (cursor.JumpToNext(x => x.MatchStloc(9)))
            {
                cursor.Emit(MmCodes.Ldarg_0);
                cursor.Emit(MmCodes.Ldloc_S, cursor.Body.Variables[8]);
                cursor.Emit(MmCodes.Ldarg_1);
                cursor.Emit(MmCodes.Ldarg_2);
                cursor.Emit(MmCodes.Ldloca_S, cursor.Body.Variables[0]);
                cursor.Emit(MmCodes.Ldloca_S, cursor.Body.Variables[6]);
                cursor.Emit(MmCodes.Ldloca_S, cursor.Body.Variables[7]);
                cursor.Emit(MmCodes.Ldloca_S, cursor.Body.Variables[9]);

                cursor.Emit(MmCodes.Call, msl);
            }
        }

        public static void MultiSwapLogic(SlotsCombat slots, List<int> updateList, int swapStart, int swapEnd, ref int amountOfUnitsSwapped, ref int[] swappedUnitIds, ref int[] swappedUnitSlotIds, ref int idx)
        {
            if (updateList.Count > 0 || idx < 0)
            {
                return;
            }

            idx = -1;
            amountOfUnitsSwapped = 0;
            updateList.Clear();

            var coveredSlots = new List<int>();
            var swappedCharacters = new List<IUnit>();

            //var currentPosition = new Dictionary<int, IUnit>();

            if (slots.CharacterSlots[swapStart].HasUnit)
            {
                swapStart = slots.CharacterSlots[swapStart].Unit.SlotID;
            }

            if (slots.CharacterSlots[swapEnd].HasUnit)
            {
                swapEnd = slots.CharacterSlots[swapEnd].Unit.LastSlotId();
            }

            for (int i = swapStart; i <= swapEnd; i++)
            {
                var slot = slots.CharacterSlots[i];
                if (slot.HasUnit)
                {
                    var canBeSwapped = slot.CanBeSwapped;
                    var lastSid = slot.Unit.LastSlotId();

                    if (canBeSwapped)
                    {
                        swappedCharacters.Add(slot.Unit);
                    }

                    for (; i <= lastSid; i++)
                    {
                        if (canBeSwapped)
                        {
                            coveredSlots.Add(i);
                            //currentPosition[i] = slot.Unit;
                        }
                    }

                    i--;
                }
                else
                {
                    coveredSlots.Add(i);
                }
            }

            var positions = GetAvailablePositions(swappedCharacters, coveredSlots, slots.CharacterSlots[swapStart].Unit);//currentPosition);

            if (positions.Count <= 0)
            {
                swappedUnitIds = new int[0];
                swappedUnitSlotIds = new int[0];
                return;
            }

            var randomPos = positions[Random.Range(0, positions.Count)];
            var updateDict = new Dictionary<IUnit, int>();

            for (int i = swapStart; i <= swapEnd; i++)
            {
                if (randomPos.TryGetValue(i, out var charHere))
                {
                    if (!updateDict.ContainsKey(charHere))
                    {
                        updateDict[charHere] = i;
                    }

                    slots.CharacterSlots[i].SetUnit(charHere);
                }
                else
                {
                    slots.CharacterSlots[i].SetUnit(null);
                }
            }

            swappedUnitIds = updateDict.Keys.Select(x => x.ID).ToArray();
            swappedUnitSlotIds = updateDict.Values.ToArray();

            updateList.AddRange(updateDict.Values.ToList());
            amountOfUnitsSwapped = updateList.Count;
        }

        public static List<Dictionary<int, IUnit>> GetAvailablePositions(List<IUnit> units, List<int> slots, IUnit stuffInFirstPosition)//Dictionary<int, IUnit> positionToIgnore)
        {
            var positions = new List<Dictionary<int, IUnit>>();

            if (units.Count <= 0)
            {
                return positions;
            }

            void AddToPositions(int u, Dictionary<int, IUnit> origPos, List<Dictionary<int, IUnit>> addList)
            {
                var unit = units[u];

                for (int i = 0; i < slots.Count - unit.Size + 1; i++)
                {
                    var valid = true;

                    for (int s = 0; s < unit.Size; s++)
                    {
                        if (slots[s + i] != slots[i] + s || (origPos != null && origPos.ContainsKey(slots[s + i])))
                        {
                            valid = false;
                            break;
                        }
                    }

                    if (!valid)
                    {
                        continue;
                    }

                    var newPos = origPos != null ? new Dictionary<int, IUnit>(origPos) : [];

                    for (int s = 0; s < unit.Size; s++)
                    {
                        newPos[slots[i + s]] = unit;
                    }

                    if (u == units.Count - 1)
                    {
                        if (!newPos.TryGetValue(slots[0], out var firstStuff))
                        {
                            firstStuff = null;
                        }

                        if (firstStuff == stuffInFirstPosition)
                        {
                            continue;
                        }
                    }

                    addList.Add(newPos);
                }
            }

            AddToPositions(0, null, positions);

            if (units.Count <= 1 || positions.Count <= 0)
            {
                return positions;
            }

            for (int u = 1; u < units.Count; u++)
            {
                var newGen = new List<Dictionary<int, IUnit>>();

                foreach (var pos in positions)
                {
                    AddToPositions(u, pos, newGen);
                }

                positions = newGen;
            }

            return positions;
        }

        public static int CancelLoop(int _)
        {
            return -1;
        }
        #endregion

        #region ui
        [HarmonyILManipulator]
        [HarmonyPatch(typeof(CombatVisualizationController), nameof(CombatVisualizationController.UpdateCharacterSwapStates))]
        public static void SwapStateUIFix(ILContext ctx)
        {
            var cursor = new ILCursor(ctx);

            while (cursor.JumpToNext(x => x.MatchStloc(3)))
            {
                cursor.Emit(MmCodes.Ldloc_0);
                cursor.Emit(MmCodes.Ldloc_1);
                cursor.Emit(MmCodes.Ldloc_3);
                cursor.Emit(MmCodes.Call, fssa);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CombatVisualizationController), nameof(CombatVisualizationController.FirstInitialization))]
        public static void CHANGETHEWHOLEFUCKINGUISTRUCTUREWHYNOT(CombatVisualizationController __instance)
        {
            var gameCanvas = __instance.transform.Find("Canvas");
            var lzone = gameCanvas.Find("LowerZone");
            var backCanvas = lzone.Find("CharacterBackCanvas");

            if (lzone.Find("CharacterBackmostCanvas") == null)
            {
                var newcanvas = Object.Instantiate(backCanvas, lzone);
                newcanvas.name = "CharacterBackmostCanvas";

                newcanvas.SetSiblingIndex(backCanvas.GetSiblingIndex());

                for (int i = 0; i < newcanvas.childCount; i++)
                {
                    var child1 = newcanvas.GetChild(i); // this is BackSlot (1) for example

                    if (child1.name.Contains("Separator"))
                    {
                        continue; //separators are important :)
                    }

                    for (int j = 0; j < child1.childCount; j++)
                    {
                        var child2 = child1.GetChild(j); // this is BackCharacterSlot for example

                        if (child2.name != "Image")
                        {
                            Object.Destroy(child2.gameObject);
                        }
                    }
                }

                var minChildrenJustToBeSafe = Mathf.Min(backCanvas.childCount, newcanvas.childCount);

                for (int i = 0; i < minChildrenJustToBeSafe; i++)
                {
                    var nc_child = newcanvas.GetChild(i);
                    var bc_child = backCanvas.GetChild(i);

                    if (nc_child.name.Contains("Separator") || bc_child.name.Contains("Separator"))
                    {
                        continue;
                    }

                    for (int j = 0; j < bc_child.childCount; j++)
                    {
                        var bc_child_child = bc_child.GetChild(j);

                        if (bc_child_child.name == "Image" || bc_child_child.name.Contains("Character"))
                        {
                            continue;
                        }

                        bc_child_child.SetParent(nc_child, true);
                    }
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CombatVisualizationController), nameof(CombatVisualizationController.TryGrabCharacterSlot))]
        public static void TrySelectWideCharacter(CombatVisualizationController __instance, DraggableCombatLayout dragSlot)
        {
            foreach (CharacterCombatUIInfo value in __instance._charactersInCombat.Values)
            {
                var real = value.RealCharacter();
                if (real != null && real.Size > 1 && dragSlot.SlotID > value.SlotID && dragSlot.SlotID < value.SlotID + real.Size)
                {
                    __instance.TryShowCharacterIDInformation(value.ID, "");
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CombatVisualizationController), nameof(CombatVisualizationController.TryShowCharacterGrid))]
        public static void ShowAllSlots(CombatVisualizationController __instance, int characterID)
        {
            if (__instance._charactersInCombat.TryGetValue(characterID, out var value))
            {
                var real = value.RealCharacter();

                if (real != null && real.Size > 1)
                {
                    var sid = value.SlotID;

                    for (int i = 1; i < real.Size; i++)
                    {
                        __instance._characterZone.SetSlotCharGridAcivity(__instance._characterSlots[sid + i], true);
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CombatVisualizationController), nameof(CombatVisualizationController.TryHideCharacterGrid))]
        public static void HideAllSlots(CombatVisualizationController __instance, int characterID)
        {
            if (__instance._charactersInCombat.TryGetValue(characterID, out var value))
            {
                var real = value.RealCharacter();

                if (real != null && real.Size > 1)
                {
                    var sid = value.SlotID;

                    for (int i = 1; i < real.Size; i++)
                    {
                        __instance._characterZone.SetSlotCharGridAcivity(__instance._characterSlots[sid + i], false);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(CharacterInfoLayout), nameof(CharacterInfoLayout.InitializeCharacter))]
        [HarmonyPostfix]
        public static void ConnectCharacter_Init(int id)
        {
            ConnectFieldLayoutAndDoOtherStuff(CombatManager.Instance._combatUI, id);
        }

        [HarmonyPatch(typeof(CharacterInfoLayout), nameof(CharacterInfoLayout.TransformCharacter))]
        [HarmonyPostfix]
        public static void UpdateStuff(CharacterCombatUIInfo charaInfo)
        {
            ConnectFieldLayoutAndDoOtherStuff(CombatManager.Instance._combatUI, charaInfo.ID);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CharacterCombat), nameof(CharacterCombat.Size), MethodType.Setter)]
        public static void ChangeUISize(CharacterCombat __instance, int value)
        {
            if ((value != 1 || __instance._size != 0) && __instance._size != value)
            {
                CombatManager.Instance.AddUIAction(new ChangeCharacterSizeUIAction(__instance.ID, value));
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CharacterInFieldLayout), nameof(CharacterInFieldLayout.Position), MethodType.Getter)]
        public static void ModifyPosition(CharacterInFieldLayout __instance, ref Vector3 __result)
        {
            var hold = __instance.GetComponent<WCP_CharacterHolder>();

            if (hold != null && CombatManager.Instance._stats.Characters.TryGetValue(hold.id, out var cc))
            {
                __result += new Vector3(216f * Mathf.Max(cc.Size - 1, 0) * __instance.transform.lossyScale.x, 0f, 0f);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CharacterInFieldLayout), nameof(CharacterInFieldLayout.UpdateSlotID))]
        public static void UpdateSlotIdOverrides(CharacterInFieldLayout __instance)
        {
            var children = __instance.GetComponentsInChildren<WideCharacterSlotOverride>();

            foreach (var child in children)
            {
                child.SlotID = __instance.SlotID + child.sizeOffset;
            }
        }

        public static void FixSwapStateArrays(bool[] present, bool[] swappable, CharacterCombatUIInfo inf)
        {
            var real = inf.RealCharacter();

            if (real != null && real._size > 1)
            {
                for (int i = 1; i < real._size; i++)
                {
                    var sidHere = i + inf.SlotID;

                    present[sidHere] = true;
                    swappable[sidHere] = inf.CanSwap;
                }
            }
        }

        public static void ConnectFieldLayoutAndDoOtherStuff(CombatVisualizationController ui, int id)
        {
            if (ui._charactersInCombat.TryGetValue(id, out var uicc))
            {
                var a = ui._characterZone._characters[uicc.FieldID];

                a.GetOrAddComponent<WCP_CharacterHolder>().id = id;
                a.FieldEntity.GetOrAddComponent<WCP_CharacterHolder>().id = id;

                var real = uicc.RealCharacter();

                if (real != null)
                    SetTouchableSizeAndHealthbarPosition(CombatManager.Instance._stats, real, real.Size);
            }
        }

        public static void SetTouchableSizeAndHealthbarPosition(this CombatStats stats, CharacterCombat cc, int size)
        {
            if (stats.combatUI._charactersInCombat.TryGetValue(cc.ID, out var uicc))
            {
                var c = stats.combatUI._characterZone._characters[uicc.FieldID];
                var cifl = c._character;
                var touchable = cifl.transform.Find("TouchableItem");

                if (touchable != null)
                {
                    Transform[] touchables = null;
                    if (size > 1)
                    {
                        touchables = new Transform[size - 1];
                    }

                    for (int i = 0; i < cifl.transform.childCount; i++)
                    {
                        var child = cifl.transform.GetChild(i);

                        if (child != null && child.name.StartsWith("TouchableItem") && child.name.Length > 13 && int.TryParse(child.name.Substring(13), out var idx))
                        {
                            if (touchables != null && idx < touchables.Length)
                                touchables[idx] = child;
                            else
                                child.gameObject.SetActive(false);
                        }
                    }

                    if (touchables != null)
                    {
                        for (int i = 0; i < touchables.Length; i++)
                        {
                            var touch = touchables[i];

                            if (touch == null)
                            {
                                touch = Object.Instantiate(touchable, cifl.transform);
                                touch.name = $"TouchableItem{i}";

                                var overridetouch = touch.GetOrAddComponent<WideCharacterSlotOverride>();
                                overridetouch.parentCharacter = cifl;
                                overridetouch.sizeOffset = i + 1;
                                overridetouch.InitializeLayout(cifl.SlotID + overridetouch.sizeOffset, DraggableCombatLayoutType.CharacterSlot);

                            }

                            var last = i == touchables.Length - 1;

                            touch.localPosition = new(216f * 2 * (i + 1) - (last ? 22.75f : 0f), touchable.localPosition.y, 0f);
                            touch.localScale = new(last ? 1.1375f : 1.275f, 1f, 1f);

                        }
                    }

                    if (size > 1)
                    {
                        touchable.localScale = new(1.1375f, 1f, 1f);
                        touchable.localPosition = new(22.75f, touchable.localPosition.y, 0f);
                    }
                    else
                    {
                        touchable.localScale = new(1f, 1f, 1f);
                        touchable.localPosition = new(0f, touchable.localPosition.y, 0f);
                    }

                    var ot = touchable.GetOrAddComponent<WideCharacterSlotOverride>();
                    ot.parentCharacter = cifl;
                    ot.sizeOffset = 0;
                    ot.InitializeLayout(cifl.SlotID + ot.sizeOffset, DraggableCombatLayoutType.CharacterSlot);
                }

                if (c._health != null)
                {
                    c._health.transform.localPosition = new(216f * Mathf.Max(size - 1, 0), c._health.transform.localPosition.y, 0f);
                }
                if (c._statusListLayout != null)
                {
                    c._statusListLayout.transform.localPosition = c._statusListLayout.GetOrAddComponent<LocalPositionConstantSetter>().targetPos = new(268.5186f + (216f * 2f * Mathf.Max(size - 1, 0)), c._statusListLayout.transform.localPosition.y, 0f);
                }
            }
        }
        #endregion

        #region out of combat selection
        [HarmonyPatch(typeof(PlayerInGameData), nameof(PlayerInGameData.GetFirstEmptyPartySlotFromCenter))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> FixEmptySelection(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Bne_Un)
                {
                    yield return new(OpCodes.Ldloc_0);
                    yield return new(OpCodes.Ldloc_1);
                    yield return new(OpCodes.Ldarg_0);

                    yield return new(OpCodes.Call, rsvif);
                }
                yield return instruction;
            }
        }

        [HarmonyPatch(typeof(PlayerInGameData), MethodType.Constructor, typeof(PlayerSaveData))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ReplaceEmptySelectonForWideCharacters_Constructor(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                yield return instruction;
                if (instruction.Calls(gfepsfc))
                {
                    yield return new(OpCodes.Ldarg_0);
                    yield return new(OpCodes.Ldloc_3);

                    yield return new(OpCodes.Call, rfesfwc);
                }
            }
        }

        [HarmonyPatch(typeof(PlayerInGameData), nameof(PlayerInGameData.AddNewCharacter))]
        [HarmonyPatch(typeof(PlayerInGameData), nameof(PlayerInGameData.AddExtraCharacter))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ReplaceEmptySelectonForWideCharacters_NewCharacter(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                yield return instruction;
                if (instruction.Calls(gfepsfc))
                {
                    yield return new(OpCodes.Ldarg_0);
                    yield return new(OpCodes.Ldloc_0);

                    yield return new(OpCodes.Call, rfesfwc);
                }
            }
        }

        [HarmonyPatch(typeof(PlayerInGameData), nameof(PlayerInGameData.SetMainCharacterInParty))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ReplaceEmptySelectonForWideCharacters_MainCharacter(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                yield return instruction;
                if (instruction.Calls(gfepsfc))
                {
                    yield return new(OpCodes.Ldarg_0);
                    yield return new(OpCodes.Ldloc_0);
                    yield return new(OpCodes.Call, cs);

                    yield return new(OpCodes.Call, rfesfwc);
                }
            }
        }

        [HarmonyPatch(typeof(PlayerInGameData), nameof(PlayerInGameData.GetPartySlotID))]
        [HarmonyPostfix]
        public static void AddWideCharacters(PlayerInGameData __instance, ref int __result, int id)
        {
            if (__result == -1 && id >= 0 && id < __instance._partySlotsCharIndex.Length)
            {
                for (int i = 0; i < id; i++)
                {
                    var charIdHere = __instance._partySlotsCharIndex[i];

                    if (charIdHere >= 0)
                    {
                        var charHere = __instance._characterList[charIdHere];

                        if (charHere != null && charHere.Character != null && charHere.Character is CharacterSOAdvanced adv && adv.size > 1 && adv.size + i - 1 >= id)
                        {
                            __result = charIdHere;
                            break;
                        }
                    }
                }
            }
        }

        public static int ReplaceSecondValueIfFails(int current, int[] stuffArray, int idx, PlayerInGameData data)
        {
            var position = stuffArray[idx];

            for (int i = 0; i < position; i++)
            {
                var charIdxHere = data._partySlotsCharIndex[i];

                if (charIdxHere >= 0)
                {
                    var charHere = data._characterList[charIdxHere];

                    if (charHere != null && charHere.Character != null)
                    {
                        var fld = charHere.Character.GetType().GetField("size", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        if (fld != null)
                        {
                            try
                            {
                                int size = (int)fld.GetValue(charHere);

                                var lastSlot = i + size - 1;

                                if (lastSlot >= position)
                                {
                                    return -2;
                                }
                            }
                            catch { }
                        }
                    }
                }
            }

            return current;
        }

        public static int ReplaceFirstEmptySlotForWideCharacters(int current, PlayerInGameData data, int charIdx)
        {
            var chr = data._characterList[charIdx];

            if (chr != null && chr.Character != null && chr.Character is CharacterSOAdvanced adv && adv.size > 1)
            {
                var positions = new int[] { 2, 1, 3, 0, 4 };

                foreach (var pos in positions)
                {
                    if (data._partySlotsCharIndex[pos] == -1 && pos + adv.size - 1 <= 4)
                    {
                        var valid = true;

                        for (int i = 0; i < pos; i++)
                        {
                            var charIdxHere = data._partySlotsCharIndex[i];

                            if (charIdxHere >= 0)
                            {
                                var charHere = data._characterList[charIdxHere];

                                if (charHere != null && charHere.Character != null)
                                {
                                    var fld = charHere.Character.GetType().GetField("size", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                                    if (fld != null)
                                    {
                                        int size = (int)fld.GetValue(charHere);

                                        var lastSlot = i + size - 1;

                                        if (lastSlot >= pos)
                                        {
                                            valid = false;
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        if (!valid)
                        {
                            continue;
                        }

                        for (int i = 0; i < adv.size; i++)
                        {
                            var poshere = pos + i;

                            if (data._partySlotsCharIndex[poshere] != -1)
                            {
                                valid = false;
                                break;
                            }
                        }

                        if (!valid)
                        {
                            continue;
                        }

                        return pos;
                    }
                }

                return -1;
            }
            else
            {
                return current;
            }
        }

        [HarmonyPatch(typeof(PlayerInGameData), nameof(PlayerInGameData.SetCharacterPartySlot))]
        [HarmonyPrefix]
        public static bool Yeah(PlayerInGameData __instance, ref bool __result, int characterIndex, int partySlot)
        {
            if (characterIndex < 0 || characterIndex >= __instance._characterList.Length)
            {
                return true;
            }

            var ch = __instance._characterList[characterIndex];

            if (ch == null || ch.Character == null || ch.Character is not CharacterSOAdvanced adv || adv.size <= 1)
            {
                return true;
            }

            var size = adv.size;
            __result = false;

            var max = __instance._partySlotsCharIndex.Length - adv.size;

            if (max < 0)
            {
                return false;
            }

            partySlot = Mathf.Clamp(partySlot, -1, max);

            if (__instance.CharactersInPartyCount <= 1 && partySlot < 0)
            {
                return false;
            }

            var oldSlot = ch.SetPartySlot(partySlot);
            __result = true;

            if (oldSlot >= 0)
            {
                __instance._partySlotsCharIndex[oldSlot] = -1;
            }

            if (partySlot < 0)
            {
                return false;
            }

            for (int i = partySlot; i < partySlot + size; i++)
            {
                if (__instance._partySlotsCharIndex[i] >= 0)
                {
                    var charhere = __instance._characterList[__instance._partySlotsCharIndex[i]];

                    if (charhere != null)
                    {
                        charhere.SetPartySlot(-1);
                        __instance._partySlotsCharIndex[i] = -1;
                    }
                }
            }

            var f = Mathf.Min(partySlot, __instance._partySlotsCharIndex.Length);

            for (int i = 0; i < f; i++)
            {
                var idx = __instance._partySlotsCharIndex[i];

                if (idx < 0)
                {
                    continue;
                }

                var charhere = __instance._characterList[idx];

                if (charhere == null || charhere.Character == null)
                {
                    continue;
                }

                var type = charhere.Character.GetType();

                if (type == typeof(CharacterSO))
                {
                    continue;
                }

                var sizeVar = type.GetField("size", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                if (sizeVar == null)
                {
                    continue;
                }

                try
                {
                    var sizehere = (int)sizeVar.GetValue(charhere.Character);

                    if (sizehere > 1 && charhere.PartySlot + sizehere > partySlot)
                    {
                        charhere.SetPartySlot(-1);
                        __instance._partySlotsCharIndex[i] = -1;
                    }
                }
                catch { }
            }

            __instance._partySlotsCharIndex[partySlot] = characterIndex;
            return false;
        }

        [HarmonyPatch(typeof(PlayerInGameData), nameof(PlayerInGameData.SetCharacterPartySlot))]
        [HarmonyILManipulator]
        public static void FuckingKillWideCharacters(ILContext ctx)
        {
            var cursor = new ILCursor(ctx);

            if (cursor.JumpToNext(x => x.MatchLdelemI4()))
            {
                cursor.Emit(MmCodes.Ldarg_0);
                cursor.Emit(MmCodes.Ldarg_2);

                cursor.Emit(MmCodes.Call, k);
            }
        }

        public static int KILL(int current, PlayerInGameData dat, int partySlot)
        {
            var f = Mathf.Min(partySlot, dat._partySlotsCharIndex.Length);

            for (int i = 0; i < f; i++)
            {
                var idx = dat._partySlotsCharIndex[i];

                if (idx < 0)
                {
                    continue;
                }

                var charhere = dat._characterList[idx];

                if (charhere == null || charhere.Character == null || charhere.Character is not CharacterSOAdvanced advhere || advhere.size <= 1)
                {
                    continue;
                }

                if (charhere.PartySlot + advhere.size > partySlot)
                {
                    charhere.SetPartySlot(-1);
                    dat._partySlotsCharIndex[i] = -1;
                }
            }

            if (dat._partySlotsCharIndex[partySlot] < 0)
            {
                return current;
            }

            var charIdx = dat._partySlotsCharIndex[partySlot];
            var ch = dat._characterList[charIdx];

            if (ch == null || ch.Character == null || ch.Character is not CharacterSOAdvanced adv || adv.size <= 1)
            {
                return current;
            }

            ch.SetPartySlot(-1);
            dat._partySlotsCharIndex[partySlot] = -1;

            return -2;
        }

        [HarmonyPatch(typeof(OverworldNewManager), nameof(OverworldNewManager.HandleUIDrop))]
        [HarmonyPatch(typeof(OverworldManagerBG), nameof(OverworldManagerBG.HandleUIDrop))]
        [HarmonyPatch(typeof(GoodEndingOverworldManagerBG), nameof(GoodEndingOverworldManagerBG.HandleUIDrop))]
        [HarmonyPatch(typeof(TutorialOverworldManagerBG), nameof(TutorialOverworldManagerBG.HandleUIDrop))]
        [HarmonyILManipulator]
        public static void ChangeSelectionSlotId(ILContext ctx)
        {
            var cursor = new ILCursor(ctx);

            if (cursor.JumpToNext(x => x.Calls(iid_get)))
            {
                cursor.Emit(MmCodes.Ldarg_0);
                cursor.Emit(MmCodes.Ldloc_0);
                cursor.Emit(MmCodes.Ldloc_1);

                cursor.Emit(MmCodes.Call, cusid);
            }
        }

        public static int ChangeUISlotId(int origSid, object manager, DraggableOWLayout drop, DraggableOWLayout drag)
        {
            if (manager == null || drag.GetComponentInParent<PartyMembersUIHandler>() != null)
                return origSid;

            GameInformationHolder infoHolder = null;

            if (manager is OverworldNewManager onm)
                infoHolder = onm._informationHolder;
            else if (manager is OverworldManagerBG ombg)
                infoHolder = ombg._informationHolder;
            else if (manager is GoodEndingOverworldManagerBG geombg)
                infoHolder = geombg._informationHolder;
            else if (manager is TutorialOverworldManagerBG tombg)
                infoHolder = tombg._informationHolder;

            if (infoHolder == null)
                return origSid;

            var playerData = infoHolder._run.playerData;

            if (drag.ItemID >= playerData._characterList.Length)
            {
                return origSid;
            }

            var charHere = playerData._characterList[drag.ItemID];

            if (charHere.PartySlot < 0)
            {
                return origSid;
            }

            var offset = drag.UISlotID - charHere.PartySlot;

            return Mathf.Max(drop.UISlotID - offset, 0);
        }
        #endregion

        #region fields
        public static int[] idsCache;
        public static int[] slotIdsCache;

        public static IUnit firstGuy;
        public static IUnit secondGuy;

        public static int firstGuySlotId;
        public static int secondGuySlotId;

        public static IUnit realFirstGuy;
        public static int realFirstGuySlotId;

        public static List<IUnit> secondguysForPreUpdate = [];
        public static List<IUnit> secondguysForPostUpdate = [];

        public static Dictionary<IUnit, int> secondguySidUpdateDict = [];

        public static Dictionary<int, IUnit> firstguyPreChanges = [];
        public static Dictionary<int, IUnit> firstguyPostChanges = [];

        public static Dictionary<int, IUnit> secondguyPreChanges = [];
        public static Dictionary<int, IUnit> secondguyPostChanges = [];

        public static MethodInfo logic = AccessTools.Method(typeof(WideCharacterPatches), nameof(DoSwapLogicAndCBA));

        public static MethodInfo getunit = AccessTools.PropertyGetter(typeof(CombatSlot), nameof(CombatSlot.Unit));
        public static MethodInfo setunit = AccessTools.Method(typeof(CombatSlot), nameof(CombatSlot.SetUnit));

        public static MethodInfo fg = AccessTools.Method(typeof(WideCharacterPatches), nameof(SetFirstGuy));
        public static MethodInfo sg = AccessTools.Method(typeof(WideCharacterPatches), nameof(SetSecondGuy));

        public static MethodInfo fgpc = AccessTools.Method(typeof(WideCharacterPatches), nameof(ApplyFirstGuyPostChanges));
        public static MethodInfo sgpc = AccessTools.Method(typeof(WideCharacterPatches), nameof(ApplySecondGuyPostChanges));

        public static MethodInfo usg_pre = AccessTools.Method(typeof(WideCharacterPatches), nameof(UpdateSecondGuys_Pre));
        public static MethodInfo usg_post = AccessTools.Method(typeof(WideCharacterPatches), nameof(UpdateSecondGuys_Post));

        public static MethodInfo ofgu = AccessTools.Method(typeof(WideCharacterPatches), nameof(OrigFirstGuyUpdate));
        public static MethodInfo dusg = AccessTools.Method(typeof(WideCharacterPatches), nameof(DontUpdateSecondGuy));

        public static MethodInfo rid = AccessTools.Method(typeof(WideCharacterPatches), nameof(ReplaceIds));
        public static MethodInfo rsid = AccessTools.Method(typeof(WideCharacterPatches), nameof(ReplaceSIDs));

        public static MethodInfo rsvif = AccessTools.Method(typeof(WideCharacterPatches), nameof(ReplaceSecondValueIfFails));
        public static MethodInfo gfepsfc = AccessTools.Method(typeof(PlayerInGameData), nameof(PlayerInGameData.GetFirstEmptyPartySlotFromCenter));
        public static MethodInfo rfesfwc = AccessTools.Method(typeof(WideCharacterPatches), nameof(ReplaceFirstEmptySlotForWideCharacters));
        public static MethodInfo cs = AccessTools.PropertyGetter(typeof(CharacterInGameData), nameof(CharacterInGameData.CharacterSlot));

        public static MethodInfo hu = AccessTools.PropertyGetter(typeof(CombatSlot), nameof(CombatSlot.HasUnit));

        public static MethodInfo cl = AccessTools.Method(typeof(WideCharacterPatches), nameof(CancelLoop));
        public static MethodInfo msl = AccessTools.Method(typeof(WideCharacterPatches), nameof(MultiSwapLogic));

        public static MethodInfo fssa = AccessTools.Method(typeof(WideCharacterPatches), nameof(FixSwapStateArrays));

        public static MethodInfo iid_get = AccessTools.PropertyGetter(typeof(DraggableOWLayout), nameof(DraggableOWLayout.UISlotID));
        public static MethodInfo cusid = AccessTools.Method(typeof(WideCharacterPatches), nameof(ChangeUISlotId));
        public static MethodInfo k = AccessTools.Method(typeof(WideCharacterPatches), nameof(KILL));

        public static MethodInfo gcfs = AccessTools.Method(typeof(SlotsCombat), nameof(SlotsCombat.GetCharacterFitSlot));
        public static MethodInfo grcs = AccessTools.Method(typeof(CombatStats), nameof(CombatStats.GetRandomCharacterSlot));

        public static MethodInfo correctsize = AccessTools.Method(typeof(WideCharacterPatches), nameof(CorrectSize));
        public static Type sca_execute = AccessTools.TypeByName("SpawnCharacterAction+<Execute>d__11");
        public static FieldInfo sca_this = AccessTools.Field(sca_execute, "<>4__this");
        public static FieldInfo sca_stats = AccessTools.Field(sca_execute, "stats");
        public static MethodInfo crs = AccessTools.Method(typeof(WideCharacterPatches), nameof(CorrectRandomSlot));

        public static MethodInfo tc = AccessTools.Method(typeof(CharacterCombat), nameof(CharacterCombat.TransformCharacter));
        public static MethodInfo ss = AccessTools.Method(typeof(WideCharacterPatches), nameof(SaveSize));
        public static MethodInfo csfs = AccessTools.Method(typeof(WideCharacterPatches), nameof(ChangeSlotsForSize));

        public static MethodInfo cso = AccessTools.Method(typeof(WideCharacterPatches), nameof(CorrectSizeOffset));
        public static MethodInfo gcts = AccessTools.Method(typeof(SlotsCombat), nameof(SlotsCombat.GetCharacterTargetSlot));
        public static MethodInfo gets = AccessTools.Method(typeof(SlotsCombat), nameof(SlotsCombat.GetEnemyTargetSlot));
        #endregion
    }

    public class ChangeCharacterSizeUIAction(int id, int size) : CombatAction
    {
        public int id = id;
        public int size = size;

        public override IEnumerator Execute(CombatStats stats)
        {
            CharacterCombat characterCombat = stats.TryGetCharacterOnField(id);
            if (characterCombat != null)
            {
                stats.SetTouchableSizeAndHealthbarPosition(characterCombat, size);
            }
            yield return null;
        }
    }

    public class WideCharacterSlotOverride : DraggableCombatLayout
    {
        public CharacterInFieldLayout parentCharacter;
        public int sizeOffset;

        public override UnityEngine.UI.Image SlotImage => parentCharacter.SlotImage;
    }

    public class WCP_CharacterHolder : MonoBehaviour
    {
        public int id;
    }

}
