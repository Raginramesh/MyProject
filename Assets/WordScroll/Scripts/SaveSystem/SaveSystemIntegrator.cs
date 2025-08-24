using UnityEngine;
using System.Collections.Generic;
using MoreMountains.Tools;

namespace WordScroll.SaveSystem
{
    /// <summary>
    /// Component to help integrate the save system with existing game systems
    /// Acts as a bridge between the game and the save manager
    /// </summary>
    public class SaveSystemIntegrator : MonoBehaviour
    {
        [Header("Save System Integration")]
        [SerializeField] private bool enableSaveSystem = true;
        [SerializeField] private bool enableAchievements = true;
        [SerializeField] private bool enableDebugLogs = true;
        
        [Header("Level System Integration")]
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private GameManager gameManager;
        
        private WordScrollSaveManager saveManager;
        private float sessionStartTime;
        private Dictionary<string, int> sessionStats = new Dictionary<string, int>();
        
        private void Start()
        {
            // Initialize session tracking
            sessionStartTime = Time.time;
            InitializeSessionStats();
            
            // Get references
            if (levelManager == null)
                levelManager = LevelManager.Instance;
                
            if (gameManager == null)
                gameManager = GameManager.instance;
                
            saveManager = WordScrollSaveManager.Instance;
            
            // Subscribe to events
            SubscribeToEvents();
            
            if (enableDebugLogs)
            {
                Debug.Log("[SaveSystemIntegrator] Initialized successfully");
            }
        }
        
        private void InitializeSessionStats()
        {
            sessionStats["wordsFound"] = 0;
            sessionStats["levelsCompleted"] = 0;
            sessionStats["totalScore"] = 0;
            sessionStats["movesUsed"] = 0;
        }
        
        private void SubscribeToEvents()
        {
            if (levelManager != null)
            {
                LevelManager.OnLevelCompleted += OnLevelCompleted;
                LevelManager.OnLevelStarted += OnLevelStarted;
            }
            
            // Subscribe to save system events
            if (saveManager != null)
            {
                WordScrollSaveManager.OnSaveDataLoaded += OnSaveDataLoaded;
                WordScrollSaveManager.OnSaveDataSaved += OnSaveDataSaved;
                WordScrollSaveManager.OnSaveError += OnSaveError;
            }
        }
        
        private void OnDestroy()
        {
            // Unsubscribe from events
            if (levelManager != null)
            {
                LevelManager.OnLevelCompleted -= OnLevelCompleted;
                LevelManager.OnLevelStarted -= OnLevelStarted;
            }
            
            if (saveManager != null)
            {
                WordScrollSaveManager.OnSaveDataLoaded -= OnSaveDataLoaded;
                WordScrollSaveManager.OnSaveDataSaved -= OnSaveDataSaved;
                WordScrollSaveManager.OnSaveError -= OnSaveError;
            }
        }
        
        #region EVENT_HANDLERS
        
