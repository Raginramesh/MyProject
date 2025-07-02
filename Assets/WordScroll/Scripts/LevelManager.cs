using UnityEngine;
using System.Collections.Generic;
using System;

public class LevelManager : MonoBehaviour
{
    [Header("Level Configuration")]
    [SerializeField] private List<LevelData> allLevels = new List<LevelData>();
    [SerializeField] private LevelData currentLevel;
    [SerializeField] private bool debugMode = false;
    
    [Header("Level Progress")]
    [SerializeField] private int currentLevelIndex = 0;
    [SerializeField] private int currentMoves = 0;
    [SerializeField] private int currentScore = 0;
    
    // Events for UI updates
    public static event Action<LevelData> OnLevelStarted;
    public static event Action<LevelData, int, int> OnLevelCompleted; // level, score, stars
    public static event Action<LevelData> OnLevelFailed;
    public static event Action<int> OnMovesChanged;
    public static event Action<int> OnScoreChanged;
    public static event Action<int> OnMovesRemaining;
    
    // Singleton pattern
    public static LevelManager Instance { get; private set; }
    
    // Public properties
    public LevelData CurrentLevel => currentLevel;
    public int CurrentMoves => currentMoves;
    public int CurrentScore => currentScore;
    public bool IsLevelActive { get; private set; } = false;
    
