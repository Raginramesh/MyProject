using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject that defines a collection of words for the word placement game.
/// Contains word data, difficulty settings, and game parameters.
/// </summary>
[CreateAssetMenu(fileName = "WordList", menuName = "Word Game/Word List", order = 1)]
public class WordListScriptableObject : ScriptableObject
{
    [Header("Word List Info")]
    [SerializeField] private string listName = "Default Word List";
    [SerializeField] private string description = "A collection of words for the word placement game";
    [SerializeField] private int version = 1;

    [Header("Game Settings")]
    [SerializeField] private int minWordsToWin = 5;
    [SerializeField] private float timeLimit = 300f; // 5 minutes
    [SerializeField] private int targetScore = 500;
    [SerializeField] private bool allowRotation = true;

    [Header("Difficulty Settings")]
    [SerializeField] private int minDifficulty = 1;
    [SerializeField] private int maxDifficulty = 5;
    [SerializeField] private DifficultyDistribution difficultyDistribution = DifficultyDistribution.Balanced;

    [Header("Words")]
    [SerializeField] private WordData[] words = new WordData[0];

    #region Public Properties

    public string ListName => listName;
    public string Description => description;
    public int Version => version;
    public int MinWordsToWin => minWordsToWin;
    public float TimeLimit => timeLimit;
    public int TargetScore => targetScore;
    public bool AllowRotation => allowRotation;
    public int MinDifficulty => minDifficulty;
    public int MaxDifficulty => maxDifficulty;
    public DifficultyDistribution DifficultyDistribution => difficultyDistribution;
    public WordData[] Words => words;

    #endregion

    #region Word Management

    /// <summary>
    /// Get all words of a specific difficulty level
    /// </summary>
    public List<WordData> GetWordsByDifficulty(int difficulty)
    {
        List<WordData> result = new List<WordData>();
        
        foreach (var word in words)
        {
            if (word.difficulty == difficulty)
            {
                result.Add(word);
            }
        }
        
        return result;
    }

    /// <summary>
    /// Get words within a difficulty range
    /// </summary>
    public List<WordData> GetWordsByDifficultyRange(int minDiff, int maxDiff)
    {
        List<WordData> result = new List<WordData>();
        
        foreach (var word in words)
        {
            if (word.difficulty >= minDiff && word.difficulty <= maxDiff)
            {
                result.Add(word);
            }
        }
        
        return result;
    }

    /// <summary>
    /// Get words by length
    /// </summary>
    public List<WordData> GetWordsByLength(int minLength, int maxLength)
    {
        List<WordData> result = new List<WordData>();
        
        foreach (var word in words)
        {
            if (word.word.Length >= minLength && word.word.Length <= maxLength)
            {
                result.Add(word);
            }
        }
        
        return result;
    }

    /// <summary>
    /// Get random words with specified count and difficulty distribution
    /// </summary>
    public List<WordData> GetRandomWords(int count, DifficultyDistribution distribution = DifficultyDistribution.Balanced)
    {
        List<WordData> result = new List<WordData>();
        List<WordData> availableWords = new List<WordData>(words);
        
        // Shuffle available words
        for (int i = 0; i < availableWords.Count; i++)
        {
            WordData temp = availableWords[i];
            int randomIndex = Random.Range(i, availableWords.Count);
            availableWords[i] = availableWords[randomIndex];
            availableWords[randomIndex] = temp;
        }
        
        // Select words based on distribution
        switch (distribution)
        {
            case DifficultyDistribution.Easy:
                result.AddRange(GetWordsByDifficultyRange(1, 2));
                break;
                
            case DifficultyDistribution.Medium:
                result.AddRange(GetWordsByDifficultyRange(2, 4));
                break;
                
            case DifficultyDistribution.Hard:
                result.AddRange(GetWordsByDifficultyRange(3, 5));
                break;
                
            case DifficultyDistribution.Balanced:
                // Mix of all difficulties
                result.AddRange(availableWords);
                break;
                
            case DifficultyDistribution.Progressive:
                // Start easy, get harder
                var sortedWords = new List<WordData>(availableWords);
                sortedWords.Sort((a, b) => a.difficulty.CompareTo(b.difficulty));
                result.AddRange(sortedWords);
                break;
        }
        
        // Return up to requested count
        return result.GetRange(0, Mathf.Min(count, result.Count));
    }

    /// <summary>
    /// Calculate total score for all words in the list
    /// </summary>
    public int CalculateTotalScore()
    {
        int total = 0;
        
        foreach (var word in words)
        {
            total += word.CalculateScore();
        }
        
        return total;
    }

