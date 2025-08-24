using System;
using System.Collections.Generic;
using UnityEngine;

namespace WordScroll.SaveSystem
{
    /// <summary>
    /// Main save data structure for WordScroll game
    /// Contains all persistent player progress, settings, and statistics
    /// </summary>
    [System.Serializable]
    public class WordScrollSaveData
    {
        [Header("Save File Info")]
        public string saveVersion = "1.0";
        public string saveDate;
        public string lastPlayedDate;
        
        [Header("Game Progress")]
        public int currentLevelIndex = 0;
        public int totalLevelsCompleted = 0;
        public int totalStarsEarned = 0;
        public float totalPlayTime = 0f;
        public int highestLevelUnlocked = 0;
        
        [Header("Level Progress Data")]
        public List<LevelProgressData> levelProgress = new List<LevelProgressData>();
        
        [Header("Game Settings")]
        public GameSettings gameSettings = new GameSettings();
        
        [Header("Player Statistics")]
        public PlayerStatistics statistics = new PlayerStatistics();
        
        [Header("Achievement Data")]
        public List<AchievementProgress> achievements = new List<AchievementProgress>();
        
        /// <summary>
        /// Constructor initializes with default values
        /// </summary>
        public WordScrollSaveData()
        {
            saveDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            lastPlayedDate = saveDate;
            gameSettings = new GameSettings();
            statistics = new PlayerStatistics();
        }
        
        /// <summary>
        /// Get level progress data for a specific level index
        /// </summary>
        public LevelProgressData GetLevelProgress(int levelIndex)
        {
            foreach (var progress in levelProgress)
            {
                if (progress.levelIndex == levelIndex)
                    return progress;
            }
            return null;
        }
        
        /// <summary>
        /// Update or add level progress data
        /// </summary>
        public void UpdateLevelProgress(LevelProgressData newProgress)
        {
            for (int i = 0; i < levelProgress.Count; i++)
            {
                if (levelProgress[i].levelIndex == newProgress.levelIndex)
                {
                    levelProgress[i] = newProgress;
                    return;
                }
            }
            // If not found, add new entry
            levelProgress.Add(newProgress);
        }
        
        /// <summary>
        /// Update save timestamps
        /// </summary>
        public void UpdateTimestamps()
        {
            lastPlayedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }

    /// <summary>
    /// Individual level progress tracking
    /// Supports both Scrabble-style and Wordle-style levels
    /// </summary>
    [System.Serializable]
    public class LevelProgressData
    {
        [Header("Level Identity")]
        public int levelIndex;
        public string levelName;
        public LevelGameMode gameMode;
        
        [Header("Completion Status")]
        public bool isCompleted;
        public bool isUnlocked;
        public int timesPlayed;
        public int timesCompleted;
        
        [Header("Best Performance")]
        public int bestScore;
        public int starsEarned;
        public int bestMoveCount;
        public float bestTime;
        public float bestEfficiencyPercentage;
        
        [Header("Timestamps")]
        public string firstCompletionDate;
        public string lastPlayedDate;
        public string bestPerformanceDate;
        
        [Header("Wordle-Style Specific")]
        public List<string> foundTargetWords = new List<string>();
        public List<string> allTargetWords = new List<string>();
        public bool allTargetWordsFound;
        
        [Header("Scrabble-Style Specific")]
        public int targetScore;
        public float scorePercentage;
        
