using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Existing Tiny Swords terrain resources used only by terrain-v2 projection.</summary>
[CreateAssetMenu(menuName = "RPG Demo/Map Generation/Terrain V2 Palette")]
public sealed class MapTerrainV2Palette : ScriptableObject
{
    [SerializeField] private TerrainAutotileSet grassAndCliff;
    [SerializeField] private TileBase stairUpperLeft;
    [SerializeField] private TileBase stairUpperRight;
    [SerializeField] private TileBase stairLowerLeft;
    [SerializeField] private TileBase stairLowerRight;
    [SerializeField] private Texture2D shadowTexture;
    [SerializeField] private Texture2D foamTexture;

    public TerrainAutotileSet GrassAndCliff => grassAndCliff;
    public TileBase StairUpperLeft => stairUpperLeft;
    public TileBase StairUpperRight => stairUpperRight;
    public TileBase StairLowerLeft => stairLowerLeft;
    public TileBase StairLowerRight => stairLowerRight;
    public Texture2D ShadowTexture => shadowTexture;
    public Texture2D FoamTexture => foamTexture;

    public bool IsComplete => grassAndCliff != null &&
        grassAndCliff.HasCompleteTopology && grassAndCliff.HasCompleteSouthFaces &&
        stairUpperLeft != null && stairUpperRight != null &&
        stairLowerLeft != null && stairLowerRight != null &&
        shadowTexture != null && foamTexture != null;
}
