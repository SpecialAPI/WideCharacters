using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace WideCharacters.Example
{
    public static class ResourceLoader
    {
        public static Texture2D LoadTexture(string name)
        {
            if (TryReadFromResource(name.TryAddExtension("png"), out var ba))
            {
                var tex = new Texture2D(1, 1);
                tex.LoadImage(ba);
                tex.filterMode = FilterMode.Point;
                return tex;
            }
            return null;
        }

        public static bool TryReadFromResource(string resname, out byte[] ba)
        {
            var names = Assembly.GetExecutingAssembly().GetManifestResourceNames().Where(x => x.EndsWith($".{resname}"));
            if (names.Count() > 0)
            {
                using var strem = Assembly.GetExecutingAssembly().GetManifestResourceStream(names.First());
                ba = new byte[strem.Length];
                strem.Read(ba, 0, ba.Length);
                return true;
            }
            Debug.LogError($"Couldn't load from resource name {resname}, returning an empty byte array.");
            ba = new byte[0];
            return false;
        }

        public static Sprite LoadSprite(string name, int pixelsperunit = 32, Vector2? pivot = null)
        {
            var tex = LoadTexture(name);
            if (tex != null)
            {
                return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), pivot ?? new Vector2(0.5f, 0.5f), pixelsperunit);
            }
            return null;
        }

        public static string TryAddExtension(this string n, string e)
        {
            if (n.EndsWith($".{e}"))
            {
                return n;
            }
            return n + $".{e}";
        }
    }
}