        /// <summary>
        /// Constructor for new level progress
        /// </summary>
        public LevelProgressData(int index, LevelData levelData)
        {
            levelIndex = index;
            levelName = levelData.LevelName;
            gameMode = levelData.GameMode;
            
            isCompleted = false;
            isUnlocked = (index == 0); // First level is always unlocked
            timesPlayed = 0;
            timesCompleted = 0;
            
            bestScore = 0;
            starsEarned = 0;
            bestMoveCount = int.MaxValue;
            bestTime = float.MaxValue;
            bestEfficiencyPercentage = 0f;
            
            foundTargetWords = new List<string>();
            if (levelData.IsWordleStyle)
            {
                allTargetWords = new List<string>(levelData.TargetWords);
            }
            allTargetWordsFound = false;
            
            if (levelData.IsScrabbleStyle)
            {
                targetScore = levelData.TargetScore;
            }
            scorePercentage = 0f;
            
            lastPlayedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
        
        /// <summary>
        /// Update progress with new completion data
        /// </summary>
        public void UpdateCompletion(int score, int moves, float time, int stars, LevelData levelData)
        {
            timesPlayed++;
            lastPlayedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
            // Check if this is a new completion or improvement
            if (!isCompleted || score > bestScore)
            {
                if (!isCompleted)
                {
                    isCompleted = true;
                    timesCompleted = 1;
                    firstCompletionDate = lastPlayedDate;
                }
                else
                {
                    timesCompleted++;
                }
                
                // Update best scores
                if (score > bestScore)
                {
                    bestScore = score;
                    bestPerformanceDate = lastPlayedDate;
                }
                
                if (moves < bestMoveCount)
                {
                    bestMoveCount = moves;
                }
                
                if (time < bestTime)
                {
                    bestTime = time;
                }
                
                if (stars > starsEarned)
                {
                    starsEarned = stars;
                }
                
                // Update game mode specific data
                if (levelData.IsScrabbleStyle)
                {
                    scorePercentage = levelData.GetScorePercentage(score);
                }
                else if (levelData.IsWordleStyle)
                {
                    allTargetWordsFound = levelData.AreAllTargetWordsFound();
                    if (levelData.WinConditionType == LevelWinConditionType.MoveBased)
                    {
                        bestEfficiencyPercentage = ((float)moves / levelData.MaxMoves) * 100f;
                    }
                    else if (levelData.WinConditionType == LevelWinConditionType.TimeBased)
                    {
                        bestEfficiencyPercentage = (time / levelData.TimeLimit) * 100f;
                    }
                }
            }
        }
        
        /// <summary>
        /// Add a found target word (Wordle-style levels)
        /// </summary>
        public void AddFoundTargetWord(string word)
        {
            if (!foundTargetWords.Contains(word))
            {
                foundTargetWords.Add(word);
            }
        }
    }

    /// <summary>
    /// Game settings and preferences
    /// </summary>
    [System.Serializable]
    public class GameSettings
    {
        [Header("Audio Settings")]
        public bool musicEnabled = true;
        public bool soundEffectsEnabled = true;
        public float masterVolume = 1.0f;
        public float musicVolume = 0.8f;
        public float sfxVolume = 1.0f;
        
        [Header("Haptics Settings")]
        public bool hapticsEnabled = true;
        public float hapticIntensity = 1.0f;
        
        [Header("Gameplay Settings")]
        public bool showHints = true;
        public bool autoAdvanceNextLevel = true;
        public float autoAdvanceDelay = 3.0f;
        public bool showLetterScores = true; // For Scrabble-style levels
        public bool showWordValidationEffects = true;
        
        [Header("UI Settings")]
        public bool showMovesRemaining = true;
        public bool showScoreBreakdown = true;
        public bool showStarProgress = true;
        public bool useColorBlindFriendlyMode = false;
        
        [Header("Debug Settings")]
        public bool debugMode = false;
        public bool showLevelDebugInfo = false;
    }

    /// <summary>
    /// Player statistics and analytics
    /// </summary>
    [System.Serializable]
    public class PlayerStatistics
    {
        [Header("Word Discovery")]
        public int totalWordsFound = 0;
        public int totalLettersUsed = 0;
        public int totalTargetWordsFound = 0;
        public int longestWordFound = 0;
        public string favoriteWordLength = "4"; // Most common word length found
        
        [Header("Level Performance")]
        public int perfectLevels = 0; // 3-star completions
        public int levelsCompletedFirstTry = 0;
        public float averageStarsPerLevel = 0f;
        public float averageCompletionTime = 0f;
        public float averageMovesPerLevel = 0f;
        
        [Header("Score Statistics")]
        public int totalPointsEarned = 0;
        public int highestSingleWordScore = 0;
        public int highestLevelScore = 0;
        public float averageScorePerWord = 0f;
        
