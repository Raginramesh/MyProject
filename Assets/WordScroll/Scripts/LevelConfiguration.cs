using UnityEngine;

[CreateAssetMenu(fileName = "Level Configuration", menuName = "Word Scroll/Level Configuration")]
public class LevelConfiguration : ScriptableObject
{
    [Header("Sample Level Creation")]
    [SerializeField] private bool createSampleLevels = true;
    [SerializeField] private int numberOfSampleLevels = 10;
    
    [Header("Default Settings")]
    [SerializeField] private int baseGridSize = 5;
    [SerializeField] private int baseTargetScore = 100;
    [SerializeField] private int baseMoves = 10;
    [SerializeField] private float difficultyScaling = 1.2f; // Multiplier per level
    
    /// <summary>
    /// Generate sample level data for testing
    /// </summary>
    public LevelData[] GenerateSampleLevels()
    {
        if (!createSampleLevels) return new LevelData[0];
        
        LevelData[] levels = new LevelData[numberOfSampleLevels];
        
        for (int i = 0; i < numberOfSampleLevels; i++)
        {
            // Calculate scaled values for this level
            int levelNumber = i + 1;
            float scaleFactor = Mathf.Pow(difficultyScaling, i);
            
            int targetScore = Mathf.RoundToInt(baseTargetScore * scaleFactor);
            int maxMoves = Mathf.RoundToInt(baseMoves * (1 + i * 0.1f)); // Slight increase in moves
            
            // Create level data
            LevelData level = CreateInstance<LevelData>();
            level.name = $"Level_{levelNumber:D2}";
            
            // Use reflection to set private fields (for demonstration)
            SetPrivateField(level, "levelNumber", levelNumber);
            SetPrivateField(level, "levelName", $"Level {levelNumber}");
            SetPrivateField(level, "levelDescription", GetLevelDescription(levelNumber));
            SetPrivateField(level, "targetScore", targetScore);
            SetPrivateField(level, "oneStarScore", Mathf.RoundToInt(targetScore * 0.3f));     // 30% for 1 star
            SetPrivateField(level, "twoStarScore", Mathf.RoundToInt(targetScore * 0.7f));     // 70% for 2 stars
            SetPrivateField(level, "threeStarScore", targetScore);                            // 100% for 3 stars
            SetPrivateField(level, "maxMoves", maxMoves);
            SetPrivateField(level, "unlimitedMoves", false);
            SetPrivateField(level, "gridSize", baseGridSize);
            SetPrivateField(level, "customLetterSet", "");
            SetPrivateField(level, "timeBonus", 1.0f);
            SetPrivateField(level, "scoreMultiplier", 1.0f + (i * 0.05f)); // Slight bonus for later levels
            SetPrivateField(level, "enableSpecialTiles", i >= 5); // Enable special tiles from level 6+
            SetPrivateField(level, "isUnlocked", i == 0); // Only first level unlocked
            SetPrivateField(level, "isTutorialLevel", i == 0); // First level is tutorial
            
            levels[i] = level;
        }
        
        // Link levels together
        for (int i = 0; i < levels.Length - 1; i++)
        {
            SetPrivateField(levels[i], "nextLevel", levels[i + 1]);
        }
        
        return levels;
    }
    
    /// <summary>
    /// Get description for a specific level
    /// </summary>
    private string GetLevelDescription(int levelNumber)
    {
        switch (levelNumber)
        {
            case 1:
                return "Welcome! Learn the basics by reaching the target score.";
            case 2:
                return "Build on your skills with a slightly higher challenge.";
            case 3:
                return "Time to test your word-finding abilities!";
            case 4:
                return "The difficulty increases - stay focused!";
            case 5:
                return "You're getting the hang of it. Keep going!";
            case 6:
                return "Special tiles are now available. Use them wisely!";
            case 7:
                return "Master level gameplay with more complex challenges.";
            case 8:
                return "Advanced word combinations await you.";
            case 9:
                return "Near the peak - show your expertise!";
            case 10:
                return "The ultimate challenge. Prove your mastery!";
            default:
                return $"Challenge yourself with Level {levelNumber}!";
        }
    }
    
    /// <summary>
    /// Helper method to set private fields using reflection
    /// </summary>
    private void SetPrivateField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(obj, value);
        }
        else
        {
            Debug.LogWarning($"Field '{fieldName}' not found in {obj.GetType().Name}");
        }
    }
}
