using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Handles scoring calculations for the word placement game.
/// Implements Scrabble-style scoring with bonuses and multipliers.
/// </summary>
public class WordPlacementScorer : MonoBehaviour
{
    [Header("Base Scoring")]
    [SerializeField] private int baseWordBonus = 10;
    [SerializeField] private float difficultyMultiplier = 1.5f;
    [SerializeField] private float lengthBonus = 2f;

    [Header("Intersection Bonuses")]
    [SerializeField] private int singleIntersectionBonus = 5;
    [SerializeField] private int multipleIntersectionBonus = 10;
    [SerializeField] private float intersectionMultiplier = 1.2f;

    [Header("Special Bonuses")]
    [SerializeField] private int firstWordBonus = 25;
    [SerializeField] private int centerCellBonus = 15;
    [SerializeField] private int longWordBonus = 20; // For words 7+ letters
    [SerializeField] private int perfectPlacementBonus = 50; // For using all available intersections

    [Header("Combo System")]
    [SerializeField] private bool enableComboSystem = true;
    [SerializeField] private float comboMultiplier = 1.1f;
    [SerializeField] private int maxComboLevel = 5;

    [Header("Time Bonuses")]
    [SerializeField] private bool enableTimeBonuses = true;
    [SerializeField] private float quickPlacementThreshold = 10f; // seconds
    [SerializeField] private int quickPlacementBonus = 15;

    // Scoring state
    private int currentComboLevel = 0;
    private float lastPlacementTime = 0f;
    private PlacementValidator placementValidator;
    private DynamicGridManager gridManager;

    // Score tracking
    private Dictionary<string, int> wordScores = new Dictionary<string, int>();
    private List<ScoringEvent> recentScoringEvents = new List<ScoringEvent>();

    // Events
    public System.Action<WordScoreResult> OnWordScored;
    public System.Action<int> OnComboLevelChanged;
    public System.Action<ScoringEvent> OnScoringEvent;

    #region Initialization

    void Awake()
    {
        placementValidator = FindObjectOfType<PlacementValidator>();
        gridManager = FindObjectOfType<DynamicGridManager>();
        
        if (placementValidator == null)
        {
            Debug.LogWarning("WordPlacementScorer: No PlacementValidator found!");
        }
        
        if (gridManager == null)
        {
            Debug.LogWarning("WordPlacementScorer: No DynamicGridManager found!");
        }
    }

    void Start()
    {
        ResetScoring();
    }

    #endregion

    #region Main Scoring

    /// <summary>
    /// Calculate the score for a placed word tile
    /// </summary>
    public int CalculateWordScore(WordTile wordTile)
    {
        if (wordTile == null) return 0;

        WordScoreResult result = new WordScoreResult();
        result.word = wordTile.Word;
        result.baseTileScore = wordTile.TotalScore;

        // Start with base tile score
        int totalScore = result.baseTileScore;

        // Add base word bonus
        totalScore += baseWordBonus;
        result.baseWordBonus = baseWordBonus;

        // Apply difficulty multiplier
        float difficultyBonus = (wordTile.Difficulty - 1) * difficultyMultiplier;
        totalScore = Mathf.RoundToInt(totalScore * (1f + difficultyBonus));
        result.difficultyBonus = Mathf.RoundToInt(result.baseTileScore * difficultyBonus);

        // Length bonus
        int lengthBonusValue = Mathf.RoundToInt(wordTile.Word.Length * lengthBonus);
        totalScore += lengthBonusValue;
        result.lengthBonus = lengthBonusValue;

        // Special bonuses
        totalScore += CalculateSpecialBonuses(wordTile, result);

        // Intersection bonuses
        totalScore += CalculateIntersectionBonuses(wordTile, result);

        // Combo multiplier
        if (enableComboSystem && currentComboLevel > 0)
        {
            float comboBonus = Mathf.Pow(comboMultiplier, currentComboLevel);
            int comboBonusValue = Mathf.RoundToInt(totalScore * (comboBonus - 1f));
            totalScore += comboBonusValue;
            result.comboBonus = comboBonusValue;
            result.comboLevel = currentComboLevel;
        }

        // Time bonus
        if (enableTimeBonuses)
        {
            int timeBonusValue = CalculateTimeBonus();
            totalScore += timeBonusValue;
            result.timeBonus = timeBonusValue;
        }

        result.finalScore = totalScore;

        // Record the score
        wordScores[wordTile.Word] = totalScore;
        
        // Update combo
        UpdateComboLevel(result);

        // Create scoring event
        ScoringEvent scoringEvent = new ScoringEvent
        {
            word = wordTile.Word,
            score = totalScore,
            timestamp = Time.time,
            breakdown = result
        };
        recentScoringEvents.Add(scoringEvent);

        // Trigger events
        OnWordScored?.Invoke(result);
        OnScoringEvent?.Invoke(scoringEvent);

        Debug.Log($"Word '{wordTile.Word}' scored {totalScore} points. Base: {result.baseTileScore}, Final: {totalScore}");

        return totalScore;
    }

