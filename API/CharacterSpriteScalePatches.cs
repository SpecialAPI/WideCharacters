using System;
using System.Collections.Generic;
using UnityEngine.UI;
using HarmonyLib;
using UnityEngine;
using MonoMod.Cil;

using Object = UnityEngine.Object;

namespace WideCharacterAPI
{
    [HarmonyPatch]
    public static class CharacterSpriteScalePatches
    {
        // IMPORTANT!!! CHANGE THIS VALUE TO *YOUR* MOD'S GUID
        public const string MOD_GUID = "SpecialAPI.WideCharactersExample";

        [HarmonyPatch(typeof(SelectableCharacterInformationLayout), nameof(SelectableCharacterInformationLayout.SetInformation))]
        [HarmonyPostfix]
        public static void Changes_SelectableCharacterLayout(SelectableCharacterInformationLayout __instance, CharacterSO character)
        {
            ChangeImageSizeAndPosition(__instance._image, character, __instance._character, 0f);
        }

        [HarmonyPatch(typeof(CharacterUILayout), nameof(CharacterUILayout.SetInformation))]
        [HarmonyPostfix]
        public static void Changes_CharacterUILayout(CharacterUILayout __instance, CharacterSO data)
        {
            ChangeImageSizeAndPosition(__instance._image, data, __instance.GetOrAddComponent<CSSP_CharacterStorer>().storedCharacter);
            __instance.GetOrAddComponent<CSSP_CharacterStorer>().storedCharacter = data;
        }

        [HarmonyPatch(typeof(ExchangeCharacterUILayout), nameof(ExchangeCharacterUILayout.SetInformation))]
        [HarmonyPostfix]
        public static void Changes_ExchangeCharacterUILayout(ExchangeCharacterUILayout __instance, ExchangeCharacterData data)
        {
            ChangeImageSizeAndPosition(__instance._image, data.Character, __instance.GetOrAddComponent<CSSP_CharacterStorer>().storedCharacter);
            __instance.GetOrAddComponent<CSSP_CharacterStorer>().storedCharacter = data.Character;
        }

        [HarmonyPatch(typeof(InteractablePartyCharacterUILayout), nameof(InteractablePartyCharacterUILayout.SetInformation), typeof(IMinimalCharacterInfo), typeof(bool), typeof(bool), typeof(bool))]
        [HarmonyPostfix]
        public static void Changes_InteractablePartyCharacterUILayout(InteractablePartyCharacterUILayout __instance, IMinimalCharacterInfo data)
        {
            ChangeImageSizeAndPosition(__instance._image, data.Character, __instance.GetOrAddComponent<CSSP_CharacterStorer>().storedCharacter, 94f / 107.5f);
            __instance.GetOrAddComponent<CSSP_CharacterStorer>().storedCharacter = data.Character;
        }

        [HarmonyPatch(typeof(PartyCharacterUILayout), nameof(PartyCharacterUILayout.SetInformation), typeof(IMinimalCharacterInfo), typeof(bool), typeof(bool), typeof(bool))]
        [HarmonyPostfix]
        public static void Changes_PartyCharacterUILayout1(PartyCharacterUILayout __instance, IMinimalCharacterInfo data)
        {
            ChangeImageSizeAndPosition(__instance._image, data.Character, __instance.GetOrAddComponent<CSSP_CharacterStorer>().storedCharacter, 0f);
            __instance.GetOrAddComponent<CSSP_CharacterStorer>().storedCharacter = data.Character;
        }

        [HarmonyPatch(typeof(PartyCharacterUILayout), nameof(PartyCharacterUILayout.SetInformation), typeof(ExchangeCharacterData), typeof(bool), typeof(bool))]
        [HarmonyPostfix]
        public static void Changes_PartyCharacterUILayout2(PartyCharacterUILayout __instance, ExchangeCharacterData data)
        {
            ChangeImageSizeAndPosition(__instance._image, data.Character, __instance.GetOrAddComponent<CSSP_CharacterStorer>().storedCharacter, 0.5f);
            __instance.GetOrAddComponent<CSSP_CharacterStorer>().storedCharacter = data.Character;
        }

        [HarmonyPatch(typeof(RankUpCharacterUILayout), nameof(RankUpCharacterUILayout.SetInformation))]
        [HarmonyPostfix]
        public static void Changes_RankUpCharacterUILayout(RankUpCharacterUILayout __instance, IMinimalCharacterInfo data)
        {
            ChangeImageSizeAndPosition(__instance._image, data.Character, __instance.GetOrAddComponent<CSSP_CharacterStorer>().storedCharacter, 0.5f);
            __instance.GetOrAddComponent<CSSP_CharacterStorer>().storedCharacter = data.Character;
        }

        [HarmonyPatch(typeof(InformationZoneLayout), nameof(InformationZoneLayout.SetCharacterInformation))]
        [HarmonyPatch(typeof(InformationZoneLayout), nameof(InformationZoneLayout.SetEnemyInformation))]
        [HarmonyPostfix]
        public static void InformationZoneReset(InformationZoneLayout __instance)
        {
            __instance._unitPortrait.enabled = true;

            var img = __instance._unitPortrait.transform.Find($"{MOD_GUID}_AdvancedUnitPortrait");
            if (img != null)
                img.gameObject.SetActive(false);
        }

