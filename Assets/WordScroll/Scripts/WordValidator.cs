using UnityEngine;
using System.Collections.Generic;
using TMPro;
using System.Linq;
using DG.Tweening;

public class WordValidator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Assign the WordGridManager script instance here")]
    [SerializeField] private WordGridManager wordGridManager;

    [Tooltip("Assign the TextMeshProUGUI component to display the score")]
    [SerializeField] private TextMeshProUGUI scoreTextDisplay;

    [Header("Word List")]
    [Tooltip("TextAsset containing the list of valid words (one word per line)")]
    [SerializeField] private TextAsset wordListAsset;

    private HashSet<string> validWords = new HashSet<string>();
    private int playerScore = 0;

    // Scrabble letter values
    private Dictionary<char, int> letterValues = new Dictionary<char, int>()
    {
        {'A', 1}, {'E', 1}, {'I', 1}, {'O', 1}, {'U', 1}, {'L', 1}, {'N', 1}, {'S', 1}, {'T', 1}, {'R', 1},
        {'D', 2}, {'G', 2},
        {'B', 3}, {'C', 3}, {'M', 3}, {'P', 3},
        {'F', 4}, {'H', 4}, {'V', 4}, {'W', 4}, {'Y', 4},
        {'K', 5},
        {'J', 8}, {'X', 8},
        {'Q', 10}, {'Z', 10}
    };

    void Start()
    {
        if (wordGridManager == null)
        {
            Debug.LogError("WordGridManager reference is not assigned in the WordValidator inspector!", this);
            return;
        }

        if (wordListAsset == null)
        {
            Debug.LogError("Word List TextAsset is not assigned in the WordValidator inspector!", this);
            return;
        }

        LoadWordList();
        UpdateScoreDisplay();
        CheckInitialWords(); // Check for initial words at start
    }

    private void LoadWordList()
    {
        string[] words = wordListAsset.text.Split('\n');
        foreach (string word in words)
        {
            validWords.Add(word.Trim().ToUpper()); // Trim whitespace and convert to uppercase
        }
    }

    public void ValidateWords()
    {
        List<string> foundWords = new List<string>();
        int gridSize = wordGridManager.GridSize;

        // Check rows
        for (int row = 0; row < gridSize; row++)
        {
            string rowWord = "";
            for (int col = 0; col < gridSize; col++)
            {
                rowWord += wordGridManager.GetLetterAt(row, col);
            }
            CheckAndAddWord(rowWord, foundWords);
        }

        // Check columns
        for (int col = 0; col < gridSize; col++)
        {
            string colWord = "";
            for (int row = 0; row < gridSize; row++)
            {
                colWord += wordGridManager.GetLetterAt(row, col);
            }
            CheckAndAddWord(colWord, foundWords);
        }

        // Process found words
        if (foundWords.Count > 0)
        {
            foreach (string word in foundWords)
            {
                playerScore += CalculateWordScore(word);
            }
            UpdateScoreDisplay();
            ReplaceUsedLetters(foundWords);
        }
    }

    private void CheckAndAddWord(string word, List<string> foundWords)
    {
        if (word.Length >= 3 && validWords.Contains(word.ToUpper())) // Convert to uppercase here
        {
            foundWords.Add(word.ToUpper());
            Debug.Log($"Found word: {word.ToUpper()}");
        }
    }

    private int CalculateWordScore(string word)
    {
        int score = 0;
        foreach (char letter in word)
        {
            if (letterValues.ContainsKey(letter))
            {
                score += letterValues[letter];
            }
        }
        return score;
    }

    private void UpdateScoreDisplay()
    {
        if (scoreTextDisplay != null)
        {
            scoreTextDisplay.text = "Score: " + playerScore;
        }
    }

    private void ReplaceUsedLetters(List<string> usedWords)
    {
        int gridSize = wordGridManager.GridSize;
        List<Vector2Int> cellsToReplace = new List<Vector2Int>(); // Store cells to replace

        foreach (string word in usedWords)
        {
            // Check rows for the word
            for (int row = 0; row < gridSize; row++)
            {
                string rowWord = "";
                for (int col = 0; col < gridSize; col++)
                {
                    rowWord += wordGridManager.GetLetterAt(row, col);
                }
                if (rowWord.Contains(word))
                {
                    int wordIndex = rowWord.IndexOf(word);
                    for (int col = 0; col < word.Length; col++)
                    {
                        cellsToReplace.Add(new Vector2Int(row, wordIndex + col));
                    }
                    break; // Move to the next word
                }
            }

            // Check columns for the word
            for (int col = 0; col < gridSize; col++)
            {
                string colWord = "";
                for (int row = 0; row < gridSize; row++)
                {
                    colWord += wordGridManager.GetLetterAt(row, col);
                }
                if (colWord.Contains(word))
                {
                    int wordIndex = colWord.IndexOf(word);
                    for (int row = 0; row < word.Length; row++)
                    {
                        cellsToReplace.Add(new Vector2Int(wordIndex + row, col));
                    }
                    break; // Move to the next word
                }
            }
        }

        // Animate cell disappearances
        Sequence replaceSequence = DOTween.Sequence();
        foreach (Vector2Int cellPos in cellsToReplace)
        {
            RectTransform cellRect = wordGridManager.GetCellRect(cellPos.x, cellPos.y);
            if (cellRect != null)
            {
                replaceSequence.Insert(0, cellRect.DOScale(0f, 0.2f)); // Animate cell scale down
            }
        }

        // Replace letters and animate new cells in
        replaceSequence.OnComplete(() =>
        {
            foreach (Vector2Int cellPos in cellsToReplace)
            {
                wordGridManager.ReplaceLetter(cellPos.x, cellPos.y);
            }
        });
    }

    private void CheckInitialWords()
    {
        List<string> initialWords = new List<string>();
        int gridSize = wordGridManager.GridSize;

        // Check rows
        for (int row = 0; row < gridSize; row++)
        {
            string rowWord = "";
            for (int col = 0; col < gridSize; col++)
            {
                rowWord += wordGridManager.GetLetterAt(row, col);
            }
            CheckAndAddWord(rowWord, initialWords);
        }

        // Check columns
        for (int col = 0; col < gridSize; col++)
        {
            string colWord = "";
            for (int row = 0; row < gridSize; row++)
            {
                colWord += wordGridManager.GetLetterAt(row, col);
            }
            CheckAndAddWord(colWord, initialWords);
        }

        if (initialWords.Count > 0)
        {
            Debug.Log("Initial valid words found:");
            foreach (string word in initialWords)
            {
                Debug.Log("- " + word);
            }
        }
        else
        {
            Debug.Log("No initial valid words found.");
        }
    }
}