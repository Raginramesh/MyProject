using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MoreMountains.Tools;

namespace WordScroll.SaveSystem
{
    /// <summary>
    /// Main save manager for WordScroll game
    /// Handles all save/load operations with JSON serialization
    /// Integrates with Feel framework's achievement system
    /// </summary>
    public class WordScrollSaveManager : MMPersistentSingleton<WordScrollSaveManager>
    {
        [Header("Save Configuration")]
        [SerializeField] private string saveFileName = "WordScrollSave";
        [SerializeField] private string saveFileExtension = ".json";
        [SerializeField] private string saveFolderName = "WordScroll";
        [SerializeField] private bool enableAutoSave = true;
        [SerializeField] private float autoSaveInterval = 30f; // Auto-save every 30 seconds
        
        [Header("Achievement Integration")]
        [SerializeField] private MMAchievementList achievementList;
        [SerializeField] private bool enableAchievements = true;
        
        [Header("Debug Settings")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool enableBackupSaves = true;
        [SerializeField] private int maxBackupFiles = 3;
        
        // Events
        public static event System.Action<WordScrollSaveData> OnSaveDataLoaded;
        public static event System.Action<WordScrollSaveData> OnSaveDataSaved;
        public static event System.Action<string> OnSaveError;
        
        // Private members
        private WordScrollSaveData currentSaveData;
        private Coroutine autoSaveCoroutine;
        private string fullSavePath;
        private bool isDirty = false; // Track if data needs saving
        
        // Public properties
        public WordScrollSaveData CurrentSaveData => currentSaveData;
        public bool HasSaveData => currentSaveData != null;
        public bool IsDirty => isDirty;
        
        #region INITIALIZATION
        
        protected override void Awake()
        {
            base.Awake();
            
            // Initialize save path
            string saveDirectory = Path.Combine(Application.persistentDataPath, saveFolderName);
            if (!Directory.Exists(saveDirectory))
            {
                Directory.CreateDirectory(saveDirectory);
            }
            
            fullSavePath = Path.Combine(saveDirectory, saveFileName + saveFileExtension);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[WordScrollSaveManager] Initialized with save path: {fullSavePath}");
            }
        }
        
        protected virtual void Start()
        {
            // Initialize achievement system if enabled
            if (enableAchievements && achievementList != null)
            {
                InitializeAchievementSystem();
            }
            
            // Load existing save data or create new
            LoadGameData();
            
            // Start auto-save if enabled
            if (enableAutoSave)
            {
                StartAutoSave();
            }
        }
        
        private void InitializeAchievementSystem()
        {
            MMAchievementManager.LoadAchievementList(achievementList);
            MMAchievementManager.LoadSavedAchievements();
            
            if (enableDebugLogs)
            {
                Debug.Log("[WordScrollSaveManager] Achievement system initialized");
            }
        }
        
        #endregion
        
        #region SAVE_AND_LOAD
        