        private void OnLevelStarted(LevelData level)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[SaveSystemIntegrator] Level started: {level.LevelName}");
            }
        }
        
        private void OnLevelCompleted(LevelData level, int finalScore, int stars)
        {
            if (!enableSaveSystem) return;
            
            // Update session stats
            sessionStats["levelsCompleted"]++;
            sessionStats["totalScore"] += finalScore;
            
            if (levelManager != null)
            {
                sessionStats["movesUsed"] += levelManager.CurrentMoves;
            }
            
            // Trigger achievements if enabled
            if (enableAchievements)
            {
                CheckAndTriggerAchievements(level, finalScore, stars);
            }
            
            if (enableDebugLogs)
            {
                Debug.Log($"[SaveSystemIntegrator] Level completed: {level.LevelName}, Score: {finalScore}, Stars: {stars}");
            }
        }
        
        private void OnSaveDataLoaded(WordScrollSaveData saveData)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[SaveSystemIntegrator] Save data loaded - Level: {saveData.currentLevelIndex}, Stars: {saveData.totalStarsEarned}");
            }
        }
        
        private void OnSaveDataSaved(WordScrollSaveData saveData)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[SaveSystemIntegrator] Save data saved successfully");
            }
        }
        
        private void OnSaveError(string error)
        {
            Debug.LogError($"[SaveSystemIntegrator] Save error: {error}");
        }
        
        #endregion
        
        #region ACHIEVEMENT_INTEGRATION
        
        private void CheckAndTriggerAchievements(LevelData level, int finalScore, int stars)
        {
            if (!enableAchievements || saveManager?.CurrentSaveData == null) return;
            
            var saveData = saveManager.CurrentSaveData;
            
            // First level completion
            if (level.LevelNumber == 1 && stars > 0)
            {
                MMAchievementManager.UnlockAchievement("first_steps");
            }
            
            // Progress-based achievements
            MMAchievementManager.SetProgress("getting_started", saveData.totalLevelsCompleted);
            MMAchievementManager.SetProgress("word_explorer", saveData.totalLevelsCompleted);
            MMAchievementManager.SetProgress("completionist", saveData.totalLevelsCompleted);
            
            // Perfect score achievements
            if (stars == 3)
            {
                MMAchievementManager.UnlockAchievement("perfect_score");
                MMAchievementManager.AddProgress("perfect_levels", 1);
            }
            
            // Speed achievement for timed levels
            if (level.UsesTimer && levelManager != null)
            {
                var levelStartTimeField = levelManager.GetType().GetField("levelStartTime", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (levelStartTimeField != null)
                {
                    var levelStartTimeValue = levelStartTimeField.GetValue(levelManager);
                    if (levelStartTimeValue is float levelStartTime)
                    {
                        float levelTime = Time.time - levelStartTime;
                        
                        if (levelTime < 120f && levelTime > 0f) // Under 2 minutes
                        {
                            MMAchievementManager.UnlockAchievement("speed_demon");
                        }
                    }
                }
            }
            
            // Efficiency achievement
            if (level.UsesMoves && levelManager != null)
            {
                float efficiency = (float)levelManager.CurrentMoves / level.MaxMoves;
                if (efficiency < 0.5f) // Used less than 50% of moves
                {
                    MMAchievementManager.UnlockAchievement("efficiency_expert");
                }
            }
            
            // Word discovery achievements
            MMAchievementManager.SetProgress("word_finder", saveData.statistics.totalWordsFound);
            MMAchievementManager.SetProgress("word_master", saveData.statistics.totalWordsFound);
            MMAchievementManager.SetProgress("vocabulary_expert", saveData.statistics.totalWordsFound);
            
            // Game mode specific achievements
            if (level.IsScrabbleStyle)
            {
                MMAchievementManager.SetProgress("scrabble_master", saveData.statistics.scrabbleStyleLevelsCompleted);
            }
            else if (level.IsWordleStyle)
            {
                MMAchievementManager.SetProgress("wordle_wizard", saveData.statistics.wordleStyleLevelsCompleted);
            }
            
            // Session-based achievements
            CheckSessionAchievements();
        }
        
        private void CheckSessionAchievements()
        {
            float sessionTime = Time.time - sessionStartTime;
            
            // Marathon session (1 hour)
            if (sessionTime > 3600f)
            {
                MMAchievementManager.UnlockAchievement("marathon_session");
            }
        }
        
        #endregion
        
        #region PUBLIC_API
        
        /// <summary>
        /// Manually trigger a save
        /// </summary>
        public void TriggerSave()
        {
            if (saveManager != null)
            {
                saveManager.ForceSave();
            }
        }
        
        /// <summary>
        /// Get current session statistics
        /// </summary>
        public Dictionary<string, int> GetSessionStats()
        {
            return new Dictionary<string, int>(sessionStats);
        }
        
        /// <summary>
        /// Add to session word count (call this when words are found)
        /// </summary>
        public void AddSessionWordFound(int count = 1)
        {
            sessionStats["wordsFound"] += count;
            
            // Update achievements
            if (enableAchievements && saveManager?.CurrentSaveData != null)
            {
                var totalWords = saveManager.CurrentSaveData.statistics.totalWordsFound + sessionStats["wordsFound"];
                MMAchievementManager.SetProgress("word_finder", totalWords);
                MMAchievementManager.SetProgress("word_master", totalWords);
                MMAchievementManager.SetProgress("vocabulary_expert", totalWords);
            }
        }
        
        /// <summary>
        /// Check for long word achievement
        /// </summary>
        public void CheckLongWordAchievement(string word)
        {
            if (enableAchievements && word.Length >= 8)
            {
                MMAchievementManager.UnlockAchievement("long_word_specialist");
            }
        }
        
        /// <summary>
        /// Reset save data (for testing)
        /// </summary>
        public void ResetSaveData()
        {
            if (saveManager != null)
            {
                saveManager.ResetSaveData();
            }
        }
        
        /// <summary>
        /// Get save file info
        /// </summary>
        public void PrintSaveInfo()
        {
            if (saveManager != null)
            {
                var size = saveManager.GetSaveFileSize();
                Debug.Log($"[SaveSystemIntegrator] Save file size: {size} bytes");
                
                if (saveManager.HasSaveData)
                {
                    var data = saveManager.CurrentSaveData;
                    Debug.Log($"[SaveSystemIntegrator] Current progress:");
                    Debug.Log($"  ↳ Level: {data.currentLevelIndex}");
                    Debug.Log($"  ↳ Total Stars: {data.totalStarsEarned}");
                    Debug.Log($"  ↳ Total Levels: {data.totalLevelsCompleted}");
                    Debug.Log($"  ↳ Play Time: {data.totalPlayTime:F1}s");
                }
            }
        }
        
        #endregion
        
        #region DEBUG_FUNCTIONS
        
        [ContextMenu("Print Save Info")]
        private void DebugPrintSaveInfo()
        {
            PrintSaveInfo();
        }
        
        [ContextMenu("Force Save")]
        private void DebugForceSave()
        {
            TriggerSave();
        }
        
        [ContextMenu("Reset Save Data")]
        private void DebugResetSaveData()
        {
            if (Application.isEditor)
            {
                ResetSaveData();
            }
        }
        
        #endregion
    }
}
