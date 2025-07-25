using UnityEngine;

/// <summary>
/// Game mode enumeration for level configuration
/// </summary>
public enum LevelGameMode
{
    ScrabbleStyle,  // Free-form word discovery with scoring (existing system)
    WordleStyle     // Target word discovery with letter feedback
}

/// <summary>
/// Win condition type for Wordle Style levels
/// </summary>
public enum LevelWinConditionType
{
    MoveBased,      // Win based on move count efficiency
    TimeBased       // Win based on time efficiency
}

[CreateAssetMenu(fileName = "New Level", menuName = "Word Scroll/Level Data")]
public class LevelData : ScriptableObject
{
    [Header("Level Identity")]
    [SerializeField] private int levelNumber = 1;
    [SerializeField] private string levelName = "Level 1";
    [TextArea(2, 4)]
    [SerializeField] private string levelDescription = "Complete the level by reaching the target score within the move limit.";
    
    [Header("Game Mode Configuration")]
    [SerializeField] private LevelGameMode gameMode = LevelGameMode.ScrabbleStyle;
    [Tooltip("Grid size configuration - overrides any other grid size settings in the project")]
    [Range(3, 10)]
    [SerializeField] private int gridSize = 5;
    
    [Header("Scrabble Style Settings (Score-based gameplay)")]
    [SerializeField] private int targetScore = 100;
    
    [Header("Star Rating Thresholds (Percentage of Target Score)")]
    [Range(0f, 100f)]
    [SerializeField] private float oneStarPercentage = 50f;   // 50% of target for 1 star
    [Range(0f, 100f)]
    [SerializeField] private float twoStarPercentage = 75f;   // 75% of target for 2 stars  
    [Range(0f, 100f)]
    [SerializeField] private float threeStarPercentage = 100f; // 100% of target for 3 stars
    
    [Header("Wordle Style Settings (Target word discovery)")]
    [Tooltip("List of target words that players must discover")]
    [SerializeField] private string[] targetWords = new string[0];
    [SerializeField] private LevelWinConditionType winConditionType = LevelWinConditionType.MoveBased;
    
    [Header("Win Condition - Move Based")]
    [SerializeField] private int maxMoves = 10;
    [SerializeField] private bool unlimitedMoves = false;
    
    [Header("Win Condition - Time Based")]
    [SerializeField] private float timeLimit = 300f; // seconds (5 minutes default)
    
    [Header("Wordle Style Star Thresholds (Efficiency Percentage)")]
    [Tooltip("Complete within this % of time/moves for 3 stars")]
    [Range(1f, 50f)]
    [SerializeField] private float threeStarEfficiencyPercentage = 10f; // Complete within 10% of limit
    [Tooltip("Complete within this % of time/moves for 2 stars")]
    [Range(1f, 80f)]
    [SerializeField] private float twoStarEfficiencyPercentage = 50f;   // Complete within 50% of limit  
    [Tooltip("Complete within this % of time/moves for 1 star")]
    [Range(1f, 100f)]
    [SerializeField] private float oneStarEfficiencyPercentage = 90f;   // Complete within 90% of limit
    
    [Header("Grid Population")]
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
    public LevelGameMode GameMode => gameMode;
    public int GridSize => gridSize;
    
    // Scrabble Style properties
    public int TargetScore => targetScore;
    public float OneStarPercentage => oneStarPercentage;
    public float TwoStarPercentage => twoStarPercentage;
    public float ThreeStarPercentage => threeStarPercentage;
    
    // Wordle Style properties
    public string[] TargetWords => targetWords;
    public LevelWinConditionType WinConditionType => winConditionType;
    public float TimeLimit => timeLimit;
    public float ThreeStarEfficiencyPercentage => threeStarEfficiencyPercentage;
    public float TwoStarEfficiencyPercentage => twoStarEfficiencyPercentage;
    public float OneStarEfficiencyPercentage => oneStarEfficiencyPercentage;
    
