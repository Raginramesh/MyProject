using UnityEngine;
using TMPro; // Required for TextMeshPro UI elements
using UnityEngine.SceneManagement; // Required for scene reloading
using System.Collections.Generic; // Needed for Dictionary

public class GameManager : MonoBehaviour
{
    // --- Enums ---
    public enum ScoringMode
    {
        LengthBased, // Original system: score = word.Length * pointsPerLetter
        ScrabbleBased // New system: score = sum of letter values
    }

    public enum GameState { Initializing, Playing, Paused, GameOver }

    // Enum to control what is displayed in the status group
    public enum DisplayMode
    {
        Timer,
        Moves,
        None
    }

    [Header("Game State")]
    [SerializeField] private GameState currentState = GameState.Initializing;
    public GameState CurrentState => currentState; // Public read-only property

    [Header("Game Mode & Display")]
    [Tooltip("Selects whether to use Timer, Moves limit, or None")]
    [SerializeField] private DisplayMode currentDisplayMode = DisplayMode.Timer;
    [Tooltip("Time limit in seconds (Used only if Display Mode is Timer)")]
    [SerializeField] private float gameTimeLimit = 120f;
    [Tooltip("Starting number of moves (Used only if Display Mode is Moves)")]
    [SerializeField] private int startingMoves = 50;

    [Header("Scene Navigation")] // Header for scene configuration
    [Tooltip("The exact name of the scene to load when the Home button is pressed.")]
    [SerializeField] private string homeSceneName = "HomeScreen"; // Configurable scene name

    [Header("Scoring")]
    [SerializeField] private ScoringMode currentScoringMode = ScoringMode.LengthBased; // Inspector setting for scoring mode
    [Tooltip("Points per letter (Used only for LengthBased scoring)")]
    [SerializeField] private int pointsPerLetter = 10; // Kept for LengthBased mode

    // Dictionary for Scrabble letter values
    private Dictionary<char, int> scrabbleLetterValues;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [Tooltip("Assign the parent GameObject containing the Timer and Moves Text elements")]
    [SerializeField] private GameObject statusDisplayGroup; // Renamed from timerDisplayGroup
    [Tooltip("Assign the TextMeshProUGUI component for the Timer (must be child of Status Display Group)")]
    [SerializeField] private TextMeshProUGUI timerText;
    [Tooltip("Assign the TextMeshProUGUI component for Moves display (must be child of Status Display Group)")]
    [SerializeField] private TextMeshProUGUI movesText;
    [SerializeField] private GameObject gameOverPanel; // Assign your Game Over UI panel
    [SerializeField] private GameObject pausePanel; // Assign your Pause Menu UI panel

    [Header("Component References")]
    [SerializeField] private WordGridManager wordGridManager;
    [SerializeField] private WordValidator wordValidator;
    [SerializeField] private GridInputHandler gridInputHandler;

    // Internal State
    private float currentTimeRemaining;
    private int currentMovesRemaining;
    private int currentScore = 0;

    // --- Unity Lifecycle Methods ---

    void Awake()
    {
        // Initialize Scrabble values first
        InitializeScrabbleValues();

        // Attempt to find references if not set in Inspector (robustness)
        if (wordGridManager == null) wordGridManager = FindFirstObjectByType<WordGridManager>();
        if (wordValidator == null) wordValidator = FindFirstObjectByType<WordValidator>();
        if (gridInputHandler == null) gridInputHandler = FindFirstObjectByType<GridInputHandler>();

        // Error checking for critical components
        if (wordGridManager == null) Debug.LogError("GameManager: WordGridManager not found!", this);
        if (wordValidator == null) Debug.LogError("GameManager: WordValidator not found!", this);
        if (gridInputHandler == null) Debug.LogError("GameManager: GridInputHandler not found!", this);

        // Check UI references based on DisplayMode
        if (statusDisplayGroup == null) Debug.LogWarning("GameManager: Status Display Group reference not set. Timer/Moves UI cannot be managed.", this);
        if (timerText == null && currentDisplayMode == DisplayMode.Timer) Debug.LogWarning("GameManager: Timer Text reference not set, but Display Mode is Timer.", this);
        if (movesText == null && currentDisplayMode == DisplayMode.Moves) Debug.LogWarning("GameManager: Moves Text reference not set, but Display Mode is Moves.", this);
        if (string.IsNullOrEmpty(homeSceneName)) Debug.LogWarning("GameManager: Home Scene Name is not set in the Inspector!", this); // Check for scene name

        // Ensure UI panels are initially hidden (or set active state based on design)
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);

