using UnityEngine;
using System.Collections.Generic;
using System.Text; // For StringBuilder
using System.Linq;
using System.IO;

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

    // Optional: Add an orientation property for easier filtering
    public enum WordOrientation { Horizontal, Vertical, SingleLetter }
    public WordOrientation GetOrientation()
    {
        if (Coordinates == null || Coordinates.Count <= 1) return WordOrientation.SingleLetter;
        return (Coordinates[1].x == Coordinates[0].x) ? WordOrientation.Vertical : WordOrientation.Horizontal;
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
    [SerializeField] private int minWordLength = 3;
    [Tooltip("Maximum length of a word to be considered valid. Set to grid size or higher for no effective upper limit beyond grid dimensions.")]
    [SerializeField] private int maxWordLength = 10;

    private HashSet<string> validWordsDictionary = new HashSet<string>();
    private HashSet<string> wordsFoundThisSession = new HashSet<string>();

    void Awake()
    {
        if (wordGridManager == null) wordGridManager = FindFirstObjectByType<WordGridManager>();
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();

        if (wordGridManager == null) { Debug.LogError("WV: WordGridManager missing!", this); enabled = false; return; }
        if (gameManager == null) { Debug.LogWarning("WV: GameManager reference not set in Awake (may be set later).", this); }
        if (wordListFile == null) { Debug.LogError("WV: Word List File missing!", this); enabled = false; return; }

        if (minWordLength > maxWordLength && maxWordLength > 0)
        {
            Debug.LogWarning($"WordValidator: minWordLength ({minWordLength}) is greater than maxWordLength ({maxWordLength}). Adjusting minWordLength to be equal to maxWordLength.", this);
            minWordLength = maxWordLength;
        }
        if (maxWordLength <= 0 && wordGridManager != null)
        {
            maxWordLength = wordGridManager.gridSize;
        }
        else if (maxWordLength <= 0)
        {
            maxWordLength = 10; // Fallback
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
            foreach (string line in lines)
            {
                string word = line.Trim().ToUpperInvariant();
                if (word.Length >= minWordLength && word.Length <= maxWordLength) // Consider maxWordLength here too
                {
                    validWordsDictionary.Add(word);
                }
            }
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

    public List<FoundWordData> FindAllPotentialWords()
    {
        List<FoundWordData> potentialWords = new List<FoundWordData>();
        if (wordGridManager == null || wordGridManager.gridData == null || validWordsDictionary == null || validWordsDictionary.Count == 0)
        {
            return potentialWords;
        }

        int currentGridSize = wordGridManager.gridSize;
        char[,] gridData = wordGridManager.gridData;
        int effectiveMaxSearchLength = Mathf.Min(maxWordLength, currentGridSize);

        // Temporary list to gather all words before filtering
        List<FoundWordData> allFoundRaw = new List<FoundWordData>();

        for (int r = 0; r < currentGridSize; r++)
        {
            StringBuilder rowBuilder = new StringBuilder(currentGridSize);
            for (int c = 0; c < currentGridSize; c++) { rowBuilder.Append(gridData[r, c]); }
            FindWordsInLine(rowBuilder.ToString(), r, true, allFoundRaw, effectiveMaxSearchLength);
        }

        for (int c = 0; c < currentGridSize; c++)
        {
            StringBuilder colBuilder = new StringBuilder(currentGridSize);
            for (int r = 0; r < currentGridSize; r++) { colBuilder.Append(gridData[r, c]); }
            FindWordsInLine(colBuilder.ToString(), c, false, allFoundRaw, effectiveMaxSearchLength);
        }

        // Filter out duplicates based on exact coordinates and word (e.g. single letter words found in both passes)
        // And then filter sub-words
        return FilterSubWordsAndDuplicates(allFoundRaw);
    }

    private void FindWordsInLine(string line, int lineIndex, bool isRow, List<FoundWordData> foundList, int effectiveMaxLen)
    {
        int n = line.Length;
        for (int start = 0; start < n; start++)
        {
            for (int len = minWordLength; len <= effectiveMaxLen; len++)
            {
                if (start + len > n) break;

                string sub = line.Substring(start, len).ToUpperInvariant();

                if (validWordsDictionary.Contains(sub) && !wordsFoundThisSession.Contains(sub))
                {
                    List<Vector2Int> wordCoords = CalculateCoordinates(lineIndex, start, len, isRow);
                    if (wordCoords != null && wordCoords.Count == len)
                    {
                        // Add without the complex duplicate check here; we'll handle it in FilterSubWordsAndDuplicates
                        foundList.Add(new FoundWordData(sub, wordCoords));
                    }
                }
            }
        }
    }

    private List<FoundWordData> FilterSubWordsAndDuplicates(List<FoundWordData> allFoundWords)
    {
        if (allFoundWords == null || allFoundWords.Count == 0)
            return new List<FoundWordData>();

        // 1. Remove exact duplicates (same word, same coordinates)
        // Using a HashSet of a string representation of the word and its coordinates
        var uniqueEntries = new HashSet<string>();
        List<FoundWordData> uniqueWordDataList = new List<FoundWordData>();

        foreach (var wd in allFoundWords)
        {
            StringBuilder coordString = new StringBuilder();
            foreach (var coord in wd.Coordinates.OrderBy(c => c.x).ThenBy(c => c.y)) // Consistent ordering
            {
                coordString.Append($"({coord.x},{coord.y})");
            }
            string signature = $"{wd.Word}_{coordString.ToString()}";
            if (uniqueEntries.Add(signature))
            {
                uniqueWordDataList.Add(wd);
            }
        }

        // 2. Filter sub-words: Prioritize longer words
        // Sort by length descending.
        uniqueWordDataList.Sort((a, b) => {
            int lengthComparison = b.Word.Length.CompareTo(a.Word.Length);
            if (lengthComparison != 0) return lengthComparison;
            // Optional: further sort by coordinates if lengths are equal, for deterministic behavior
            // For now, existing order for equal lengths is fine.
            return 0;
        });

        List<FoundWordData> finalList = new List<FoundWordData>();
        foreach (var currentWordData in uniqueWordDataList)
        {
            bool isSubWordOfAlreadyAdded = false;
            foreach (var addedWordData in finalList)
            {
                // Check if currentWordData is a sub-word of addedWordData
                // and they share the same starting cell and orientation.
                if (addedWordData.Word.Length > currentWordData.Word.Length &&
                    addedWordData.Word.StartsWith(currentWordData.Word) &&
                    currentWordData.Coordinates.Count > 0 && addedWordData.Coordinates.Count > 0 &&
                    currentWordData.Coordinates[0] == addedWordData.Coordinates[0] && // Same start coordinate
                    currentWordData.GetOrientation() == addedWordData.GetOrientation()) // Same orientation
                {
                    // To be a true sub-word in this context, all coordinates of currentWordData
                    // must be part of addedWordData's initial coordinates.
                    bool allCoordsMatchPrefix = true;
                    for (int i = 0; i < currentWordData.Coordinates.Count; i++)
                    {
                        if (currentWordData.Coordinates[i] != addedWordData.Coordinates[i])
                        {
                            allCoordsMatchPrefix = false;
                            break;
                        }
                    }
                    if (allCoordsMatchPrefix)
                    {
                        isSubWordOfAlreadyAdded = true;
                        break;
                    }
                }
            }

            if (!isSubWordOfAlreadyAdded)
            {
                finalList.Add(currentWordData);
            }
        }
        return finalList;
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
                Debug.LogError($"CalculateCoordinates: Coordinate [{r},{c}] out of bounds for grid size {currentGridSize}. Word: line {lineIndex}, start {startIndexInLine}, length {wordLength}, isRow {isRow}", this);
                return null;
            }
        }
        return coords;
    }
}