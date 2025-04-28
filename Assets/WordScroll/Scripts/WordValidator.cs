using UnityEngine;
using System.Collections.Generic; // For Lists and HashSet
using System.Text; // For StringBuilder
using System.Linq; // For operations like Reverse

public class WordValidator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WordGridManager wordGridManager; // Reference to access grid data
    [SerializeField] private GameManager gameManager;       // Reference to update score/state

    [Header("Word List")]
    [SerializeField] private TextAsset wordListFile; // Assign your word list .txt file here
    [SerializeField] private int minWordLength = 3;  // Minimum length of a word to be considered valid

    // Internal data structures
    private HashSet<string> validWords = new HashSet<string>(); // Fast lookup for valid words
    private HashSet<string> wordsFoundThisSession = new HashSet<string>(); // Track words found in the current game session
    private HashSet<string> wordsFoundThisCycle = new HashSet<string>(); // Track words found in the current validation check to avoid double scoring overlaps
    private List<Vector2Int> cellsToReplaceThisCycle = new List<Vector2Int>(); // Coordinates of cells to replace after validation

    void Awake()
    {
        // Ensure references are set
        if (wordGridManager == null) { Debug.LogError("WordValidator: WordGridManager reference missing!", this); enabled = false; return; }
        if (gameManager == null) { Debug.LogError("WordValidator: GameManager reference missing!", this); enabled = false; return; }
        if (wordListFile == null) { Debug.LogError("WordValidator: Word List File not assigned!", this); enabled = false; return; }

        // Load the word list
        LoadWordList();
    }

    // --- NEW: Method to allow GameManager to set its reference ---
    public void SetGameManager(GameManager manager)
    {
        this.gameManager = manager;
        if (this.gameManager != null)
        {
            Debug.Log("WordValidator: GameManager reference set via SetGameManager.", this);
        }
        else
        {
            Debug.LogWarning("WordValidator: SetGameManager called with a null manager.", this);
        }
    }
    // Loads words from the TextAsset into the HashSet
    void LoadWordList()
    {
        validWords.Clear(); // Clear previous words if any
        if (wordListFile != null)
        {
            // Split the file content into lines
            string[] lines = wordListFile.text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            int loadedCount = 0;
            foreach (string line in lines)
            {
                // Process each word: convert to uppercase and trim whitespace
                string word = line.Trim().ToUpperInvariant();
                // Add to HashSet if it meets the minimum length requirement
                if (word.Length >= minWordLength)
                {
                    validWords.Add(word);
                    loadedCount++;
                }
            }
            Debug.Log($"WordValidator: Loaded {loadedCount} words (min length {minWordLength}) from '{wordListFile.name}'. Total entries in file: {lines.Length}.");
        }
        else
        {
            Debug.LogError("WordValidator: Word list file is null, cannot load words.", this);
        }
    }

    // Resets the list of words found in the current game session
    public void ResetFoundWordsList()
    {
        wordsFoundThisSession.Clear();
        Debug.Log("WordValidator: Found words list reset for new session.");
    }

    // --- Main Validation Logic ---

    // Called by WordGridManager or GridInputHandler when validation is needed
    public void ValidateWords()
    {
        // Ensure grid data is accessible
        if (wordGridManager == null || wordGridManager.gridData == null)
        {
            Debug.LogError("WordValidator: Cannot validate, WordGridManager or gridData is null.", this);
            return;
        }

        // Clear lists for the current validation cycle
        wordsFoundThisCycle.Clear();
        cellsToReplaceThisCycle.Clear();
        // Debug.Log("WordValidator: Starting validation cycle.");

        int gridSize = wordGridManager.gridSize;
        char[,] gridData = wordGridManager.gridData;

        // --- Check Rows (Left-to-Right ONLY) ---
        for (int r = 0; r < gridSize; r++)
        {
            StringBuilder rowBuilder = new StringBuilder(gridSize);
            for (int c = 0; c < gridSize; c++)
            {
                rowBuilder.Append(gridData[r, c]);
            }
            string rowString = rowBuilder.ToString();
            // Debug.Log($"Checking Row {r} LTR: {rowString}");
            FindWordsInString(rowString, r, true); // isRow = true

            // --- REMOVED Right-to-Left Check ---
        }

        // --- Check Columns (Top-to-Bottom ONLY) ---
        for (int c = 0; c < gridSize; c++)
        {
            StringBuilder colBuilder = new StringBuilder(gridSize);
            for (int r = 0; r < gridSize; r++)
            {
                colBuilder.Append(gridData[r, c]);
            }
            string colString = colBuilder.ToString();
            // Debug.Log($"Checking Col {c} TTB: {colString}");
            FindWordsInString(colString, c, false); // isRow = false

            // --- REMOVED Bottom-to-Top Check ---
        }

        // --- Process Results of Validation Cycle ---
        if (cellsToReplaceThisCycle.Count > 0)
        {
            Debug.Log($"WordValidator: Found {wordsFoundThisCycle.Count} new word(s) this cycle. Requesting replacement for {cellsToReplaceThisCycle.Count} cells.");
            // Request the grid manager to replace the letters at the collected coordinates
            wordGridManager.ReplaceLettersAt(cellsToReplaceThisCycle);

            // Add words found this cycle to the session list
            foreach (string word in wordsFoundThisCycle)
            {
                wordsFoundThisSession.Add(word);
            }
        }
        else
        {
            // Debug.Log("WordValidator: No new words found this cycle.");
        }
    }


    // Finds valid words within a given string (representing a row or column)
    // 'index' is the row or column number, 'isRow' indicates the direction
    private void FindWordsInString(string line, int index, bool isRow)
    {
        int n = line.Length;
        // Iterate through all possible start positions
        for (int start = 0; start < n; start++)
        {
            // Iterate through all possible end positions for substrings
            for (int end = start; end < n; end++)
            {
                // Check if the substring length meets the minimum requirement
                int len = end - start + 1;
                if (len >= minWordLength)
                {
                    // Extract the substring
                    string sub = line.Substring(start, len);
                    // Check if it's a valid word according to our loaded list
                    CheckWord(sub, index, start, isRow); // Pass start position and direction
                }
            }
        }
    }

    // Checks a single potential word against the dictionary and session list
    // 'lineIndex' is the row or column number
    // 'startIndexInLine' is where the word starts within the row/column string
    // 'isRow' indicates the direction
    private void CheckWord(string word, int lineIndex, int startIndexInLine, bool isRow)
    {
        // Check if the word exists in our valid word dictionary
        if (validWords.Contains(word))
        {
            // Check if this specific word instance has ALREADY been found in THIS validation cycle
            // OR if the word has been found previously in the entire game session.
            if (!wordsFoundThisCycle.Contains(word) && !wordsFoundThisSession.Contains(word))
            {
                Debug.Log($"Word Found: {word} (Session New: {!wordsFoundThisSession.Contains(word)})");

                // Mark word as found for this cycle to prevent double-counting overlaps immediately
                wordsFoundThisCycle.Add(word);

                // Notify GameManager to update score, etc.
                if (gameManager != null)
                {
                    gameManager.AddScoreForWord(word);
                }

                // Record the grid coordinates of the cells making up this word
                RecordCellCoordinates(lineIndex, startIndexInLine, word.Length, isRow);
            }
        }
    }


    // Calculates and records the grid coordinates for each letter of a found word
    // 'lineIndex' is the row (if isRow) or column (if !isRow)
    // 'startIndexInLine' is where the word starts within that row/column string
    // 'wordLength' is the length of the found word
    // 'isRow' indicates if the word was found horizontally
    private void RecordCellCoordinates(int lineIndex, int startIndexInLine, int wordLength, bool isRow)
    {
        // Debug.Log($"Recording coordinates for word at {(isRow ? "Row" : "Col")} {lineIndex}, StartIndex {startIndexInLine}, Length {wordLength}");

        for (int i = 0; i < wordLength; i++)
        {
            Vector2Int coord;
            if (isRow) // Word is horizontal (Left-to-Right)
            {
                int col = startIndexInLine + i;
                coord = new Vector2Int(lineIndex, col); // (row, col)
            }
            else // Word is vertical (Top-to-Bottom)
            {
                int row = startIndexInLine + i;
                coord = new Vector2Int(row, lineIndex); // (row, col)
            }

            // --- REMOVED Reversed Calculation Logic ---

            // Add coordinate to the list for replacement, ensuring no duplicates within this cycle
            if (!cellsToReplaceThisCycle.Contains(coord))
            {
                cellsToReplaceThisCycle.Add(coord);
                // Debug.Log($"  - Added cell coordinate: {coord}");
            }
        }
    }
}