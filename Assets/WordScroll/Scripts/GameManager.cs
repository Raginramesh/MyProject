using UnityEngine;
using TMPro; // Required for TextMeshPro UI elements
using UnityEngine.SceneManagement; // Required for scene reloading
using System.Collections.Generic; // Needed for Dictionary
using DG.Tweening; // Still needed for DelayedCall

public class GameManager : MonoBehaviour
{
    // --- Enums ---
    public enum ScoringMode { LengthBased, ScrabbleBased }
    public enum GameState { Initializing, Playing, Paused, GameOver }
    public enum DisplayMode { Timer, Moves, None }

    [Header("Game State")]
    [SerializeField] private GameState currentState = GameState.Initializing;
    public GameState CurrentState => currentState;

    [Header("Game Mode & Display")]
    [SerializeField] private DisplayMode currentDisplayMode = DisplayMode.Timer;
    [SerializeField] private float gameTimeLimit = 120f;
    [SerializeField] private int startingMoves = 50;

    [Header("Scene Navigation")]
    [SerializeField] private string homeSceneName = "HomeScreen";

    [Header("Scoring")]
    [SerializeField] private ScoringMode currentScoringMode = ScoringMode.LengthBased;
    [SerializeField] private int pointsPerLetter = 10;
    private Dictionary<char, int> scrabbleLetterValues;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private GameObject statusDisplayGroup;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI movesText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject pausePanel;

    [Header("Component References")]
    [SerializeField] private WordGridManager wordGridManager;
    [SerializeField] private WordValidator wordValidator;
    [SerializeField] private GridInputHandler gridInputHandler;
    [SerializeField] private EffectsManager effectsManager; // Reference to the effects handler

    [Header("Timing")]
    [Tooltip("Delay factor (multiplies animation pre-fly delay) before replacing letters & scoring.")]
    [SerializeField] private float replacementDelayFactor = 1.0f;

    // Internal State
    private float currentTimeRemaining;
    private int currentMovesRemaining;
    private int currentScore = 0;
    private static GameManager instance;

    // --- Unity Lifecycle Methods ---
    void Awake()
    {
        // Singleton setup
        if (instance == null) { instance = this; /* DontDestroyOnLoad(gameObject); */ }
        else if (instance != this) { Destroy(gameObject); return; }

        InitializeScrabbleValues();

        // Attempt to find references if not set in Inspector
        if (wordGridManager == null) wordGridManager = FindFirstObjectByType<WordGridManager>();
        if (wordValidator == null) wordValidator = FindFirstObjectByType<WordValidator>();
        if (gridInputHandler == null) gridInputHandler = FindFirstObjectByType<GridInputHandler>();
        if (effectsManager == null) effectsManager = FindFirstObjectByType<EffectsManager>();

        // Error checking for critical components
        if (wordGridManager == null) Debug.LogError("GM: WordGridManager missing!", this);
        if (wordValidator == null) Debug.LogError("GM: WordValidator missing!", this);
        if (gridInputHandler == null) Debug.LogError("GM: GridInputHandler missing!", this);
        if (effectsManager == null) Debug.LogError("GM: EffectsManager missing!", this);
        if (scoreText == null) Debug.LogError("GM: Score Text missing!", this);
        if (statusDisplayGroup == null && currentDisplayMode != DisplayMode.None) Debug.LogWarning("GM: Status Display Group missing!", this);
        if (timerText == null && currentDisplayMode == DisplayMode.Timer) Debug.LogWarning("GM: Timer Text missing!", this);
        if (movesText == null && currentDisplayMode == DisplayMode.Moves) Debug.LogWarning("GM: Moves Text missing!", this);
        if (string.IsNullOrEmpty(homeSceneName)) Debug.LogWarning("GM: Home Scene Name is not set.", this);

        // Ensure UI panels are initially hidden
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    void InitializeScrabbleValues()
    {
        // Standard Scrabble values
        scrabbleLetterValues = new Dictionary<char, int>() {
            {'A', 1}, {'E', 1}, {'I', 1}, {'O', 1}, {'U', 1}, {'L', 1}, {'N', 1}, {'S', 1}, {'T', 1}, {'R', 1},
            {'D', 2}, {'G', 2}, {'B', 3}, {'C', 3}, {'M', 3}, {'P', 3}, {'F', 4}, {'H', 4}, {'V', 4},
            {'W', 4}, {'Y', 4}, {'K', 5}, {'J', 8}, {'X', 8}, {'Q', 10}, {'Z', 10}
        };
    }

    void Start()
    {
        SetState(GameState.Initializing);
        StartGame();
    }

    void Update()
    {
        if (currentState == GameState.Playing && currentDisplayMode == DisplayMode.Timer)
        {
            UpdateTimer();
        }
    }

    // --- State Management ---
    private void SetState(GameState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        Debug.Log($"GameManager: State changed to {currentState}");
        switch (currentState)
        {
            case GameState.Initializing: Time.timeScale = 1f; break;
            case GameState.Playing: Time.timeScale = 1f; if (gridInputHandler != null) gridInputHandler.enabled = true; if (pausePanel != null) pausePanel.SetActive(false); break;
            case GameState.Paused: Time.timeScale = 0f; if (gridInputHandler != null) gridInputHandler.enabled = false; if (pausePanel != null) pausePanel.SetActive(true); break;
            case GameState.GameOver: Time.timeScale = 1f; if (gridInputHandler != null) gridInputHandler.enabled = false; break;
        }
    }

    private void StartGame()
    {
        Debug.Log("GM: Starting New Game Setup...");
        SetState(GameState.Initializing);
        currentScore = 0; UpdateScoreUI();

        // Setup Status Display Group
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

        // Reset/Initialize Grid & Validator
        if (wordGridManager != null) wordGridManager.InitializeGrid(); else { Debug.LogError("GM: Cannot init grid!", this); return; }
        if (wordValidator != null) { wordValidator.ResetFoundWordsList(); wordValidator.SetGameManager(this); wordValidator.ValidateWords(); } else { Debug.LogError("GM: Cannot reset validator!", this); }

        // Enable Input & Hide Panels
        if (gridInputHandler != null) gridInputHandler.enabled = true; else { Debug.LogError("GM: Cannot enable input!", this); }
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);

        SetState(GameState.Playing); Debug.Log("GM: New Game Started.", this);
    }