        [HarmonyPatch(typeof(InformationZoneLayout), nameof(InformationZoneLayout.SetCharacterInformation))]
        [HarmonyPostfix]
        public static void InformationZoneChanges(InformationZoneLayout __instance, CharacterCombatUIInfo character)
        {
            var charso = character.CharacterBase;

            if (charso is CharacterSOAdvanced ch)
            {
                __instance._unitPortrait.enabled = false;

                var imgTransform = __instance._unitPortrait.transform.Find($"{MOD_GUID}_AdvancedUnitPortrait");
                GameObject img;

                if (imgTransform == null)
                {
                    img = Object.Instantiate(__instance._unitPortrait.gameObject, __instance._unitPortrait.transform);
                    imgTransform = img.transform;

                    img.name = $"{MOD_GUID}_AdvancedUnitPortrait";
                    imgTransform.localPosition = Vector3.zero;
                    imgTransform.SetAsFirstSibling();

                    var toDestroy = new List<GameObject>();

                    for (int i = 0; i < imgTransform.childCount; i++)
                    {
                        toDestroy.Add(imgTransform.GetChild(i).gameObject);
                    }

                    foreach (var obj in toDestroy)
                    {
                        if (obj != null)
                        {
                            Object.Destroy(obj);
                        }
                    }

                    img.AddComponent<LocalPositionConstantSetter>();
                }
                else
                {
                    img = imgTransform.gameObject;
                }

                var advimg = img.GetComponent<Image>();
                advimg.enabled = true;

                var offs = ch.spriteOffset;
                var scale = ch.spriteScale;
                var sprite = character.Portrait;

                if(ch.combatMenuSprite != null)
                {
                    sprite = ch.combatMenuSprite;
                    scale = ch.combatMenuSpriteScale;
                    offs = ch.combatMenuSpriteOffset;
                }
                else if(ch.menuSprite != null)
                {
                    sprite = ch.menuSprite;
                    scale = ch.menuSpriteScale;
                    offs = ch.menuSpriteOffset;
                }

                advimg.sprite = sprite;

                imgTransform.localScale = scale;
                imgTransform.localPosition = img.GetComponent<LocalPositionConstantSetter>().targetPos = offs * (94f / 107.5f);

                img.SetActive(true);
            }
        }

        [HarmonyPatch(typeof(CharacterInfoLayout), nameof(CharacterInfoLayout.InitializeCharacter))]
        [HarmonyPostfix]
        public static void InitializeFieldCharacter(CharacterInfoLayout __instance, CharacterCombatUIInfo charaInfo)
        {
            ChangeImageSizeAndPosition(__instance._character._renderer, charaInfo.CharacterBase, __instance._character.GetOrAddComponent<CSSP_CharacterStorer>().storedCharacter, 1f, false, __instance._character._renderer.transform.parent, false);
            __instance._character.GetOrAddComponent<CSSP_CharacterStorer>().storedCharacter = charaInfo.CharacterBase;
        }

        [HarmonyPatch(typeof(CharacterInfoLayout), nameof(CharacterInfoLayout.TransformCharacter))]
        [HarmonyPostfix]
        public static void TransformFieldCharacter(CharacterInfoLayout __instance, CharacterCombatUIInfo charaInfo)
        {
            ChangeImageSizeAndPosition(__instance._character._renderer, charaInfo.CharacterBase, __instance._character.GetOrAddComponent<CSSP_CharacterStorer>().storedCharacter, 1f, false, __instance._character._renderer.transform.parent, false);
            __instance._character.GetOrAddComponent<CSSP_CharacterStorer>().storedCharacter = charaInfo.CharacterBase;
        }

        public static void ChangeImageSizeAndPosition(Image img, CharacterSO current, CharacterSO previous, float offsetMultiplier = 1f, bool menu = true, Transform overrideXOffsetTransform = null, bool doPrevious = true)
        {
            if (img == null)
            {
                return;
            }

            if (previous != null && previous is CharacterSOAdvanced prevCh && doPrevious)
            {
                var useMenu = menu && prevCh.menuSprite != null;

                img.transform.localScale = WideCharacterTools.Vector3Divide(img.transform.localScale, useMenu ? prevCh.menuSpriteScale : prevCh.spriteScale);

                if (overrideXOffsetTransform != null)
                {
                    img.transform.localPosition -= (useMenu ? prevCh.menuSpriteOffset : prevCh.spriteOffset).y * offsetMultiplier * Vector3.up;
                    overrideXOffsetTransform.localPosition -= (useMenu ? prevCh.menuSpriteOffset : prevCh.spriteOffset).x * offsetMultiplier * Vector3.right;
                }
                else
                {
                    img.transform.localPosition -= (useMenu ? prevCh.menuSpriteOffset : prevCh.spriteOffset) * offsetMultiplier;
                }
            }

            if (current != null && current is CharacterSOAdvanced currCh)
            {
                var useMenu = menu && currCh.menuSprite != null;

                img.transform.localScale = Vector3.Scale(img.transform.localScale, useMenu ? currCh.menuSpriteScale : currCh.spriteScale);

                if (overrideXOffsetTransform != null)
                {
                    img.transform.localPosition += (useMenu ? currCh.menuSpriteOffset : currCh.spriteOffset).y * offsetMultiplier * Vector3.up;
                    overrideXOffsetTransform.localPosition += (useMenu ? currCh.menuSpriteOffset : currCh.spriteOffset).x * offsetMultiplier * Vector3.right;
                }
                else
                {
                    img.transform.localPosition += (useMenu ? currCh.menuSpriteOffset : currCh.spriteOffset) * offsetMultiplier;
                }

                if (useMenu)
                {
                    img.sprite = currCh.menuSprite;
                }
            }
        }
    }

    public class CSSP_CharacterStorer : MonoBehaviour
    {
        public CharacterSO storedCharacter;
    }
}
