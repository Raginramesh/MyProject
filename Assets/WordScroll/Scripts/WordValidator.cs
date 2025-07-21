using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System.Linq;
// Removed System.IO as it wasn't used

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

    public enum WordOrientation { Horizontal, Vertical, SingleLetter, Undefined }
    public WordOrientation GetOrientation()
    {
        if (Coordinates == null || Coordinates.Count == 0) return WordOrientation.Undefined;
        if (Coordinates.Count == 1) return WordOrientation.SingleLetter;
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
    [SerializeField] private int minWordLength = 3;
    [SerializeField] private int maxWordLength = 10;
    
    [Header("Validation Mode Settings")]
    [SerializeField] private bool centerRowOnlyMode = true;
    [Tooltip("When enabled, only horizontal words in the center row of the grid will be validated. When disabled, words can be found in any row/column.")]

    private HashSet<string> validWordsDictionary = new HashSet<string>();
    private HashSet<string> wordsFoundThisSession = new HashSet<string>();

    // Cached scoring settings
    private GameManager.ScoringMode cachedScoringMode;
    private int cachedPointsPerLetter;
    private Dictionary<char, int> cachedScrabbleValues;
    private bool scoringConfigured = false;

    void Awake()
    {
        // Attempt to find GameManager if not assigned
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        if (wordGridManager == null) wordGridManager = FindFirstObjectByType<WordGridManager>();

        if (wordGridManager == null) { Debug.LogError("WV: WordGridManager missing!", this); enabled = false; return; }
        if (wordListFile == null) { Debug.LogError("WV: Word List File missing!", this); enabled = false; return; }

        ConfigureScoring(); // Fetch scoring settings from GameManager

        if (minWordLength <= 0) minWordLength = 1;
        if (minWordLength > maxWordLength && maxWordLength > 0)
        {
            minWordLength = maxWordLength;
        }
        int effectiveGridSize = (wordGridManager != null && wordGridManager.gridSize > 0) ? wordGridManager.gridSize : 10;
        if (maxWordLength <= 0) maxWordLength = effectiveGridSize;

        LoadWordList();
    }

    public void SetGameManager(GameManager manager)
    {
        this.gameManager = manager;
        ConfigureScoring(); // Re-configure if GM is set later
    }

    private void ConfigureScoring()
    {
        if (gameManager != null)
        {
            cachedScoringMode = gameManager.GetCurrentScoringModeSetting();
            cachedPointsPerLetter = gameManager.GetPointsPerLetterSetting();
            // Ensure scrabble values are copied to a new dictionary
            var gmScrabbleValues = gameManager.GetScrabbleLetterValues();
            if (gmScrabbleValues != null)
            {
                cachedScrabbleValues = new Dictionary<char, int>(gmScrabbleValues);
            }
            else
            {
                cachedScrabbleValues = new Dictionary<char, int>(); // Empty if GM's is null
                Debug.LogWarning("WV: GameManager's Scrabble values were null during configuration.");
            }
            scoringConfigured = true;
            // Debug.Log("WV: Scoring configuration received from GameManager.");
        }
        else
        {
            scoringConfigured = false;
            // Debug.LogError("WV: GameManager not available to fetch scoring configuration during Awake/Set. Word scoring for filtering might be incorrect.");
        }
    }

    // Internal scoring method for WordValidator's filtering
    private int CalculateScoreForWordInternal(FoundWordData wordData)
    {
        if (!scoringConfigured || wordData.Word == null) return 0;

        int totalScore = 0;
        foreach (char letterChar in wordData.Word)
        {
            char upperLetter = char.ToUpperInvariant(letterChar);
            if (cachedScoringMode == GameManager.ScoringMode.ScrabbleBased)
            {
                if (cachedScrabbleValues != null && cachedScrabbleValues.TryGetValue(upperLetter, out int val))
                {
                    totalScore += val;
                }
                else
                {
                    totalScore += 1; // Fallback for Scrabble
                }
            }
            else // LengthBased
            {
                totalScore += cachedPointsPerLetter;
            }
        }
        return totalScore;
    }


    void LoadWordList()
    {
        validWordsDictionary.Clear();
        if (wordListFile != null)
        {
            string[] lines = wordListFile.text.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string word = line.Trim().ToUpperInvariant();
                if (word.Length >= minWordLength && word.Length <= maxWordLength)
                {
                    validWordsDictionary.Add(word);
                }
            }
        }
    }

    public void ResetFoundWordsList() => wordsFoundThisSession.Clear();
    public void MarkWordAsFoundInSession(string word)
    {
        if (!string.IsNullOrEmpty(word)) wordsFoundThisSession.Add(word.ToUpperInvariant());
    }
    public bool IsWordFoundThisSession(string word)
    {
        if (string.IsNullOrEmpty(word)) return false;
        return wordsFoundThisSession.Contains(word.ToUpperInvariant());
    }

    // Validation Mode Control Methods
    public bool IsCenterRowOnlyModeEnabled() => centerRowOnlyMode;
    
    public void SetCenterRowOnlyMode(bool enabled)
    {
        if (centerRowOnlyMode != enabled)
        {
            centerRowOnlyMode = enabled;
            Debug.Log($"🎯 [WordValidator] Validation mode changed to: {(enabled ? "Center Row Only" : "Full Grid")}");
        }
    }
    
    public int GetCenterRowIndex()
    {
        if (wordGridManager == null) return -1;
        int gridSize = wordGridManager.gridSize;
        return (gridSize - 1) / 2;
    }
    
    /// <summary>
    /// Option A: Strict Grid-Size Validation
    /// Validates that sequences match the expected length based on grid size
    /// 5x5: accepts 5-letter, 4-letter+1blank, 3-letter+2blanks
    /// 3x3: accepts 3-letter, 2-letter+1blank  
    /// </summary>
    private bool IsValidGridSizeSequence(string sequence, int sequenceLength, int gridSize)
    {
        // Only validate sequences that match the exact grid size
        if (sequenceLength != gridSize)
            return false;
            
        // Check if sequence has valid structure (letters + trailing blanks only)
        if (!HasValidSequenceStructure(sequence))
            return false;
            
        // Extract the actual word part (without trailing blanks)
        string wordPart = ExtractWordFromSequence(sequence);
        int wordLength = wordPart.Length;
        int blankCount = sequenceLength - wordLength;
        
        // Validate word + blank combinations based on grid size
        return IsValidWordBlankCombination(wordLength, blankCount, gridSize);
    }
    
    /// <summary>
    /// Validates specific word + blank combinations for different grid sizes
    /// </summary>
    private bool IsValidWordBlankCombination(int wordLength, int blankCount, int gridSize)
    {
        switch (gridSize)
        {
            case 3:
                // 3x3 grid: 3-letter OR 2-letter+1blank
                return (wordLength == 3 && blankCount == 0) || 
                       (wordLength == 2 && blankCount == 1);
                       
            case 5:
                // 5x5 grid: 5-letter OR 4-letter+1blank OR 3-letter+2blanks
                return (wordLength == 5 && blankCount == 0) || 
                       (wordLength == 4 && blankCount == 1) || 
                       (wordLength == 3 && blankCount == 2);
                       
            default:
                // For other grid sizes, use flexible rule: word + blanks = grid size
                return (wordLength + blankCount == gridSize) && (wordLength >= minWordLength);
        }
    }
    
    /// <summary>
    /// Validates that blanks only appear at the beginning or end of sequences
    /// No blanks allowed in the middle of words
    /// Valid: [_][C][A][T] or [C][A][T][_] or [_][_][C][A][T] or [C][A][T][_][_]
    /// Invalid: [C][A][_][T] - blank in middle
    /// </summary>
    private bool HasValidSequenceStructure(string sequence)
    {
        int firstLetterIndex = -1;
        int lastLetterIndex = -1;
        
        // Find the first and last letter positions
        for (int i = 0; i < sequence.Length; i++)
        {
            char c = sequence[i];
            if (c != ' ' && c != '\0') // Non-blank cell
            {
                if (firstLetterIndex == -1)
                    firstLetterIndex = i; // First letter found
                lastLetterIndex = i; // Update last letter position
            }
        }
        
        // If no letters found, it's invalid (all blanks)
        if (firstLetterIndex == -1)
            return false;
        
        // Check that all characters between first and last letter are also letters (no blanks in middle)
        for (int i = firstLetterIndex; i <= lastLetterIndex; i++)
        {
            char c = sequence[i];
            if (c == ' ' || c == '\0') // Found blank between first and last letter
                return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Extracts the word part from a sequence, ignoring leading and trailing blanks
    /// [C][A][T][S][_] → "CATS"
    /// [_][C][A][T][S] → "CATS" 
    /// [_][_][C][A][T] → "CAT"
    /// [C][A][T][_][_] → "CAT"
    /// </summary>
    private string ExtractWordFromSequence(string sequence)
    {
        StringBuilder wordBuilder = new StringBuilder();
        
        // Find the continuous letter sequence (skip leading and trailing blanks)
        bool foundFirstLetter = false;
        for (int i = 0; i < sequence.Length; i++)
        {
            char c = sequence[i];
            
            if (c != ' ' && c != '\0') // Non-blank cell
            {
                foundFirstLetter = true;
                wordBuilder.Append(c);
            }
            else if (foundFirstLetter) // Blank after we've started finding letters
            {
                // This must be a trailing blank, stop here
                break;
            }
            // If not foundFirstLetter yet, this is a leading blank, so skip it
        }
        
        return wordBuilder.ToString();
    }

    /// <summary>
    /// Helper method to convert CellData array to char array for validation
    /// This method extracts the letter values from CellData and handles blank cells appropriately
    /// </summary>
    public void DebugCenterRowValidation()
    {
        if (wordGridManager == null || wordGridManager.gridData == null)
        {
            Debug.LogWarning("🚫 [WordValidator] Cannot debug: WordGridManager or grid data is null");
            return;
        }
        
        int gridSize = wordGridManager.gridSize;
        int centerRow = GetCenterRowIndex();
        // Get grid data as char array for compatibility with existing debug logic
        char[,] gridData = ConvertCellDataToCharArray(wordGridManager.gridData);
        
        Debug.Log($"🎯 === CENTER ROW VALIDATION DEBUG ===");
        Debug.Log($"📊 Grid Size: {gridSize}×{gridSize}");
        Debug.Log($"🎯 Center Row Index: {centerRow}");
        Debug.Log($"⚙️ Center Row Only Mode: {(centerRowOnlyMode ? "ENABLED" : "DISABLED")}");
        
        // Show full grid with center row highlighted
        System.Text.StringBuilder gridDisplay = new System.Text.StringBuilder();
        gridDisplay.AppendLine("📋 Full Grid (★ marks center row):");
        
        for (int row = 0; row < gridSize; row++)
        {
            string rowPrefix = (row == centerRow) ? "★ " : "  ";
            gridDisplay.Append(rowPrefix + "Row " + row + ": ");
            
            for (int col = 0; col < gridSize; col++)
            {
                gridDisplay.Append(gridData[row, col] + " ");
            }
            gridDisplay.AppendLine();
        }
        
        Debug.Log(gridDisplay.ToString());
        
        // Show center row specifically
        System.Text.StringBuilder centerRowBuilder = new System.Text.StringBuilder();
        for (int c = 0; c < gridSize; c++)
        {
            centerRowBuilder.Append(gridData[centerRow, c]);
        }
        string centerRowString = centerRowBuilder.ToString();
        
        Debug.Log($"🎯 CENTER ROW CONTENT: \"{centerRowString}\"");
        
        // Show what words would be found
        var potentialWords = FindAllPotentialWords();
        Debug.Log($"🔍 Words found with current settings: {potentialWords.Count}");
        
        foreach (var word in potentialWords)
        {
            string coordString = string.Join(", ", word.Coordinates.Select(c => $"({c.x},{c.y})"));
            Debug.Log($"   • \"{word.Word}\" at coordinates: {coordString}");
        }
        
        Debug.Log($"🎯 === END DEBUG ===");
    }

    public List<FoundWordData> FindAllPotentialWords()
    {
        List<FoundWordData> potentialWords = new List<FoundWordData>();
        if (wordGridManager == null || wordGridManager.gridData == null || !scoringConfigured) // Check scoringConfigured
        {
            // Debug.LogWarning("[WV.FindAllPotentialWords] Pre-checks failed (grid, dictionary, or scoring not configured).");
            return potentialWords;
        }

        int currentGridSize = wordGridManager.gridSize;
        // Get grid data as char array for compatibility with existing validation logic
        char[,] gridData = ConvertCellDataToCharArray(wordGridManager.gridData);
        int effectiveMaxSearchLength = Mathf.Min(maxWordLength, currentGridSize);
        List<FoundWordData> allFoundRaw = new List<FoundWordData>();

        if (centerRowOnlyMode)
        {
            // CENTER ROW ONLY VALIDATION: Only scan horizontally in the center row
            int centerRow = (currentGridSize - 1) / 2; // Calculate center row for NxN grid
            
            Debug.Log($"🎯 [WordValidator] Center-row-only validation enabled! Grid size: {currentGridSize}, Center row: {centerRow}");
            
            // Build the center row string
            StringBuilder centerRowBuilder = new StringBuilder(currentGridSize);
            for (int c = 0; c < currentGridSize; c++) 
            { 
                centerRowBuilder.Append(gridData[centerRow, c]); 
            }
            
            // Only scan horizontally in the center row
            FindWordsInLine(centerRowBuilder.ToString(), centerRow, true, allFoundRaw, effectiveMaxSearchLength, currentGridSize);
            
            Debug.Log($"🔍 [WordValidator] Found {allFoundRaw.Count} raw words in center row {centerRow}: \"{centerRowBuilder.ToString()}\"");
        }
        else
        {
            // ORIGINAL VALIDATION: Scan all rows and columns
            Debug.Log($"🔄 [WordValidator] Full grid validation mode - scanning all rows and columns");
            
            for (int r = 0; r < currentGridSize; r++)
            {
                StringBuilder rowBuilder = new StringBuilder(currentGridSize);
                for (int c = 0; c < currentGridSize; c++) { rowBuilder.Append(gridData[r, c]); }
                FindWordsInLine(rowBuilder.ToString(), r, true, allFoundRaw, effectiveMaxSearchLength, currentGridSize);
            }
            for (int c = 0; c < currentGridSize; c++)
            {
                StringBuilder colBuilder = new StringBuilder(currentGridSize);
                for (int r = 0; r < currentGridSize; r++) { colBuilder.Append(gridData[r, c]); }
                FindWordsInLine(colBuilder.ToString(), c, false, allFoundRaw, effectiveMaxSearchLength, currentGridSize);
            }
        }
        
        return FilterWords(allFoundRaw); // Renamed main filtering method
    }

    private void FindWordsInLine(string line, int lineIndex, bool isRow, List<FoundWordData> foundList, int effectiveMaxLen, int currentGridSize)
    {
        int n = line.Length;
        for (int start = 0; start < n; start++)
        {
            for (int len = minWordLength; len <= Mathf.Min(effectiveMaxLen, n - start); len++)
            {
                string sequence = line.Substring(start, len).ToUpperInvariant();
                
                // Apply Option A: Strict Grid-Size Validation
                if (IsValidGridSizeSequence(sequence, len, currentGridSize))
                {
                    // Extract word part (ignore trailing blanks for dictionary lookup)
                    string wordForValidation = ExtractWordFromSequence(sequence);
                    
                    if (!string.IsNullOrEmpty(wordForValidation) && 
                        validWordsDictionary.Contains(wordForValidation) && 
                        !IsWordFoundThisSession(wordForValidation))
                    {
                        List<Vector2Int> wordCoords = CalculateCoordinates(lineIndex, start, len, isRow, currentGridSize);
                        if (wordCoords != null && wordCoords.Count == len)
                        {
                            // Store the original word (not the sequence with blanks)
                            foundList.Add(new FoundWordData(wordForValidation, wordCoords));
                        }
                    }
                }
            }
        }
    }

    // Main filtering pipeline
    private List<FoundWordData> FilterWords(List<FoundWordData> allFoundWords)
    {
        if (allFoundWords == null || allFoundWords.Count == 0) return new List<FoundWordData>();

        // 1. Get unique entries (word string + exact ordered coordinates + orientation)
        var uniqueWords = GetUniqueWordData(allFoundWords);
        // Debug.Log($"[WV.FilterWords] Step 1 - Unique words found: {uniqueWords.Count}");

        // 2. Filter true sub-words (e.g., "NIFE" within "KNIFE")
        var nonSubWords = FilterTrueSubWords(uniqueWords);
        // Debug.Log($"[WV.FilterWords] Step 2 - After true sub-word filter: {nonSubWords.Count}");

        // 3. Filter same-axis overlaps (NEW RULE)
        var finalFilteredWords = FilterSameAxisOverlaps(nonSubWords);
        // Debug.Log($"[WV.FilterWords] Step 3 - After same-axis overlap filter: {finalFilteredWords.Count}");

        // foreach(var wd in finalFilteredWords.OrderByDescending(w=>w.Word.Length)) { Debug.Log($"  Final Kept: {wd.Word} (Score: {CalculateScoreForWordInternal(wd)})"); }
        return finalFilteredWords;
    }

    private List<FoundWordData> GetUniqueWordData(List<FoundWordData> allFoundWords)
    {
        var uniqueWordDataList = new List<FoundWordData>();
        var uniqueSignatures = new HashSet<string>();
        foreach (var wd in allFoundWords)
        {
            StringBuilder coordSignatureBuilder = new StringBuilder();
            if (wd.Coordinates != null) { foreach (var coord in wd.Coordinates) { coordSignatureBuilder.Append($"({coord.x},{coord.y})"); } }
            string signature = $"{wd.Word}_{coordSignatureBuilder.ToString()}_{wd.GetOrientation()}";
            if (uniqueSignatures.Add(signature)) { uniqueWordDataList.Add(wd); }
        }
        return uniqueWordDataList;
    }

    private List<FoundWordData> FilterTrueSubWords(List<FoundWordData> uniqueWords)
    {
        if (uniqueWords == null || uniqueWords.Count <= 1) return uniqueWords ?? new List<FoundWordData>();

        var sortedByLength = uniqueWords.OrderByDescending(w => w.Word.Length)
                                       .ThenBy(w => w.ID) // Stable sort
                                       .ToList();

        List<FoundWordData> keptWords = new List<FoundWordData>();
        HashSet<System.Guid> discardedWordIds = new HashSet<System.Guid>();

        for (int i = 0; i < sortedByLength.Count; i++)
        {
            FoundWordData currentWord = sortedByLength[i];
            if (discardedWordIds.Contains(currentWord.ID)) continue;

            keptWords.Add(currentWord); // Add current word (longest at this point)

            // Check if any other (necessarily shorter or same length but diff ID) words are sub-words of this currentWord
            for (int j = 0; j < sortedByLength.Count; j++)
            {
                if (i == j) continue;
                FoundWordData otherWord = sortedByLength[j];
                if (discardedWordIds.Contains(otherWord.ID)) continue;

                if (IsSubWordAndContained(otherWord, currentWord))
                {
                    // Debug.Log($"[WV.FilterTrueSubWords] Discarding '{otherWord.Word}' as sub-word of '{currentWord.Word}'");
                    discardedWordIds.Add(otherWord.ID);
                }
            }
        }
        return keptWords;
    }

    private bool IsSubWordAndContained(FoundWordData shorterWord, FoundWordData longerWord)
    {
        if (shorterWord.Coordinates == null || longerWord.Coordinates == null) return false;
        if (shorterWord.GetOrientation() != longerWord.GetOrientation() || shorterWord.GetOrientation() == FoundWordData.WordOrientation.Undefined) return false;
        if (shorterWord.Coordinates.Count == 0 || shorterWord.Coordinates.Count >= longerWord.Coordinates.Count) return false;

        for (int i = 0; i <= longerWord.Coordinates.Count - shorterWord.Coordinates.Count; i++)
        {
            bool segmentMatch = true;
            for (int j = 0; j < shorterWord.Coordinates.Count; j++)
            {
                if (longerWord.Coordinates[i + j] != shorterWord.Coordinates[j]) { segmentMatch = false; break; }
            }
            if (segmentMatch)
            {
                if (longerWord.Word.Length >= i + shorterWord.Word.Length)
                {
                    return longerWord.Word.Substring(i, shorterWord.Word.Length) == shorterWord.Word;
                }
            }
        }
        return false;
    }

    private List<FoundWordData> FilterSameAxisOverlaps(List<FoundWordData> words)
    {
        if (words == null || words.Count <= 1) return words ?? new List<FoundWordData>();

        List<FoundWordData> result = new List<FoundWordData>();
        HashSet<System.Guid> discardedIds = new HashSet<System.Guid>();

        // Sort to make comparisons more deterministic, though direct pairwise comparison is done.
        // Sorting by score descending, then length descending can help if we want to prioritize keeping the "best" word first.
        var sortedForOverlapCheck = words.OrderByDescending(CalculateScoreForWordInternal)
                                         .ThenByDescending(w => w.Word.Length)
                                         .ThenBy(w => w.ID)
                                         .ToList();

        for (int i = 0; i < sortedForOverlapCheck.Count; i++)
        {
            FoundWordData wordA = sortedForOverlapCheck[i];
            if (discardedIds.Contains(wordA.ID)) continue;

            for (int j = i + 1; j < sortedForOverlapCheck.Count; j++)
            {
                FoundWordData wordB = sortedForOverlapCheck[j];
                if (discardedIds.Contains(wordB.ID)) continue;

                if (DoWordsOverlapOnSameAxis(wordA, wordB))
                {
                    // Debug.Log($"[WV.FilterSameAxisOverlaps] Overlap detected: '{wordA.Word}' and '{wordB.Word}'");
                    int scoreA = CalculateScoreForWordInternal(wordA);
                    int scoreB = CalculateScoreForWordInternal(wordB);

                    if (scoreB > scoreA)
                    {
                        // Debug.Log($"  Discarding '{wordA.Word}' (Score: {scoreA}) in favor of '{wordB.Word}' (Score: {scoreB})");
                        discardedIds.Add(wordA.ID);
                        break; // wordA is discarded, move to the next wordA
                    }
                    else if (scoreA > scoreB)
                    {
                        // Debug.Log($"  Discarding '{wordB.Word}' (Score: {scoreB}) in favor of '{wordA.Word}' (Score: {scoreA})");
                        discardedIds.Add(wordB.ID);
                    }
                    else // Scores are equal, compare by length
                    {
                        if (wordB.Word.Length > wordA.Word.Length)
                        {
                            // Debug.Log($"  Scores equal. Discarding '{wordA.Word}' (Len: {wordA.Word.Length}) for '{wordB.Word}' (Len: {wordB.Word.Length})");
                            discardedIds.Add(wordA.ID);
                            break; // wordA is discarded
                        }
                        else if (wordA.Word.Length > wordB.Word.Length)
                        {
                            // Debug.Log($"  Scores equal. Discarding '{wordB.Word}' (Len: {wordB.Word.Length}) for '{wordA.Word}' (Len: {wordA.Word.Length})");
                            discardedIds.Add(wordB.ID);
                        }
                        else // Scores and lengths are equal, discard one deterministically (e.g., by ID)
                        {
                            // To ensure one is discarded, compare IDs. Arbitrarily discard the one with "larger" GUID string.
                            if (string.Compare(wordB.ID.ToString(), wordA.ID.ToString(), System.StringComparison.Ordinal) > 0)
                            {
                                // Debug.Log($"  Scores & Lengths equal. Discarding '{wordA.Word}' by ID tie-break.");
                                discardedIds.Add(wordA.ID);
                                break; // wordA is discarded
                            }
                            else
                            {
                                // Debug.Log($"  Scores & Lengths equal. Discarding '{wordB.Word}' by ID tie-break.");
                                discardedIds.Add(wordB.ID);
                            }
                        }
                    }
                }
            }
            // If wordA was not discarded by any wordB, add it to the result.
            if (!discardedIds.Contains(wordA.ID))
            {
                result.Add(wordA);
            }
        }
        return result;
    }

    private bool DoWordsOverlapOnSameAxis(FoundWordData wordA, FoundWordData wordB)
    {
        if (wordA.Coordinates == null || wordB.Coordinates == null) return false;
        if (wordA.GetOrientation() != wordB.GetOrientation() ||
            wordA.GetOrientation() == FoundWordData.WordOrientation.Undefined ||
            wordA.GetOrientation() == FoundWordData.WordOrientation.SingleLetter) // Single letters don't overlap in a meaningful way for this rule
        {
            return false;
        }

        // Check for any shared coordinates
        foreach (var coordA in wordA.Coordinates)
        {
            if (wordB.Coordinates.Contains(coordA))
            {
                return true; // Found a shared coordinate
            }
        }
        return false;
    }

    // This method is for GameManager to check for crossword-style intersections for combos
    public bool CheckIntersection(FoundWordData wordA, FoundWordData wordB, out Vector2Int sharedCell)
    {
        sharedCell = new Vector2Int(-1, -1);
        if (wordA.ID == wordB.ID || wordA.Coordinates == null || wordB.Coordinates == null || wordA.Coordinates.Count == 0 || wordB.Coordinates.Count == 0)
            return false;

        FoundWordData.WordOrientation orientationA = wordA.GetOrientation();
        FoundWordData.WordOrientation orientationB = wordB.GetOrientation();

        // For combos, they MUST be on opposite axes and not single letters/undefined
        if (orientationA == FoundWordData.WordOrientation.SingleLetter ||
            orientationB == FoundWordData.WordOrientation.SingleLetter ||
            orientationA == FoundWordData.WordOrientation.Undefined ||
            orientationB == FoundWordData.WordOrientation.Undefined ||
            orientationA == orientationB)
            return false;

        List<Vector2Int> sharedCoordsList = wordA.Coordinates.Intersect(wordB.Coordinates).ToList();
        if (sharedCoordsList.Count == 1) // Must intersect at exactly one cell for this type of combo
        {
            sharedCell = sharedCoordsList[0];
            return true;
        }
        return false;
    }

    private List<Vector2Int> CalculateCoordinates(int lineIndex, int startIndexInLine, int wordLength, bool isRow, int currentGridSize)
    {
        List<Vector2Int> coords = new List<Vector2Int>(wordLength);
        for (int i = 0; i < wordLength; i++)
        {
            int r, c;
            if (isRow) { r = lineIndex; c = startIndexInLine + i; }
            else { r = startIndexInLine + i; c = lineIndex; }

            if (r >= 0 && r < currentGridSize && c >= 0 && c < currentGridSize)
            {
                coords.Add(new Vector2Int(r, c));
            }
            else
            {
                Debug.LogError($"CalculateCoordinates: Coord [{r},{c}] out of bounds for grid {currentGridSize}. Word: line {lineIndex}, start {startIndexInLine}, len {wordLength}, isRow {isRow}", this);
                return null;
            }
        }
        return coords;
    }

    /// <summary>
    /// Converts CellData array to char array for compatibility with existing validation logic
    /// This method extracts the letter values from CellData and handles blank cells appropriately
    /// </summary>
    private char[,] ConvertCellDataToCharArray(CellData[,] cellDataArray)
    {
        int rows = cellDataArray.GetLength(0);
        int cols = cellDataArray.GetLength(1);
        char[,] charArray = new char[rows, cols];

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                CellData cellData = cellDataArray[r, c];
                if (cellData.IsBlank || !cellData.participatesInValidation)
                {
                    // Use space character for blank cells or cells that don't participate in validation
                    charArray[r, c] = ' ';
                }
                else
                {
                    // Use the actual letter value from the cell
                    charArray[r, c] = cellData.letterValue;
                }
            }
        }

        return charArray;
    }
}