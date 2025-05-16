using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using DG.Tweening;
using System.Linq;

public class GameManager : MonoBehaviour
{
    public enum ScoringMode { LengthBased, ScrabbleBased }
    public enum GameState { Initializing, Playing, Paused, GameOver }
    public enum DisplayMode { Timer, Moves, None }

    [Header("Game State")]
    [SerializeField] private GameState currentState = GameState.Initializing;
    public GameState CurrentStatePublic => currentState;

    public bool IsAnyAnimationPlaying =>
        (wordGridManager != null && wordGridManager.isAnimating) ||
        (effectsManager != null && effectsManager.IsAnimating) ||
        (gridInputHandler != null && gridInputHandler.IsPerformingInertiaScroll);

    [Header("Game Mode & Display")]
    [SerializeField] private DisplayMode currentDisplayMode = DisplayMode.Timer;
    public DisplayMode CurrentGameDisplayMode => currentDisplayMode;
    [SerializeField] private float gameTimeLimit = 120f;
    [SerializeField] private int startingMoves = 50;

    [Header("Scene Navigation")]
    [SerializeField] private string homeSceneName = "HomeScreen";

    [Header("Scoring")]
    [SerializeField] private ScoringMode currentScoringMode = ScoringMode.LengthBased;
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

    [Header("Timing")]
    [SerializeField] private float replacementDelayAfterEffectStart = 0.4f;

    [Header("Effects")]
    [SerializeField] private float scoreShakeDuration = 0.2f;
    [SerializeField] private float scoreShakeStrength = 3f;
    [SerializeField] private int scoreShakeVibrato = 15;

    private float currentTimeRemaining;
    private int currentMovesRemaining;
    private int currentScore = 0;
    private static GameManager instance;

    // <<< NEW: To store words available for tapping >>>
    private List<FoundWordData> currentPotentialWords = new List<FoundWordData>();

