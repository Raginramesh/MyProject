using UnityEngine;
using System.Collections.Generic;
using System.Text; // For StringBuilder
using System.Linq; // For LINQ (optional, not heavily used here)
using System.IO; // For reading dictionary file (though TextAsset is used)

// --- Struct to hold found word data (can be outside or inside the class) ---
public struct FoundWordData
{
    public string Word;
    public List<Vector2Int> Coordinates;

    public FoundWordData(string word, List<Vector2Int> coordinates)
    {
        Word = word;
        Coordinates = coordinates;
    }
}


public class WordValidator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WordGridManager wordGridManager;
    [SerializeField] private GameManager gameManager; // Keep ref for state checks if needed

    [Header("Word List")]
    [SerializeField] private TextAsset wordListFile; // Assign your dictionary .txt file here
    [SerializeField] private int minWordLength = 3;

    // Internal data structures
    private HashSet<string> validWords = new HashSet<string>(); // Stores the dictionary words
    private HashSet<string> wordsFoundThisSession = new HashSet<string>(); // Tracks words already found IN THIS GAME SESSION

    // Removed: wordsFoundThisCycle - logic changed

    void Awake()
    {
        // Ensure references...
        if (wordGridManager == null) wordGridManager = FindFirstObjectByType<WordGridManager>();
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();

        if (wordGridManager == null) { Debug.LogError("WV: WordGridManager missing!", this); enabled = false; return; }
        // Note: GameManager reference might be set later via SetGameManager, so check might be less strict here.
        if (gameManager == null) { Debug.LogWarning("WV: GameManager reference not set in Awake (may be set later).", this); }
        if (wordListFile == null) { Debug.LogError("WV: Word List File missing!", this); enabled = false; return; }

        LoadWordList();
    }

    /// <summary>
    /// Allows GameManager to set its reference if not assigned in Inspector.
    /// </summary>
    public void SetGameManager(GameManager manager) { this.gameManager = manager; }

    /// <summary>
    /// Loads words from the TextAsset into the validWords HashSet.
    /// </summary>
    void LoadWordList()
    {
        validWords.Clear();
        if (wordListFile != null)
        {
            string[] lines = wordListFile.text.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
            int loadedCount = 0;
            foreach (string line in lines)
            {
                string word = line.Trim().ToUpperInvariant();
                if (word.Length >= minWordLength) { validWords.Add(word); loadedCount++; }
            }
            // Debug.Log($"WV: Loaded {loadedCount} words (min length {minWordLength}) from '{wordListFile.name}'.");
        }
        else { Debug.LogError("WV: Word list file is null!"); }
    }

    public void ResetFoundWordsList()
    {
        wordsFoundThisSession.Clear();
        // Debug.Log("WV: Found words list (session) reset.");
    }

    // --- <<< NEW Public Method >>> ---
    /// <summary>
    /// Marks a word as found for this game session. Called by GameManager AFTER processing.
    /// </summary>
    /// <param name="word">The word string (will be converted to uppercase).</param>
    public void MarkWordAsFoundInSession(string word)
    {
        if (!string.IsNullOrEmpty(word))
        {
            wordsFoundThisSession.Add(word.ToUpperInvariant());
            // Debug.Log($"WV: Marked '{word.ToUpperInvariant()}' as found in session.");
        }
    }


    public List<FoundWordData> ValidateWords()
    {
        List<FoundWordData> newlyFoundWords = new List<FoundWordData>();
        if (wordGridManager == null || wordGridManager.gridData == null || validWords == null || validWords.Count == 0) { return newlyFoundWords; }
        if (gameManager != null && gameManager.CurrentStatePublic != GameManager.GameState.Playing) { return newlyFoundWords; }

        int gridSize = wordGridManager.gridSize;
        char[,] gridData = wordGridManager.gridData;
        HashSet<string> wordsFoundThisPass = new HashSet<string>(); // Temp set for this pass only

        // Check Rows
        for (int r = 0; r < gridSize; r++)
        {
            StringBuilder rowBuilder = new StringBuilder(gridSize);
            for (int c = 0; c < gridSize; c++) { rowBuilder.Append(gridData[r, c]); }
            FindWordsInLine(rowBuilder.ToString(), r, true, newlyFoundWords, wordsFoundThisPass);
        }
        // Check Columns
        for (int c = 0; c < gridSize; c++)
        {
            StringBuilder colBuilder = new StringBuilder(gridSize);
            for (int r = 0; r < gridSize; r++) { colBuilder.Append(gridData[r, c]); }
            FindWordsInLine(colBuilder.ToString(), c, false, newlyFoundWords, wordsFoundThisPass);
        }

        // --- <<< REMOVED THIS LOOP >>> ---
        // foreach(var wordData in newlyFoundWords) {
        //     wordsFoundThisSession.Add(wordData.Word); // DO NOT ADD HERE
        // }
        // --- <<< END REMOVAL >>> ---

        // Debug.Log($"WV: Validation complete. Found {newlyFoundWords.Count} new words this pass.");
        return newlyFoundWords;
    }

    private void FindWordsInLine(string line, int lineIndex, bool isRow, List<FoundWordData> foundList, HashSet<string> foundThisPass)
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
                    // Check dictionary, session list, and current pass list
                    if (validWords.Contains(sub) && !wordsFoundThisSession.Contains(sub) && !foundThisPass.Contains(sub))
                    {
                        List<Vector2Int> wordCoords = CalculateCoordinates(lineIndex, start, len, isRow);
                        if (wordCoords != null && wordCoords.Count == len)
                        {
                            foundList.Add(new FoundWordData(sub, wordCoords));
                            foundThisPass.Add(sub); // Add to pass set to prevent duplicates *within this pass*
                        }
                    }
                }
            }
        }
    }

    private List<Vector2Int> CalculateCoordinates(int lineIndex, int startIndexInLine, int wordLength, bool isRow)
    {
        List<Vector2Int> coords = new List<Vector2Int>(wordLength);
        if (wordGridManager == null) return null;
        for (int i = 0; i < wordLength; i++)
        {
            int r, c;
            if (isRow) { r = lineIndex; c = startIndexInLine + i; }
            else { r = startIndexInLine + i; c = lineIndex; }
            if (r >= 0 && r < wordGridManager.gridSize && c >= 0 && c < wordGridManager.gridSize) { coords.Add(new Vector2Int(r, c)); }
            else { Debug.LogError($"CalculateCoordinates: Coord [{r},{c}] out of bounds!"); return null; }
        }
        return coords;
    }
} // End of WordValidator class