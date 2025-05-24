using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using System.Linq;
using System;
using Unity.VisualScripting;

public class GameManager : MonoBehaviour
{
    public enum ScoringMode { LengthBased, ScrabbleBased }
    public enum GameState { Initializing, Playing, Paused, GameOver }
    public enum DisplayMode { Timer, Moves, None }

    [Header("Game State")]
    [SerializeField] private GameState currentState = GameState.Initializing;
    public GameState CurrentStatePublic => currentState;

    private bool isProcessingSequentialWords = false;
    public bool IsAnyAnimationPlaying
    {
        get
        {
            return isProcessingSequentialWords ||
                   (effectsManager != null && effectsManager.IsAnimating) ||
                   (wordGridManager != null && wordGridManager.isAnimating) ||
                   (gridInputHandler != null && gridInputHandler.IsPerformingInertiaScroll);
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
    public ScoringMode CurrentScoringMode => currentScoringMode;
    [Tooltip("Points per letter, used if Scoring Mode is LengthBased for actual scoring, and for ScrabbleBased if a letter has no defined Scrabble value (as a fallback, typically 1).")]
    [SerializeField] private int pointsPerLetter = 10; // Used for LengthBased scoring and fallback
    private Dictionary<char, int> scrabbleLetterValues;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private RectTransform scoreTextRectTransform;
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

    [Header("Timing & Combo Settings")]
    [SerializeField] private float replacementDelayAfterEffectStart = 0.4f; // Delay after *all* words in sequence are processed, before grid replacement
    [SerializeField] private float visualPauseBetweenWordsInSequence = 0.25f; // Pause after one word finishes, before next one starts in a combo

    [Header("Effects")]
    [SerializeField] private float scoreShakeDuration = 0.2f;
    [SerializeField] private float scoreShakeStrength = 3f;
    [SerializeField] private int scoreShakeVibrato = 15;

    private float currentTimeRemaining;
    private int currentMovesRemaining;
    private int currentScore = 0;
    public static GameManager instance;

    private List<FoundWordData> currentPotentialWords = new List<FoundWordData>();
    // private HashSet<System.Guid> idsOfWordsInCurrentSequence = new HashSet<System.Guid>(); // Less critical now, direct iteration
    // private Dictionary<System.Guid, List<GameObject>> wordToFloatingPrefabsMap = new Dictionary<System.Guid, List<GameObject>>(); // Will be managed per-word inside loop

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
        if (scoreText == null) Debug.LogError("GM: Score Text (TMP) missing!", this);
        if (scoreTextRectTransform == null && effectsManager != null) Debug.LogWarning("GM: Score Text RectTransform is not set. EffectsManager might need this explicitly if not passed.", this);


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
                // Clear any active floating letters. Since wordToFloatingPrefabsMap is now transient (per word),
                // this call might need to be smarter or EffectsManager needs a global clear.
                // For now, assuming EffectsManager can handle clearing its own active tweens/objects if needed.
                effectsManager.ClearAllFloatingLetters(new Dictionary<Guid, List<GameObject>>()); // Pass empty or make EM clear all
            }
        }
        isProcessingSequentialWords = false; // Ensure this is reset if state changes abruptly

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
                Time.timeScale = 1f; // Keep time normal for game over animations/UI
                if (gridInputHandler != null) gridInputHandler.enabled = false;
                break;
        }
    }

    private void StartGame()
    {
        SetState(GameState.Initializing);
        currentScore = 0;
        UpdateScoreUI();
        currentPotentialWords.Clear();
        // idsOfWordsInCurrentSequence.Clear(); // Not used this way anymore
        // wordToFloatingPrefabsMap.Clear(); // Not used this way anymore

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

    private void EndGame(bool timeout = false, bool noMoves = false)
    {
        if (currentState == GameState.GameOver) return;
        // Ensure game ends only for the configured mode
        if ((timeout && currentDisplayMode != DisplayMode.Timer) || (noMoves && currentDisplayMode != DisplayMode.Moves)) return;

        SetState(GameState.GameOver);
        currentPotentialWords.Clear();
        if (wordGridManager != null) wordGridManager.ClearAllCellHighlights();
        if (gameOverPanel != null) { gameOverPanel.SetActive(true); }
        // Potentially save score or trigger other game over logic here
    }

    public void UpdatePotentialWordsDisplay(List<FoundWordData> potentialWordsFromValidator)
    {
        if (currentState != GameState.Playing && currentState != GameState.Initializing)
        {
            currentPotentialWords.Clear();
            if (wordGridManager != null) wordGridManager.ClearAllCellHighlights();
            return;
        }
        currentPotentialWords = potentialWordsFromValidator ?? new List<FoundWordData>();
        if (wordGridManager != null)
        {
            wordGridManager.HighlightPotentialWordCells(currentPotentialWords);
        }
    }

    // --- NEW SCORING HELPER METHODS ---
    private int GetPointsForActualScoring(char letter)
    {
        char upperLetter = char.ToUpperInvariant(letter);
        if (currentScoringMode == ScoringMode.ScrabbleBased)
        {
            if (scrabbleLetterValues.TryGetValue(upperLetter, out int val))
            {
                return val;
            }
            return 1; // Fallback for Scrabble letters not in dictionary (e.g. blank tile if implemented)
        }
        // LengthBased scoring
        return pointsPerLetter;
    }

    private int CalculateTotalScoreForWord(FoundWordData wordData)
    {
        if (wordData.Word == null) return 0;
        int totalScore = 0;
        foreach (char letter in wordData.Word)
        {
            totalScore += GetPointsForActualScoring(letter);
        }
        return totalScore;
    }
    // --- END NEW SCORING HELPER METHODS ---

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
                !wordValidator.IsWordFoundThisSession(potentialWord.Word)) // Ensure it's not already found
            {
                initialCandidatesFromTap.Add(potentialWord);
            }
        }

        if (initialCandidatesFromTap.Count == 0)
        {
            return false;
        }

        List<FoundWordData> allConnectedCandidates = FindAllConnectedWords(initialCandidatesFromTap);

        if (allConnectedCandidates.Count == 0)
        {
            return false;
        }

        List<FoundWordData> wordsToProcessInSequence = FilterSubWordsFromBatch(allConnectedCandidates);

        wordsToProcessInSequence = wordsToProcessInSequence
                                    .Where(w => !wordValidator.IsWordFoundThisSession(w.Word))
                                    .DistinctBy(w => w.ID) // Ensure unique instances
                                    .ToList();

        if (wordsToProcessInSequence.Count == 0)
        {
            return false;
        }

        // --- SORTING WORDS BY POINTS (NEW) ---
        wordsToProcessInSequence = wordsToProcessInSequence
            .OrderByDescending(w => CalculateTotalScoreForWord(w)) // Primary: Score
            .ThenByDescending(w => w.Word.Length)                  // Secondary: Length
            .ThenBy(w => w.ID.ToString())                          // Tertiary: ID (for stable sort)
            .ToList();
        // --- END SORTING ---

        Debug.Log($"GM.AttemptTap: Starting sequence for {wordsToProcessInSequence.Count} FINAL words (Sorted by Points): " +
                  $"{string.Join(", ", wordsToProcessInSequence.Select(w => w.Word + $"({CalculateTotalScoreForWord(w)}pts, {w.ID.ToString().Substring(0, 4)})"))}");

        StartCoroutine(ProcessWordsSequentially(wordsToProcessInSequence));
        return true;
    }

    private bool AreCoordinatesContainedAndAligned(
        List<Vector2Int> innerCoords, FoundWordData.WordOrientation innerOrientation,
        List<Vector2Int> outerCoords, FoundWordData.WordOrientation outerOrientation)
    {
        if (innerCoords == null || outerCoords == null || innerCoords.Count == 0 || innerCoords.Count > outerCoords.Count)
        {
            return false;
        }
        if (innerOrientation != outerOrientation) // Must have same orientation to be a sub-word in this context
        {
            return false;
        }
        // Check if innerCoords is a contiguous subsegment of outerCoords
        for (int i = 0; i <= outerCoords.Count - innerCoords.Count; i++)
        {
            bool match = true;
            for (int j = 0; j < innerCoords.Count; j++)
            {
                if (outerCoords[i + j] != innerCoords[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) return true;
        }
        return false;
    }

    private List<FoundWordData> FilterSubWordsFromBatch(List<FoundWordData> candidates)
    {
        if (candidates == null || candidates.Count <= 1)
        {
            return candidates ?? new List<FoundWordData>();
        }

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

                // Check if otherWord is a sub-word of currentWord
                if (otherWord.Word.Length < currentWord.Word.Length &&
                    currentWord.Word.Contains(otherWord.Word) && // Basic string containment
                    AreCoordinatesContainedAndAligned( // And actual coordinate alignment
                        otherWord.Coordinates, otherWord.GetOrientation(),
                        currentWord.Coordinates, currentWord.GetOrientation()))
                {
                    discardedWordIds.Add(otherWord.ID);
                }
            }
        }

        foreach (var word in sortedCandidates)
        {
            if (!discardedWordIds.Contains(word.ID))
            {
                keptWords.Add(word);
            }
        }
        return keptWords;
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
            Vector2Int intersectionPoint; // Re-declare or ensure scope

            foreach (var potentialWordOnGrid in currentPotentialWords) // Use the GM's current list
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

    // SelectPrimaryFromCandidates might not be needed if we process all connected words,
    // or it could be used by GridInputHandler if it needs to pick one for visual feedback before GM takes over.
    // For now, assuming the tap directly feeds into the multi-word processing.
    /*
    private FoundWordData? SelectPrimaryFromCandidates(List<FoundWordData> candidates)
    {
        if (candidates == null || candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0];
        return candidates.OrderByDescending(w => w.Word.Length).ThenBy(w => w.GetOrientation()).ThenBy(w => w.Coordinates[0].x).ThenBy(w => w.Coordinates[0].y).First();
    }
    */

    private IEnumerator ProcessWordsSequentially(List<FoundWordData> wordsToAnimateInOrder)
    {
        if (wordsToAnimateInOrder == null || wordsToAnimateInOrder.Count == 0)
        {
            yield break;
        }

        isProcessingSequentialWords = true;
        List<Vector2Int> allUniqueAffectedCoordinatesFromThisSequence = new List<Vector2Int>();

        // --- LOOP THROUGH EACH WORD IN THE SEQUENCE (NEW STRUCTURE) ---
        for (int i = 0; i < wordsToAnimateInOrder.Count; i++)
        {
            FoundWordData currentWordData = wordsToAnimateInOrder[i];

            // Skip if somehow already processed (e.g. if list contained duplicates not caught by earlier filters)
            if (wordValidator.IsWordFoundThisSession(currentWordData.Word))
            {
                Debug.LogWarning($"GM.ProcessSeq: Word '{currentWordData.Word}' was already marked as found. Skipping its animation phase.");
                continue;
            }

            Debug.Log($"GM.ProcessSeq: Starting animation for word {i + 1}/{wordsToAnimateInOrder.Count}: {currentWordData.Word} (ID: {currentWordData.ID})");

            // Collect coordinates for this word (will be added to the master list for final replacement)
            allUniqueAffectedCoordinatesFromThisSequence.AddRange(currentWordData.Coordinates);

            // --- PHASE 1 (Per Word): Make original cells invisible & Spawn Floating Letters ---
            if (wordGridManager != null)
            {
                foreach (Vector2Int coord in currentWordData.Coordinates)
                {
                    CellController cell = wordGridManager.GetCellController(coord);
                    if (cell != null)
                    {
                        CanvasGroup cg = cell.GetComponent<CanvasGroup>();
                        if (cg == null) cg = cell.gameObject.AddComponent<CanvasGroup>();
                        cg.alpha = 0f; // Make original cell invisible
                    }
                }
            }

            List<GameObject> floatingPrefabsForThisWord = new List<GameObject>();
            if (effectsManager != null)
            {
                List<RectTransform> sourceCellRects = GetRectTransformsForCoords(currentWordData.Coordinates);
                if (sourceCellRects != null && sourceCellRects.Count == currentWordData.Word.Length)
                {
                    floatingPrefabsForThisWord = effectsManager.SpawnAndFloatLetterPrefabs(sourceCellRects, currentWordData.Word);
                }
                else
                {
                    Debug.LogError($"GM.ProcessSeq (Phase 1 for {currentWordData.Word}): Could not get valid RectTransforms. Skipping floating letters.");
                }
            }

            // Remove this specific word from currentPotentialWords and clear its highlight
            // This ensures it's not re-highlighted if validation happens mid-sequence for some reason
            currentPotentialWords.RemoveAll(pwd => pwd.ID == currentWordData.ID);
            if (wordGridManager != null) wordGridManager.ClearHighlightForSpecificWord(currentWordData);


            // --- PHASE 1.5 (Per Word): Lift-Off Animation ---
            if (effectsManager != null && floatingPrefabsForThisWord.Count > 0)
            {
                Debug.Log($"GM.ProcessSeq: Lift-Off for {floatingPrefabsForThisWord.Count} letters of '{currentWordData.Word}'.");
                yield return StartCoroutine(effectsManager.PerformGlobalLiftOff(floatingPrefabsForThisWord)); // Re-using global lift-off for a single word's letters
            }

            // --- PHASE 2 (Per Word): Fly Letters to Score & Score Update ---
            if (floatingPrefabsForThisWord.Count > 0) // If we have prefabs to animate
            {
                List<int> individualLetterScores = new List<int>();
                foreach (char letter in currentWordData.Word)
                {
                    individualLetterScores.Add(GetPointsForActualScoring(letter));
                }

                if (effectsManager != null)
                {
                    yield return StartCoroutine(effectsManager.FlyPrefabsToScoreSequentially(floatingPrefabsForThisWord, individualLetterScores, HandleSingleLetterScore));
                }
                else // Fallback if no effects manager, just score directly
                {
                    foreach (int scoreValue in individualLetterScores) HandleSingleLetterScore(scoreValue);
                }
            }
            else // No floating prefabs, score directly
            {
                Debug.LogWarning($"GM.ProcessSeq (Phase 2 for {currentWordData.Word}): No floating prefabs. Scoring directly.");
                foreach (char letter in currentWordData.Word)
                {
                    HandleSingleLetterScore(GetPointsForActualScoring(letter));
                }
            }

            wordValidator.MarkWordAsFoundInSession(currentWordData.Word);
            // Floating prefabs for *this word* are handled (destroyed) by FlyPrefabsToScoreSequentially or should be cleaned up by EffectsManager.

            // Optional: Decrement moves if game mode is Moves (if a word found = 1 move)
            // if (currentDisplayMode == DisplayMode.Moves) DecrementMoves(); // Consider if each word in combo costs a move

            if (i < wordsToAnimateInOrder.Count - 1) // If there are more words in the sequence
            {
                if (visualPauseBetweenWordsInSequence > 0)
                {
                    yield return new WaitForSeconds(visualPauseBetweenWordsInSequence);
                }
            }
        } // --- END LOOP THROUGH EACH WORD ---


        // --- PHASE 3 (Global, after all words in sequence): Grid Replacement ---
        if (replacementDelayAfterEffectStart > 0)
        {
            yield return new WaitForSeconds(replacementDelayAfterEffectStart);
        }

        List<Vector2Int> distinctAffectedCoordinates = allUniqueAffectedCoordinatesFromThisSequence.Distinct().ToList();
        if (wordGridManager != null && distinctAffectedCoordinates.Count > 0)
        {
            Debug.Log($"GM.ProcessSeq (Phase 3): Replacing {distinctAffectedCoordinates.Count} cells.");
            wordGridManager.ReplaceLettersAt(distinctAffectedCoordinates, true); // true for fadeIn
            yield return new WaitUntil(() => !wordGridManager.isAnimating); // Wait for replacement animation

            // Crucial: Trigger re-validation for the entire grid after replacements
            wordGridManager.TriggerValidationCheckAndHighlightUpdate();
        }

        isProcessingSequentialWords = false;
        // currentPotentialWords list is already updated by removing processed words.
        // The TriggerValidationCheckAndHighlightUpdate above will refresh it based on the new grid.
    }


    public void ClearPotentialWords() // Called by WordGridManager.ClearAllCellHighlights
    {
        currentPotentialWords.Clear();
    }

    private void HandleSingleLetterScore(int pointsToAdd)
    {
        if (pointsToAdd <= 0 || currentState == GameState.GameOver) return; // Don't add score if game is over
        currentScore += pointsToAdd;
        UpdateScoreUI();
        if (scoreTextRectTransform != null)
        {
            scoreTextRectTransform.DOKill(true); // Complete any ongoing animation immediately
            scoreTextRectTransform.DOShakePosition(scoreShakeDuration, scoreShakeStrength, scoreShakeVibrato, 90, false, true)
                .SetUpdate(true); // Ensure works even if Time.timeScale is 0 (e.g. if called during pause, though unlikely here)
        }
    }

    // This method is used by CellController to determine what score value to DISPLAY on a tile.
    // It's different from GetPointsForActualScoring used for word totals.
    public int CalculateScoreValueForLetter(char letter)
    {
        char upperLetter = char.ToUpperInvariant(letter);
        if (currentScoringMode == ScoringMode.ScrabbleBased && scrabbleLetterValues.TryGetValue(upperLetter, out int val))
            return val;
        // For LengthBased, individual tiles usually don't show the "pointsPerLetter" value,
        // but the word's total score is based on length. So, return 0 for tile display.
        if (currentScoringMode == ScoringMode.LengthBased) return 0;
        return 0; // Default if not Scrabble or letter not found
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null) scoreText.text = currentScore.ToString();
    }

    private void UpdateTimer()
    {
        if (currentTimeRemaining > 0)
        {
            currentTimeRemaining -= Time.deltaTime;
            UpdateTimerUI();
            if (currentTimeRemaining <= 0) { currentTimeRemaining = 0; UpdateTimerUI(); EndGame(timeout: true); }
        }
    }
    private void UpdateTimerUI()
    {
        if (currentDisplayMode == DisplayMode.Timer && timerText != null && statusDisplayGroup != null && statusDisplayGroup.activeSelf)
        {
            timerText.text = $"{(int)(currentTimeRemaining / 60):00}:{(int)(currentTimeRemaining % 60):00}";
        }
    }

    public void DecrementMoves()
    {
        if (currentState != GameState.Playing || currentDisplayMode != DisplayMode.Moves) return;
        currentMovesRemaining--;
        UpdateMovesUI();
        if (currentMovesRemaining <= 0) { currentMovesRemaining = 0; UpdateMovesUI(); EndGame(noMoves: true); }
    }
    private void UpdateMovesUI()
    {
        if (currentDisplayMode == DisplayMode.Moves && movesText != null && statusDisplayGroup != null && statusDisplayGroup.activeSelf)
        {
            movesText.text = currentMovesRemaining.ToString();
        }
    }

    public void RestartGame()
    {
        Time.timeScale = 1f; // Ensure time scale is reset before loading new scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    public void PauseGame()
    {
        if (currentState == GameState.Playing && !IsAnyAnimationPlaying) SetState(GameState.Paused);
    }
    public void ResumeGame()
    {
        if (currentState == GameState.Paused) SetState(GameState.Playing);
    }
    public void GoToHomeScreen()
    {
        if (string.IsNullOrEmpty(homeSceneName)) { Debug.LogError("Home Scene Name not set!", this); return; }
        Time.timeScale = 1f;
        SceneManager.LoadScene(homeSceneName);
    }
    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private List<RectTransform> GetRectTransformsForCoords(List<Vector2Int> coords)
    {
        List<RectTransform> rects = new List<RectTransform>();
        if (wordGridManager == null || coords == null) { return null; }
        foreach (var coord in coords)
        {
            CellController cell = wordGridManager.GetCellController(coord);
            if (cell != null && cell.RectTransform != null && cell.gameObject.activeInHierarchy) // Check activeInHierarchy
            {
                rects.Add(cell.RectTransform);
            }
            else
            {
                Debug.LogError($"GM.GetRects: Could not get active CellController/RectTransform for coord {coord}. Word processing might be affected.");
                return null; // Critical if a cell is missing for animation
            }
        }
        return rects;
    }

    public List<FoundWordData> GetCurrentPotentialWords()
    {
        return new List<FoundWordData>(currentPotentialWords); // Return a copy
    }

    // This method was used by WordGridManager to check if a word (that it's trying to clear highlight for)
    // is part of the *current* animation sequence.
    // With the new per-word processing, this specific check might be less relevant in WGM,
    // or WGM's ClearHighlightForSpecificWord needs to be aware that only one word is "active" at a time.
    // For now, it always returns false as the old `idsOfWordsInCurrentSequence` is not maintained globally.
    // WGM's logic for clearing highlights might need to rely more on the main `currentPotentialWords` list.
    public bool IsWordInCurrentProcessingSequence(System.Guid wordId)
    {
        // This logic needs to be re-evaluated. In the new model, only one word is "in process" at a time
        // within the ProcessWordsSequentially loop.
        // For external checks (like from WGM), it's safer to assume a word being cleared is not "in sequence"
        // unless WGM is passed the specific word currently being animated.
        // The GameManager now removes words from currentPotentialWords as they are processed.
        return false; // Or, if needed, could check against the 'currentWordData.ID' inside the loop if called from within.
    }
}

// Ensure LinqExtensions is still available if not in a separate file.
// Assuming it's in a global scope or another script.
/*
public static class LinqExtensions
{
    public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
        HashSet<TKey> seenKeys = new HashSet<TKey>();
        foreach (TSource element in source) { if (seenKeys.Add(keySelector(element))) yield return element; }
    }
}
*/