    // Common properties
    public int MaxMoves => maxMoves;
    public bool UnlimitedMoves => unlimitedMoves;
    public string CustomLetterSet => customLetterSet;
    public float TimeBonus => timeBonus;
    public float ScoreMultiplier => scoreMultiplier;
    public bool EnableSpecialTiles => enableSpecialTiles;
    public LevelData NextLevel => nextLevel;
    public bool IsUnlocked => isUnlocked;
    public bool IsTutorialLevel => isTutorialLevel;
    
    // Calculated star score thresholds for Scrabble Style (score-based)
    public int OneStarScore => Mathf.RoundToInt(targetScore * oneStarPercentage / 100f);
    public int TwoStarScore => Mathf.RoundToInt(targetScore * twoStarPercentage / 100f);
    public int ThreeStarScore => Mathf.RoundToInt(targetScore * threeStarPercentage / 100f);
    
    // Helper properties
    public bool IsWordleStyle => gameMode == LevelGameMode.WordleStyle;
    public bool IsScrabbleStyle => gameMode == LevelGameMode.ScrabbleStyle;
    public bool UsesTimer => IsWordleStyle && winConditionType == LevelWinConditionType.TimeBased;
    public bool UsesMoves => IsScrabbleStyle || (IsWordleStyle && winConditionType == LevelWinConditionType.MoveBased);
    public int TargetWordCount => targetWords?.Length ?? 0;
    
    /// <summary>
    /// Calculate star rating based on achieved score (Scrabble Style only)
    /// </summary>
    public int GetStarRating(int achievedScore)
    {
        if (IsWordleStyle)
        {
            Debug.LogWarning("GetStarRating(score) called on Wordle Style level. Use GetStarRatingByEfficiency instead.");
            return 0;
        }
        
        if (achievedScore >= ThreeStarScore) return 3;
        if (achievedScore >= TwoStarScore) return 2;
        if (achievedScore >= OneStarScore) return 1;
        return 0;
    }
    
    /// <summary>
    /// Calculate star rating based on efficiency for Wordle Style levels
    /// </summary>
    /// <param name="usedAmount">Moves used or time taken</param>
    /// <param name="totalAmount">Max moves or time limit</param>
    /// <param name="allWordsFound">Whether all target words were found</param>
    public int GetStarRatingByEfficiency(float usedAmount, float totalAmount, bool allWordsFound = true)
    {
        if (IsScrabbleStyle)
        {
            Debug.LogWarning("GetStarRatingByEfficiency called on Scrabble Style level. Use GetStarRating instead.");
            return 0;
        }
        
        if (!allWordsFound) return 0; // No stars if not all words found
        
        float efficiencyPercentage = (usedAmount / totalAmount) * 100f;
        
        if (efficiencyPercentage <= threeStarEfficiencyPercentage) return 3;
        if (efficiencyPercentage <= twoStarEfficiencyPercentage) return 2;
        if (efficiencyPercentage <= oneStarEfficiencyPercentage) return 1;
        return 0;
    }
    
    /// <summary>
    /// Get star rating thresholds info for Wordle Style
    /// </summary>
    public string GetEfficiencyThresholdInfo()
    {
        if (IsScrabbleStyle) return "Scrabble Style uses score-based star rating";
        
        string limitType = UsesTimer ? "time" : "moves";
        float limitValue = UsesTimer ? timeLimit : maxMoves;
        
        float threeStarLimit = limitValue * (threeStarEfficiencyPercentage / 100f);
        float twoStarLimit = limitValue * (twoStarEfficiencyPercentage / 100f);
        float oneStarLimit = limitValue * (oneStarEfficiencyPercentage / 100f);
        
        return $"Efficiency Thresholds: 3⭐≤{threeStarLimit:F1} {limitType} ({threeStarEfficiencyPercentage}%), " +
               $"2⭐≤{twoStarLimit:F1} {limitType} ({twoStarEfficiencyPercentage}%), " +
               $"1⭐≤{oneStarLimit:F1} {limitType} ({oneStarEfficiencyPercentage}%)";
    }
    