    void Awake()
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeLevels();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        // Load level progress from save data
        LoadLevelProgress();
    }
    
    /// <summary>
    /// Initialize levels and unlock the first one
    /// </summary>
    private void InitializeLevels()
    {
        if (allLevels.Count > 0)
        {
            // Always unlock the first level
            allLevels[0].UnlockLevel();
            
            if (debugMode)
            {
                // In debug mode, unlock all levels
                foreach (var level in allLevels)
                {
                    level.UnlockLevel();
                }
            }
        }
    }
    
    /// <summary>
    /// Start a specific level
    /// </summary>
    public bool StartLevel(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= allLevels.Count)
        {
            Debug.LogError($"LevelManager: Invalid level index {levelIndex}");
            return false;
        }
        
        var level = allLevels[levelIndex];
        if (!level.IsUnlocked && !debugMode)
        {
            Debug.LogWarning($"LevelManager: Level {levelIndex} is not unlocked");
            return false;
        }
        
        return StartLevel(level);
    }
    
    /// <summary>
    /// Start a specific level
    /// </summary>
    public bool StartLevel(LevelData level)
    {
        if (level == null)
        {
            Debug.LogError("LevelManager: Cannot start null level");
            return false;
        }
        
        currentLevel = level;
        currentLevelIndex = allLevels.IndexOf(level);
        currentMoves = 0;
        currentScore = 0;
        IsLevelActive = true;
        
        Debug.Log($"🎮 Starting Level {level.LevelNumber}: {level.LevelName}");
        Debug.Log($"📊 Target Score: {level.TargetScore}, Max Moves: {(level.UnlimitedMoves ? "Unlimited" : level.MaxMoves.ToString())}");
        
        // Notify listeners
        OnLevelStarted?.Invoke(level);
        OnMovesChanged?.Invoke(currentMoves);
        OnScoreChanged?.Invoke(currentScore);
        OnMovesRemaining?.Invoke(level.GetRemainingMoves(currentMoves));
        
        return true;
    }
    
    /// <summary>
    /// Add a move to the current level
    /// </summary>
    public void AddMove()
    {
        if (!IsLevelActive || currentLevel == null) return;
        
        currentMoves++;
        OnMovesChanged?.Invoke(currentMoves);
        OnMovesRemaining?.Invoke(currentLevel.GetRemainingMoves(currentMoves));
        
        Debug.Log($"🎯 Move {currentMoves}/{(currentLevel.UnlimitedMoves ? "∞" : currentLevel.MaxMoves.ToString())}");
        
        // Check if moves are exhausted
        if (!currentLevel.HasMovesRemaining(currentMoves))
        {
            CheckLevelCompletion();
        }
    }
    
    /// <summary>
    /// Add score to the current level
    /// </summary>
    public void AddScore(int points)
    {
        if (!IsLevelActive || currentLevel == null) return;
        
        // Apply level score multiplier
        int adjustedPoints = Mathf.RoundToInt(points * currentLevel.ScoreMultiplier);
        currentScore += adjustedPoints;
        
        OnScoreChanged?.Invoke(currentScore);
        
        Debug.Log($"📈 Score: {currentScore} (+{adjustedPoints}) Target: {currentLevel.TargetScore}");
        
        // Level completion is based on moves, not score
        // Score only affects star rating
    }
    
    /// <summary>
    /// Check if the level should end (when moves are exhausted)
    /// </summary>
    private void CheckLevelCompletion()
    {
        if (!IsLevelActive || currentLevel == null) return;
        
        bool hasMovesLeft = currentLevel.HasMovesRemaining(currentMoves);
        bool levelCompleted = currentLevel.IsLevelCompletedByMoves(currentMoves);
        
        if (levelCompleted || !hasMovesLeft)
        {
            // Level completed - all moves used
            CompleteLevel();
        }
        // Otherwise, continue playing
    }
    
    /// <summary>
    /// Complete the current level
    /// </summary>
    private void CompleteLevel()
    {
        if (!IsLevelActive || currentLevel == null) return;
        
        IsLevelActive = false;
        int stars = currentLevel.GetStarRating(currentScore);
        
        Debug.Log($"🌟 Level Completed! Score: {currentScore}, Stars: {stars}");
        
        // Unlock next level
        if (currentLevel.NextLevel != null)
        {
            currentLevel.NextLevel.UnlockLevel();
            Debug.Log($"🔓 Unlocked next level: {currentLevel.NextLevel.LevelName}");
        }
        
        // Save progress
        SaveLevelProgress();
        
        // Notify listeners
        OnLevelCompleted?.Invoke(currentLevel, currentScore, stars);
    }
    
    /// <summary>
    /// Fail the current level
    /// </summary>
    private void FailLevel()
    {
        if (!IsLevelActive || currentLevel == null) return;
        
        IsLevelActive = false;
        
        Debug.Log($"❌ Level Failed! Score: {currentScore}/{currentLevel.TargetScore}, Moves: {currentMoves}/{currentLevel.MaxMoves}");
        
        // Notify listeners
        OnLevelFailed?.Invoke(currentLevel);
    }
    
    /// <summary>
    /// Start the next level in sequence
    /// </summary>
    public bool StartNextLevel()
    {
        if (currentLevel?.NextLevel != null)
        {
            return StartLevel(currentLevel.NextLevel);
        }
        
        // Try next level in list
        if (currentLevelIndex + 1 < allLevels.Count)
        {
            return StartLevel(currentLevelIndex + 1);
        }
        
        Debug.Log("🏆 All levels completed!");
        return false;
    }
    
    /// <summary>
    /// Save level progress (placeholder - implement with your save system)
    /// </summary>
    private void SaveLevelProgress()
    {
        // TODO: Implement save system
        PlayerPrefs.SetInt("CurrentLevelIndex", currentLevelIndex);
        PlayerPrefs.SetInt($"Level_{currentLevelIndex}_HighScore", currentScore);
        PlayerPrefs.Save();
    }
    
    /// <summary>
    /// Load level progress (placeholder - implement with your save system)
    /// </summary>
    private void LoadLevelProgress()
    {
        // TODO: Implement save system
        int savedLevelIndex = PlayerPrefs.GetInt("CurrentLevelIndex", 0);
        if (savedLevelIndex < allLevels.Count)
        {
            currentLevelIndex = savedLevelIndex;
        }
    }
    
    /// <summary>
    /// Get high score for a specific level
    /// </summary>
    public int GetLevelHighScore(int levelIndex)
    {
        return PlayerPrefs.GetInt($"Level_{levelIndex}_HighScore", 0);
    }
}