        Debug.Log($"GameManager Awake: Display Mode: {currentDisplayMode}, Scoring Mode: {currentScoringMode}, Home Scene: '{homeSceneName}'", this);
    }

    // Method to set up Scrabble points
    void InitializeScrabbleValues()
    {
        scrabbleLetterValues = new Dictionary<char, int>()
        {
            {'A', 1}, {'E', 1}, {'I', 1}, {'O', 1}, {'U', 1}, {'L', 1}, {'N', 1}, {'S', 1}, {'T', 1}, {'R', 1},
            {'D', 2}, {'G', 2},
            {'B', 3}, {'C', 3}, {'M', 3}, {'P', 3},
            {'F', 4}, {'H', 4}, {'V', 4}, {'W', 4}, {'Y', 4},
            {'K', 5},
            {'J', 8}, {'X', 8},
            {'Q', 10}, {'Z', 10}
        };
    }

    void Start()
    {
        SetState(GameState.Initializing);
        StartGame();
    }

    void Update()
    {
        // Only update timer if playing AND in Timer mode
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
            case GameState.Initializing:
                Time.timeScale = 1f;
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
                Time.timeScale = 1f; // Keep time normal for game over UI, but disable input
                if (gridInputHandler != null) gridInputHandler.enabled = false;
                // Game over panel activation is handled in EndGame()
                break;
        }
    }

    private void StartGame()
    {
        Debug.Log("GameManager: Starting New Game Setup...");
        SetState(GameState.Initializing);

        currentScore = 0;
        UpdateScoreUI();

        // Setup Status Display Group based on DisplayMode
        if (statusDisplayGroup != null)
        {
            bool isGroupActive = currentDisplayMode != DisplayMode.None;
            statusDisplayGroup.SetActive(isGroupActive);

            if (isGroupActive)
            {
                // Activate/Deactivate Timer and Moves Text elements within the group
                if (timerText != null) timerText.gameObject.SetActive(currentDisplayMode == DisplayMode.Timer);
                if (movesText != null) movesText.gameObject.SetActive(currentDisplayMode == DisplayMode.Moves);

                // Initialize the correct value based on the mode
                if (currentDisplayMode == DisplayMode.Timer)
                {
                    currentTimeRemaining = gameTimeLimit;
                    UpdateTimerUI();
                    Debug.Log($"Display Mode: Timer Initialized ({gameTimeLimit}s)");
                }
                else if (currentDisplayMode == DisplayMode.Moves)
                {
                    currentMovesRemaining = startingMoves;
                    UpdateMovesUI();
                    Debug.Log($"Display Mode: Moves Initialized ({startingMoves} moves)");
                }
            }
            else
            {
                Debug.Log("Display Mode: None. Status Group Hidden.");
            }
        }
        else // Log warning only if a mode requires the group but it's missing
        {
            if (currentDisplayMode != DisplayMode.None)
                Debug.LogWarning("Status Display Group not assigned! Cannot show Timer or Moves.");
        }

        // Reset/Initialize Grid
        if (wordGridManager != null)
        {
            wordGridManager.InitializeGrid();
        }
        else { Debug.LogError("Cannot initialize grid - WordGridManager reference missing!", this); return; }

        // Reset Validator
        if (wordValidator != null)
        {
            wordValidator.ResetFoundWordsList();
            wordValidator.SetGameManager(this);
            wordValidator.ValidateWords();
        }
        else { Debug.LogError("Cannot reset validator - WordValidator reference missing!", this); }

        // Enable Input
        if (gridInputHandler != null)
        {
            gridInputHandler.enabled = true;
        }
        else { Debug.LogError("Cannot enable input - GridInputHandler reference missing!", this); }

        // Hide Panels
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);

        SetState(GameState.Playing);
        Debug.Log("GameManager: New Game Started. State set to Playing.", this);
    }

    private void EndGame(bool timeout = false, bool noMoves = false)
    {
        // Check DisplayMode before triggering game over reason
        if (timeout && currentDisplayMode != DisplayMode.Timer)
        {
            Debug.LogWarning("EndGame called with timeout=true, but not in Timer mode. Ignoring timeout reason.");
            return;
        }
        if (noMoves && currentDisplayMode != DisplayMode.Moves)
        {
            Debug.LogWarning("EndGame called with noMoves=true, but not in Moves mode. Ignoring noMoves reason.");
            return;
        }

        if (currentState == GameState.GameOver) return; // Prevent multiple calls

        string reason = timeout ? "Time Ran Out" : (noMoves ? "No Moves Left" : "Manual Trigger");
        Debug.Log($"Game Over! Reason: {reason}");
        SetState(GameState.GameOver);

        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        else { Debug.LogWarning("GameManager: GameOver Panel reference not set!", this); }
    }

    // --- Score Handling ---
    public void AddScoreForWord(string word)
    {
        if (currentState != GameState.Playing || string.IsNullOrEmpty(word)) return;

        int scoreToAdd = 0;
        switch (currentScoringMode)
        {
            case ScoringMode.LengthBased:
                scoreToAdd = word.Length * pointsPerLetter;
                break;
            case ScoringMode.ScrabbleBased:
                scoreToAdd = 0;
                foreach (char letter in word)
                {
                    char upperLetter = char.ToUpperInvariant(letter);
                    if (scrabbleLetterValues.TryGetValue(upperLetter, out int letterValue))
                    {
                        scoreToAdd += letterValue;
                    }
                }
                break;
            default:
                Debug.LogError($"Unknown Scoring Mode: {currentScoringMode}");
                break;
        }

        if (scoreToAdd > 0)
        {
            currentScore += scoreToAdd;
            Debug.Log($"Added {scoreToAdd} points (Mode: {currentScoringMode}) for word '{word}'. New Total Score: {currentScore}");
            UpdateScoreUI();
        }
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + currentScore.ToString();
        }
    }

    // --- Timer Handling ---
    private void UpdateTimer()
    {
        // Redundant check (already in Update), but safe
        if (currentDisplayMode != DisplayMode.Timer) return;

        if (currentTimeRemaining > 0)
        {
            currentTimeRemaining -= Time.deltaTime;
            UpdateTimerUI();

            if (currentTimeRemaining <= 0)
            {
                currentTimeRemaining = 0;
                UpdateTimerUI();
                EndGame(timeout: true);
            }
        }
    }

    private void UpdateTimerUI()
    {
        // Check mode and references
        if (currentDisplayMode == DisplayMode.Timer && timerText != null && statusDisplayGroup != null && statusDisplayGroup.activeSelf)
        {
            int minutes = Mathf.FloorToInt(currentTimeRemaining / 60);
            int seconds = Mathf.FloorToInt(currentTimeRemaining % 60);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    // --- Moves Handling ---
    public void DecrementMoves()
    {
        // Only decrement if playing AND in Moves mode
        if (currentState != GameState.Playing || currentDisplayMode != DisplayMode.Moves) return;

        currentMovesRemaining--;
        Debug.Log($"Move Used. Remaining Moves: {currentMovesRemaining}");
        UpdateMovesUI();

        if (currentMovesRemaining <= 0)
        {
            currentMovesRemaining = 0;
            UpdateMovesUI();
            EndGame(noMoves: true);
        }
    }

    private void UpdateMovesUI()
    {
        // Check mode and references
        if (currentDisplayMode == DisplayMode.Moves && movesText != null && statusDisplayGroup != null && statusDisplayGroup.activeSelf)
        {
            movesText.text = "Moves: " + currentMovesRemaining.ToString();
        }
    }

    // --- UI Button Actions ---

    public void RestartGame()
    {
        Debug.Log("Restarting Game...");
        Time.timeScale = 1f; // Ensure time scale is reset
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void PauseGame()
    {
        if (currentState == GameState.Playing)
        {
            SetState(GameState.Paused);
        }
    }

    public void ResumeGame()
    {
        if (currentState == GameState.Paused)
        {
            SetState(GameState.Playing);
        }
    }

    // MODIFIED FUNCTION for the Home Button
    public void GoToHomeScreen()
    {
        // Check if a scene name has been provided in the inspector
        if (string.IsNullOrEmpty(homeSceneName))
        {
            Debug.LogError("Home button pressed, but Home Scene Name is not set in GameManager Inspector!", this);
            return;
        }

        Debug.Log($"Going to Home Screen: '{homeSceneName}'...");
        Time.timeScale = 1f; // Ensure time scale is reset before leaving scene

        // Load the scene using the name specified in the inspector
        // --- Make sure the scene named 'homeSceneName' is added to File > Build Settings... ---
        SceneManager.LoadScene(homeSceneName);
    }


    public void QuitGame() // Example Quit button action
    {
        Debug.Log("Quitting Game...");
        Application.Quit(); // Works in standalone builds
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Stops playing in the editor
#endif
    }

} // End of GameManager class