        /// <summary>
        /// Load game data from file, or create new if none exists
        /// </summary>
        public void LoadGameData()
        {
            try
            {
                if (File.Exists(fullSavePath))
                {
                    string jsonData = File.ReadAllText(fullSavePath);
                    currentSaveData = JsonUtility.FromJson<WordScrollSaveData>(jsonData);
                    
                    if (currentSaveData == null)
                    {
                        throw new Exception("Failed to deserialize save data");
                    }
                    
                    // Update last played date
                    currentSaveData.UpdateTimestamps();
                    
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[WordScrollSaveManager] Save data loaded successfully. Version: {currentSaveData.saveVersion}");
                        Debug.Log($"[WordScrollSaveManager] ↳ Current Level: {currentSaveData.currentLevelIndex}");
                        Debug.Log($"[WordScrollSaveManager] ↳ Total Stars: {currentSaveData.totalStarsEarned}");
                        Debug.Log($"[WordScrollSaveManager] ↳ Levels Completed: {currentSaveData.totalLevelsCompleted}");
                    }
                    
                    OnSaveDataLoaded?.Invoke(currentSaveData);
                }
                else
                {
                    // Create new save data
                    CreateNewSaveData();
                    
                    if (enableDebugLogs)
                    {
                        Debug.Log("[WordScrollSaveManager] No existing save found. Created new save data.");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[WordScrollSaveManager] Error loading save data: {e.Message}");
                
                // Try to load backup if available
                if (!TryLoadBackup())
                {
                    // Create new save data as fallback
                    CreateNewSaveData();
                    OnSaveError?.Invoke($"Failed to load save data: {e.Message}");
                }
            }
        }
        
        /// <summary>
        /// Save current game data to file
        /// </summary>
        public void SaveGameData(bool forceImmediate = false)
        {
            if (currentSaveData == null)
            {
                Debug.LogWarning("[WordScrollSaveManager] No save data to save!");
                return;
            }
            
            try
            {
                // Update timestamps
                currentSaveData.UpdateTimestamps();
                
                // Create backup if enabled
                if (enableBackupSaves && File.Exists(fullSavePath))
                {
                    CreateBackup();
                }
                
                // Serialize to JSON
                string jsonData = JsonUtility.ToJson(currentSaveData, true);
                
                // Write to file
                File.WriteAllText(fullSavePath, jsonData);
                
                // Clear dirty flag
                isDirty = false;
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[WordScrollSaveManager] Game data saved successfully to: {fullSavePath}");
                }
                
                OnSaveDataSaved?.Invoke(currentSaveData);
                
                // Save achievements if enabled
                if (enableAchievements)
                {
                    MMAchievementManager.SaveAchievements();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[WordScrollSaveManager] Error saving game data: {e.Message}");
                OnSaveError?.Invoke($"Failed to save game data: {e.Message}");
            }
        }
        
        /// <summary>
        /// Create new save data with default values
        /// </summary>
        private void CreateNewSaveData()
        {
            currentSaveData = new WordScrollSaveData();
            
            // Initialize with first level unlocked
            if (LevelManager.Instance != null)
            {
                var firstLevel = LevelManager.Instance.GetLevel(0);
                if (firstLevel != null)
                {
                    var firstLevelProgress = new LevelProgressData(0, firstLevel);
                    currentSaveData.levelProgress.Add(firstLevelProgress);
                }
            }
            
            // Mark as dirty to ensure it gets saved
            MarkDirty();
            
            OnSaveDataLoaded?.Invoke(currentSaveData);
        }
        
        #endregion
        
        #region LEVEL_PROGRESS_MANAGEMENT
        
        /// <summary>
        /// Update level progress and save
        /// </summary>
        public void UpdateLevelProgress(int levelIndex, int score, int moves, float time, int stars, LevelData levelData, List<string> foundWords = null)
        {
            if (currentSaveData == null)
            {
                Debug.LogError("[WordScrollSaveManager] No save data available for level progress update!");
                return;
            }
            
            // Get or create level progress
            LevelProgressData levelProgress = currentSaveData.GetLevelProgress(levelIndex);
            if (levelProgress == null)
            {
                levelProgress = new LevelProgressData(levelIndex, levelData);
            }
            
            // Update completion data
            levelProgress.UpdateCompletion(score, moves, time, stars, levelData);
            
            // Add found words for Wordle-style levels
            if (foundWords != null && levelData.IsWordleStyle)
            {
                foreach (string word in foundWords)
                {
                    levelProgress.AddFoundTargetWord(word);
                }
            }
            
            // Update save data
            currentSaveData.UpdateLevelProgress(levelProgress);
            
            // Update global progress
            UpdateGlobalProgress(levelProgress, levelData);
            
            // Update statistics
            UpdatePlayerStatistics(levelProgress, foundWords?.Count ?? 0, moves, score, levelData);
            
            // Check and unlock next level
            UnlockNextLevel(levelIndex);
            
            // Update achievements
            if (enableAchievements)
            {
                UpdateAchievements(levelProgress, levelData);
            }
            
            // Mark dirty and potentially save
            MarkDirty();
            
            if (enableDebugLogs)
            {
                Debug.Log($"[WordScrollSaveManager] Level {levelIndex} progress updated:");
                Debug.Log($"[WordScrollSaveManager] ↳ Score: {score}, Stars: {stars}, Completed: {levelProgress.isCompleted}");
            }
        }
        
        /// <summary>
        /// Get level progress for a specific level
        /// </summary>
        public LevelProgressData GetLevelProgress(int levelIndex)
        {
            return currentSaveData?.GetLevelProgress(levelIndex);
        }
        
        /// <summary>
        /// Check if a level is unlocked
        /// </summary>
        public bool IsLevelUnlocked(int levelIndex)
        {
            var progress = GetLevelProgress(levelIndex);
            return progress?.isUnlocked ?? (levelIndex == 0); // First level is always unlocked
        }
        
        /// <summary>
        /// Unlock next level in sequence
        /// </summary>
        private void UnlockNextLevel(int currentLevelIndex)
        {
            int nextLevelIndex = currentLevelIndex + 1;
            
            if (LevelManager.Instance != null)
            {
                var nextLevel = LevelManager.Instance.GetLevel(nextLevelIndex);
                if (nextLevel != null)
                {
                    var nextLevelProgress = currentSaveData.GetLevelProgress(nextLevelIndex);
                    if (nextLevelProgress == null)
                    {
                        nextLevelProgress = new LevelProgressData(nextLevelIndex, nextLevel);
                        currentSaveData.levelProgress.Add(nextLevelProgress);
                    }
                    
                    if (!nextLevelProgress.isUnlocked)
                    {
                        nextLevelProgress.isUnlocked = true;
                        currentSaveData.highestLevelUnlocked = Mathf.Max(currentSaveData.highestLevelUnlocked, nextLevelIndex);
                        
                        if (enableDebugLogs)
                        {
                            Debug.Log($"[WordScrollSaveManager] Unlocked level {nextLevelIndex}: {nextLevel.LevelName}");
                        }
                    }
                }
            }
        }
        
        #endregion
        
        #region STATISTICS_AND_ACHIEVEMENTS
        
        /// <summary>
        /// Update global progress counters
        /// </summary>
        private void UpdateGlobalProgress(LevelProgressData levelProgress, LevelData levelData)
        {
            if (levelProgress.isCompleted)
            {
                // Update totals
                currentSaveData.totalLevelsCompleted = 0;
                currentSaveData.totalStarsEarned = 0;
                
                // Recalculate from all level progress
                foreach (var progress in currentSaveData.levelProgress)
                {
                    if (progress.isCompleted)
                    {
                        currentSaveData.totalLevelsCompleted++;
                        currentSaveData.totalStarsEarned += progress.starsEarned;
                    }
                }
                
                // Update current level index to highest completed
                if (levelProgress.levelIndex >= currentSaveData.currentLevelIndex)
                {
                    currentSaveData.currentLevelIndex = levelProgress.levelIndex + 1; // Next level to play
                }
            }
        }
        
        /// <summary>
        /// Update player statistics
        /// </summary>
        private void UpdatePlayerStatistics(LevelProgressData levelProgress, int wordsFound, int lettersUsed, int singleWordScore, LevelData levelData)
        {
            currentSaveData.statistics.UpdateStatistics(levelProgress, wordsFound, lettersUsed, singleWordScore, levelData);
        }
        
        /// <summary>
        /// Update achievement progress
        /// </summary>
        private void UpdateAchievements(LevelProgressData levelProgress, LevelData levelData)
        {
            // Example achievement updates - customize based on your needs
            
            // "First Steps" - Complete first level
            if (levelProgress.levelIndex == 0 && levelProgress.isCompleted)
            {
                MMAchievementManager.UnlockAchievement("first_steps");
            }
            
            // "Perfect Score" - Get 3 stars
            if (levelProgress.starsEarned == 3)
            {
                MMAchievementManager.AddProgress("perfect_levels", 1);
            }
            
            // "Word Master" - Total words found
            MMAchievementManager.SetProgress("word_master", currentSaveData.statistics.totalWordsFound);
            
            // "Completionist" - Complete all levels
            if (currentSaveData.totalLevelsCompleted >= GetTotalLevelCount())
            {
                MMAchievementManager.UnlockAchievement("completionist");
            }
            
            // "Speed Demon" - Complete level quickly
            if (levelData.UsesTimer && levelProgress.bestTime < 120f) // Under 2 minutes
            {
                MMAchievementManager.UnlockAchievement("speed_demon");
            }
        }
        
        /// <summary>
        /// Get total number of levels (helper for achievements)
        /// </summary>
        private int GetTotalLevelCount()
        {
            return LevelManager.Instance?.GetTotalLevelCount() ?? 0;
        }
        
        #endregion
        
        #region AUTO_SAVE_AND_BACKUP
        
        /// <summary>
        /// Start auto-save coroutine
        /// </summary>
        private void StartAutoSave()
        {
            if (autoSaveCoroutine != null)
            {
                StopCoroutine(autoSaveCoroutine);
            }
            
            autoSaveCoroutine = StartCoroutine(AutoSaveCoroutine());
        }
        
        /// <summary>
        /// Auto-save coroutine
        /// </summary>
        private IEnumerator AutoSaveCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(autoSaveInterval);
                
                if (isDirty)
                {
                    SaveGameData();
                    
                    if (enableDebugLogs)
                    {
                        Debug.Log("[WordScrollSaveManager] Auto-save completed");
                    }
                }
            }
        }
        
