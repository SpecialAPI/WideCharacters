using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace WideCharacterAPI
{
    public class CharacterSOAdvanced : CharacterSO
    {
        public Vector3 spriteScale = Vector3.one;
        public Vector3 spriteOffset;

        public Sprite menuSprite;
        public Vector3 menuSpriteScale = Vector3.one;
        public Vector3 menuSpriteOffset;

        public Sprite combatMenuSprite;
        public Vector3 combatMenuSpriteScale = Vector3.one;
        public Vector3 combatMenuSpriteOffset;

        public int size = 1;
    }
}
