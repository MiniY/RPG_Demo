using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MapVNext
{
    public sealed class MapVNextAssets : ScriptableObject
    {
        public const string Identity = "map-vnext-p1-stairs-20261028-r3d";
        public static readonly int[] Families = { 5, 3, 2, 1, 4 };
        public Tile[] NativeTiles;
        public MapGenerationSettings SourceSettings;
        public MapTerrainV2Palette SourcePalette;
        public GameObject PlayerPrefab;
        public string InputFingerprint;

        public Tile Native(int elevation, int role)
        {
            if (elevation < 0 || elevation >= Families.Length)
                throw new NotSupportedException("UnsupportedGrassElevation:" + elevation);
            if (role < 0 || role >= 44) throw new ArgumentOutOfRangeException(nameof(role));
            Tile tile = NativeTiles[elevation * 44 + role];
            if (tile == null || tile.sprite == null) throw new InvalidOperationException("MissingNativeTile");
            return tile;
        }

        public int Role(Tile tile)
        {
            if (tile == null || tile.sprite == null) throw new InvalidOperationException("MissingProjectionSprite");
            for (int i = 0; i < 44; i++)
                if (Native(0, i).sprite.rect == tile.sprite.rect) return i;
            throw new NotSupportedException("UnmappedNativeRect:" + tile.sprite.rect);
        }

        public static void ValidateFamilies(int[] requested)
        {
            if(requested == null || requested.Length != 5) throw new InvalidOperationException("WrongColorCount");
            for(int i=0;i<5;i++) if(requested[i] != Families[i]) throw new InvalidOperationException("WrongColorBinding:H"+i);
        }
    }
}
