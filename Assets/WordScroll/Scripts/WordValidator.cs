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

    public enum WordOrientation { Horizontal, Vertical, SingleLetter }
    public WordOrientation GetOrientation()
    {
        if (Coordinates == null || Coordinates.Count == 0) return WordOrientation.SingleLetter; // Should not happen for valid words
        if (Coordinates.Count == 1) return WordOrientation.SingleLetter;
        return (Coordinates[1].x == Coordinates[0].x) ? WordOrientation.Vertical : WordOrientation.Horizontal;
    }
}


public class WordValidator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WordGridManager wordGridManager;
    [SerializeField] private GameManager gameManager; // Assuming GameManager reference is available

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
        // gameManager might be set later or not strictly needed in WordValidator's Awake if circular dependency is an issue
        if (wordListFile == null) { Debug.LogError("WV: Word List File missing!", this); enabled = false; return; }

        if (minWordLength > maxWordLength && maxWordLength > 0)
        {
            Debug.LogWarning($"WordValidator: minWordLength ({minWordLength}) is greater than maxWordLength ({maxWordLength}). Adjusting minWordLength to be equal to maxWordLength.", this);
            minWordLength = maxWordLength;
        }
        if (maxWordLength <= 0 && wordGridManager != null && wordGridManager.gridSize > 0) // Check gridSize > 0
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
                // Filter by length during loading as well
                if (word.Length >= minWordLength && word.Length <= maxWordLength)
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

    // NEW METHOD: IsWordFoundThisSession
    public bool IsWordFoundThisSession(string word)
    {
        if (string.IsNullOrEmpty(word)) return false;
        return wordsFoundThisSession.Contains(word.ToUpperInvariant());
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

                // Check if already found this session BEFORE adding to potential list for this pass
                if (validWordsDictionary.Contains(sub) && !IsWordFoundThisSession(sub))
                {
                    List<Vector2Int> wordCoords = CalculateCoordinates(lineIndex, start, len, isRow);
                    if (wordCoords != null && wordCoords.Count == len)
                    {
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

        var uniqueEntries = new HashSet<string>();
        List<FoundWordData> uniqueWordDataList = new List<FoundWordData>();

        foreach (var wd in allFoundWords)
        {
            StringBuilder coordString = new StringBuilder();
            // Sort coordinates for a consistent signature
            foreach (var coord in wd.Coordinates.OrderBy(c => c.x).ThenBy(c => c.y))
            {
                coordString.Append($"({coord.x},{coord.y})");
            }
            string signature = $"{wd.Word}_{coordString.ToString()}";
            if (uniqueEntries.Add(signature))
            {
                uniqueWordDataList.Add(wd);
            }
        }

        uniqueWordDataList.Sort((a, b) => {
            int lengthComparison = b.Word.Length.CompareTo(a.Word.Length);
            if (lengthComparison != 0) return lengthComparison;
            // Tie-breaking for consistent order (e.g. by first coordinate)
            if (a.Coordinates.Count > 0 && b.Coordinates.Count > 0)
            {
                int rCompare = a.Coordinates[0].x.CompareTo(b.Coordinates[0].x);
                if (rCompare != 0) return rCompare;
                return a.Coordinates[0].y.CompareTo(b.Coordinates[0].y);
            }
            return 0;
        });

        List<FoundWordData> finalList = new List<FoundWordData>();
        foreach (var currentWordData in uniqueWordDataList)
        {
            bool isSubWordOfAlreadyAdded = false;
            foreach (var addedWordData in finalList)
            {
                if (addedWordData.Word.Length > currentWordData.Word.Length &&
                    addedWordData.Word.StartsWith(currentWordData.Word) &&
                    currentWordData.Coordinates.Count > 0 && addedWordData.Coordinates.Count > 0 &&
                    currentWordData.Coordinates[0] == addedWordData.Coordinates[0] &&
                    currentWordData.GetOrientation() == addedWordData.GetOrientation())
                {
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

    // NEW METHOD: CheckIntersection
    public bool CheckIntersection(FoundWordData wordA, FoundWordData wordB, out Vector2Int sharedCell)
    {
        sharedCell = new Vector2Int(-1, -1);
        if (wordA.ID == wordB.ID || wordA.Coordinates == null || wordB.Coordinates == null)
        {
            return false;
        }

        FoundWordData.WordOrientation orientationA = wordA.GetOrientation();
        FoundWordData.WordOrientation orientationB = wordB.GetOrientation();

        // Must be on opposite axes (one horizontal, one vertical)
        // Single letter words cannot be an "axis" for this rule.
        if (orientationA == FoundWordData.WordOrientation.SingleLetter ||
            orientationB == FoundWordData.WordOrientation.SingleLetter ||
            orientationA == orientationB)
        {
            return false;
        }

        List<Vector2Int> sharedCoords = new List<Vector2Int>();
        foreach (var coordA in wordA.Coordinates)
        {
            if (wordB.Coordinates.Contains(coordA))
            {
                sharedCoords.Add(coordA);
            }
        }

        if (sharedCoords.Count == 1)
        {
            sharedCell = sharedCoords[0];
            return true;
        }

        return false;
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
            else
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
                Debug.LogError($"CalculateCoordinates: Coordinate [{r},{c}] out of bounds. Word: line {lineIndex}, start {startIndexInLine}, length {wordLength}, isRow {isRow}", this);
                return null;
            }
        }
        return coords;
    }
}