    private void EndGame(bool timeout = false, bool noMoves = false)
    {
        if (timeout && currentDisplayMode != DisplayMode.Timer) return;
        if (noMoves && currentDisplayMode != DisplayMode.Moves) return;
        if (currentState == GameState.GameOver) return;
        string reason = timeout ? "Time Ran Out" : (noMoves ? "No Moves Left" : "Manual");
        Debug.Log($"Game Over! Reason: {reason}"); SetState(GameState.GameOver);
        if (gameOverPanel != null) gameOverPanel.SetActive(true); else { Debug.LogWarning("GM: GameOver Panel missing!", this); }
    }

    // --- Scoring & Word Processing ---

    // Orchestrator called by WordValidator
    public void ProcessFoundWord(string word, List<Vector2Int> wordCoords)
    {
        if (currentState != GameState.Playing || string.IsNullOrEmpty(word) || wordCoords == null || wordCoords.Count == 0) return;
        if (effectsManager == null || wordGridManager == null) { Debug.LogError("GM: Missing EffectsManager or WordGridManager!", this); return; }

        // 1. Get RectTransforms for animation source positions
        List<RectTransform> sourceCellRects = GetRectTransformsForCoords(wordCoords);
        if (sourceCellRects == null || sourceCellRects.Count == 0) { Debug.LogError($"ProcessFoundWord: Could not get RectTransforms for '{word}'.", this); return; }

        // 2. Immediately fade out original cells using CellController
        foreach (Vector2Int coord in wordCoords)
        {
            CellController cell = wordGridManager.GetCellController(coord);
            cell?.FadeOutImmediate(); // Fade out the original cell instantly
        }

        // 3. Start the visual effect animation via EffectsManager
        effectsManager.PlayFlyToScoreEffect(sourceCellRects, word);

        // 4. Schedule the scoring and replacement/fade-in
        float baseDelay = effectsManager.FlyToScorePreFlyDelay; // Get timing from EffectsManager
        float actualDelay = baseDelay * replacementDelayFactor; // Apply user-defined factor

        DOVirtual.DelayedCall(actualDelay, () => {
            // This code runs after the fly-up and float pause duration
            if (this == null || wordGridManager == null) return; // Safety check if GM was destroyed during delay

            int scoreToAdd = CalculateScoreValue(word); // Calculate score
            if (scoreToAdd > 0) { AddScoreForWord(word, scoreToAdd); } // Add score (updates value and UI)

            // Replace letters in the grid AND tell WordGridManager to fade them in
            wordGridManager.ReplaceLettersAt(wordCoords, true); // Pass 'true' to enable fade-in

        }, false); // ignoreTimeScale = false (usually desired for UI animations)
    }