    #endregion

    #region Bonus Calculations

    private int CalculateSpecialBonuses(WordTile wordTile, WordScoreResult result)
    {
        int bonuses = 0;

        // First word bonus
        if (placementValidator != null && !placementValidator.IsFirstWordPlaced)
        {
            bonuses += firstWordBonus;
            result.firstWordBonus = firstWordBonus;
        }

        // Center cell bonus (if word passes through center)
        if (PassesThroughCenter(wordTile))
        {
            bonuses += centerCellBonus;
            result.centerCellBonus = centerCellBonus;
        }

        // Long word bonus
        if (wordTile.Word.Length >= 7)
        {
            bonuses += longWordBonus;
            result.longWordBonus = longWordBonus;
        }

        return bonuses;
    }

    private int CalculateIntersectionBonuses(WordTile wordTile, WordScoreResult result)
    {
        // This would need to be implemented with actual placement data
        // For now, return a base intersection bonus
        int intersectionCount = GetIntersectionCount(wordTile);
        
        if (intersectionCount == 0)
        {
            return 0;
        }

        int bonus = 0;
        
        if (intersectionCount == 1)
        {
            bonus = singleIntersectionBonus;
        }
        else
        {
            bonus = intersectionCount * multipleIntersectionBonus;
            bonus = Mathf.RoundToInt(bonus * intersectionMultiplier);
        }

        result.intersectionBonus = bonus;
        result.intersectionCount = intersectionCount;

        return bonus;
    }

    private int CalculateTimeBonus()
    {
        float timeSinceLastPlacement = Time.time - lastPlacementTime;
        
        if (timeSinceLastPlacement <= quickPlacementThreshold)
        {
            return quickPlacementBonus;
        }
        
        return 0;
    }

    #endregion

    #region Combo System

    private void UpdateComboLevel(WordScoreResult result)
    {
        if (!enableComboSystem) return;

        // Increase combo for good placements
        bool shouldIncreaseCombo = false;

        // Conditions that increase combo
        if (result.intersectionCount > 0) shouldIncreaseCombo = true;
        if (result.timeBonus > 0) shouldIncreaseCombo = true;
        if (result.word.Length >= 6) shouldIncreaseCombo = true;

        if (shouldIncreaseCombo)
        {
            currentComboLevel = Mathf.Min(currentComboLevel + 1, maxComboLevel);
        }
        else
        {
            // Reset combo for simple placements
            currentComboLevel = 0;
        }

        OnComboLevelChanged?.Invoke(currentComboLevel);
        lastPlacementTime = Time.time;
    }

    public void ResetCombo()
    {
        currentComboLevel = 0;
        OnComboLevelChanged?.Invoke(currentComboLevel);
    }

    #endregion

    #region Helper Methods

    private bool PassesThroughCenter(WordTile wordTile)
    {
        // This would need actual placement position data
        // For now, assume first word passes through center
        if (placementValidator != null && !placementValidator.IsFirstWordPlaced)
        {
            return true;
        }
        return false;
    }

