using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

using Random = UnityEngine.Random;

namespace WideCharacterAPI
{
    public static class WideCharacterTools
    {
        public static T GetOrAddComponent<T>(this Component comp) where T : Component
        {
            if (comp == null || comp.gameObject == null)
            {
                return null;
            }
            return comp.gameObject.GetOrAddComponent<T>();
        }

        public static T GetOrAddComponent<T>(this GameObject go) where T : Component
        {
            if (go == null)
            {
                return null;
            }
            if (go.GetComponent<T>() != null)
            {
                return go.GetComponent<T>();
            }
            return go.AddComponent<T>();
        }

        public static CharacterCombat RealCharacter(this CharacterCombatUIInfo self)
        {
            return CombatManager.Instance._stats.Characters[self.ID];
        }

        public static IEnumerable MatchBefore(this ILCursor curs, Func<Instruction, bool> predicate)
        {
            for (; curs.JumpBeforeNext(predicate); curs.JumpToNext(predicate))
            {
                yield return null;
            }
        }

        public static VariableDefinition DeclareLocal<T>(this ILContext ctx)
        {
            var loc = new VariableDefinition(ctx.Import(typeof(T)));
            ctx.Body.Variables.Add(loc);

            return loc;
        }

        public static VariableDefinition DeclareLocal<T>(this ILCursor curs)
        {
            return curs.Context.DeclareLocal<T>();
        }

        public static bool JumpToNext(this ILCursor curs, Func<Instruction, bool> predicate, int times = 1)
        {
            for (int i = 0; i < times; i++)
            {
                if (!curs.TryGotoNext(MoveType.After, predicate))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool JumpBeforeNext(this ILCursor curs, Func<Instruction, bool> predicate, int times = 1)
        {
            //Debug.Log($"jump before next, curr idx {curs.Index}");
            for (int i = 0; i < times - 1; i++)
            {
                if (!curs.TryGotoNext(MoveType.After, predicate))
                {
                    return false;
                }
                //Debug.Log($"   curr idx {curs.Index}");
            }
            if (curs.TryGotoNext(MoveType.Before, predicate))
            {
                //Debug.Log($"   end {curs.Index}");
                return true;
            }
            return false;
        }

        public static bool Calls(this Instruction instr, MethodBase mthd)
        {
            return instr.MatchCallOrCallvirt(mthd);
        }

        public static int LastSlotId(this IUnit u)
        {
            return u.SlotID + u.Size - 1;
        }

        public static Vector3 Vector3Divide(Vector3 left, Vector3 right)
        {
            return new(left.x / right.x, left.y / right.y, left.z / right.z);
        }

        public static int GetRandomCharacterSlotWithSize(this CombatStats stat, int size)
        {
            var slot = -1;
            var remainingSlots = new List<int>();
            for (int i = 0; i < stat.combatSlots.CharacterSlots.Length; i++)
            {
                remainingSlots.Add(i);
            }
            while (remainingSlots.Count > 0)
            {
                var idx = Random.Range(0, remainingSlots.Count);
                var slothere = remainingSlots[idx];
                remainingSlots.RemoveAt(idx);
                slot = stat.combatSlots.GetCharacterFitSlot(slothere, size);
                if (slot != -1)
                {
                    break;
                }
            }
            return slot;
        }
    }

    public class LocalPositionConstantSetter : MonoBehaviour
    {
        public Vector3 targetPos;

        public void LateUpdate()
        {
            transform.localPosition = targetPos;
        }
    }
}
