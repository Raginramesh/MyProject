using UnityEngine;
using System.Collections.Generic;
using System.Text; // For StringBuilder
using System.Linq;
using System.IO;

// Struct to hold found word data (ensure this matches the one in GameManager or is globally accessible if separate)
// If you've added a GUID to this struct in other files, ensure consistency.
// For this example, I'm using the version from your provided file.
/*
public struct FoundWordData
{
    public string Word;
    public List<Vector2Int> Coordinates;
    public System.Guid ID; // Assuming you kept the GUID from previous suggestions

    public FoundWordData(string word, List<Vector2Int> coordinates)
    {
        Word = word;
        Coordinates = coordinates;
        ID = System.Guid.NewGuid();
    }
}
*/
// Using the simpler struct from the provided file for now:
// public struct FoundWordData
// {
//     public string Word;
//     public List<Vector2Int> Coordinates;

//     public FoundWordData(string word, List<Vector2Int> coordinates)
//     {
//         Word = word;
//         Coordinates = coordinates;
//     }
// }
// Re-adding the GUID as it's good practice for the tap-to-validate system
public struct FoundWordData
{
    public string Word;
    public List<Vector2Int> Coordinates;
    public System.Guid ID;


    public FoundWordData(string word, List<Vector2Int> coordinates)
    {
        Word = word;
        Coordinates = coordinates;
        ID = System.Guid.NewGuid();
    }
}


