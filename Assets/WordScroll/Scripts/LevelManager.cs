using UnityEngine;
using System.Collections.Generic;
using System;
using WordScroll.SaveSystem;

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
    
    // Level timing for save system
    private float levelStartTime = 0f;
    private List<string> foundTargetWordsThisLevel = new List<string>();
    
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
        
        // Initialize timing and target words tracking for save system
        levelStartTime = Time.time;
        foundTargetWordsThisLevel.Clear();
        
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
    /// Check if level should complete based on Wordle-style conditions (all target words found)
    /// </summary>
    public void CheckWordleCompletion()
    {
        if (!IsLevelActive || currentLevel == null || !currentLevel.IsWordleStyle) return;
        
        // For Wordle-style levels, complete when all target words are found
        CompleteLevel();
    }
    
    /// <summary>
    /// Force level completion (used for timer expiry or other forced completion conditions)
    /// </summary>
    public void ForceCompleteLevel()
    {
        if (!IsLevelActive || currentLevel == null) return;
        
        Debug.Log("🏁 Force completing level due to external condition (timer, etc.)");
        CompleteLevel();
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
        
        // Save progress using the new save system
        SaveLevelProgressWithSystem(stars);
        
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
    /// Save level progress using the new save system
    /// </summary>
    private void SaveLevelProgress()
    {
        // Use the new save system if available, otherwise fallback to PlayerPrefs
        var saveManagerType = System.Type.GetType("WordScroll.SaveSystem.WordScrollSaveManager");
        if (saveManagerType != null)
        {
            var instanceProperty = saveManagerType.GetProperty("Instance");
            var saveManagerInstance = instanceProperty?.GetValue(null);
            
            if (saveManagerInstance != null)
            {
                var markDirtyMethod = saveManagerType.GetMethod("MarkDirty");
                markDirtyMethod?.Invoke(saveManagerInstance, null);
                return;
            }
        }
        
        // Fallback to PlayerPrefs if save manager not available
        PlayerPrefs.SetInt("CurrentLevelIndex", currentLevelIndex);
        PlayerPrefs.SetInt($"Level_{currentLevelIndex}_HighScore", currentScore);
        PlayerPrefs.Save();
    }
    
    /// <summary>
    /// Load level progress using the new save system
    /// </summary>
    private void LoadLevelProgress()
    {
        // Use the new save system if available, otherwise fallback to PlayerPrefs
        var saveManagerType = System.Type.GetType("WordScroll.SaveSystem.WordScrollSaveManager");
        if (saveManagerType != null)
        {
            var instanceProperty = saveManagerType.GetProperty("Instance");
            var saveManagerInstance = instanceProperty?.GetValue(null);
            
            if (saveManagerInstance != null)
            {
                var hasDataProperty = saveManagerType.GetProperty("HasSaveData");
                var hasData = (bool)(hasDataProperty?.GetValue(saveManagerInstance) ?? false);
                
                if (hasData)
                {
                    var currentSaveDataProperty = saveManagerType.GetProperty("CurrentSaveData");
                    var saveData = currentSaveDataProperty?.GetValue(saveManagerInstance);
                    
                    if (saveData != null)
                    {
                        var currentLevelIndexField = saveData.GetType().GetField("currentLevelIndex");
                        if (currentLevelIndexField != null)
                        {
                            currentLevelIndex = (int)currentLevelIndexField.GetValue(saveData);
                            
                            // Ensure the level index is valid
                            if (currentLevelIndex >= allLevels.Count)
                            {
                                currentLevelIndex = allLevels.Count - 1;
                            }
                            return;
                        }
                    }
                }
            }
        }
        
        // Fallback to PlayerPrefs if save manager not available
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
        // Use the new save system if available, otherwise fallback to PlayerPrefs
        var saveManagerType = System.Type.GetType("WordScroll.SaveSystem.WordScrollSaveManager");
        if (saveManagerType != null)
        {
            var instanceProperty = saveManagerType.GetProperty("Instance");
            var saveManagerInstance = instanceProperty?.GetValue(null);
            
            if (saveManagerInstance != null)
            {
                var getLevelProgressMethod = saveManagerType.GetMethod("GetLevelProgress");
                var levelProgress = getLevelProgressMethod?.Invoke(saveManagerInstance, new object[] { levelIndex });
                
                if (levelProgress != null)
                {
                    var bestScoreField = levelProgress.GetType().GetField("bestScore");
                    if (bestScoreField != null)
                    {
                        return (int)bestScoreField.GetValue(levelProgress);
                    }
                }
            }
        }
        
        // Fallback to PlayerPrefs
        return PlayerPrefs.GetInt($"Level_{levelIndex}_HighScore", 0);
    }
    
    /// <summary>
    /// Get a specific level by index
    /// </summary>
    public LevelData GetLevel(int levelIndex)
    {
        if (levelIndex >= 0 && levelIndex < allLevels.Count)
        {
            return allLevels[levelIndex];
        }
        return null;
    }
    
    /// <summary>
    /// Get total number of levels
    /// </summary>
    public int GetTotalLevelCount()
    {
        return allLevels.Count;
    }
    
    /// <summary>
    /// Check if a level is unlocked using save system
    /// </summary>
    public bool IsLevelUnlocked(int levelIndex)
    {
        if (debugMode) return true; // All levels unlocked in debug mode
        
        // Use the new save system if available, otherwise fallback to PlayerPrefs
        var saveManagerType = System.Type.GetType("WordScroll.SaveSystem.WordScrollSaveManager");
        if (saveManagerType != null)
        {
            var instanceProperty = saveManagerType.GetProperty("Instance");
            var saveManagerInstance = instanceProperty?.GetValue(null);
            
            if (saveManagerInstance != null)
            {
                var isLevelUnlockedMethod = saveManagerType.GetMethod("IsLevelUnlocked");
                var result = isLevelUnlockedMethod?.Invoke(saveManagerInstance, new object[] { levelIndex });
                if (result != null)
                {
                    return (bool)result;
                }
            }
        }
        
        // Fallback: level is unlocked if previous level was completed or it's the first level
        return levelIndex == 0 || PlayerPrefs.GetInt($"Level_{levelIndex - 1}_Completed", 0) == 1;
    }
    
    /// <summary>
    /// Add a found target word for Wordle-style levels
    /// </summary>
    public void AddFoundTargetWord(string word)
    {
        if (currentLevel != null && currentLevel.IsWordleStyle)
        {
            if (!foundTargetWordsThisLevel.Contains(word))
            {
                foundTargetWordsThisLevel.Add(word);
                Debug.Log($"🎯 Target word found: {word}. Total found: {foundTargetWordsThisLevel.Count}/{currentLevel.TargetWordCount}");
            }
        }
    }
    
    /// <summary>
    /// Get the list of found target words for this level
    /// </summary>
    private List<string> GetFoundTargetWords()
    {
        return new List<string>(foundTargetWordsThisLevel);
    }
    
    /// <summary>
    /// Save level progress with comprehensive save system integration
    /// </summary>
    private void SaveLevelProgressWithSystem(int stars)
    {
        // Try to use the new save system first using reflection
        var saveManagerType = System.Type.GetType("WordScroll.SaveSystem.WordScrollSaveManager");
        if (saveManagerType != null)
        {
            var instanceProperty = saveManagerType.GetProperty("Instance");
            var saveManagerInstance = instanceProperty?.GetValue(null);
            
            if (saveManagerInstance != null)
            {
                // Calculate level completion time
                float levelTime = Time.time - levelStartTime;
                
                // Get found words for Wordle-style levels
                List<string> foundWords = null;
                if (currentLevel.IsWordleStyle)
                {
                    foundWords = GetFoundTargetWords();
                }
                
                // Update save data with comprehensive level completion info
                var updateMethod = saveManagerType.GetMethod("UpdateLevelProgress");
                if (updateMethod != null)
                {
                    updateMethod.Invoke(saveManagerInstance, new object[] 
                    {
                        currentLevelIndex, 
                        currentScore, 
                        currentMoves, 
                        levelTime, 
                        stars, 
                        currentLevel, 
                        foundWords
                    });
                    return;
                }
            }
        }
        
        // Fallback to old save method
        SaveLevelProgress();
    }
}
