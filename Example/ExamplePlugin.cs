using BepInEx;
using HarmonyLib;
using System;

namespace WideCharacters.Example
{
    [BepInPlugin("SpecialAPI.WideCharactersExample", "Wide Characters Example", "1.0.0")]
    public class ExamplePlugin : BaseUnityPlugin
    {
        public void Awake()
        {
            new Harmony("SpecialAPI.WideCharactersExample").PatchAll();

            ExampleWideCharacter.Add();
        }
    }
}