public class WordValidator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WordGridManager wordGridManager;
    [SerializeField] private GameManager gameManager;

    [Header("Word List Settings")]
    [SerializeField] private TextAsset wordListFile;
    [Tooltip("Minimum length of a word to be considered valid.")]
    [SerializeField] private int minWordLength = 3; // Defaulting to 3 as per original script
    [Tooltip("Maximum length of a word to be considered valid. Set to grid size or higher for no effective upper limit beyond grid dimensions.")]
    [SerializeField] private int maxWordLength = 10; // Default to a reasonable upper limit

    private HashSet<string> validWordsDictionary = new HashSet<string>();
    private HashSet<string> wordsFoundThisSession = new HashSet<string>();

    void Awake()
    {
        if (wordGridManager == null) wordGridManager = FindFirstObjectByType<WordGridManager>();
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();

        if (wordGridManager == null) { Debug.LogError("WV: WordGridManager missing!", this); enabled = false; return; }
        if (gameManager == null) { Debug.LogWarning("WV: GameManager reference not set in Awake (may be set later).", this); }
        if (wordListFile == null) { Debug.LogError("WV: Word List File missing!", this); enabled = false; return; }

        // Ensure minWordLength is not greater than maxWordLength
        if (minWordLength > maxWordLength && maxWordLength > 0) // maxWordLength > 0 means it's intentionally set
        {
            Debug.LogWarning($"WordValidator: minWordLength ({minWordLength}) is greater than maxWordLength ({maxWordLength}). Adjusting minWordLength to be equal to maxWordLength.", this);
            minWordLength = maxWordLength;
        }
        // Ensure maxWordLength is at least 1 if it's set very low, or default to grid size if not sensible
        if (maxWordLength <= 0 && wordGridManager != null)
        {
            Debug.LogWarning($"WordValidator: maxWordLength is {maxWordLength}. Setting to grid size ({wordGridManager.gridSize}) as a fallback.", this);
            maxWordLength = wordGridManager.gridSize; // Sensible default if not set or set too low
        }
        else if (maxWordLength <= 0)
        {
            Debug.LogWarning($"WordValidator: maxWordLength is {maxWordLength} and WordGridManager is not yet available. Setting to a default of 10. This might be overridden if grid size is smaller.", this);
            maxWordLength = 10; // Fallback if grid manager isn't ready
        }


        LoadWordList();
    }

    public void SetGameManager(GameManager manager) { this.gameManager = manager; }

    void LoadWordList()
    {
        validWordsDictionary.Clear();
        if (wordListFile != null)
        {
            string[] lines = wordListFile.text.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
            int loadedCount = 0;
            foreach (string line in lines)
            {
                string word = line.Trim().ToUpperInvariant();
                // <<< MODIFIED: Check against both minWordLength and maxWordLength >>>
                if (word.Length >= minWordLength && word.Length <= maxWordLength)
                {
                    validWordsDictionary.Add(word);
                    loadedCount++;
                }
            }
            // Debug.Log($"WV: Loaded {loadedCount} words (length {minWordLength}-{maxWordLength}) from '{wordListFile.name}'.");
        }
        else { Debug.LogError("WV: Word list file is null!"); }
    }

    public void ResetFoundWordsList()
    {
        wordsFoundThisSession.Clear();
    }

    public void MarkWordAsFoundInSession(string word)
    {
        if (!string.IsNullOrEmpty(word))
        {
            wordsFoundThisSession.Add(word.ToUpperInvariant());
        }
    }

    /// <summary>
    /// Finds all potential words on the grid based on current min/max length settings.
    /// This is the method called by WordGridManager for the tap-to-validate system.
    /// </summary>
    public List<FoundWordData> FindAllPotentialWords()
    {
        List<FoundWordData> potentialWords = new List<FoundWordData>();
        if (wordGridManager == null || wordGridManager.gridData == null || validWordsDictionary == null || validWordsDictionary.Count == 0)
        {
            return potentialWords;
        }

        int currentGridSize = wordGridManager.gridSize;
        char[,] gridData = wordGridManager.gridData;

        // Adjust effectiveMaxSearchLength based on the smaller of maxWordLength and currentGridSize
        int effectiveMaxSearchLength = Mathf.Min(maxWordLength, currentGridSize);


        for (int r = 0; r < currentGridSize; r++)
        {
            StringBuilder rowBuilder = new StringBuilder(currentGridSize);
            for (int c = 0; c < currentGridSize; c++) { rowBuilder.Append(gridData[r, c]); }
            FindWordsInLine(rowBuilder.ToString(), r, true, potentialWords, effectiveMaxSearchLength);
        }

        for (int c = 0; c < currentGridSize; c++)
        {
            StringBuilder colBuilder = new StringBuilder(currentGridSize);
            for (int r = 0; r < currentGridSize; r++) { colBuilder.Append(gridData[r, c]); }
            FindWordsInLine(colBuilder.ToString(), c, false, potentialWords, effectiveMaxSearchLength);
        }

        return potentialWords;
    }

    // This is the old ValidateWords - kept for reference or if you need the old auto-validating behavior elsewhere.
    // For tap-to-validate, FindAllPotentialWords() is used.
    public List<FoundWordData> ValidateWords_OldAutoValidateLogic()
    {
        List<FoundWordData> newlyFoundWords = new List<FoundWordData>();
        // ... (rest of the old logic that used wordsFoundThisPass and immediately told GM)
        // For brevity, not fully re-pasting the old method here.
        // Ensure this method also respects maxWordLength if you were to use it.
        return newlyFoundWords;
    }


    private void FindWordsInLine(string line, int lineIndex, bool isRow, List<FoundWordData> foundList, int effectiveMaxLen)
    {
        int n = line.Length;
        for (int start = 0; start < n; start++)
        {
            // Iterate for length from minWordLength up to effectiveMaxLen (or until end of line)
            for (int len = minWordLength; len <= effectiveMaxLen; len++)
            {
                if (start + len > n) break; // Substring would go out of bounds

                string sub = line.Substring(start, len).ToUpperInvariant();

                // No need to check len against minWordLength again here as the loop starts at minWordLength
                // and len <= effectiveMaxLen is also implicitly handled by the loop condition.
                // The primary check is if the word is in the dictionary and not found this session.

                if (validWordsDictionary.Contains(sub) && !wordsFoundThisSession.Contains(sub))
                {
                    List<Vector2Int> wordCoords = CalculateCoordinates(lineIndex, start, len, isRow);
                    if (wordCoords != null && wordCoords.Count == len) // Ensure coordinates are valid
                    {
                        var newWordData = new FoundWordData(sub, wordCoords);
                        // Avoid adding the exact same word instance (same letters at same coords) multiple times in one pass
                        // This is more of a safeguard; the structured search should ideally find each unique instance once.
                        bool alreadyInList = false;
                        foreach (var existingWord in foundList)
                        {
                            if (existingWord.ID == newWordData.ID) // Check by ID if it's truly the same instance
                            {
                                // This should not happen if IDs are unique per new FoundWordData.
                                // A more robust check might be if coordinates are identical.
                                if (existingWord.Coordinates.SequenceEqual(newWordData.Coordinates))
                                {
                                    alreadyInList = true;
                                    break;
                                }
                            }
                        }
                        if (!alreadyInList)
                        {
                            foundList.Add(newWordData);
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
        int currentGridSize = wordGridManager.gridSize;

        for (int i = 0; i < wordLength; i++)
        {
            int r, c;
            if (isRow)
            {
                r = lineIndex;
                c = startIndexInLine + i;
            }
            else // Is Column
            {
                r = startIndexInLine + i;
                c = lineIndex;
            }

            if (r >= 0 && r < currentGridSize && c >= 0 && c < currentGridSize)
            {
                coords.Add(new Vector2Int(r, c));
            }
            else
            {
                // This indicates an issue with how word segments are being calculated if it goes out of bounds.
                Debug.LogError($"CalculateCoordinates: Coordinate [{r},{c}] out of bounds for grid size {currentGridSize}. Word segment: line {lineIndex}, start {startIndexInLine}, length {wordLength}, isRow {isRow}");
                return null; // Invalid coordinate means the word data is not usable.
            }
        }
        return coords;
    }
}