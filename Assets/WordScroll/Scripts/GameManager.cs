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
                   (wordGridManager != null && wordGridManager.isAnimating);
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

    [Header("Modifier References")]
    private WordScroll.Modifiers.ModifierManager modifierManager;

    [Header("Timing & Combo Settings")]
    [SerializeField] private float replacementDelayAfterEffectStart = 0.4f;
    [SerializeField] private float visualPauseBetweenWordsInSequence = 0.25f;

    [Header("Effects")]
    [SerializeField] private float scoreShakeDuration = 0.2f;
    [SerializeField] private float scoreShakeStrength = 3f;
    [SerializeField] private int scoreShakeVibrato = 15;

    private float currentTimeRemaining;
    private int currentMovesRemaining;
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

        // Subscribe to ScoreManager's OnScoreChanged event for UI updates
        if (WordScroll.Managers.ScoreManager.Instance != null)
        {
            WordScroll.Managers.ScoreManager.OnScoreChanged += OnScoreChangedHandler;
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from ScoreManager event
        if (WordScroll.Managers.ScoreManager.Instance != null)
        {
            WordScroll.Managers.ScoreManager.OnScoreChanged -= OnScoreChangedHandler;
        }
    }

    private void OnScoreChangedHandler(int newScore)
    {
        // Update UI and check win condition
        if (scoreProgressBar != null)
        {
            scoreProgressBar.value = Mathf.Min(newScore, targetScoreForLevel);
        }
        if (scoreText != null)
        {
            scoreText.text = $"{newScore} / {targetScoreForLevel}";
        }
        if (newScore >= targetScoreForLevel && currentState == GameState.Playing && !hasWon)
        {
            PlayerWins();
        }
        if (scoreTextRectTransform != null && currentState == GameState.Playing)
        {
            scoreTextRectTransform.DOKill(true);
            scoreTextRectTransform.DOShakePosition(scoreShakeDuration, scoreShakeStrength, scoreShakeVibrato, 90, false, true).SetUpdate(true);
        }
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
        hasWon = false; // Reset win state

        if (scoreProgressBar != null)
        {
            scoreProgressBar.maxValue = targetScoreForLevel;
            scoreProgressBar.value = 0;
        }
        if (scoreText != null)
        {
            scoreText.text = $"0 / {targetScoreForLevel}";
        }
        // Reset ScoreManager's score
        if (WordScroll.Managers.ScoreManager.Instance != null)
        {
            WordScroll.Managers.ScoreManager.Instance.ResetScore();
        }

        currentPotentialWords.Clear();
        currentAppliedHighlightColors.Clear();

        if (modifierManager == null)
        {
            modifierManager = FindFirstObjectByType<WordScroll.Modifiers.ModifierManager>();
        }

        int movesToSet = startingMoves;
        if (modifierManager != null)
        {
            var moveReductionMod = modifierManager.GetActiveModifierByType(WordScroll.Modifiers.ModifierEffectType.GeneralScoreBonusAndMoveReduction);
            if (moveReductionMod != null)
            {
                float reduction = moveReductionMod.moveReductionPercentage;
                movesToSet = Mathf.RoundToInt(startingMoves * (1f - reduction));
                movesToSet = Mathf.Max(1, movesToSet); // Ensure at least 1 move
                Debug.Log($"GameManager: Applied move reduction modifier '{moveReductionMod.cardName}'. Moves reduced to {movesToSet}");
            }
        }

        if (statusDisplayGroup != null)
        {
            bool isGroupActive = currentDisplayMode != DisplayMode.None;
            statusDisplayGroup.SetActive(isGroupActive);
            if (isGroupActive)
            {
                if (timerText != null) timerText.gameObject.SetActive(currentDisplayMode == DisplayMode.Timer);
                if (movesText != null) movesText.gameObject.SetActive(currentDisplayMode == DisplayMode.Moves);
                if (currentDisplayMode == DisplayMode.Timer) { currentTimeRemaining = gameTimeLimit; UpdateTimerUI(); }
                else if (currentDisplayMode == DisplayMode.Moves) { currentMovesRemaining = movesToSet; UpdateMovesUI(); }
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

    private void UpdateTimer()
    {
        if (currentState != GameState.Playing) return; // Only update timer if playing

        if (currentTimeRemaining > 0)
        {
            currentTimeRemaining -= Time.deltaTime;
            UpdateTimerUI();
            if (currentTimeRemaining <= 0)
            {
                currentTimeRemaining = 0;
                UpdateTimerUI();
                EndGame(timeout: true, noMoves: false, playerDidWin: false);
            }
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
        if (currentState != GameState.Playing) return; // Only decrement if playing

        currentMovesRemaining--;
        UpdateMovesUI();
        if (currentMovesRemaining <= 0)
        {
            currentMovesRemaining = 0;
            UpdateMovesUI();
            EndGame(timeout: false, noMoves: true, playerDidWin: false);
        }
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
                Debug.LogError($"GM.GetRects: Could not get active CellController/RectTransform for coord {coord}.");
                return null;
            }
        }
        return rects;
    }

    public List<FoundWordData> GetCurrentPotentialWords() => new List<FoundWordData>(currentPotentialWords);
    public Dictionary<System.Guid, Color> GetCurrentAppliedHighlightColors() => new Dictionary<System.Guid, Color>(currentAppliedHighlightColors);
    public bool IsWordInCurrentProcessingSequence(System.Guid wordId) => false;

    // Make this coroutine public so it can be started from other scripts
    public IEnumerator ProcessWordsSequentially(List<FoundWordData> wordsToAnimateInOrder)
    {
        if (wordsToAnimateInOrder == null || wordsToAnimateInOrder.Count == 0) yield break;

        isProcessingSequentialWords = true;
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
                // Fly prefabs to score and destroy them after animation
                List<int> individualLetterScores = currentWordData.Word.Select(letter => 1).ToList(); // Or use your scoring logic
                yield return StartCoroutine(effectsManager.FlyPrefabsToScoreSequentially(floatingPrefabsForThisWord, individualLetterScores, null));
            }

            // --- CENTRALIZED SCORING ---
            int wordScore = 0;
            if (WordScroll.Managers.ScoreManager.Instance != null)
            {
                wordScore = WordScroll.Managers.ScoreManager.Instance.CalculateWordScore(currentWordData.Word, currentWordData.Word.ToList());
            }
            else
            {
                Debug.LogError("ScoreManager.Instance is null! Word score not added.");
            }

            wordValidator.MarkWordAsFoundInSession(currentWordData.Word);

            // Check for win condition immediately after a word is scored and marked as found
            if (WordScroll.Managers.ScoreManager.Instance != null && WordScroll.Managers.ScoreManager.Instance.PlayerScore >= targetScoreForLevel && currentState == GameState.Playing && !hasWon)
            {
                PlayerWins();
                if(currentState == GameState.GameOver) 
                {
                    isProcessingSequentialWords = false;
                    yield break;
                }
            }

            if (wordIdx < wordsToAnimateInOrder.Count - 1 && visualPauseBetweenWordsInSequence > 0)
            {
                if(currentState == GameState.GameOver) 
                {
                     isProcessingSequentialWords = false;
                     yield break;
                }
                yield return new WaitForSeconds(visualPauseBetweenWordsInSequence);
            }
        }

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
            if (currentState == GameState.Playing)
            {
                wordGridManager.TriggerValidationCheckAndHighlightUpdate();
            }
        }
        isProcessingSequentialWords = false;
    }
}