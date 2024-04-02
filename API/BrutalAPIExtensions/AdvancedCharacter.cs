using BrutalAPI;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

public class AdvancedCharacter
{
    public WideCharacterAPI.CharacterSOAdvanced c = ScriptableObject.CreateInstance<WideCharacterAPI.CharacterSOAdvanced>();
    public SelectableCharacterData charData = new("Belm", BrutalAPI.ResourceLoader.LoadSprite("BasicPartyMemberUnlocked"), BrutalAPI.ResourceLoader.LoadSprite("BasicPartyMemberLocked"));

    //Basics
    public string name = "Belm";
    public string characterID = "";
    public EntityIDs entityID;
    public ManaColorSO healthColor = Pigments.Purple;

    //Abilities
    public bool usesBaseAbility = true;
    public Ability baseAbility = null;
    public bool usesAllAbilities = false;
    public BasePassiveAbilitySO[] passives = new BasePassiveAbilitySO[0];
    public CharacterRankedData[] levels = new CharacterRankedData[4];

    //Visuals
    public Sprite frontSprite = BrutalAPI.ResourceLoader.LoadSprite("BasicPartyMemberFront");
    public Sprite backSprite = BrutalAPI.ResourceLoader.LoadSprite("BasicPartyMemberBack");
    public Sprite overworldSprite = BrutalAPI.ResourceLoader.LoadSprite("BasicPartyMemberOverworld", 32, new Vector2(0.5f, 0));
    public Sprite lockedSprite = BrutalAPI.ResourceLoader.LoadSprite("BasicPartyMemberLocked");
    public Sprite unlockedSprite = BrutalAPI.ResourceLoader.LoadSprite("BasicPartyMemberUnlocked");
    public ExtraCharacterCombatSpritesSO extraSprites;
    public bool walksInOverworld = true;
    public bool isSecret = false;
    public bool menuChar = true;
    public bool isSupport = false;
    public bool appearsInShops = true;
    public List<int> ignoredAbilities = [];

    //Audio
    public string hurtSound = "";
    public string deathSound = "";
    public string dialogueSound = "";

    //Unlocks
    public UnlockableID heavenUnlock = UnlockableID.None;
    public UnlockableID osmanUnlock = UnlockableID.None;

    //Advanced
    public int Size = 1;

    public Vector3 SpriteScale = Vector3.one;
    public Vector3 SpriteOffset;

    public Sprite MenuSprite;
    public Vector3 MenuSpriteScale = Vector3.one;
    public Vector3 MenuSpriteOffset;

    public Sprite CombatMenuSprite;
    public Vector3 CombatMenuSpriteScale = Vector3.one;
    public Vector3 CombatMenuSpriteOffset;

    public void CalculateSpriteScaleAndOffsetForSize()
    {
        SpriteScale = Vector3.one * (1 + 2 * Mathf.Max(Size - 1, 0));
        SpriteOffset = new Vector3(214.8f, 214.8f, 0f) * Mathf.Max(Size - 1, 0);
    }

    public void AddLevel(int health, Ability[] abilities, int level)
    {
        var data = new CharacterRankedData()
        {
            rankAbilities = new CharacterAbility[abilities.Length],
            health = health
        };

        for (int i = 0; i < abilities.Length; i++)
        {
            data.rankAbilities[i] = abilities[i].CharacterAbility();
        }

        levels[level] = data;
    }