    /// <summary>
    /// Calculate star rating based on percentage of target score (Scrabble Style only)
    /// </summary>
    public int GetStarRatingByPercentage(float percentage)
    {
        if (IsWordleStyle)
        {
            Debug.LogWarning("GetStarRatingByPercentage called on Wordle Style level. Use GetStarRatingByEfficiency instead.");
            return 0;
        }
        
        if (percentage >= threeStarPercentage) return 3;
        if (percentage >= twoStarPercentage) return 2;
        if (percentage >= oneStarPercentage) return 1;
        return 0;
    }
    
    /// <summary>
    /// Get the percentage of target score achieved (Scrabble Style only)
    /// </summary>
    public float GetScorePercentage(int achievedScore)
    {
        if (IsWordleStyle)
        {
            Debug.LogWarning("GetScorePercentage called on Wordle Style level.");
            return 0f;
        }
        
        if (targetScore <= 0) return 0f;
        return (float)achievedScore / targetScore * 100f;
    }
    
    /// <summary>
    /// Check if a word is a target word for this level (Wordle Style only)
    /// </summary>
    public bool IsTargetWord(string word)
    {
        if (IsScrabbleStyle || targetWords == null) return false;
        
        foreach (string targetWord in targetWords)
        {
            if (string.Equals(word, targetWord, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
    
    /// <summary>
    /// Get all letters from target words (for grid population) - includes duplicates
    /// This ensures ALL letters needed to form the target words are available in the grid
    /// </summary>
    public char[] GetTargetWordLetters()
    {
        if (IsScrabbleStyle || targetWords == null) return new char[0];
        
        var allLetters = new System.Collections.Generic.List<char>();
        foreach (string word in targetWords)
        {
            foreach (char letter in word.ToUpper())
            {
                allLetters.Add(letter);
            }
        }
        
        return allLetters.ToArray();
    }
    
    /// <summary>
    /// Get unique letters from target words (for debugging/display purposes)
    /// </summary>
    public char[] GetUniqueTargetWordLetters()
    {
        if (IsScrabbleStyle || targetWords == null) return new char[0];
        
        var letters = new System.Collections.Generic.HashSet<char>();
        foreach (string word in targetWords)
        {
            foreach (char letter in word.ToUpper())
            {
                letters.Add(letter);
            }
        }
        
        var result = new char[letters.Count];
        letters.CopyTo(result);
        return result;
    }
    
    /// <summary>
    /// Validate target words for Wordle Style levels
    /// </summary>
    public bool ValidateTargetWords()
    {
        if (IsScrabbleStyle) return true;
        
        if (targetWords == null || targetWords.Length == 0)
        {
            Debug.LogWarning($"Level {levelNumber}: Wordle Style level has no target words defined!");
            return false;
        }
        
        // Check for duplicates and invalid words
        var wordSet = new System.Collections.Generic.HashSet<string>();
        foreach (string word in targetWords)
        {
            if (string.IsNullOrEmpty(word))
            {
                Debug.LogWarning($"Level {levelNumber}: Empty target word found!");
                return false;
            }
            
            string upperWord = word.ToUpper();
            if (wordSet.Contains(upperWord))
            {
                Debug.LogWarning($"Level {levelNumber}: Duplicate target word '{word}' found!");
                return false;
            }
            wordSet.Add(upperWord);
            
            // Check word length fits in grid
            if (word.Length > gridSize)
            {
                Debug.LogWarning($"Level {levelNumber}: Target word '{word}' is too long for {gridSize}x{gridSize} grid!");
                return false;
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// Check if the level is completed based on moves used
    /// Level completes when all moves are exhausted (or unlimited moves)
    /// For Wordle Style: Also check if all target words are found
    /// </summary>
    public bool IsLevelCompletedByMoves(int currentMoves)
    {
        if (unlimitedMoves) return false; // Never auto-complete with unlimited moves
        return currentMoves >= maxMoves;
    }
    
    /// <summary>
    /// Check if level is completed based on time elapsed (Wordle Style with timer)
    /// </summary>
    public bool IsLevelCompletedByTime(float timeElapsed)
    {
        if (!UsesTimer) return false;
        return timeElapsed >= timeLimit;
    }
    
    /// <summary>
    /// Check if all target words have been found (Wordle Style win condition)
    /// </summary>
    public bool AreAllTargetWordsFound(System.Collections.Generic.HashSet<string> foundWords)
    {
        if (IsScrabbleStyle || targetWords == null) return false;
        
        foreach (string targetWord in targetWords)
        {
            bool found = false;
            foreach (string foundWord in foundWords)
            {
                if (string.Equals(targetWord, foundWord, System.StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }
            if (!found) return false;
        }
        return true;
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
    /// Get remaining time
    /// </summary>
    public float GetRemainingTime(float timeElapsed)
    {
        if (!UsesTimer) return -1f; // -1 indicates not using timer
        return Mathf.Max(0f, timeLimit - timeElapsed);
    }
    
    /// <summary>
    /// Validate star rating percentages (for editor use)
    /// </summary>
    public bool ValidateStarPercentages()
    {
        if (IsScrabbleStyle)
        {
            return oneStarPercentage <= twoStarPercentage && 
                   twoStarPercentage <= threeStarPercentage &&
                   oneStarPercentage >= 0f && 
                   threeStarPercentage <= 100f;
        }
        else // Wordle Style
        {
            return threeStarEfficiencyPercentage <= twoStarEfficiencyPercentage && 
                   twoStarEfficiencyPercentage <= oneStarEfficiencyPercentage &&
                   threeStarEfficiencyPercentage >= 1f && 
                   oneStarEfficiencyPercentage <= 100f;
        }
    }
    
    /// <summary>
    /// Get debug info about star thresholds
    /// </summary>
    public string GetStarThresholdInfo()
    {
        if (IsScrabbleStyle)
        {
            return $"Star Thresholds: 1⭐{OneStarScore} ({oneStarPercentage}%), " +
                   $"2⭐{TwoStarScore} ({twoStarPercentage}%), " +
                   $"3⭐{ThreeStarScore} ({threeStarPercentage}%) of {targetScore}";
        }
        else
        {
            return GetEfficiencyThresholdInfo();
        }
    }
    
#if UNITY_EDITOR
    /// <summary>
    /// Update calculated scores when percentages change in editor
    /// </summary>
    private void OnValidate()
    {
        // Validate grid size
        gridSize = Mathf.Clamp(gridSize, 3, 10);
        
        if (IsScrabbleStyle)
        {
            // Ensure Scrabble Style percentage order is correct
            if (oneStarPercentage > twoStarPercentage)
                twoStarPercentage = oneStarPercentage;
            if (twoStarPercentage > threeStarPercentage)
                threeStarPercentage = twoStarPercentage;
                
            // Clamp Scrabble Style percentages to valid range
            oneStarPercentage = Mathf.Clamp(oneStarPercentage, 0f, 100f);
            twoStarPercentage = Mathf.Clamp(twoStarPercentage, 0f, 100f);
            threeStarPercentage = Mathf.Clamp(threeStarPercentage, 0f, 100f);
        }
        else if (IsWordleStyle)
        {
            // Ensure Wordle Style efficiency order is correct (lower is better)
            if (threeStarEfficiencyPercentage > twoStarEfficiencyPercentage)
                twoStarEfficiencyPercentage = threeStarEfficiencyPercentage;
            if (twoStarEfficiencyPercentage > oneStarEfficiencyPercentage)
                oneStarEfficiencyPercentage = twoStarEfficiencyPercentage;
                
            // Clamp Wordle Style efficiency percentages
            threeStarEfficiencyPercentage = Mathf.Clamp(threeStarEfficiencyPercentage, 1f, 50f);
            twoStarEfficiencyPercentage = Mathf.Clamp(twoStarEfficiencyPercentage, threeStarEfficiencyPercentage, 80f);
            oneStarEfficiencyPercentage = Mathf.Clamp(oneStarEfficiencyPercentage, twoStarEfficiencyPercentage, 100f);
            
            // Validate time limit
            timeLimit = Mathf.Max(10f, timeLimit); // Minimum 10 seconds
            
            // Validate target words
            ValidateTargetWords();
        }
        
        // Validate moves
        maxMoves = Mathf.Max(1, maxMoves);
    }
#endif
}