    void Awake()
    {
        if (instance == null) { instance = this; /* DontDestroyOnLoad(gameObject); */ }
        else if (instance != this) { Destroy(gameObject); return; }

        InitializeScrabbleValues();
        if (wordGridManager == null) wordGridManager = FindFirstObjectByType<WordGridManager>();
        if (wordValidator == null) wordValidator = FindFirstObjectByType<WordValidator>();
        if (gridInputHandler == null) gridInputHandler = FindFirstObjectByType<GridInputHandler>();
        if (effectsManager == null) effectsManager = FindFirstObjectByType<EffectsManager>();
        if (scoreTextRectTransform == null && scoreText != null) scoreTextRectTransform = scoreText.GetComponent<RectTransform>();

        // Critical reference checks
        if (wordGridManager == null) Debug.LogError("GM: WordGridManager missing!", this);
        if (wordValidator == null) Debug.LogError("GM: WordValidator missing!", this);
        // GridInputHandler is now even more critical for tap
        if (gridInputHandler == null) Debug.LogError("GM: GridInputHandler missing! Tapping will not work.", this);
        if (effectsManager == null) Debug.LogError("GM: EffectsManager missing!", this);
        if (scoreText == null) Debug.LogError("GM: Score Text (TMP) missing!", this);

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
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
        { SetState(GameState.Initializing); }
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
        if (currentState == newState) return;
        currentState = newState;
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
        currentScore = 0; UpdateScoreUI();
        currentPotentialWords.Clear(); // Clear any potential words from a previous game

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
        if (wordGridManager != null) { wordGridManager.InitializeGrid(); }
        else { Debug.LogError("GM: Cannot initialize grid - WordGridManager missing!", this); return; }

        if (wordValidator != null)
        {
            wordValidator.ResetFoundWordsList();
            wordValidator.SetGameManager(this);
            if (wordGridManager != null)
            {
                // <<< CHANGED: Trigger new highlight update flow >>>
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

        string reason = timeout ? "Time Ran Out" : (noMoves ? "No Moves Left" : "Game Over");
        SetState(GameState.GameOver);
        currentPotentialWords.Clear(); // Clear potential words on game over
        if (wordGridManager != null) wordGridManager.ClearAllCellHighlights();


        if (gameOverPanel != null) { gameOverPanel.SetActive(true); /* Populate reason/score */ }
    }

    // --- Tap-to-Validate Logic ---

    /// <summary>
    /// Called by WordGridManager after its validation. Updates highlights.
    /// </summary>
    public void UpdatePotentialWordsDisplay(List<FoundWordData> potentialWordsFromValidator)
    {
        if (currentState != GameState.Playing && currentState != GameState.Initializing) // Allow highlight update during init
        {
            // If not playing, ensure no words are stored and highlights are cleared.
            currentPotentialWords.Clear();
            if (wordGridManager != null) wordGridManager.ClearAllCellHighlights();
            return;
        }

        currentPotentialWords = potentialWordsFromValidator ?? new List<FoundWordData>();

        if (wordGridManager != null)
        {
            // wordGridManager.ClearAllCellHighlights(false); // Clear old ones, but GM holds current list
            wordGridManager.HighlightPotentialWordCells(currentPotentialWords);
        }
        // Debug.Log($"GM: Updated potential words display. {currentPotentialWords.Count} words are tappable.");
    }

    /// <summary>
    /// Called by GridInputHandler when a cell is tapped.
    /// </summary>
    public bool AttemptTapValidation(Vector2Int tappedCoordinate)
    {
        if (currentState != GameState.Playing || IsAnyAnimationPlaying)
        {
            // Debug.Log("GM: Tap validation ignored (not playing or animation active).");
            return false;
        }

        FoundWordData tappedWord = default;
        bool foundMatch = false;

        // Iterate backwards to allow safe removal if needed, though we're not removing here.
        // We need to find the specific FoundWordData that contains this coordinate.
        // A single coordinate might be part of multiple potential words if they overlap.
        // For simplicity, we'll process the first one found that contains the coordinate.
        // More complex logic could present a choice to the user if multiple words share the tapped cell.
        foreach (FoundWordData potentialWord in currentPotentialWords)
        {
            if (potentialWord.Coordinates.Contains(tappedCoordinate))
            {
                tappedWord = potentialWord;
                foundMatch = true;
                break;
            }
        }

        if (foundMatch)
        {
            // Debug.Log($"GM: Tapped word '{tappedWord.Word}' at {tappedCoordinate}. Processing.");
            ProcessSingleWordInternal(tappedWord); // This will handle scoring, effects, and re-validation
            return true;
        }
        else
        {
            // Debug.Log($"GM: Tap at {tappedCoordinate} did not correspond to a known potential word.");
            return false;
        }
    }


    private void ProcessSingleWordInternal(FoundWordData wordData)
    {
        // This method remains largely the same, but it's now triggered by a successful tap
        // instead of directly by WordValidator.
        if (currentState != GameState.Playing || string.IsNullOrEmpty(wordData.Word) || wordData.Coordinates == null || wordData.Coordinates.Count == 0)
        { return; }
        if (effectsManager == null || wordGridManager == null || wordValidator == null)
        { Debug.LogError("GM ProcessSingleWord: Missing critical component reference!"); return; }

        List<RectTransform> sourceCellRects = GetRectTransformsForCoords(wordData.Coordinates);
        if (sourceCellRects == null || sourceCellRects.Count == 0)
        {
            // This might happen if cells were somehow invalidated between tap and processing.
            // Re-trigger validation to refresh state.
            // if (currentState == GameState.Playing && !IsAnyAnimationPlaying) wordGridManager?.TriggerValidationCheckAndHighlightUpdate();
            return;
        }

        // Important: Remove this word from currentPotentialWords so it can't be tapped again
        // before the list is refreshed. Compare by ID for uniqueness.
        currentPotentialWords.RemoveAll(pwd => pwd.ID == wordData.ID);
        // Also, immediately clear its highlight from the grid before effects play
        // This provides instant feedback that the tap was registered for *this* word.
        foreach (Vector2Int coord in wordData.Coordinates)
        {
            CellController cell = wordGridManager.GetCellController(coord);
            if (cell != null)
            {
                // Reset to its default color
                cell.SetHighlightState(false, cell.GetDefaultColor());
            }
        }


        foreach (Vector2Int coord in wordData.Coordinates)
        {
            CellController cell = wordGridManager.GetCellController(coord);
            cell?.FadeOutImmediate();
        }

        List<int> individualLetterScores = wordData.Word.Select(letter => CalculateScoreValueForLetter(letter)).ToList();
        effectsManager.PlayFlyToScoreEffect(sourceCellRects, wordData.Word, individualLetterScores, HandleSingleLetterScore);

        DOVirtual.DelayedCall(replacementDelayAfterEffectStart, () => {
            if (this == null || wordGridManager == null || wordValidator == null || currentState != GameState.Playing) { return; }

            wordGridManager.ReplaceLettersAt(wordData.Coordinates, true);
            wordValidator.MarkWordAsFoundInSession(wordData.Word); // Mark word as found *after* successful processing

            float fadeInDuration = wordGridManager.CellFadeInDuration;
            DOVirtual.DelayedCall(fadeInDuration + 0.05f, () => {
                if (this == null || wordValidator == null || wordGridManager == null || currentState != GameState.Playing) { return; }
                if (!IsAnyAnimationPlaying)
                {
                    // <<< CHANGED: Trigger new highlight update flow >>>
                    wordGridManager.TriggerValidationCheckAndHighlightUpdate();
                }
            }, false);
        }, false);
    }

    public void ClearPotentialWords() // Called by WordGridManager on its ClearAllHighlights
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

    private int CalculateScoreValueForLetter(char letter)
    {
        char upperLetter = char.ToUpperInvariant(letter);
        if (currentScoringMode == ScoringMode.ScrabbleBased && scrabbleLetterValues.TryGetValue(upperLetter, out int val))
            return val;
        if (currentScoringMode == ScoringMode.LengthBased) return pointsPerLetter;
        return 0;
    }

    private void UpdateScoreUI() { if (scoreText != null) scoreText.text = "Score: " + currentScore.ToString(); }

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
        currentMovesRemaining--; UpdateMovesUI();
        if (currentMovesRemaining <= 0) { currentMovesRemaining = 0; UpdateMovesUI(); EndGame(noMoves: true); }
    }
    private void UpdateMovesUI()
    {
        if (currentDisplayMode == DisplayMode.Moves && movesText != null && statusDisplayGroup != null && statusDisplayGroup.activeSelf)
        {
            movesText.text = "Moves: " + currentMovesRemaining.ToString();
        }
    }

    public void RestartGame() { Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
    public void PauseGame() { if (currentState == GameState.Playing && !IsAnyAnimationPlaying) SetState(GameState.Paused); }
    public void ResumeGame() { if (currentState == GameState.Paused) SetState(GameState.Playing); }
    public void GoToHomeScreen()
    {
        if (string.IsNullOrEmpty(homeSceneName)) { Debug.LogError("Home Scene Name not set!", this); return; }
        Time.timeScale = 1f; SceneManager.LoadScene(homeSceneName);
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
        if (wordGridManager == null || coords == null) return null;
        foreach (var coord in coords)
        {
            CellController cell = wordGridManager.GetCellController(coord);
            if (cell != null && cell.RectTransform != null) rects.Add(cell.RectTransform);
            else { /* Debug.LogWarning($"GetRectTransformsForCoords: No valid Cell/RectTransform at {coord}");*/ return null; }
        }
        return rects;
    }
}