    /// <summary>
    /// Get statistics about the word list
    /// </summary>
    public WordListStats GetStatistics()
    {
        WordListStats stats = new WordListStats();
        
        stats.totalWords = words.Length;
        stats.averageLength = 0;
        stats.minLength = int.MaxValue;
        stats.maxLength = 0;
        stats.totalScore = 0;
        stats.difficultyDistribution = new int[6]; // 0-5 difficulty levels
        
        foreach (var word in words)
        {
            int length = word.word.Length;
            int score = word.CalculateScore();
            
            stats.averageLength += length;
            stats.minLength = Mathf.Min(stats.minLength, length);
            stats.maxLength = Mathf.Max(stats.maxLength, length);
            stats.totalScore += score;
            
            if (word.difficulty >= 0 && word.difficulty < stats.difficultyDistribution.Length)
            {
                stats.difficultyDistribution[word.difficulty]++;
            }
        }
        
        if (words.Length > 0)
        {
            stats.averageLength /= words.Length;
            stats.averageScore = stats.totalScore / words.Length;
        }
        
        return stats;
    }

    #endregion

    #region Validation

    /// <summary>
    /// Validate the word list for common issues
    /// </summary>
    public List<string> ValidateWordList()
    {
        List<string> issues = new List<string>();
        
        // Check for empty words
        for (int i = 0; i < words.Length; i++)
        {
            if (string.IsNullOrEmpty(words[i].word))
            {
                issues.Add($"Word at index {i} is empty");
            }
        }
        
        // Check for duplicate words
        HashSet<string> uniqueWords = new HashSet<string>();
        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i].word.ToUpper();
            if (uniqueWords.Contains(word))
            {
                issues.Add($"Duplicate word found: {word}");
            }
            else
            {
                uniqueWords.Add(word);
            }
        }
        
        // Check difficulty ranges
        foreach (var word in words)
        {
            if (word.difficulty < 1 || word.difficulty > 5)
            {
                issues.Add($"Word '{word.word}' has invalid difficulty: {word.difficulty}");
            }
        }
        
        // Check if we have enough words
        if (words.Length < minWordsToWin)
        {
            issues.Add($"Not enough words ({words.Length}) to meet minimum win requirement ({minWordsToWin})");
        }
        
        return issues;
    }

    #endregion

    #region Editor Utilities

    #if UNITY_EDITOR
    /// <summary>
    /// Add a word to the list (Editor only)
    /// </summary>
    public void AddWord(string word, int difficulty = 1, string[] hints = null)
    {
        System.Array.Resize(ref words, words.Length + 1);
        words[words.Length - 1] = new WordData
        {
            word = word.ToUpper(),
            difficulty = difficulty,
            hints = hints ?? new string[0]
        };
        
        UnityEditor.EditorUtility.SetDirty(this);
    }

    /// <summary>
    /// Remove a word from the list (Editor only)
    /// </summary>
    public void RemoveWord(int index)
    {
        if (index >= 0 && index < words.Length)
        {
            var newWords = new WordData[words.Length - 1];
            System.Array.Copy(words, 0, newWords, 0, index);
            System.Array.Copy(words, index + 1, newWords, index, words.Length - index - 1);
            words = newWords;
            
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }

    /// <summary>
    /// Sort words by difficulty (Editor only)
    /// </summary>
    public void SortWordsByDifficulty()
    {
        System.Array.Sort(words, (a, b) => a.difficulty.CompareTo(b.difficulty));
        UnityEditor.EditorUtility.SetDirty(this);
    }

    /// <summary>
    /// Sort words alphabetically (Editor only)
    /// </summary>
    public void SortWordsAlphabetically()
    {
        System.Array.Sort(words, (a, b) => a.word.CompareTo(b.word));
        UnityEditor.EditorUtility.SetDirty(this);
    }
    #endif

    #endregion
}

/// <summary>
/// Data structure for individual words
/// </summary>
[System.Serializable]
public class WordData
{
    [Header("Word Info")]
    public string word = "";
    public int difficulty = 1;
    public string[] hints = new string[0];

    [Header("Scoring")]
    public int baseScore = 0; // If 0, will be calculated from letters
    public float scoreMultiplier = 1f;

    [Header("Metadata")]
    public string category = "";
    public string[] tags = new string[0];

    /// <summary>
    /// Calculate the score for this word
    /// </summary>
    public int CalculateScore()
    {
        if (baseScore > 0)
        {
            return Mathf.RoundToInt(baseScore * scoreMultiplier);
        }
        
        // Calculate from letter values
        int score = 0;
        foreach (char letter in word.ToUpper())
        {
            score += LetterBlock.GetStandardLetterScore(letter);
        }
        
        return Mathf.RoundToInt(score * scoreMultiplier);
    }

    /// <summary>
    /// Get a random hint for this word
    /// </summary>
    public string GetRandomHint()
    {
        if (hints == null || hints.Length == 0)
        {
            return $"A {word.Length}-letter word";
        }
        
        return hints[Random.Range(0, hints.Length)];
    }
}

/// <summary>
/// Difficulty distribution options
/// </summary>
public enum DifficultyDistribution
{
    Easy,       // Mostly easy words
    Medium,     // Medium difficulty
    Hard,       // Mostly hard words
    Balanced,   // Even mix of all difficulties
    Progressive // Start easy, get progressively harder
}

/// <summary>
/// Statistics about a word list
/// </summary>
[System.Serializable]
public class WordListStats
{
    public int totalWords;
    public int averageLength;
    public int minLength;
    public int maxLength;
    public int totalScore;
    public int averageScore;
    public int[] difficultyDistribution;
}