    // Only updates score value and UI (called by delayed action)
    private void AddScoreForWord(string wordForLog, int scoreToAdd)
    {
        if (currentState != GameState.Playing || scoreToAdd <= 0) return;
        currentScore += scoreToAdd;
        // Debug.Log($"Scored {scoreToAdd} points for word '{wordForLog}'. New Total Score: {currentScore}");
        UpdateScoreUI(); // Update UI (Consider DOCounter later for smooth update)
    }

    // Helper to calculate score value based on current mode
    private int CalculateScoreValue(string word)
    {
        int scoreValue = 0;
        switch (currentScoringMode)
        {
            case ScoringMode.LengthBased: scoreValue = word.Length * pointsPerLetter; break;
            case ScoringMode.ScrabbleBased: scoreValue = 0; foreach (char c in word.ToUpperInvariant()) { if (scrabbleLetterValues.TryGetValue(c, out int val)) scoreValue += val; } break;
            default: Debug.LogError($"Unknown Scoring Mode: {currentScoringMode}"); break;
        }
        return scoreValue;
    }

    // Updates score UI text
    private void UpdateScoreUI() { if (scoreText != null) { scoreText.text = "Score: " + currentScore.ToString(); } }

    // --- Timer Handling ---
    private void UpdateTimer() { if (currentTimeRemaining > 0) { currentTimeRemaining -= Time.deltaTime; UpdateTimerUI(); if (currentTimeRemaining <= 0) { currentTimeRemaining = 0; UpdateTimerUI(); EndGame(timeout: true); } } }
    private void UpdateTimerUI() { if (currentDisplayMode == DisplayMode.Timer && timerText != null && statusDisplayGroup != null && statusDisplayGroup.activeSelf) { int min = Mathf.FloorToInt(currentTimeRemaining / 60); int sec = Mathf.FloorToInt(currentTimeRemaining % 60); timerText.text = $"{min:00}:{sec:00}"; } }

    // --- Moves Handling ---
    public void DecrementMoves() { if (currentState != GameState.Playing || currentDisplayMode != DisplayMode.Moves) return; currentMovesRemaining--; UpdateMovesUI(); if (currentMovesRemaining <= 0) { currentMovesRemaining = 0; UpdateMovesUI(); EndGame(noMoves: true); } }
    private void UpdateMovesUI() { if (currentDisplayMode == DisplayMode.Moves && movesText != null && statusDisplayGroup != null && statusDisplayGroup.activeSelf) { movesText.text = "Moves: " + currentMovesRemaining.ToString(); } }

    // --- UI Button Actions ---
    public void RestartGame() { Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
    public void PauseGame() { if (currentState == GameState.Playing) SetState(GameState.Paused); }
    public void ResumeGame() { if (currentState == GameState.Paused) SetState(GameState.Playing); }
    public void GoToHomeScreen() { if (string.IsNullOrEmpty(homeSceneName)) { Debug.LogError("Home Scene Name not set!", this); return; } Time.timeScale = 1f; SceneManager.LoadScene(homeSceneName); }
    public void QuitGame()
    {
        Application.Quit();
        #if UNITY_EDITOR 
        UnityEditor.EditorApplication.isPlaying = false; 
        #endif 
    }


        // Helper to get RectTransforms from coordinates using CellControllers
        private List<RectTransform> GetRectTransformsForCoords(List<Vector2Int> coords)
        {
            List<RectTransform> rects = new List<RectTransform>();
            if (wordGridManager == null || coords == null)
            {
                Debug.LogError("GetRectTransformsForCoords: WordGridManager or coords is null."); return rects;
            }
            foreach (var coord in coords)
            {
                CellController cell = wordGridManager.GetCellController(coord); // Get controller from grid manager
                if (cell != null)
                {
                    rects.Add(cell.RectTransform); // Get RectTransform from CellController
                }
                else { Debug.LogWarning($"GetRectTransformsForCoords: No CellController found at {coord}"); }
            }
            // No need to check count mismatch here as we iterate coords directly
            return rects;
        }

    } // End of GameManager class