using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System.Linq;

public class WordValidator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WordGridManager wordGridManager;
    [SerializeField] private GameManager gameManager;

    [Header("Word List")]
    [SerializeField] private TextAsset wordListFile;
    [SerializeField] private int minWordLength = 3;

    // Internal data structures
    private HashSet<string> validWords = new HashSet<string>();
    private HashSet<string> wordsFoundThisSession = new HashSet<string>();
    private HashSet<string> wordsFoundThisCycle = new HashSet<string>();

    void Awake()
    {
        // Ensure references...
        if (wordGridManager == null) { Debug.LogError("WV: WordGridManager missing!", this); enabled = false; return; }
        if (gameManager == null) { Debug.LogError("WV: GameManager missing!", this); enabled = false; return; }
        if (wordListFile == null) { Debug.LogError("WV: Word List File missing!", this); enabled = false; return; }
        LoadWordList();
    }

    public void SetGameManager(GameManager manager) { this.gameManager = manager; }

    void LoadWordList()
    {
        validWords.Clear();
        if (wordListFile != null)
        {
            string[] lines = wordListFile.text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            int loadedCount = 0;
            foreach (string line in lines)
            {
                string word = line.Trim().ToUpperInvariant();
                if (word.Length >= minWordLength) { validWords.Add(word); loadedCount++; }
            }
            Debug.Log($"WV: Loaded {loadedCount} words (min {minWordLength}) from '{wordListFile.name}'.");
        }
        else { Debug.LogError("WV: Word list file is null!"); }
    }

    public void ResetFoundWordsList() { wordsFoundThisSession.Clear(); Debug.Log("WV: Found words list reset."); }

    // --- Main Validation Logic ---
    public void ValidateWords()
    {
        if (wordGridManager == null || wordGridManager.gridData == null || gameManager == null) { return; }
        if (gameManager.CurrentState != GameManager.GameState.Playing && gameManager.CurrentState != GameManager.GameState.Initializing) { return; }

        wordsFoundThisCycle.Clear();
        int gridSize = wordGridManager.gridSize;
        char[,] gridData = wordGridManager.gridData;

        // Check Rows (LTR)
        for (int r = 0; r < gridSize; r++)
        {
            StringBuilder rowBuilder = new StringBuilder(gridSize);
            for (int c = 0; c < gridSize; c++) { rowBuilder.Append(gridData[r, c]); }
            FindWordsInString(rowBuilder.ToString(), r, true);
        }
        // Check Columns (TTB)
        for (int c = 0; c < gridSize; c++)
        {
            StringBuilder colBuilder = new StringBuilder(gridSize);
            for (int r = 0; r < gridSize; r++) { colBuilder.Append(gridData[r, c]); }
            FindWordsInString(colBuilder.ToString(), c, false);
        }
        // Add cycle words to session list
        if (wordsFoundThisCycle.Count > 0) { foreach (string word in wordsFoundThisCycle) { wordsFoundThisSession.Add(word); } }
    }


    private void FindWordsInString(string line, int lineIndex, bool isRow)
    {
        int n = line.Length;
        for (int start = 0; start < n; start++)
        {
            for (int end = start; end < n; end++)
            {
                int len = end - start + 1;
                if (len >= minWordLength)
                {
                    string sub = line.Substring(start, len).ToUpperInvariant();
                    CheckWord(sub, lineIndex, start, isRow);
                }
            }
        }
    }

    // Calls GameManager.ProcessFoundWord
    private void CheckWord(string word, int lineIndex, int startIndexInLine, bool isRow)
    {
        if (validWords.Contains(word) && !wordsFoundThisCycle.Contains(word) && !wordsFoundThisSession.Contains(word))
        {
            wordsFoundThisCycle.Add(word); // Mark for this cycle
            List<Vector2Int> wordCoords = CalculateCoordinates(lineIndex, startIndexInLine, word.Length, isRow); // Calculate Coords ONLY
            if (gameManager != null && wordCoords != null && wordCoords.Count > 0)
            {
                gameManager.ProcessFoundWord(word, wordCoords); // Tell GM to handle it
            }
            else { Debug.LogWarning($"Could not process word {word}. GM null or failed to get coords."); }
        }
    }

    // Helper Calculates Coordinates ONLY
    private List<Vector2Int> CalculateCoordinates(int lineIndex, int startIndexInLine, int wordLength, bool isRow)
    {
        List<Vector2Int> coords = new List<Vector2Int>(wordLength);
        for (int i = 0; i < wordLength; i++)
        {
            int r, c;
            if (isRow) { r = lineIndex; c = startIndexInLine + i; }
            else { r = startIndexInLine + i; c = lineIndex; }
            Vector2Int coord = new Vector2Int(r, c);
            if (wordGridManager != null && r >= 0 && r < wordGridManager.gridSize && c >= 0 && c < wordGridManager.gridSize)
            {
                coords.Add(coord);
            }
            else { Debug.LogWarning($"CalculateCoordinates: Coord [{r},{c}] out of bounds."); }
        }
        return coords;
    }

} // End of WordValidator class