    public void AddCharacter()
    {
        charData.LoadedCharacter = c;
        charData._characterName = characterID == "" ? name + "_CH" : characterID;
        if (menuChar)
        {
            charData._portrait = unlockedSprite;
            charData._noPortrait = lockedSprite;
        }
        charData._isSecret = isSecret;

        c.name = charData._characterName;
        c._characterName = name;
        c.characterEntityID = entityID;
        c.healthColor = healthColor;
        c.usesBasicAbility = usesBaseAbility;
        c.basicCharAbility = baseAbility == null ? BrutalAPI.BrutalAPI.slapCharAbility : baseAbility.CharacterAbility();
        c.usesAllAbilities = usesAllAbilities;
        c.rankedData = levels;
        c.passiveAbilities = passives;
        c.characterSprite = frontSprite;
        c.characterBackSprite = backSprite;
        c.extraCombatSprites = extraSprites;
        c.characterOWSprite = overworldSprite;
        c.movesOnOverworld = walksInOverworld;
        c.damageSound = hurtSound;
        c.deathSound = deathSound;
        c.speakerDataName = name;
        c.dxSound = dialogueSound;

        c.size = Size;
        c.spriteScale = SpriteScale;
        c.spriteOffset = SpriteOffset;
        c.menuSprite = MenuSprite;
        c.menuSpriteScale = MenuSpriteScale;
        c.menuSpriteOffset = MenuSpriteOffset;
        c.combatMenuSprite = CombatMenuSprite;
        c.combatMenuSpriteScale = CombatMenuSpriteScale;
        c.combatMenuSpriteOffset = CombatMenuSpriteOffset;

        //Add character to menu
        if (menuChar)
        {
            var oldData = BrutalAPI.BrutalAPI.selCharsSO._characters;
            var charList = new List<SelectableCharacterData>();
            foreach (SelectableCharacterData i in oldData)
            {
                charList.Add(i);
            }
            charList.Add(charData);
            BrutalAPI.BrutalAPI.selCharsSO._characters = [.. charList];

            //Selection Bias
            if (isSupport) BrutalAPI.BrutalAPI.selCharsSO._supportCharacters.Add(new CharacterRefString(charData._characterName), new CharacterIgnoredAbilities() { ignoredAbilities = ignoredAbilities });
            else BrutalAPI.BrutalAPI.selCharsSO._dpsCharacters.Add(new CharacterRefString(charData._characterName), new CharacterIgnoredAbilities() { ignoredAbilities = ignoredAbilities });
        }

        //Remove character from shops
        if (!appearsInShops)
        {
            for (int i = 0; i < BrutalAPI.BrutalAPI.hardAreas.Count; i++)
            {
                BrutalAPI.BrutalAPI.hardAreas[i]._omittedCharacters.Add(charData._characterName);
            }
            for (int i = 0; i < BrutalAPI.BrutalAPI.easyAreas.Count; i++)
            {
                BrutalAPI.BrutalAPI.easyAreas[i]._omittedCharacters.Add(charData._characterName);
            }
        }

        //Unlock character
        if (!LoadedAssetsHandler.LoadedCharacters.ContainsKey(charData._characterName))
        {
            LoadedAssetsHandler.LoadedCharacters.Add(charData._characterName, c);
        }

        //Add unlocks
        /*if (heavenUnlock != UnlockableID.None)
        {
            BrutalAPI.BrutalAPI.unlockablesDatabase._heavenIDs.Add(entityID, heavenUnlock);
        }
        if (osmanUnlock != UnlockableID.None)
        {
            BrutalAPI.BrutalAPI.unlockablesDatabase._osmanIDs.Add(entityID, osmanUnlock);
        }*/

        //Add abilities to database
        foreach (var level in levels)
        {
            foreach (var ability in level.rankAbilities)
            {
                if (!LoadedAssetsHandler.LoadedCharacterAbilities.ContainsKey(ability.ability._abilityName))
                    LoadedAssetsHandler.LoadedCharacterAbilities.Add(ability.ability._abilityName, ability.ability);

                if (!LoadedAssetsHandler.GetAbilityDB()._characterAbilityPool.Contains(ability.ability._abilityName))
                    LoadedAssetsHandler.GetAbilityDB()._characterAbilityPool.Add(ability.ability._abilityName);
            }
        }

        Debug.Log("Added character " + charData._characterName);
        BrutalAPI.BrutalAPI.moddedChars.Add(c);
    }
}
