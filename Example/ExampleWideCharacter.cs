using BrutalAPI;
using System;
using System.Collections.Generic;
using System.Text;

namespace WideCharacters.Example
{
    public static class ExampleWideCharacter
    {
        public static void Add()
        {
            var ch = new AdvancedCharacter();

            ch.name = "Fake Magnificus";
            ch.entityID = (EntityIDs)957192377;
            ch.healthColor = Pigments.Yellow;

            ch.usesBaseAbility = true;
            ch.usesAllAbilities = true;
            ch.levels = new CharacterRankedData[1];

            ch.frontSprite = ResourceLoader.LoadSprite("fakemagnificus");
            ch.backSprite = ResourceLoader.LoadSprite("fakemagnificus_back");
            ch.overworldSprite = ResourceLoader.LoadSprite("fakemagnificus_ow", 32, new(0.25f, 0f));

            ch.lockedSprite = ResourceLoader.LoadSprite("fakemagnificus_locked");
            ch.unlockedSprite = ResourceLoader.LoadSprite("fakemagnificus_unlocked");
            ch.isSecret = false;
            ch.menuChar = true;

            ch.isSupport = false;
            ch.appearsInShops = false;
            ch.ignoredAbilities = new();

            ch.hurtSound = LoadedAssetsHandler.GetCharcater("Gospel_CH").damageSound;
            ch.deathSound = LoadedAssetsHandler.GetCharcater("Gospel_CH").deathSound;
            ch.dialogueSound = LoadedAssetsHandler.GetCharcater("Gospel_CH").dxSound;

            ch.Size = 2;
            ch.CalculateSpriteScaleAndOffsetForSize();

            ch.AddLevel(30, new Ability[]
            {
                new()
                {
                    name = "Stand Still",
                    description = "Does absolutely nothing",
                    cost = new ManaColorSO[0],
                    animationTarget = Slots.Self,
                    canBeRerolled = true,
                    effects = new Effect[0],
                    priority = 0,
                    rarity = 0,
                    sprite = ResourceLoader.LoadSprite("fakemagnificus_ability"),
                    visuals = null
                }
            }, 0);

            ch.MenuSprite = ResourceLoader.LoadSprite("fakemagnificus_menu"); //override sprite for when the character is shown in the rank up screen/combat menu/etc so that the sprite doesn't appear oversized

            ch.AddCharacter();
        }
    }
}
