using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using System.Linq;
using System; // Required for System.Guid
using UnityEngine.UI; // Required for Slider

public class GameManager : MonoBehaviour
{
    public enum ScoringMode { LengthBased, ScrabbleBased }
    public enum GameState { Initializing, Playing, Paused, GameOver } // Could add LevelComplete if more distinction is needed
    public enum DisplayMode { Timer, Moves, None }

    [Header("Game State")]
    [SerializeField] private GameState currentState = GameState.Initializing;
    public GameState CurrentStatePublic => currentState;
    private bool hasWon = false; // Flag to indicate if the player has won
    public bool HasWon => hasWon; // Public getter for UI scripts

    private bool isProcessingSequentialWords = false;
    public bool IsAnyAnimationPlaying
    {
        get
        {
            return isProcessingSequentialWords ||
                   (effectsManager != null && effectsManager.IsAnimating) ||
                   (wordGridManager != null && wordGridManager.isAnimating) ||
                   IsNumericalScoreAnimating;
        }
    }

    [Header("Game Mode & Display")]
    [SerializeField] private DisplayMode currentDisplayMode = DisplayMode.Timer;
    public DisplayMode CurrentGameDisplayMode => currentDisplayMode;
    [SerializeField] private float gameTimeLimit = 120f;
    [SerializeField] private int startingMoves = 50;

    [Header("Scene Navigation")]
    [SerializeField] private string homeSceneName = "HomeScreen";

    [Header("Scoring")]
    [SerializeField] private ScoringMode currentScoringMode = ScoringMode.LengthBased;
    public ScoringMode GetCurrentScoringModeSetting() => currentScoringMode;

    [Tooltip("Points per letter, used if Scoring Mode is LengthBased for actual scoring, and for ScrabbleBased if a letter has no defined Scrabble value (as a fallback, typically 1).")]
    [SerializeField] private int pointsPerLetter = 10;
    public int GetPointsPerLetterSetting() => pointsPerLetter;
    [SerializeField] private int targetScoreForLevel = 1000;

    private Dictionary<char, int> scrabbleLetterValues;
    public IReadOnlyDictionary<char, int> GetScrabbleLetterValues() => scrabbleLetterValues;


    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private RectTransform scoreTextRectTransform;
    [SerializeField] private Slider scoreProgressBar;
    [SerializeField] private GameObject statusDisplayGroup;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI movesText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject pausePanel;

    [Header("Component References")]
    [SerializeField] private WordGridManager wordGridManager;
    [SerializeField] private WordValidator wordValidator;
    [SerializeField] private GridInputHandler gridInputHandler;
    [SerializeField] private EffectsManager effectsManager;
    [SerializeField] private NumericalScoreUI numericalScoreUI;

    [Header("Timing & Combo Settings")]
    [SerializeField] private float replacementDelayAfterEffectStart = 0.4f;
    [SerializeField] private float visualPauseBetweenWordsInSequence = 0.25f;

    [Header("Effects")]
    [SerializeField] private float scoreShakeDuration = 0.2f;
    [SerializeField] private float scoreShakeStrength = 3f;
    [SerializeField] private int scoreShakeVibrato = 15;

    private float currentTimeRemaining;
    private int currentMovesRemaining;
    private int currentScore = 0;
    public int CurrentScore => currentScore; // Public getter for currentScore
    public static GameManager instance;

    private List<FoundWordData> currentPotentialWords = new List<FoundWordData>();
    private Dictionary<System.Guid, Color> currentAppliedHighlightColors = new Dictionary<System.Guid, Color>();


    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        InitializeScrabbleValues();
        if (wordGridManager == null) wordGridManager = FindFirstObjectByType<WordGridManager>();
        if (wordValidator == null) wordValidator = FindFirstObjectByType<WordValidator>();
        if (gridInputHandler == null) gridInputHandler = FindFirstObjectByType<GridInputHandler>();
        if (effectsManager == null) effectsManager = FindFirstObjectByType<EffectsManager>();
        if (scoreTextRectTransform == null && scoreText != null) scoreTextRectTransform = scoreText.GetComponent<RectTransform>();

