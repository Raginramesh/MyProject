using UnityEngine;

[CreateAssetMenu(fileName = "New Level", menuName = "Word Scroll/Level Data")]
public class LevelData : ScriptableObject
{
    [Header("Level Identity")]
    [SerializeField] private int levelNumber = 1;
    [SerializeField] private string levelName = "Level 1";
    [TextArea(2, 4)]
    [SerializeField] private string levelDescription = "Complete the level by reaching the target score within the move limit.";
    
    [Header("Score Requirements")]
    [SerializeField] private int targetScore = 100;
    
    [Header("Star Rating Thresholds (Percentage of Target Score)")]
    [Range(0f, 100f)]
    [SerializeField] private float oneStarPercentage = 50f;   // 50% of target for 1 star
    [Range(0f, 100f)]
    [SerializeField] private float twoStarPercentage = 75f;   // 75% of target for 2 stars  
    [Range(0f, 100f)]
    [SerializeField] private float threeStarPercentage = 100f; // 100% of target for 3 stars
    
    [Header("Move Constraints")]
    [SerializeField] private int maxMoves = 10;
    [SerializeField] private bool unlimitedMoves = false;
    
    [Header("Grid Configuration")]
    [SerializeField] private int gridSize = 5;
    [SerializeField] private string customLetterSet = ""; // Empty = use default
    
    [Header("Difficulty Modifiers")]
    [SerializeField] private float timeBonus = 1.0f;     // Multiplier for time-based scoring
    [SerializeField] private float scoreMultiplier = 1.0f; // General score multiplier
    [SerializeField] private bool enableSpecialTiles = false;
    
    [Header("Level Progression")]
    [SerializeField] private LevelData nextLevel;
    [SerializeField] private bool isUnlocked = false;
    [SerializeField] private bool isTutorialLevel = false;
    
    // Public properties
    public int LevelNumber => levelNumber;
    public string LevelName => levelName;
    public string LevelDescription => levelDescription;
    public int TargetScore => targetScore;
    public float OneStarPercentage => oneStarPercentage;
    public float TwoStarPercentage => twoStarPercentage;
    public float ThreeStarPercentage => threeStarPercentage;
    
    // Calculated star score thresholds based on percentages
    public int OneStarScore => Mathf.RoundToInt(targetScore * oneStarPercentage / 100f);
    public int TwoStarScore => Mathf.RoundToInt(targetScore * twoStarPercentage / 100f);
    public int ThreeStarScore => Mathf.RoundToInt(targetScore * threeStarPercentage / 100f);
    
    public int MaxMoves => maxMoves;
    public bool UnlimitedMoves => unlimitedMoves;
    public int GridSize => gridSize;
    public string CustomLetterSet => customLetterSet;
    public float TimeBonus => timeBonus;
    public float ScoreMultiplier => scoreMultiplier;
    public bool EnableSpecialTiles => enableSpecialTiles;
    public LevelData NextLevel => nextLevel;
    public bool IsUnlocked => isUnlocked;
    public bool IsTutorialLevel => isTutorialLevel;
    
    /// <summary>
    /// Calculate star rating based on achieved score (using percentage thresholds)
    /// </summary>
    public int GetStarRating(int achievedScore)
    {
        if (achievedScore >= ThreeStarScore) return 3;
        if (achievedScore >= TwoStarScore) return 2;
        if (achievedScore >= OneStarScore) return 1;
        return 0;
    }
    
    /// <summary>
    /// Calculate star rating based on percentage of target score
    /// </summary>
    public int GetStarRatingByPercentage(float percentage)
    {
        if (percentage >= threeStarPercentage) return 3;
        if (percentage >= twoStarPercentage) return 2;
        if (percentage >= oneStarPercentage) return 1;
        return 0;
    }
    
    /// <summary>
    /// Get the percentage of target score achieved
    /// </summary>
    public float GetScorePercentage(int achievedScore)
    {
        if (targetScore <= 0) return 0f;
        return (float)achievedScore / targetScore * 100f;
    }
    
    /// <summary>
    /// Check if the level is completed based on moves used
    /// Level completes when all moves are exhausted (or unlimited moves)
    /// </summary>
    public bool IsLevelCompletedByMoves(int currentMoves)
    {
        if (unlimitedMoves) return false; // Never auto-complete with unlimited moves
        return currentMoves >= maxMoves;
    }
    
    /// <summary>
    /// Unlock this level
    /// </summary>
    public void UnlockLevel()
    {
        isUnlocked = true;
    }
    
    /// <summary>
    /// Check if player has moves remaining
    /// </summary>
    public bool HasMovesRemaining(int currentMoves)
    {
        if (unlimitedMoves) return true;
        return currentMoves < maxMoves;
    }
    
    /// <summary>
    /// Get remaining moves
    /// </summary>
    public int GetRemainingMoves(int currentMoves)
    {
        if (unlimitedMoves) return -1; // -1 indicates unlimited
        return Mathf.Max(0, maxMoves - currentMoves);
    }
    
    /// <summary>
    /// Validate star rating percentages (for editor use)
    /// </summary>
    public bool ValidateStarPercentages()
    {
        return oneStarPercentage <= twoStarPercentage && 
               twoStarPercentage <= threeStarPercentage &&
               oneStarPercentage >= 0f && 
               threeStarPercentage <= 100f;
    }
    
    /// <summary>
    /// Get debug info about star thresholds
    /// </summary>
    public string GetStarThresholdInfo()
    {
        return $"Star Thresholds: 1⭐{OneStarScore} ({oneStarPercentage}%), " +
               $"2⭐{TwoStarScore} ({twoStarPercentage}%), " +
               $"3⭐{ThreeStarScore} ({threeStarPercentage}%) of {targetScore}";
    }
    
#if UNITY_EDITOR
    /// <summary>
    /// Update calculated scores when percentages change in editor
    /// </summary>
    private void OnValidate()
    {
        // Ensure percentage order is correct
        if (oneStarPercentage > twoStarPercentage)
            twoStarPercentage = oneStarPercentage;
        if (twoStarPercentage > threeStarPercentage)
            threeStarPercentage = twoStarPercentage;
            
        // Clamp percentages to valid range
        oneStarPercentage = Mathf.Clamp(oneStarPercentage, 0f, 100f);
        twoStarPercentage = Mathf.Clamp(twoStarPercentage, 0f, 100f);
        threeStarPercentage = Mathf.Clamp(threeStarPercentage, 0f, 100f);
    }
#endif
}
