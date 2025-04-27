using UnityEngine;
using System.Collections.Generic;
using System.IO; // Required for reading the word list file
using System.Linq; // Required for HashSet and Reverse()
using DG.Tweening; // Add DOTween for potential visual feedback

public class WordValidator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WordGridManager wordGridManager;
    [SerializeField] private GameManager gameManager; // Reference to update score

    [Header("Word List")]
    [SerializeField] private TextAsset wordListFile; // Assign your word list TXT file here
    [SerializeField] private int minWordLength = 3; // Minimum length for a word to be valid

    // Using HashSet for efficient word lookups (O(1) average time complexity)
    private HashSet<string> validWords;

    // Keep track of words found in the current grid state to avoid duplicates per validation cycle
    private HashSet<string> wordsFoundThisCycle;

    // --- NEW: Keep track of cells to replace ---
    // List of (row, column) coordinates
    private List<Vector2Int> cellsToReplaceThisCycle;

    void Awake()
    {
        // Validate references
        if (wordGridManager == null)
        {
            Debug.LogError("WordValidator: WordGridManager reference not set in Inspector!", this);
            enabled = false; return;
        }
        // GameManager reference check will be done later as it might be set via SetGameManager

        // Load the dictionary
        LoadWordList();

        // Initialize the list to store cell coordinates for replacement
        cellsToReplaceThisCycle = new List<Vector2Int>();
    }

    void Start()
    {
        // Ensure GameManager reference is set if not done by Awake or SetGameManager
        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>(); // Attempt to find it
            if (gameManager == null)
            {
                Debug.LogError("WordValidator: GameManager reference could not be found!", this);
                // Depending on your game, you might want to disable the validator if GM is missing
                // enabled = false;
                // return;
            }
            else
            {
                Debug.Log("WordValidator: Found GameManager in Start.", this);
            }
        }
    }

    // Allows GameManager to set its reference during initialization phase
    public void SetGameManager(GameManager manager)
    {
        gameManager = manager;
        if (gameManager != null) Debug.Log("WordValidator: GameManager reference set via SetGameManager.", this);
    }


    // Loads the word list from the assigned TextAsset into the HashSet
    void LoadWordList()
    {
        if (wordListFile == null)
        {
            Debug.LogError("WordValidator: Word List File not assigned in Inspector! Cannot validate words.", this);
            validWords = new HashSet<string>(); // Initialize empty to prevent null reference errors later
            enabled = false; // Disable script if word list is missing
            return;
        }

        // Initialize the HashSet, using case-insensitive comparison
        validWords = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        // Read all lines from the TextAsset efficiently
        try
        {
            using (StringReader reader = new StringReader(wordListFile.text))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string word = line.Trim(); // Remove leading/trailing whitespace
                    // Add only words that meet the minimum length requirement
                    if (word.Length >= minWordLength)
                    {
                        validWords.Add(word);
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"WordValidator: Error reading word list file '{wordListFile.name}'. Exception: {ex.Message}", this);
            validWords = new HashSet<string>(); // Ensure it's initialized even on error
            enabled = false;
            return;
        }


        if (validWords.Count > 0)
        {
            Debug.Log($"WordValidator: Loaded {validWords.Count} words (min length {minWordLength}) from '{wordListFile.name}'.", this);
        }
        else
        {
            Debug.LogWarning($"WordValidator: No words loaded from '{wordListFile.name}' or none met the minimum length of {minWordLength}. Validation might not work.", this);
            // Consider disabling if no words loaded?
            // enabled = false;
        }
    }

    // Main validation method called externally (e.g., by WordGridManager after scroll)
    public void ValidateWords()
    {
        // Pre-checks for essential components and data
        if (wordGridManager == null || wordGridManager.gridData == null)
        {
            Debug.LogError("WordValidator: Cannot validate, WordGridManager or its gridData is null.", this);
            return;
        }
        if (validWords == null || validWords.Count == 0)
        {
            Debug.LogWarning("WordValidator: Cannot validate, word list is not loaded or empty.", this);
            return;
        }

        // Reset tracking sets for this validation pass
        wordsFoundThisCycle = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        cellsToReplaceThisCycle.Clear(); // Clear list of cells to replace

        int gridSize = wordGridManager.gridSize;
        Debug.Log("WordValidator: Starting word validation cycle.", this);

        // --- Check Horizontally (Forward and Backward) ---
        for (int r = 0; r < gridSize; r++)
        {
            // Build the forward string for the row
            string rowStringForward = "";
            for (int c = 0; c < gridSize; c++)
            {
                rowStringForward += wordGridManager.gridData[r, c];
            }
            // Check for words in the forward string
            FindWordsInString(rowStringForward, r, -1, true, false);
            // Check for words in the reversed string
            FindWordsInString(new string(rowStringForward.Reverse().ToArray()), r, -1, true, true);
        }

        // --- Check Vertically (Forward and Backward) ---
        for (int c = 0; c < gridSize; c++)
        {
            // Build the forward string for the column (top-to-bottom)
            string colStringForward = "";
            for (int r = 0; r < gridSize; r++)
            {
                colStringForward += wordGridManager.gridData[r, c];
            }
            // Check for words in the forward string (downwards)
            FindWordsInString(colStringForward, -1, c, false, false);
            // Check for words in the reversed string (upwards)
            FindWordsInString(new string(colStringForward.Reverse().ToArray()), -1, c, false, true);
        }

        // --- Optional: Diagonal Checks ---
        // If implementing diagonals, add similar loops here, being careful with coordinate calculation.

        // Log results of the validation cycle
        Debug.Log($"WordValidator: Finished validation cycle. Found {wordsFoundThisCycle.Count} new valid words. Cells to replace: {cellsToReplaceThisCycle.Count}", this);

        // --- Trigger Replacement in WordGridManager ---
        // If any cells were marked for replacement, tell the grid manager
        if (cellsToReplaceThisCycle.Count > 0 && wordGridManager != null)
        {
            // You can choose to replace immediately or use a coroutine for delay/effects
            // Immediate replacement:
            wordGridManager.ReplaceLettersAt(cellsToReplaceThisCycle);

            // Coroutine for delayed replacement (Uncomment if needed):
            // StartCoroutine(ReplaceCellsAfterDelay(new List<Vector2Int>(cellsToReplaceThisCycle), 0.2f)); // Pass a copy
        }
        // If no cells to replace, the cycle ends here.
    }

    // Helper method to find words within a single line (row or column, forward or reversed)
    // Added 'isReversed' flag to help with coordinate calculation later
    void FindWordsInString(string line, int rowIndex, int colIndex, bool isHorizontal, bool isReversed)
    {
        // Iterate through all possible substring lengths (from min length up to full line)
        for (int len = minWordLength; len <= line.Length; len++)
        {
            // Iterate through all possible starting positions for a substring of length 'len'
            for (int i = 0; i <= line.Length - len; i++)
            {
                string sub = line.Substring(i, len);
                // Check if this substring is a valid word and handle scoring/replacement marking
                CheckWord(sub, i, len, rowIndex, colIndex, isHorizontal, isReversed);
            }
        }
    }


    // Checks a specific substring against the dictionary and manages scoring/replacement marking
    // Added 'isReversed' flag
    void CheckWord(string word, int subStartIndex, int length, int rowIndex, int colIndex, bool isHorizontal, bool isReversed)
    {
        // Check if the substring exists in our loaded dictionary
        if (validWords.Contains(word))
        {
            // Check if this word was *already found and processed in this specific validation cycle*
            // .Add() returns true if the word was not already in the set for this cycle
            if (wordsFoundThisCycle.Add(word))
            {
                // --- VALID WORD FOUND! ---
                Debug.Log($"Valid word found: '{word}' (Length: {length})", this);

                // --- Calculate Score ---
                int scoreForThisWord = CalculateScore(word);

                // --- Update Game Manager's Score ---
                if (gameManager != null)
                {
                    gameManager.AddScore(scoreForThisWord);
                    // Optional: More detailed log including current total score
                    // Debug.Log($"Added {scoreForThisWord} points for '{word}'. Current Total Score: {gameManager.Score}", this);
                }
                else
                {
                    Debug.LogWarning($"Valid word '{word}' found, but GameManager reference is missing. Cannot add score.", this);
                }

                // --- Record Cell Coordinates for Replacement ---
                // Determine the actual grid coordinates (r, c) for each letter in the found word
                RecordCellCoordinates(word, subStartIndex, length, rowIndex, colIndex, isHorizontal, isReversed);

                // --- Optional: Trigger Visual Feedback ---
                // You could add highlighting or particle effects here, potentially using the coordinates
                // HighlightCells(word, subStartIndex, length, rowIndex, colIndex, isHorizontal, isReversed); // Example call
            }
            // else: Word was already found in this cycle (e.g., "CAT" in "CATAT"), ignore duplicate score/replacement marking.
        }
        // else: The substring 'word' is not in the validWords dictionary.
    }

    // --- NEW: Method to Calculate and Store Cell Coordinates ---
    // Determines the (row, column) for each letter of the found word and adds them to cellsToReplaceThisCycle
    void RecordCellCoordinates(string word, int subStartIndex, int length, int rowIndex, int colIndex, bool isHorizontal, bool isReversed)
    {
        // Log entry for debugging coordinate calculation
        Debug.Log($"Recording cells for '{word}'. SubStartIdx:{subStartIndex}, Len:{length}, Row:{rowIndex}, Col:{colIndex}, Horiz:{isHorizontal}, Rev:{isReversed}");

        int gridSize = wordGridManager.gridSize; // Get grid dimensions for bounds and reverse calculation

        // Iterate through each letter index within the found word (0 to length-1)
        for (int i = 0; i < length; i++)
        {
            int r, c; // Variables to store the calculated row and column

            // Calculate coordinates based on orientation and direction
            if (isHorizontal)
            {
                r = rowIndex; // Row is constant for horizontal words
                if (isReversed)
                {
                    // Word was found in the REVERSED row string.
                    // 'subStartIndex' is the index in the reversed string.
                    // The original column index is calculated from the right edge of the grid.
                    c = gridSize - 1 - (subStartIndex + i);
                }
                else
                {
                    // Word found in the FORWARD row string.
                    // 'subStartIndex' is the starting column index.
                    c = subStartIndex + i;
                }
            }
            else // Vertical
            {
                c = colIndex; // Column is constant for vertical words
                if (isReversed)
                {
                    // Word was found in the REVERSED column string (read bottom-up).
                    // 'subStartIndex' is the index in the reversed string.
                    // The original row index is calculated from the bottom edge of the grid.
                    r = gridSize - 1 - (subStartIndex + i);
                }
                else
                {
                    // Word found in the FORWARD column string (read top-down).
                    // 'subStartIndex' is the starting row index.
                    r = subStartIndex + i;
                }
            }

            // --- Bounds Check ---
            // Verify that the calculated coordinates are within the valid grid range
            if (r >= 0 && r < gridSize && c >= 0 && c < gridSize)
            {
                Vector2Int cellCoord = new Vector2Int(r, c);
                // --- Avoid Duplicates ---
                // Check if this specific cell coordinate has already been added in THIS validation cycle
                // (e.g., from an overlapping word like 'CAR' and 'ART')
                if (!cellsToReplaceThisCycle.Contains(cellCoord))
                {
                    cellsToReplaceThisCycle.Add(cellCoord);
                    // Optional log for tracing which cells are added
                    // Debug.Log($"  -> Added cell ({r}, {c}) to replace list.");
                }
                // else { Debug.Log($"  -> Cell ({r}, {c}) already in replace list for this cycle."); }
            }
            else
            {
                // Log an error if calculation results in out-of-bounds coordinates - indicates a logic flaw
                Debug.LogError($"Calculated invalid cell coordinate ({r}, {c}) for word '{word}'! Check RecordCellCoordinates logic.", this);
            }
        }
    }


    // --- Scoring Logic ---
    // Calculates the score for a given word. Can be expanded later.
    int CalculateScore(string word)
    {
        // Simple scoring: Score = Length of the word
        int score = word.Length;

        // Potential Future Enhancements:
        // - Add points based on letter values (Scrabble style)
        // - Add bonus for longer words (e.g., score = length * length)
        // - Add bonus for using specific "bonus" tiles (if implemented)

        return score;
    }


    // --- Optional: Coroutine for Delayed Replacement ---
    // Use this if you want visual effects (like highlighting) to finish before letters are replaced.
    /*
    System.Collections.IEnumerator ReplaceCellsAfterDelay(List<Vector2Int> cells, float delay)
    {
        Debug.Log($"Starting delay of {delay}s before replacing {cells.Count} cells.");

        // --- Optional: Add Highlighting or other effects HERE ---
        // HighlightCells(cells); // You would need to create this method

        yield return new WaitForSeconds(delay); // Wait for the specified duration

        Debug.Log("Delay finished. Requesting replacement.");
        if (wordGridManager != null)
        {
            wordGridManager.ReplaceLettersAt(cells); // Call the replacement method in WordGridManager
        }
        else {
             Debug.LogError("Cannot replace cells after delay: WordGridManager reference is missing!", this);
        }
    }
    */

    // --- TODO: Placeholder for Highlighting Logic ---
    // Create methods here if you want to visually highlight the found words or cells being replaced.
    /*
    void HighlightCells(string word, int subStartIndex, int length, int rowIndex, int colIndex, bool isHorizontal, bool isReversed) { ... }
    void HighlightCells(List<Vector2Int> cellsToHighlight) { ... }
    */

} // End of WordValidator class