        if (effectsManager == null) Debug.LogError("GM: EffectsManager is MISSING! Fly-to-score will not work.", this);
        if (wordGridManager == null) Debug.LogError("GM: WordGridManager missing!", this);
        if (wordValidator == null) Debug.LogError("GM: WordValidator missing!", this);
        if (gridInputHandler == null) Debug.LogError("GM: GridInputHandler missing! Tapping will not work.", this);
        if (scoreText == null) Debug.LogError("GM: Score Text (TMP) for progress bar missing!", this);
        if (scoreProgressBar == null) Debug.LogError("GM: Score Progress Bar (Slider) missing!", this);

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);

        if (wordValidator != null) wordValidator.SetGameManager(this);
        if (wordGridManager != null) wordGridManager.SetGameManager(this);
    }

    void InitializeScrabbleValues()
    {
        scrabbleLetterValues = new Dictionary<char, int>() {
            {'A', 1}, {'B', 3}, {'C', 3}, {'D', 2}, {'E', 1}, {'F', 4}, {'G', 2},
            {'H', 4}, {'I', 1}, {'J', 8}, {'K', 5}, {'L', 1}, {'M', 3}, {'N', 1},
            {'O', 1}, {'P', 3}, {'Q', 10},{'R', 1}, {'S', 1}, {'T', 1}, {'U', 1},
            {'V', 4}, {'W', 4}, {'X', 8}, {'Y', 4}, {'Z', 10}
        };
    }

    void Start()
    {
        if (currentState != GameState.Initializing && currentState != GameState.Playing)
        {
            SetState(GameState.Initializing);
        }
        StartGame();
    }

    void Update()
    {
        if (currentState == GameState.Playing && currentDisplayMode == DisplayMode.Timer)
        {
            UpdateTimer();
        }
    }

    private void SetState(GameState newState)
    {
        if (currentState == newState && newState != GameState.Initializing) return;

        GameState previousState = currentState;
        currentState = newState;

        if (previousState == GameState.Playing && (newState == GameState.GameOver || newState == GameState.Paused))
        {
            if (effectsManager != null)
            {
                effectsManager.ClearAllFloatingLetters(new Dictionary<Guid, List<GameObject>>());
            }
        }
        isProcessingSequentialWords = false;

        switch (currentState)
        {
            case GameState.Initializing:
                Time.timeScale = 1f;
                if (gridInputHandler != null) gridInputHandler.enabled = false;
                break;
            case GameState.Playing:
                Time.timeScale = 1f;
                if (gridInputHandler != null) gridInputHandler.enabled = true;
                if (pausePanel != null) pausePanel.SetActive(false);
                break;
            case GameState.Paused:
                Time.timeScale = 0f;
                if (gridInputHandler != null) gridInputHandler.enabled = false;
                if (pausePanel != null) pausePanel.SetActive(true);
                break;
            case GameState.GameOver:
                Time.timeScale = 1f; // Keep time scale at 1 for game over animations/UI
                if (gridInputHandler != null) gridInputHandler.enabled = false;
                break;
        }
    }

    private void StartGame()
    {
        SetState(GameState.Initializing);
        currentScore = 0;
        hasWon = false; // Reset win state

        if (scoreProgressBar != null)
        {
            scoreProgressBar.maxValue = targetScoreForLevel;
            scoreProgressBar.value = 0;
        }
        UpdateScoreUI();

        currentPotentialWords.Clear();
        currentAppliedHighlightColors.Clear();

        if (statusDisplayGroup != null)
        {
            bool isGroupActive = currentDisplayMode != DisplayMode.None;
            statusDisplayGroup.SetActive(isGroupActive);
            if (isGroupActive)
            {
                if (timerText != null) timerText.gameObject.SetActive(currentDisplayMode == DisplayMode.Timer);
                if (movesText != null) movesText.gameObject.SetActive(currentDisplayMode == DisplayMode.Moves);
                if (currentDisplayMode == DisplayMode.Timer) { currentTimeRemaining = gameTimeLimit; UpdateTimerUI(); }
                else if (currentDisplayMode == DisplayMode.Moves) { currentMovesRemaining = startingMoves; UpdateMovesUI(); }
            }
        }
        if (wordGridManager != null)
        {
            wordGridManager.InitializeGrid();
        }
        else
        {
            Debug.LogError("GM: Cannot initialize grid - WordGridManager missing!", this); return;
        }

        if (wordValidator != null)
        {
            wordValidator.ResetFoundWordsList();
            if (wordGridManager != null)
            {
                wordGridManager.TriggerValidationCheckAndHighlightUpdate();
            }
        }
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        SetState(GameState.Playing);
    }

    private void PlayerWins()
    {
        if (currentState != GameState.Playing) return; // Already won or game ended otherwise

        Debug.Log("Player Wins! Target score reached.");
        EndGame(playerDidWin: true);
    }
    
    private void PlayerLoses()
    {
        if (currentState != GameState.Playing) return; // Already ended

        Debug.Log("Player Loses! Time/moves ran out.");
        EndGame(timeout: currentDisplayMode == DisplayMode.Timer, noMoves: currentDisplayMode == DisplayMode.Moves);
    }
    
    /// <summary>
    /// Shows the numerical score UI for a list of words
    /// </summary>
    private void ShowNumericalScore(List<FoundWordData> words)
    {
        if (numericalScoreUI != null && words != null && words.Count > 0)
        {
            numericalScoreUI.ShowNumericalScore(words);
        }
    }
    
    /// <summary>
    /// Checks if the numerical score UI is currently animating
    /// </summary>
    public bool IsNumericalScoreAnimating
    {
        get
        {
            return numericalScoreUI != null && numericalScoreUI.IsAnimating;
        }
    }

    private void EndGame(bool timeout = false, bool noMoves = false, bool playerDidWin = false)
    {
        if (currentState == GameState.GameOver) return; // Already in GameOver state

        if (playerDidWin)
        {
            hasWon = true;
        }
        else
        {
            // If EndGame is called for timeout or noMoves, check if player had already won.
            // If hasWon is true, it means PlayerWins() was called just before timer/moves ran out.
            if (hasWon) 
            {
                // Player reached target score just as time/moves ran out. Still a win.
                 Debug.Log("EndGame: Player had already won before timeout/noMoves.");
            }
            else // This is a loss due to timeout or no moves
            {
                if (timeout && currentDisplayMode != DisplayMode.Timer) return; // Invalid timeout call
                if (noMoves && currentDisplayMode != DisplayMode.Moves) return; // Invalid noMoves call
                hasWon = false;
                Debug.Log($"Game Over: Timeout={timeout}, NoMoves={noMoves}");
            }
        }

        SetState(GameState.GameOver);
        currentPotentialWords.Clear();
        currentAppliedHighlightColors.Clear();
        if (wordGridManager != null) wordGridManager.ClearAllCellHighlights();

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            // The script on gameOverPanel (e.g., GameOverUIController) should check GameManager.instance.HasWon
            // in its OnEnable() or Start() method to display the correct "You Win!" or "You Lose!" message.
        }
    }

    public void UpdatePotentialWordsDisplay(List<FoundWordData> potentialWordsFromValidator, Dictionary<System.Guid, Color> appliedColors)
    {
        if (currentState != GameState.Playing && currentState != GameState.Initializing)
        {
            currentPotentialWords.Clear();
            currentAppliedHighlightColors.Clear();
            if (wordGridManager != null) wordGridManager.ClearAllCellHighlights();
            return;
        }
        currentPotentialWords = potentialWordsFromValidator ?? new List<FoundWordData>();
        currentAppliedHighlightColors = appliedColors ?? new Dictionary<System.Guid, Color>();
    }

    public int GetPointsForActualScoring(char letter)
    {
        char upperLetter = char.ToUpperInvariant(letter);
        if (currentScoringMode == ScoringMode.ScrabbleBased)
        {
            if (scrabbleLetterValues.TryGetValue(upperLetter, out int val))
            {
                return val;
            }
            return 1;
        }
        return pointsPerLetter;
    }
    
    /// <summary>
    /// Gets the letter at the specified grid position
    /// </summary>
    public char GetLetterAtPosition(Vector2Int position)
    {
        if (wordGridManager != null && wordGridManager.gridData != null)
        {
            if (position.x >= 0 && position.x < wordGridManager.gridSize && 
                position.y >= 0 && position.y < wordGridManager.gridSize)
            {
                return wordGridManager.gridData[position.x, position.y];
            }
        }
        return ' '; // Return space if position is invalid
    }

    public int CalculateTotalScoreForWord(FoundWordData wordData)
    {
        if (wordData.Word == null) return 0;
        int totalScore = 0;
        foreach (char letter in wordData.Word)
        {
            totalScore += GetPointsForActualScoring(letter);
        }
        return totalScore;
    }

    public bool AttemptTapValidation(Vector2Int tappedCoordinate)
    {
        if (currentState != GameState.Playing || IsAnyAnimationPlaying)
        {
            return false;
        }

        List<FoundWordData> initialCandidatesFromTap = new List<FoundWordData>();
        foreach (var potentialWord in currentPotentialWords)
        {
            if (potentialWord.Coordinates.Contains(tappedCoordinate) &&
                !wordValidator.IsWordFoundThisSession(potentialWord.Word))
            {
                initialCandidatesFromTap.Add(potentialWord);
            }
        }

        if (initialCandidatesFromTap.Count == 0) return false;
        List<FoundWordData> allConnectedCandidates = FindAllConnectedWords(initialCandidatesFromTap);
        if (allConnectedCandidates.Count == 0) return false;

        List<FoundWordData> wordsToProcessInSequence = FilterSubWordsFromSelectedBatch(allConnectedCandidates);

        wordsToProcessInSequence = wordsToProcessInSequence
                                    .Where(w => !wordValidator.IsWordFoundThisSession(w.Word))
                                    .GroupBy(w => w.ID) 
                                    .Select(g => g.First())         
                                    .ToList();
        if (wordsToProcessInSequence.Count == 0) return false;

        wordsToProcessInSequence = wordsToProcessInSequence
            .OrderByDescending(w => CalculateTotalScoreForWord(w))
            .ThenByDescending(w => w.Word.Length)
            .ThenBy(w => w.ID.ToString())
            .ToList();

        Debug.Log("╔══════════════════════════════════════════════════════════════════╗");
        Debug.Log($"║ 🎮 WORDS FOUND: {wordsToProcessInSequence.Count.ToString().PadLeft(2)} words ready for processing {new string(' ', 20)}║");
        Debug.Log("╚══════════════════════════════════════════════════════════════════╝");
        
        foreach (var word in wordsToProcessInSequence)
        {
            int wordScore = CalculateTotalScoreForWord(word);
            Debug.Log($"📝 '{word.Word}' ({word.Word.Length} letters) = {wordScore} points");
        }
        
        int totalPotentialScore = wordsToProcessInSequence.Sum(w => CalculateTotalScoreForWord(w));
        Debug.Log($"💰 Total potential score from this tap: {totalPotentialScore} points");
        Debug.Log("");

        StartCoroutine(ProcessWordsSequentially(wordsToProcessInSequence));
        return true;
    }

    private List<FoundWordData> FilterSubWordsFromSelectedBatch(List<FoundWordData> candidates)
    {
        if (candidates == null || candidates.Count <= 1) return candidates ?? new List<FoundWordData>();

        var sortedCandidates = candidates.OrderByDescending(w => w.Word.Length).ThenBy(w => w.ID).ToList();
        List<FoundWordData> keptWords = new List<FoundWordData>();
        HashSet<System.Guid> discardedWordIds = new HashSet<System.Guid>();

        for (int i = 0; i < sortedCandidates.Count; i++)
        {
            FoundWordData currentWord = sortedCandidates[i];
            if (discardedWordIds.Contains(currentWord.ID)) continue;
            for (int j = 0; j < sortedCandidates.Count; j++)
            {
                if (i == j) continue;
                FoundWordData otherWord = sortedCandidates[j];
                if (discardedWordIds.Contains(otherWord.ID)) continue;
                if (otherWord.Word.Length < currentWord.Word.Length &&
                    currentWord.Word.Contains(otherWord.Word) &&
                    AreCoordinatesContainedAndAligned(otherWord.Coordinates, otherWord.GetOrientation(), currentWord.Coordinates, currentWord.GetOrientation()))
                {
                    discardedWordIds.Add(otherWord.ID);
                }
            }
        }
        foreach (var word in sortedCandidates)
        {
            if (!discardedWordIds.Contains(word.ID)) keptWords.Add(word);
        }
        return keptWords;
    }

    private bool AreCoordinatesContainedAndAligned(
        List<Vector2Int> innerCoords, FoundWordData.WordOrientation innerOrientation,
        List<Vector2Int> outerCoords, FoundWordData.WordOrientation outerOrientation)
    {
        if (innerCoords == null || outerCoords == null || innerCoords.Count == 0 || innerCoords.Count > outerCoords.Count) return false;
        if (innerOrientation != outerOrientation) return false;
        for (int i = 0; i <= outerCoords.Count - innerCoords.Count; i++)
        {
            bool match = true;
            for (int j = 0; j < innerCoords.Count; j++)
            {
                if (outerCoords[i + j] != innerCoords[j]) { match = false; break; }
            }
            if (match) return true;
        }
        return false;
    }

    private List<FoundWordData> FindAllConnectedWords(List<FoundWordData> startingWords)
    {
        List<FoundWordData> connectedWords = new List<FoundWordData>();
        Queue<FoundWordData> wordsToVisit = new Queue<FoundWordData>();
        HashSet<System.Guid> visitedWordIds = new HashSet<System.Guid>();

        foreach (var startWord in startingWords)
        {
            if (!wordValidator.IsWordFoundThisSession(startWord.Word) && visitedWordIds.Add(startWord.ID))
            {
                wordsToVisit.Enqueue(startWord);
                connectedWords.Add(startWord);
            }
        }

        while (wordsToVisit.Count > 0)
        {
            FoundWordData currentWord = wordsToVisit.Dequeue();
            Vector2Int intersectionPoint;
            foreach (var potentialWordOnGrid in currentPotentialWords)
            {
                if (potentialWordOnGrid.ID == currentWord.ID ||
                    visitedWordIds.Contains(potentialWordOnGrid.ID) ||
                    wordValidator.IsWordFoundThisSession(potentialWordOnGrid.Word))
                {
                    continue;
                }
                if (wordValidator.CheckIntersection(currentWord, potentialWordOnGrid, out intersectionPoint))
                {
                    if (visitedWordIds.Add(potentialWordOnGrid.ID))
                    {
                        connectedWords.Add(potentialWordOnGrid);
                        wordsToVisit.Enqueue(potentialWordOnGrid);
                    }
                }
            }
        }
        return connectedWords;
    }

    private IEnumerator ProcessWordsSequentially(List<FoundWordData> wordsToAnimateInOrder)
    {
        if (wordsToAnimateInOrder == null || wordsToAnimateInOrder.Count == 0) yield break;

        isProcessingSequentialWords = true;
        
        // NEW: Use the integrated scoring system at the beginning
        yield return StartCoroutine(ProcessScoringForWords(wordsToAnimateInOrder));
        
        // Check for win condition after scoring
        if (currentScore >= targetScoreForLevel && currentState == GameState.Playing && !hasWon)
        {
            PlayerWins();
            if(currentState == GameState.GameOver) 
            {
                isProcessingSequentialWords = false;
                yield break;
            }
        }
        List<Vector2Int> allUniqueAffectedCoordinatesFromThisSequence = new List<Vector2Int>();
        Dictionary<Vector2Int, int> cellUsageCountInSequence = new Dictionary<Vector2Int, int>();

        foreach (var wordData in wordsToAnimateInOrder)
        {
            if (wordData.Coordinates == null) continue;
            foreach (var coord in wordData.Coordinates)
            {
                if (cellUsageCountInSequence.ContainsKey(coord)) cellUsageCountInSequence[coord]++;
                else cellUsageCountInSequence[coord] = 1;
                allUniqueAffectedCoordinatesFromThisSequence.Add(coord);
            }
        }

        for (int wordIdx = 0; wordIdx < wordsToAnimateInOrder.Count; wordIdx++)
        {
            FoundWordData currentWordData = wordsToAnimateInOrder[wordIdx];
            if (wordValidator.IsWordFoundThisSession(currentWordData.Word)) continue;

            Debug.Log($"GM.ProcessSeq: Animating word {wordIdx + 1}/{wordsToAnimateInOrder.Count}: {currentWordData.Word}");

            if (wordGridManager != null && currentWordData.Coordinates != null)
            {
                foreach (Vector2Int coord in currentWordData.Coordinates)
                {
                    CellController cellController = wordGridManager.GetCellController(coord);
                    if (cellController == null) continue;

                    if (cellUsageCountInSequence.ContainsKey(coord))
                    {
                        cellUsageCountInSequence[coord]--;

                        if (cellUsageCountInSequence[coord] == 0)
                        {
                            cellController.SetAlpha(0f);
                        }
                        else
                        {
                            FoundWordData nextSharingWord = default;
                            bool foundNextUser = false;
                            for (int nextWordIdx = wordIdx + 1; nextWordIdx < wordsToAnimateInOrder.Count; nextWordIdx++)
                            {
                                if (wordsToAnimateInOrder[nextWordIdx].Coordinates != null &&
                                    wordsToAnimateInOrder[nextWordIdx].Coordinates.Contains(coord))
                                {
                                    nextSharingWord = wordsToAnimateInOrder[nextWordIdx];
                                    foundNextUser = true;
                                    break;
                                }
                            }
                            if (foundNextUser && currentAppliedHighlightColors.TryGetValue(nextSharingWord.ID, out Color nextWordColor))
                            {
                                cellController.SetHighlightState(true, nextWordColor);
                                cellController.SetAlpha(1f);
                            }
                            else
                            {
                                cellController.SetHighlightState(false, cellController.GetDefaultColor());
                                cellController.SetAlpha(1f);
                                Debug.LogWarning($"GM.ProcessSeq: Shared cell {coord} for '{currentWordData.Word}' had usage but couldn't find/color for next. Reverted.");
                            }
                        }
                    }
                    else
                    {
                        cellController.SetAlpha(0f);
                        Debug.LogError($"GM.ProcessSeq: Cell {coord} for '{currentWordData.Word}' not found in usage count. Made invisible.");
                    }
                }
            }

            List<GameObject> floatingPrefabsForThisWord = new List<GameObject>();
            if (effectsManager != null && currentWordData.Coordinates != null)
            {
                List<RectTransform> sourceCellRects = GetRectTransformsForCoords(currentWordData.Coordinates);
                if (sourceCellRects != null && sourceCellRects.Count == currentWordData.Word.Length)
                {
                    floatingPrefabsForThisWord = effectsManager.SpawnAndFloatLetterPrefabs(sourceCellRects, currentWordData.Word);
                }
            }

            currentPotentialWords.RemoveAll(pwd => pwd.ID == currentWordData.ID);

            if (effectsManager != null && floatingPrefabsForThisWord.Count > 0)
            {
                yield return StartCoroutine(effectsManager.PerformGlobalLiftOff(floatingPrefabsForThisWord));
            }

            // NEW: Only handle visual effects, no scoring (scoring is handled at the beginning)
            if (effectsManager != null && floatingPrefabsForThisWord.Count > 0)
            {
                // Just fly letters for visual effect, no score calculation
                yield return StartCoroutine(effectsManager.PerformGlobalLiftOff(floatingPrefabsForThisWord));
                
                // Clean up visual effects (simplified - no scoring)
                for (int i = floatingPrefabsForThisWord.Count - 1; i >= 0; i--)
                {
                    if (floatingPrefabsForThisWord[i] != null)
                    {
                        Destroy(floatingPrefabsForThisWord[i]);
                    }
                }
            }

            // Word is already marked as found in ProcessScoringForWords
            // No need to mark again or check win condition here


            if (wordIdx < wordsToAnimateInOrder.Count - 1 && visualPauseBetweenWordsInSequence > 0)
            {
                // If game ended due to win, don't continue with visual pause for next word
                if(currentState == GameState.GameOver) 
                {
                     isProcessingSequentialWords = false;
                     yield break;
                }
                yield return new WaitForSeconds(visualPauseBetweenWordsInSequence);
            }
        }

        // If game ended during the loop, this part might not be reached or necessary.
        if (currentState == GameState.GameOver && hasWon)
        {
            isProcessingSequentialWords = false;
            yield break;
        }

        if (replacementDelayAfterEffectStart > 0) yield return new WaitForSeconds(replacementDelayAfterEffectStart);

        List<Vector2Int> distinctAffectedCoordinates = allUniqueAffectedCoordinatesFromThisSequence.Distinct().ToList();
        if (wordGridManager != null && distinctAffectedCoordinates.Count > 0)
        {
            wordGridManager.ReplaceLettersAt(distinctAffectedCoordinates, true);
            yield return new WaitUntil(() => !wordGridManager.isAnimating);
            // Only trigger validation if the game is still playing
            if (currentState == GameState.Playing)
            {
                wordGridManager.TriggerValidationCheckAndHighlightUpdate();
            }
        }
        isProcessingSequentialWords = false;
    }

    public void ClearPotentialWords()
    {
        currentPotentialWords.Clear();
        currentAppliedHighlightColors.Clear();
    }

    /// <summary>
    /// New integrated scoring system that calculates scores for multiple words with modifiers
    /// and displays them through the NumericalScoreUI
    /// </summary>
    private IEnumerator ProcessScoringForWords(List<FoundWordData> words)
    {
        if (words == null || words.Count == 0) yield break;
        
        // Generate the complete scoring data
        var scoringData = NumericalScoringData.GenerateFromWords(words, this);
        
        // Show the numerical score UI for interesting scenarios
        var modifierManager = WordScroll.Modifiers.ModifierManager.Instance;
        bool hasActiveModifiers = modifierManager != null && modifierManager.GetAllActiveModifiers().Count > 0;
        bool hasIntersectingLetters = scoringData.intersectionScore > 0;
        
        if (words.Count > 1 || hasActiveModifiers || hasIntersectingLetters)
        {
            // Show the animated scoring breakdown
            ShowNumericalScore(words);
            yield return new WaitUntil(() => !IsNumericalScoreAnimating);
        }
        
        // Apply the final score to the game
        ApplyFinalScore(scoringData.finalScore);
        
        // Handle move reduction from modifiers
        ApplyMoveReductionFromModifiers();
        
        // Mark all words as found
        foreach (var word in words)
        {
            wordValidator.MarkWordAsFoundInSession(word.Word);
        }
    }
    
    /// <summary>
    /// Applies the final calculated score to the game total
    /// </summary>
    private void ApplyFinalScore(int finalScore)
    {
        if (finalScore <= 0 || currentState == GameState.GameOver) return;
        
        int previousScore = currentScore;
        currentScore += finalScore;
        
        Debug.Log("╔══════════════════════════════════════════════════════════════════╗");
        Debug.Log($"║ 🎯 SCORE APPLIED: +{finalScore.ToString().PadLeft(3)} points (Total: {currentScore.ToString().PadLeft(4)}) {new string(' ', 18)}║");
        Debug.Log("╚══════════════════════════════════════════════════════════════════╝");
        
        // Additional game state info
        float progressToTarget = targetScoreForLevel > 0 ? (float)currentScore / targetScoreForLevel * 100f : 0f;
        Debug.Log($"📊 Game Progress: {progressToTarget:F1}% to target ({currentScore}/{targetScoreForLevel})");
        
        if (currentState == GameState.Playing)
        {
            Debug.Log($"⏱️  Game Status: Playing | Moves Remaining: {currentMovesRemaining}");
        }
        
        UpdateScoreUI();
        
        // Update debug UI with current score via message
        try
        {
            var debugSystem = GameObject.FindGameObjectWithTag("DebugSystem");
            if (debugSystem != null)
            {
                debugSystem.SendMessage("UpdateCurrentScore", currentScore, SendMessageOptions.DontRequireReceiver);
            }
        }
        catch (UnityException ex)
        {
            // Handle missing tag gracefully - no need to spam console since this is optional
            if (!ex.Message.Contains("Tag: DebugSystem is not defined"))
            {
                Debug.LogWarning($"[GameManager] Debug system error: {ex.Message}");
            }
        }
        
        // Check for win condition
        if (currentScore >= targetScoreForLevel && currentState == GameState.Playing && !hasWon)
        {
            PlayerWins();
        }
        
        // Score shake effect
        if (scoreTextRectTransform != null && currentState == GameState.Playing)
        {
            scoreTextRectTransform.DOKill(true);
            scoreTextRectTransform.DOShakePosition(scoreShakeDuration, scoreShakeStrength, scoreShakeVibrato, 90, false, true).SetUpdate(true);
        }
    }
    
    /// <summary>
    /// Applies move reduction from active modifiers
    /// </summary>
    private void ApplyMoveReductionFromModifiers()
    {
        if (currentDisplayMode != DisplayMode.Moves) return;
        
        var modifierManager = WordScroll.Modifiers.ModifierManager.Instance;
        if (modifierManager == null) return;
        
        var activeModifiers = modifierManager.GetAllActiveModifiers();
        
        foreach (var modifier in activeModifiers)
        {
            if (modifier.effectType == WordScroll.Modifiers.ModifierEffectType.GeneralScoreBonusAndMoveReduction)
            {
                int moveReduction = Mathf.RoundToInt(modifier.moveReductionPercentage * startingMoves / 100f);
                currentMovesRemaining = Mathf.Max(0, currentMovesRemaining - moveReduction);
                
                Debug.Log("┌──────────────────────────────────────────────────────────────────┐");
                Debug.Log($"│ 🎛️  MODIFIER APPLIED: {modifier.cardName.PadRight(40)} │");
                Debug.Log("└──────────────────────────────────────────────────────────────────┘");
                Debug.Log($"📉 Move Reduction: -{moveReduction} moves ({modifier.moveReductionPercentage:F1}% of {startingMoves} starting moves)");
                Debug.Log($"⏱️  Remaining Moves: {currentMovesRemaining}");
                
                UpdateMovesUI();
                
                // Check if moves ran out
                if (currentMovesRemaining <= 0)
                {
                    PlayerLoses();
                    return;
                }
            }
        }
    }
    
    /// <summary>
    /// Legacy method - used only for visual effects animation callbacks.
    /// For actual scoring, use ProcessScoringForWords instead.
    /// </summary>
    private void HandleSingleLetterScore(int pointsToAdd)
    {
        // This is now just used for visual effect callbacks from the EffectsManager
        // The actual scoring is handled by ProcessScoringForWords
        // We can keep this empty or add just visual feedback if needed
        Debug.Log($"HandleSingleLetterScore called with {pointsToAdd} (visual effect only)");
    }
    
    /// <summary>
    /// Updates the score display UI
    /// </summary>
    private void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = currentScore.ToString();
        }
        
        if (scoreProgressBar != null)
        {
            float progress = targetScoreForLevel > 0 ? (float)currentScore / targetScoreForLevel : 0f;
            scoreProgressBar.value = Mathf.Clamp01(progress);
        }
    }
    
    /// <summary>
    /// Updates the timer display UI
    /// </summary>
    private void UpdateTimerUI()
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(currentTimeRemaining / 60f);
            int seconds = Mathf.FloorToInt(currentTimeRemaining % 60f);
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }
    
    /// <summary>
    /// Updates the moves display UI
    /// </summary>
    private void UpdateMovesUI()
    {
        if (movesText != null)
        {
            movesText.text = $"Moves: {currentMovesRemaining}";
        }
    }
    
    /// <summary>
    /// Updates the timer each frame
    /// </summary>
    private void UpdateTimer()
    {
        if (currentDisplayMode == DisplayMode.Timer && currentState == GameState.Playing)
        {
            currentTimeRemaining -= Time.deltaTime;
            UpdateTimerUI();
            
            if (currentTimeRemaining <= 0f)
            {
                currentTimeRemaining = 0f;
                PlayerLoses(); // Time's up!
            }
        }
    }
    
    /// <summary>
    /// Gets RectTransforms for the given coordinates
    /// </summary>
    private List<RectTransform> GetRectTransformsForCoords(List<Vector2Int> coordinates)
    {
        List<RectTransform> rectTransforms = new List<RectTransform>();
        
        if (wordGridManager == null || coordinates == null) return rectTransforms;
        
        foreach (var coord in coordinates)
        {
            var cellController = wordGridManager.GetCellController(coord);
            if (cellController != null && cellController.RectTransform != null)
            {
                rectTransforms.Add(cellController.RectTransform);
            }
        }
        
        return rectTransforms;
    }
    
    /// <summary>
    /// Calculates the score value for a specific letter (for UI display purposes)
    /// </summary>
    public int CalculateScoreValueForLetter(char letter)
    {
        return GetPointsForActualScoring(letter);
    }
    
    /// <summary>
    /// Decrements the move counter by 1
    /// </summary>
    public void DecrementMoves()
    {
        if (currentDisplayMode != DisplayMode.Moves) return;
        
        currentMovesRemaining--;
        UpdateMovesUI();
        
        if (currentMovesRemaining <= 0)
        {
            currentMovesRemaining = 0;
            PlayerLoses(); // No moves left!
        }
    }
    
    /// <summary>
    /// Navigate back to the home screen/main menu
    /// </summary>
    public void GoToHomeScreen()
    {
        Debug.Log($"[GameManager] Navigating to home screen: {homeSceneName}");
        
        // Stop any ongoing tweens to prevent conflicts
        DOTween.KillAll();
        
        // Load the home screen scene
        SceneManager.LoadScene(homeSceneName);
    }
    
    /// <summary>
    /// Restart the current game scene
    /// </summary>
    public void RestartGame()
    {
        Debug.Log("[GameManager] Restarting current game scene");
        
        // Stop any ongoing tweens to prevent conflicts
        DOTween.KillAll();
        
        // Reload the current scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}