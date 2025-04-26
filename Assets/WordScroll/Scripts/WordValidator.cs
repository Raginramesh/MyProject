using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq; // Needed for HashSet operations
using DG.Tweening; // Make sure you have this if using DOTween

public class WordValidator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WordGridManager wordGridManager;
    [SerializeField] private TextAsset wordListTextAsset; // Assign your wordlist.txt here

    [Header("Settings")]
    [SerializeField] private int minWordLength = 3;

    // --- ADD THIS ---
    private GameManager gameManager; // Reference to the GameManager

    private HashSet<string> validWords;
    private Dictionary<char, int> letterValues = new Dictionary<char, int>() {
        {'A', 1}, {'B', 3}, {'C', 3}, {'D', 2}, {'E', 1}, {'F', 4}, {'G', 2}, {'H', 4},
        {'I', 1}, {'J', 8}, {'K', 5}, {'L', 1}, {'M', 3}, {'N', 1}, {'O', 1}, {'P', 3},
        {'Q', 10}, {'R', 1}, {'S', 1}, {'T', 1}, {'U', 1}, {'V', 4}, {'W', 4}, {'X', 8},
        {'Y', 4}, {'Z', 10}
    }; // Example Scrabble values

    void Awake() // Use Awake for initialization before Start
    {
        LoadWordList();
    }

    // --- THIS IS THE NEW PUBLIC METHOD ---
    public void SetGameManager(GameManager manager)
    {
        gameManager = manager;
        Debug.Log("WordValidator: GameManager reference set.");
    }

    void LoadWordList()
    {
        validWords = new HashSet<string>();
        if (wordListTextAsset != null)
        {
            string[] words = wordListTextAsset.text.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (string word in words)
            {
                if (word.Length >= minWordLength)
                {
                    validWords.Add(word.ToUpper()); // Store words in uppercase for easier comparison
                }
            }
            Debug.Log($"Loaded {validWords.Count} words of length {minWordLength}+.");
        }
        else
        {
            Debug.LogError("Word list TextAsset not assigned in WordValidator!");
        }
    }

    public void ValidateWords()
    {
        if (wordGridManager == null)
        {
            Debug.LogError("WordValidator: WordGridManager reference not set!");
            return;
        }
        if (gameManager == null)
        {
            Debug.LogWarning("WordValidator: GameManager reference not set! Score will not be updated.");
            // Don't necessarily return, maybe we still want validation effect?
        }


        HashSet<(int, int)> cellsToReplace = new HashSet<(int, int)>();
        int totalScoreThisCheck = 0;

        char[,] currentGridData = wordGridManager.gridData; // Get current grid data
        int gridSize = currentGridData.GetLength(0); // Get grid size dynamically

        // Check Rows
        for (int r = 0; r < gridSize; r++)
        {
            string rowWord = "";
            for (int c = 0; c < gridSize; c++)
            {
                rowWord += currentGridData[r, c];
            }
            FindValidWordsInString(rowWord, r, -1, cellsToReplace, ref totalScoreThisCheck);
        }

        // Check Columns
        for (int c = 0; c < gridSize; c++)
        {
            string colWord = "";
            for (int r = 0; r < gridSize; r++)
            {
                colWord += currentGridData[r, c];
            }
            FindValidWordsInString(colWord, -1, c, cellsToReplace, ref totalScoreThisCheck);
        }

        // Process replacements if any words were found
        if (cellsToReplace.Count > 0)
        {
            ProcessReplacements(cellsToReplace);
            if (gameManager != null && totalScoreThisCheck > 0)
            {
                gameManager.AddScore(totalScoreThisCheck); // Add total score for this validation pass
            }
        }
        else
        {
            // Optional: Add feedback if no words found after a scroll
            // Debug.Log("No words found this turn.");
        }
    }

    // Helper to find words within a row or column string
    void FindValidWordsInString(string line, int rowIndex, int colIndex, HashSet<(int, int)> cellsToReplace, ref int currentTurnScore)
    {
        int n = line.Length;
        for (int len = minWordLength; len <= n; len++)
        {
            for (int i = 0; i <= n - len; i++)
            {
                string sub = line.Substring(i, len);
                if (validWords.Contains(sub.ToUpper())) // Check against uppercase list
                {
                    Debug.Log($"Valid word found: {sub}");
                    int wordScore = CalculateScore(sub);
                    currentTurnScore += wordScore; // Add to score for this turn

                    // Mark cells for replacement
                    for (int k = 0; k < len; k++)
                    {
                        if (rowIndex != -1) // It's a row word
                        {
                            cellsToReplace.Add((rowIndex, i + k));
                        }
                        else // It's a column word
                        {
                            cellsToReplace.Add((i + k, colIndex));
                        }
                    }
                    // Optional: Add score popup effect here
                }
            }
        }
    }


    int CalculateScore(string word)
    {
        int score = 0;
        foreach (char letter in word.ToUpper())
        {
            score += letterValues.ContainsKey(letter) ? letterValues[letter] : 0;
        }
        // Add length bonus?
        if (word.Length > 4) score += (word.Length - 4) * 5; // Example bonus
        return score;
    }

    void ProcessReplacements(HashSet<(int row, int col)> cells)
    {
        if (wordGridManager == null) return;

        Sequence sequence = DOTween.Sequence(); // Create a sequence for animations

        // First, animate all found cells scaling out
        foreach (var cellCoord in cells)
        {
            RectTransform cellRect = wordGridManager.gridCellRects[cellCoord.row, cellCoord.col];
            if (cellRect != null)
            {
                // Add scale-out animation to the sequence (at the same time)
                sequence.Join(cellRect.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack));
            }
        }

        // After scale-out is done, replace letters and animate scale-in
        sequence.AppendCallback(() => {
            foreach (var cellCoord in cells)
            {
                wordGridManager.ReplaceLetter(cellCoord.row, cellCoord.col); // Replace data and get new letter

                // Get the rect transform again (ReplaceLetter might change internal state)
                RectTransform cellRect = wordGridManager.gridCellRects[cellCoord.row, cellCoord.col];
                if (cellRect != null)
                {
                    // Ensure scale is zero before scaling in (might already be from previous tween)
                    cellRect.localScale = Vector3.zero;
                    // Add scale-in animation (start slightly delayed or concurrently)
                    sequence.Join(cellRect.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack));
                }
            }
        });

        // Play the entire animation sequence
        sequence.Play();
    }

    // Ensure WordGridManager reference is available if needed elsewhere
    // (e.g., if you add methods called by other scripts)
}