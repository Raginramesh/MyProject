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
        Coordinates = coordinates; // Should be ordered by selection/formation
        ID = System.Guid.NewGuid();
    }

    public enum WordOrientation { Horizontal, Vertical, SingleLetter, Undefined } // Added Undefined
    public WordOrientation GetOrientation()
    {
        if (Coordinates == null || Coordinates.Count == 0) return WordOrientation.Undefined;
        if (Coordinates.Count == 1) return WordOrientation.SingleLetter;

        // Check if all X are same (Vertical) or all Y are same (Horizontal)
        bool allXSame = true;
        bool allYSame = true;
        for (int i = 1; i < Coordinates.Count; i++)
        {
            if (Coordinates[i].x != Coordinates[0].x) allXSame = false;
            if (Coordinates[i].y != Coordinates[0].y) allYSame = false;
        }

        if (allXSame && !allYSame) return WordOrientation.Vertical;
        if (allYSame && !allXSame) return WordOrientation.Horizontal;

        // If coordinates are not strictly linear (e.g. a diagonal or mixed word, though not typical for this game)
        // or if it's a single letter that somehow passed length checks.
        // For this game's context, words are expected to be strictly horizontal or vertical.
        // If it's neither, it's likely an issue with coordinate generation or a single letter.
        // Defaulting to Undefined if not clearly H or V based on all coords.
        // However, FindWordsInLine only produces H or V words.
        // This GetOrientation is more general. The one used in FilterSubWords relies on how words are found.
        // The initial implementation (Coordinates[1].x == Coordinates[0].x) is fine if words are always linear.
        // Let's stick to the simpler version for now, assuming linear words from FindWordsInLine.
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
        if (wordListFile == null) { Debug.LogError("WV: Word List File missing!", this); enabled = false; return; }

        if (minWordLength <= 0) minWordLength = 1; // Ensure minWordLength is at least 1
        if (minWordLength > maxWordLength && maxWordLength > 0)
        {
            Debug.LogWarning($"WordValidator: minWordLength ({minWordLength}) is greater than maxWordLength ({maxWordLength}). Adjusting minWordLength to be equal to maxWordLength.", this);
            minWordLength = maxWordLength;
        }

        // If maxWordLength isn't set (or 0), default it to grid size or a fallback.
        int effectiveGridSize = (wordGridManager != null && wordGridManager.gridSize > 0) ? wordGridManager.gridSize : 10; // Default to 10 if grid not ready
        if (maxWordLength <= 0)
        {
            maxWordLength = effectiveGridSize;
        }
        // Ensure maxWordLength does not exceed grid dimensions if that's intended.
        // maxWordLength = Mathf.Min(maxWordLength, effectiveGridSize);


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
        // Effective max search length should not exceed current grid size or configured maxWordLength
        int effectiveMaxSearchLength = Mathf.Min(maxWordLength, currentGridSize);


        List<FoundWordData> allFoundRaw = new List<FoundWordData>();

        // Horizontal search
        for (int r = 0; r < currentGridSize; r++)
        {
            StringBuilder rowBuilder = new StringBuilder(currentGridSize);
            for (int c = 0; c < currentGridSize; c++) { rowBuilder.Append(gridData[r, c]); }
            FindWordsInLine(rowBuilder.ToString(), r, true, allFoundRaw, effectiveMaxSearchLength, currentGridSize);
        }

        // Vertical search
        for (int c = 0; c < currentGridSize; c++)
        {
            StringBuilder colBuilder = new StringBuilder(currentGridSize);
            for (int r = 0; r < currentGridSize; r++) { colBuilder.Append(gridData[r, c]); }
            FindWordsInLine(colBuilder.ToString(), c, false, allFoundRaw, effectiveMaxSearchLength, currentGridSize);
        }

        return FilterSubWordsAndDuplicates(allFoundRaw);
    }

    private void FindWordsInLine(string line, int lineIndex, bool isRow, List<FoundWordData> foundList, int effectiveMaxLen, int currentGridSize)
    {
        int n = line.Length; // Should be currentGridSize
        for (int start = 0; start < n; start++)
        {
            // Length of word to check (from minWordLength up to effectiveMaxLen or remaining line length)
            for (int len = minWordLength; len <= Mathf.Min(effectiveMaxLen, n - start); len++)
            {
                // if (start + len > n) break; // This check is handled by loop condition: len <= n - start

                string sub = line.Substring(start, len).ToUpperInvariant();

                if (validWordsDictionary.Contains(sub) && !IsWordFoundThisSession(sub))
                {
                    List<Vector2Int> wordCoords = CalculateCoordinates(lineIndex, start, len, isRow, currentGridSize);
                    if (wordCoords != null && wordCoords.Count == len) // Ensure coordinates were valid
                    {
                        foundList.Add(new FoundWordData(sub, wordCoords));
                    }
                }
            }
        }
    }

    // Helper to determine if shorterWord is a sub-word of longerWord,
    // considering coordinates and string matching.
    private bool IsSubWordAndContained(FoundWordData shorterWord, FoundWordData longerWord)
    {
        // Must have same orientation
        if (shorterWord.GetOrientation() != longerWord.GetOrientation() ||
            shorterWord.GetOrientation() == FoundWordData.WordOrientation.Undefined ||
            shorterWord.GetOrientation() == FoundWordData.WordOrientation.SingleLetter) // Single letters aren't sub-words in this context
        {
            return false;
        }

        if (shorterWord.Coordinates.Count == 0 || shorterWord.Coordinates.Count >= longerWord.Coordinates.Count)
        {
            return false; // Shorter must actually be shorter in coordinate length
        }

        // Check if shorterWord's coordinates are a contiguous sub-sequence of longerWord's coordinates
        for (int i = 0; i <= longerWord.Coordinates.Count - shorterWord.Coordinates.Count; i++)
        {
            bool segmentMatch = true;
            for (int j = 0; j < shorterWord.Coordinates.Count; j++)
            {
                if (longerWord.Coordinates[i + j] != shorterWord.Coordinates[j])
                {
                    segmentMatch = false;
                    break;
                }
            }

            if (segmentMatch)
            {
                // Coordinates match as a sub-segment.
                // Now check if the string of shorterWord matches the corresponding substring in longerWord.
                // The substring in longerWord must start at index 'i' (relative to longerWord.Word)
                // and have length of shorterWord.Word.
                if (longerWord.Word.Length >= i + shorterWord.Word.Length) // Boundary check for substring
                {
                    string subStringFromLonger = longerWord.Word.Substring(i, shorterWord.Word.Length);
                    if (subStringFromLonger == shorterWord.Word)
                    {
                        return true; // It's a true sub-word, contained and matching
                    }
                }
            }
        }
        return false;
    }


    private List<FoundWordData> FilterSubWordsAndDuplicates(List<FoundWordData> allFoundWords)
    {
        if (allFoundWords == null || allFoundWords.Count == 0)
            return new List<FoundWordData>();

        // 1. Get unique entries (word string + exact ordered coordinates + orientation)
        var uniqueWordDataList = new List<FoundWordData>();
        var uniqueSignatures = new HashSet<string>();

        foreach (var wd in allFoundWords)
        {
            StringBuilder coordSignatureBuilder = new StringBuilder();
            // Coordinates in FoundWordData are assumed to be in the order of word formation (e.g., K,N,I,F,E)
            // So, direct concatenation for signature is fine here.
            foreach (var coord in wd.Coordinates)
            {
                coordSignatureBuilder.Append($"({coord.x},{coord.y})");
            }
            // Signature includes word, its specific coordinates in order, and orientation
            string signature = $"{wd.Word}_{coordSignatureBuilder.ToString()}_{wd.GetOrientation()}";

            if (uniqueSignatures.Add(signature))
            {
                uniqueWordDataList.Add(wd);
            }
        }

        // 2. Sort by length (descending), then by a stable key for tie-breaking (e.g., first coordinate, then ID)
        uniqueWordDataList.Sort((a, b) => {
            int lengthComparison = b.Word.Length.CompareTo(a.Word.Length);
            if (lengthComparison != 0) return lengthComparison;

            if (a.Coordinates.Count > 0 && b.Coordinates.Count > 0)
            {
                int rCompare = a.Coordinates[0].x.CompareTo(b.Coordinates[0].x);
                if (rCompare != 0) return rCompare;
                int cCompare = a.Coordinates[0].y.CompareTo(b.Coordinates[0].y);
                if (cCompare != 0) return cCompare;
            }
            return a.ID.CompareTo(b.ID); // Fallback stable sort by ID
        });

        // 3. More aggressive sub-word filtering
        List<FoundWordData> finalList = new List<FoundWordData>();
        HashSet<System.Guid> discardedWordIds = new HashSet<System.Guid>();

        for (int i = 0; i < uniqueWordDataList.Count; i++)
        {
            FoundWordData currentWordData = uniqueWordDataList[i];

            // If currentWordData itself was already discarded by a longer word that contained it, skip
            if (discardedWordIds.Contains(currentWordData.ID))
            {
                continue;
            }

            // Add currentWordData to the final list (it's either the longest or not a sub-word of a previously added longer word)
            finalList.Add(currentWordData);

            // Now, iterate through ALL OTHER words in uniqueWordDataList (both shorter and potentially same length but different ID)
            // to see if THEY are sub-words of currentWordData (the one just added to finalList).
            // This is because currentWordData is the "dominant" one at this stage of the loop.
            for (int j = 0; j < uniqueWordDataList.Count; j++)
            {
                if (i == j) continue; // Don't compare with itself

                FoundWordData otherWordData = uniqueWordDataList[j];
                if (discardedWordIds.Contains(otherWordData.ID))
                {
                    continue; // otherWordData already discarded
                }

                // Check if otherWordData (which could be shorter, or same length but different instance)
                // is a sub-word of currentWordData.
                if (IsSubWordAndContained(otherWordData, currentWordData))
                {
                    // Debug.Log($"Filtering out '{otherWordData.Word}' as it is a sub-word of '{currentWordData.Word}'");
                    discardedWordIds.Add(otherWordData.ID);
                }
            }
        }
        return finalList;
    }


    public bool CheckIntersection(FoundWordData wordA, FoundWordData wordB, out Vector2Int sharedCell)
    {
        sharedCell = new Vector2Int(-1, -1);
        if (wordA.ID == wordB.ID || wordA.Coordinates == null || wordB.Coordinates == null || wordA.Coordinates.Count == 0 || wordB.Coordinates.Count == 0)
        {
            return false;
        }

        FoundWordData.WordOrientation orientationA = wordA.GetOrientation();
        FoundWordData.WordOrientation orientationB = wordB.GetOrientation();

        if (orientationA == FoundWordData.WordOrientation.SingleLetter ||
            orientationB == FoundWordData.WordOrientation.SingleLetter ||
            orientationA == FoundWordData.WordOrientation.Undefined ||
            orientationB == FoundWordData.WordOrientation.Undefined ||
            orientationA == orientationB) // Must be on opposite axes
        {
            return false;
        }

        // Find single intersection point
        List<Vector2Int> sharedCoordsList = wordA.Coordinates.Intersect(wordB.Coordinates).ToList();

        if (sharedCoordsList.Count == 1)
        {
            sharedCell = sharedCoordsList[0];
            return true;
        }

        return false;
    }

    private List<Vector2Int> CalculateCoordinates(int lineIndex, int startIndexInLine, int wordLength, bool isRow, int currentGridSize)
    {
        List<Vector2Int> coords = new List<Vector2Int>(wordLength);
        // wordGridManager might be null during initial Awake/editor, but should be set by FindAllPotentialWords call time.
        // currentGridSize is passed in, so wordGridManager direct access isn't strictly needed here if gridSize is reliable.

        for (int i = 0; i < wordLength; i++)
        {
            int r, c;
            if (isRow)
            {
                r = lineIndex;
                c = startIndexInLine + i;
            }
            else // is Column
            {
                r = startIndexInLine + i;
                c = lineIndex;
            }

            // Boundary check
            if (r >= 0 && r < currentGridSize && c >= 0 && c < currentGridSize)
            {
                coords.Add(new Vector2Int(r, c));
            }
            else
            {
                // This should not happen if FindWordsInLine's length calculation is correct (len <= n - start)
                Debug.LogError($"CalculateCoordinates: Coordinate [{r},{c}] out of bounds for grid size {currentGridSize}. Word: line {lineIndex}, start {startIndexInLine}, length {wordLength}, isRow {isRow}", this);
                return null; // Invalid coordinates
            }
        }
        return coords;
    }
}