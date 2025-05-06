using UnityEngine;
using System.Collections.Generic;

// This attribute allows you to create instances of this class
// directly from the Assets > Create menu in Unity.
[CreateAssetMenu(fileName = "LevelData_00", menuName = "WordScrabble/Level Data", order = 1)]
public class LevelData : ScriptableObject
{
    [Header("Grid Setup")]
    [Min(3)] // Ensure grid is at least 3x3
    public int gridWidth = 7;
    [Min(3)]
    public int gridHeight = 7;

    // TODO: Add field for initial tile layout (blockers, bonuses) later
    // public List<TileSetupData> initialTileLayout;

    [Header("Level Goal")]
    [Min(1)]
    public int targetScore = 10;

    [Header("Words")]
    public List<string> wordsForLevel = new List<string>();

    // You could add other level-specific things here later:
    // - Time limit
    // - Specific power-ups available
    // - Theme identifier
}

// Optional: Define a struct/class for initial tile setup if needed later
// [System.Serializable]
// public struct TileSetupData
// {
//     public Vector2Int coordinate;
//     public TileType type; // Assuming you have a TileType enum
// }