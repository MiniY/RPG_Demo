using UnityEngine;

/// <summary>Explicit new surface identity; the frozen R3 settings never opt in.</summary>
public sealed class MapSurfaceV4Settings : MapGenerationSettings
{
    [Min(.1f)] public float shorelineFramesPerSecond = 4f;
    [Range(0f, 1f)] public float lightShadowTint = .6f;
    public Texture2D shorelineTexture;
    public Texture2D waterRockTexture;
    public Texture2D contactShadowTexture;
    public string[] supplementShadowVariantIds = System.Array.Empty<string>();
}
