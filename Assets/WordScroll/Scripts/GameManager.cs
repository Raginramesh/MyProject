using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using System.Linq;
using System;

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
    // ADDED PUBLIC GETTER FOR SCORING MODE
    public ScoringMode CurrentScoringMode => currentScoringMode;
    [Tooltip("Points per letter, ONLY used if Scoring Mode is LengthBased.")]
    [SerializeField] private int pointsPerLetter = 10;
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
    [SerializeField] private float replacementDelayAfterEffectStart = 0.4f;
    [SerializeField] private float visualPauseBetweenPhases = 0.1f; // Pause after global lift-off

    [Header("Effects")]
    [SerializeField] private float scoreShakeDuration = 0.2f;
    [SerializeField] private float scoreShakeStrength = 3f;
    [SerializeField] private int scoreShakeVibrato = 15;

    private float currentTimeRemaining;
    private int currentMovesRemaining;
    private int currentScore = 0;
    public static GameManager instance; // Made public for easier access, ensure it's set in Awake

    private List<FoundWordData> currentPotentialWords = new List<FoundWordData>();
    private HashSet<System.Guid> idsOfWordsInCurrentSequence = new HashSet<System.Guid>();
    private Dictionary<System.Guid, List<GameObject>> wordToFloatingPrefabsMap = new Dictionary<System.Guid, List<GameObject>>();


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
                effectsManager.ClearAllFloatingLetters(wordToFloatingPrefabsMap);
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
                Time.timeScale = 1f;
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
        idsOfWordsInCurrentSequence.Clear();
        wordToFloatingPrefabsMap.Clear();

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
        if ((timeout && currentDisplayMode != DisplayMode.Timer) || (noMoves && currentDisplayMode != DisplayMode.Moves)) return;

        SetState(GameState.GameOver);
        currentPotentialWords.Clear();
        if (wordGridManager != null) wordGridManager.ClearAllCellHighlights();
        if (gameOverPanel != null) { gameOverPanel.SetActive(true); }
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
                                    .DistinctBy(w => w.ID)
                                    .ToList();

        if (wordsToProcessInSequence.Count == 0)
        {
            return false;
        }

        Debug.Log($"GM.AttemptTap: Starting sequence for {wordsToProcessInSequence.Count} FINAL words: " +
                  $"{string.Join(", ", wordsToProcessInSequence.Select(w => w.Word + $"({w.ID.ToString().Substring(0, 4)})"))}");

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
        if (innerOrientation != outerOrientation)
        {
            return false;
        }
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
                if (otherWord.Word.Length < currentWord.Word.Length &&
                    currentWord.Word.Contains(otherWord.Word) &&
                    AreCoordinatesContainedAndAligned(
                        otherWord.Coordinates, otherWord.GetOrientation(),
                        currentWord.Coordinates, currentWord.GetOrientation()))
                {
                    discardedWordIds.Add(otherWord.ID);
                }
            }
        }
        foreach (var word in sortedCandidates) { if (!discardedWordIds.Contains(word.ID)) keptWords.Add(word); }
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
                wordsToVisit.Enqueue(startWord); connectedWords.Add(startWord);
            }
        }
        while (wordsToVisit.Count > 0)
        {
            FoundWordData currentWord = wordsToVisit.Dequeue(); Vector2Int intersectionPoint;
            foreach (var potentialWordOnGrid in currentPotentialWords)
            {
                if (potentialWordOnGrid.ID == currentWord.ID || visitedWordIds.Contains(potentialWordOnGrid.ID) || wordValidator.IsWordFoundThisSession(potentialWordOnGrid.Word)) continue;
                if (wordValidator.CheckIntersection(currentWord, potentialWordOnGrid, out intersectionPoint))
                {
                    if (visitedWordIds.Add(potentialWordOnGrid.ID)) { connectedWords.Add(potentialWordOnGrid); wordsToVisit.Enqueue(potentialWordOnGrid); }
                }
            }
        }
        return connectedWords;
    }

    private FoundWordData? SelectPrimaryFromCandidates(List<FoundWordData> candidates)
    {
        if (candidates == null || candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0];
        return candidates.OrderByDescending(w => w.Word.Length).ThenBy(w => w.GetOrientation()).ThenBy(w => w.Coordinates[0].x).ThenBy(w => w.Coordinates[0].y).First();
    }

    private IEnumerator ProcessWordsSequentially(List<FoundWordData> wordsToAnimate)
    {
        if (wordsToAnimate == null || wordsToAnimate.Count == 0)
        {
            yield break;
        }

        isProcessingSequentialWords = true;
        idsOfWordsInCurrentSequence.Clear();
        wordToFloatingPrefabsMap.Clear();

        List<Vector2Int> allUniqueAffectedCoordinates = new List<Vector2Int>();
        foreach (var wordData in wordsToAnimate)
        {
            allUniqueAffectedCoordinates.AddRange(wordData.Coordinates);
        }
        allUniqueAffectedCoordinates = allUniqueAffectedCoordinates.Distinct().ToList();

        // --- PHASE 1: Make original cells invisible & Spawn Floating Letters ---
        if (wordGridManager != null)
        {
            foreach (Vector2Int coord in allUniqueAffectedCoordinates)
            {
                CellController cell = wordGridManager.GetCellController(coord);
                if (cell != null)
                {
                    CanvasGroup cg = cell.GetComponent<CanvasGroup>();
                    if (cg == null) cg = cell.gameObject.AddComponent<CanvasGroup>();
                    cg.alpha = 0f;
                }
            }
        }

        List<GameObject> allSpawnedPrefabsForGlobalLiftOff = new List<GameObject>();
        foreach (var wordData in wordsToAnimate)
        {
            if (wordValidator.IsWordFoundThisSession(wordData.Word))
            {
                continue;
            }
            idsOfWordsInCurrentSequence.Add(wordData.ID);

            List<RectTransform> sourceCellRects = GetRectTransformsForCoords(wordData.Coordinates);
            if (sourceCellRects == null || sourceCellRects.Count != wordData.Word.Length)
            {
                Debug.LogError($"GM.ProcessSeq (Phase 1): Could not get valid RectTransforms for word \'{wordData.Word}\'. Skipping its floating letters.");
                continue;
            }

            if (effectsManager != null)
            {
                List<GameObject> floatingPrefabsForThisWord = effectsManager.SpawnAndFloatLetterPrefabs(sourceCellRects, wordData.Word);
                wordToFloatingPrefabsMap[wordData.ID] = floatingPrefabsForThisWord;
                allSpawnedPrefabsForGlobalLiftOff.AddRange(floatingPrefabsForThisWord); // Collect for global lift-off
            }
            currentPotentialWords.RemoveAll(pwd => pwd.ID == wordData.ID);
            if (wordGridManager != null) wordGridManager.ClearHighlightForSpecificWord(wordData);
        }

        // --- NEW PHASE 1.5: Global Lift-Off for ALL spawned letters ---
        if (effectsManager != null && allSpawnedPrefabsForGlobalLiftOff.Count > 0)
        {
            Debug.Log($"GM: Initiating Global Lift-Off for {allSpawnedPrefabsForGlobalLiftOff.Count} letters.");
            yield return StartCoroutine(effectsManager.PerformGlobalLiftOff(allSpawnedPrefabsForGlobalLiftOff));
        }

        if (visualPauseBetweenPhases > 0) yield return new WaitForSeconds(visualPauseBetweenPhases); // Pause after global lift-off

        // --- PHASE 2: Sequentially Fly Letters to Score (Word by Word, Letter by Letter) ---
        foreach (var wordData in wordsToAnimate)
        {
            if (!idsOfWordsInCurrentSequence.Contains(wordData.ID))
            {
                continue;
            }
            if (wordValidator.IsWordFoundThisSession(wordData.Word))
            {
                if (wordToFloatingPrefabsMap.TryGetValue(wordData.ID, out List<GameObject> prefabsToClean))
                {
                }
                wordToFloatingPrefabsMap.Remove(wordData.ID);
                continue;
            }

            Debug.Log($"GM.ProcessSeq (Phase 2): Flying letters for word: {wordData.Word} (ID: {wordData.ID})");

            if (wordToFloatingPrefabsMap.TryGetValue(wordData.ID, out List<GameObject> floatingPrefabsForThisWord) && floatingPrefabsForThisWord.Count > 0)
            {
                List<int> individualLetterScores = wordData.Word.Select(letter => CalculateScoreValueForLetter(letter)).ToList();
                if (effectsManager != null)
                {
                    yield return StartCoroutine(effectsManager.FlyPrefabsToScoreSequentially(floatingPrefabsForThisWord, individualLetterScores, HandleSingleLetterScore));
                }
                else
                {
                    foreach (int scoreValue in individualLetterScores) HandleSingleLetterScore(scoreValue);
                }
            }
            else
            {
                Debug.LogWarning($"GM.ProcessSeq (Phase 2): No floating prefabs found in map for word \'{wordData.Word}\'. Scoring directly (if not already scored).");
                if (!wordValidator.IsWordFoundThisSession(wordData.Word))
                {
                    List<int> individualLetterScores = wordData.Word.Select(letter => CalculateScoreValueForLetter(letter)).ToList();
                    foreach (int scoreValue in individualLetterScores) HandleSingleLetterScore(scoreValue);
                }
            }

            wordValidator.MarkWordAsFoundInSession(wordData.Word);
            wordToFloatingPrefabsMap.Remove(wordData.ID);
        }

        // --- PHASE 3: Grid Replacement ---
        if (replacementDelayAfterEffectStart > 0) yield return new WaitForSeconds(replacementDelayAfterEffectStart);

        if (wordGridManager != null && allUniqueAffectedCoordinates.Count > 0)
        {
            wordGridManager.ReplaceLettersAt(allUniqueAffectedCoordinates, true);
            yield return new WaitUntil(() => !wordGridManager.isAnimating);
            wordGridManager.TriggerValidationCheckAndHighlightUpdate();
        }

        isProcessingSequentialWords = false;
        idsOfWordsInCurrentSequence.Clear();
        wordToFloatingPrefabsMap.Clear();
    }

    public void ClearPotentialWords()
    {
        currentPotentialWords.Clear();
    }

    private void HandleSingleLetterScore(int pointsToAdd)
    {
        if (pointsToAdd <= 0 || currentState == GameState.GameOver) return;
        currentScore += pointsToAdd;
        UpdateScoreUI();
        if (scoreTextRectTransform != null)
        {
            scoreTextRectTransform.DOKill(true);
            scoreTextRectTransform.DOShakePosition(scoreShakeDuration, scoreShakeStrength, scoreShakeVibrato, 90, false, true);
        }
    }

    // Public method to get score for a letter, used by CellController
    public int CalculateScoreValueForLetter(char letter)
    {
        char upperLetter = char.ToUpperInvariant(letter);
        if (currentScoringMode == ScoringMode.ScrabbleBased && scrabbleLetterValues.TryGetValue(upperLetter, out int val))
            return val;
        // If LengthBased, we don't want to show individual "pointsPerLetter" on each tile,
        // as the request is for "respective points" (implying Scrabble-like differentiation).
        // So, return 0 if not ScrabbleBased or letter has no Scrabble value.
        // CellController will hide the score text if score is 0.
        if (currentScoringMode == ScoringMode.LengthBased) return 0; // Or pointsPerLetter if you want to show it
        return 0;
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
        Time.timeScale = 1f;
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
            if (cell != null && cell.RectTransform != null && cell.gameObject.activeInHierarchy)
            {
                rects.Add(cell.RectTransform);
            }
            else
            {
                Debug.LogError($"GM.GetRects: Could not get active CellController/RectTransform for coord {coord}."); return null;
            }
        }
        return rects;
    }

    public List<FoundWordData> GetCurrentPotentialWords()
    {
        return new List<FoundWordData>(currentPotentialWords);
    }
    public bool IsWordInCurrentProcessingSequence(System.Guid wordId)
    {
        return idsOfWordsInCurrentSequence.Contains(wordId);
    }
}

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