        /// <summary>
        /// Create backup of current save file
        /// </summary>
        private void CreateBackup()
        {
            try
            {
                string backupPath = fullSavePath.Replace(saveFileExtension, $"_backup_{DateTime.Now:yyyyMMdd_HHmmss}{saveFileExtension}");
                File.Copy(fullSavePath, backupPath);
                
                // Clean up old backups
                CleanupOldBackups();
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[WordScrollSaveManager] Backup created: {backupPath}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WordScrollSaveManager] Failed to create backup: {e.Message}");
            }
        }
        
        /// <summary>
        /// Try to load from backup if main save fails
        /// </summary>
        private bool TryLoadBackup()
        {
            try
            {
                string saveDirectory = Path.GetDirectoryName(fullSavePath);
                string[] backupFiles = Directory.GetFiles(saveDirectory, $"{saveFileName}_backup_*{saveFileExtension}");
                
                if (backupFiles.Length == 0)
                {
                    return false;
                }
                
                // Sort by creation time (newest first)
                Array.Sort(backupFiles, (x, y) => File.GetCreationTime(y).CompareTo(File.GetCreationTime(x)));
                
                string latestBackup = backupFiles[0];
                string jsonData = File.ReadAllText(latestBackup);
                currentSaveData = JsonUtility.FromJson<WordScrollSaveData>(jsonData);
                
                if (currentSaveData != null)
                {
                    Debug.Log($"[WordScrollSaveManager] Loaded from backup: {latestBackup}");
                    OnSaveDataLoaded?.Invoke(currentSaveData);
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[WordScrollSaveManager] Failed to load backup: {e.Message}");
            }
            
            return false;
        }
        
        /// <summary>
        /// Clean up old backup files
        /// </summary>
        private void CleanupOldBackups()
        {
            try
            {
                string saveDirectory = Path.GetDirectoryName(fullSavePath);
                string[] backupFiles = Directory.GetFiles(saveDirectory, $"{saveFileName}_backup_*{saveFileExtension}");
                
                if (backupFiles.Length > maxBackupFiles)
                {
                    // Sort by creation time (oldest first)
                    Array.Sort(backupFiles, (x, y) => File.GetCreationTime(x).CompareTo(File.GetCreationTime(y)));
                    
                    // Delete oldest files
                    for (int i = 0; i < backupFiles.Length - maxBackupFiles; i++)
                    {
                        File.Delete(backupFiles[i]);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WordScrollSaveManager] Failed to cleanup backups: {e.Message}");
            }
        }
        
        #endregion
        
        #region PUBLIC_API
        
        /// <summary>
        /// Mark save data as dirty (needs saving)
        /// </summary>
        public void MarkDirty()
        {
            isDirty = true;
        }
        
        /// <summary>
        /// Force immediate save
        /// </summary>
        public void ForceSave()
        {
            SaveGameData(true);
        }
        
        /// <summary>
        /// Reset all save data (for testing/new game)
        /// </summary>
        public void ResetSaveData()
        {
            currentSaveData = null;
            
            if (File.Exists(fullSavePath))
            {
                File.Delete(fullSavePath);
            }
            
            CreateNewSaveData();
            SaveGameData(true);
            
            // Reset achievements
            if (enableAchievements)
            {
                MMAchievementManager.ResetAllAchievements();
            }
            
            Debug.Log("[WordScrollSaveManager] Save data reset");
        }
        
        /// <summary>
        /// Get save file size in bytes
        /// </summary>
        public long GetSaveFileSize()
        {
            if (File.Exists(fullSavePath))
            {
                return new FileInfo(fullSavePath).Length;
            }
            return 0;
        }
        
        /// <summary>
        /// Export save data as JSON string
        /// </summary>
        public string ExportSaveData()
        {
            if (currentSaveData == null) return null;
            
            return JsonUtility.ToJson(currentSaveData, true);
        }
        
        /// <summary>
        /// Import save data from JSON string
        /// </summary>
        public bool ImportSaveData(string jsonData)
        {
            try
            {
                var importedData = JsonUtility.FromJson<WordScrollSaveData>(jsonData);
                if (importedData != null)
                {
                    currentSaveData = importedData;
                    SaveGameData(true);
                    OnSaveDataLoaded?.Invoke(currentSaveData);
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[WordScrollSaveManager] Failed to import save data: {e.Message}");
                OnSaveError?.Invoke($"Failed to import save data: {e.Message}");
            }
            
            return false;
        }
        
        #endregion
        
        #region UNITY_LIFECYCLE
        
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && isDirty)
            {
                SaveGameData();
            }
        }
        
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && isDirty)
            {
                SaveGameData();
            }
        }
        
        private void OnDestroy()
        {
            if (autoSaveCoroutine != null)
            {
                StopCoroutine(autoSaveCoroutine);
            }
            
            if (isDirty)
            {
                SaveGameData();
            }
        }
        
        #endregion
    }
}