    private int GetIntersectionCount(WordTile wordTile)
    {
        // This would need to check actual grid intersections
        // For now, return a simulated value based on game state
        if (placementValidator != null && placementValidator.PlacedWordCount > 0)
        {
            // Simulate intersection count based on word length and existing words
            return Random.Range(1, Mathf.Min(wordTile.Word.Length, 3));
        }
        return 0;
    }

    #endregion

    #region Score Analysis

    /// <summary>
    /// Get the total score from all placed words
    /// </summary>
    public int GetTotalScore()
    {
        return wordScores.Values.Sum();
    }

    /// <summary>
    /// Get the highest scoring word
    /// </summary>
    public KeyValuePair<string, int> GetHighestScoringWord()
    {
        if (wordScores.Count == 0)
        {
            return new KeyValuePair<string, int>("", 0);
        }

        return wordScores.OrderByDescending(kv => kv.Value).First();
    }

    /// <summary>
    /// Get the average score per word
    /// </summary>
    public float GetAverageScore()
    {
        if (wordScores.Count == 0) return 0f;
        return (float)wordScores.Values.Sum() / wordScores.Count;
    }

    /// <summary>
    /// Get scoring statistics
    /// </summary>
    public ScoringStats GetScoringStats()
    {
        ScoringStats stats = new ScoringStats();
        
        if (wordScores.Count == 0) return stats;

        stats.totalWords = wordScores.Count;
        stats.totalScore = wordScores.Values.Sum();
        stats.averageScore = stats.totalScore / (float)stats.totalWords;
        stats.highestScore = wordScores.Values.Max();
        stats.lowestScore = wordScores.Values.Min();
        stats.currentComboLevel = currentComboLevel;
        stats.maxComboAchieved = recentScoringEvents.Max(e => e.breakdown.comboLevel);

        return stats;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Reset all scoring data
    /// </summary>
    public void ResetScoring()
    {
        wordScores.Clear();
        recentScoringEvents.Clear();
        currentComboLevel = 0;
        lastPlacementTime = 0f;
    }

    /// <summary>
    /// Remove a word's score (for undo functionality)
    /// </summary>
    public void RemoveWordScore(string word)
    {
        if (wordScores.ContainsKey(word))
        {
            wordScores.Remove(word);
            
            // Remove from recent events
            recentScoringEvents.RemoveAll(e => e.word == word);
            
            // Reset combo
            ResetCombo();
        }
    }

    /// <summary>
    /// Get recent scoring events for UI feedback
    /// </summary>
    public List<ScoringEvent> GetRecentScoringEvents(int count = 5)
    {
        return recentScoringEvents.TakeLast(count).ToList();
    }

    #endregion

    #region Getters

    public int CurrentComboLevel => currentComboLevel;
    public Dictionary<string, int> WordScores => new Dictionary<string, int>(wordScores);

    #endregion
}

/// <summary>
/// Detailed result of word scoring
/// </summary>
[System.Serializable]
public class WordScoreResult
{
    public string word;
    public int baseTileScore;
    public int baseWordBonus;
    public int difficultyBonus;
    public int lengthBonus;
    public int intersectionBonus;
    public int firstWordBonus;
    public int centerCellBonus;
    public int longWordBonus;
    public int perfectPlacementBonus;
    public int comboBonus;
    public int timeBonus;
    public int finalScore;
    
    public int intersectionCount;
    public int comboLevel;
}

/// <summary>
/// Scoring event for tracking and feedback
/// </summary>
[System.Serializable]
public class ScoringEvent
{
    public string word;
    public int score;
    public float timestamp;
    public WordScoreResult breakdown;
}

/// <summary>
/// Overall scoring statistics
/// </summary>
[System.Serializable]
public class ScoringStats
{
    public int totalWords;
    public int totalScore;
    public float averageScore;
    public int highestScore;
    public int lowestScore;
    public int currentComboLevel;
    public int maxComboAchieved;
}