        [Header("Time Statistics")]
        public float fastestLevelCompletion = float.MaxValue;
        public float longestGameSession = 0f;
        public int totalGameSessions = 0;
        
        [Header("Efficiency Metrics")]
        public float bestEfficiencyPercentage = 0f; // Best move/time efficiency
        public int consecutivePerfectLevels = 0;
        public int currentStreak = 0; // Current completion streak
        public int longestStreak = 0; // Longest completion streak
        
        [Header("Game Mode Preferences")]
        public int scrabbleStyleLevelsCompleted = 0;
        public int wordleStyleLevelsCompleted = 0;
        public LevelGameMode preferredGameMode = LevelGameMode.ScrabbleStyle;
        
        /// <summary>
        /// Update statistics based on level completion
        /// </summary>
        public void UpdateStatistics(LevelProgressData levelProgress, int wordsFound, int lettersUsed, int singleWordScore, LevelData levelData)
        {
            totalWordsFound += wordsFound;
            totalLettersUsed += lettersUsed;
            totalPointsEarned += levelProgress.bestScore;
            
            if (levelData.IsWordleStyle)
            {
                totalTargetWordsFound += levelProgress.foundTargetWords.Count;
                wordleStyleLevelsCompleted++;
            }
            else
            {
                scrabbleStyleLevelsCompleted++;
            }
            
            if (singleWordScore > highestSingleWordScore)
            {
                highestSingleWordScore = singleWordScore;
            }
            
            if (levelProgress.bestScore > highestLevelScore)
            {
                highestLevelScore = levelProgress.bestScore;
            }
            
            if (levelProgress.bestTime < fastestLevelCompletion && levelProgress.bestTime > 0)
            {
                fastestLevelCompletion = levelProgress.bestTime;
            }
            
            if (levelProgress.starsEarned == 3)
            {
                perfectLevels++;
                consecutivePerfectLevels++;
                currentStreak++;
                
                if (currentStreak > longestStreak)
                {
                    longestStreak = currentStreak;
                }
            }
            else
            {
                consecutivePerfectLevels = 0;
                if (levelProgress.starsEarned == 0)
                {
                    currentStreak = 0; // Reset streak on failure
                }
            }
            
            if (levelProgress.bestEfficiencyPercentage > bestEfficiencyPercentage)
            {
                bestEfficiencyPercentage = levelProgress.bestEfficiencyPercentage;
            }
            
            // Update averages
            RecalculateAverages();
            
            // Determine preferred game mode
            if (scrabbleStyleLevelsCompleted > wordleStyleLevelsCompleted)
            {
                preferredGameMode = LevelGameMode.ScrabbleStyle;
            }
            else if (wordleStyleLevelsCompleted > scrabbleStyleLevelsCompleted)
            {
                preferredGameMode = LevelGameMode.WordleStyle;
            }
        }
        
        /// <summary>
        /// Recalculate average statistics
        /// </summary>
        private void RecalculateAverages()
        {
            int totalLevelsCompleted = scrabbleStyleLevelsCompleted + wordleStyleLevelsCompleted;
            
            if (totalLevelsCompleted > 0)
            {
                averageStarsPerLevel = (float)perfectLevels / totalLevelsCompleted * 3f; // Approximation
            }
            
            if (totalWordsFound > 0)
            {
                averageScorePerWord = (float)totalPointsEarned / totalWordsFound;
            }
        }
    }

    /// <summary>
    /// Achievement progress tracking (for Feel framework integration)
    /// </summary>
    [System.Serializable]
    public class AchievementProgress
    {
        public string achievementId;
        public string achievementName;
        public bool isUnlocked;
        public int currentProgress;
        public int targetProgress;
        public string unlockedDate;
        
        public AchievementProgress(string id, string name, int target)
        {
            achievementId = id;
            achievementName = name;
            isUnlocked = false;
            currentProgress = 0;
            targetProgress = target;
            unlockedDate = "";
        }
        
        public void UpdateProgress(int newProgress)
        {
            currentProgress = Mathf.Min(newProgress, targetProgress);
            
            if (!isUnlocked && currentProgress >= targetProgress)
            {
                isUnlocked = true;
                unlockedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }
    }
}
