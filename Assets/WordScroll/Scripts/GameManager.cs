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

    [Header("Game State")]
    [SerializeField] private GameState currentState = GameState.Initializing;
    public GameState CurrentState => currentState; // Public read-only property

    [Header("Game Settings")]
    [SerializeField] private bool isTimerEnabled = true; // Timer Enable Flag
    [Tooltip("Time limit in seconds (Used only if Timer is Enabled)")]
    [SerializeField] private float gameTimeLimit = 120f;
    [SerializeField] private int startingMoves = 50;

    [Header("Scoring")]
    [SerializeField] private ScoringMode currentScoringMode = ScoringMode.LengthBased; // Inspector setting for scoring mode
    [Tooltip("Points per letter (Used only for LengthBased scoring)")]
    [SerializeField] private int pointsPerLetter = 10; // Kept for LengthBased mode

    // Dictionary for Scrabble letter values
    private Dictionary<char, int> scrabbleLetterValues;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [Tooltip("Assign the parent GameObject containing the Timer Text and its Background")]
    [SerializeField] private GameObject timerDisplayGroup; // Assign the parent 'BG' GameObject here
    [Tooltip("Assign the TextMeshProUGUI component for the Timer (must be child of Timer Display Group)")]
    [SerializeField] private TextMeshProUGUI timerText; // Still need this to update the text value
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
        // Check the timer group reference
        if (timerDisplayGroup == null) Debug.LogWarning("GameManager: Timer Display Group reference not set. Timer UI cannot be shown/hidden.", this);
        // Check the timer text reference only if the timer is meant to be enabled
        if (timerText == null && isTimerEnabled) Debug.LogWarning("GameManager: Timer Text reference not set. Timer value cannot be updated.", this);


        // Ensure UI panels are initially hidden (or set active state based on design)
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);

        Debug.Log($"GameManager Awake: Initializing references. Timer Enabled: {isTimerEnabled}, Scoring Mode: {currentScoringMode}", this);
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
        // Debug.Log("Initialized Scrabble letter values."); // Keep commented unless debugging
    }


    void Start()
    {
        // Set initial state and start the game setup
        SetState(GameState.Initializing);
        StartGame(); // Start the game immediately
    }

    void Update()
    {
        // Only update timer if playing AND timer is enabled
        if (currentState == GameState.Playing && isTimerEnabled)
        {
            UpdateTimer();
        }
    }

    // --- State Management ---

    private void SetState(GameState newState)
    {
        if (currentState == newState) return; // No change

        currentState = newState;
        Debug.Log($"GameManager: State changed to {currentState}");

        // Handle state transitions
        switch (currentState)
        {
            case GameState.Initializing:
                Time.timeScale = 1f; // Ensure time isn't stopped
                break;
            case GameState.Playing:
                Time.timeScale = 1f;
                if (gridInputHandler != null) gridInputHandler.enabled = true;
                if (pausePanel != null) pausePanel.SetActive(false);
                break;
            case GameState.Paused:
                Time.timeScale = 0f; // Pause game time
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
        SetState(GameState.Initializing); // Start in initializing state

        // Reset Score
        currentScore = 0;
        UpdateScoreUI(); // Update UI at the start

        // Timer Setup (Enable/Disable UI Group and set time)
        if (timerDisplayGroup != null)
        {
            // Activate/Deactivate the entire timer group based on the setting
            timerDisplayGroup.SetActive(isTimerEnabled);

            if (isTimerEnabled)
            {
                // Only proceed if the group is now active (which it should be if isTimerEnabled is true)
                currentTimeRemaining = gameTimeLimit;
                UpdateTimerUI(); // Update text value
                Debug.Log($"Timer Initialized and Enabled: {gameTimeLimit}s");
            }
            else
            {
                Debug.Log("Timer Disabled by Setting. Timer UI Hidden.");
            }
        }
        else if (isTimerEnabled) // Log warning only if timer should be enabled but group UI is missing
        {
            Debug.LogWarning("Timer is enabled, but Timer Display Group not assigned!");
        }


        // Reset Moves
        currentMovesRemaining = startingMoves;
        UpdateMovesUI();

        // Reset/Initialize Grid
        if (wordGridManager != null)
        {
            wordGridManager.InitializeGrid();
        }
        else { Debug.LogError("Cannot initialize grid - WordGridManager reference missing!", this); return; }

        // Reset Validator (found words list) and pass reference
        if (wordValidator != null)
        {
            wordValidator.ResetFoundWordsList();
            wordValidator.SetGameManager(this); // Pass reference to WordValidator
                                                // Initial validation after grid setup
            wordValidator.ValidateWords();
        }
        else { Debug.LogError("Cannot reset validator - WordValidator reference missing!", this); }


        // Enable Input Handler
        if (gridInputHandler != null)
        {
            gridInputHandler.enabled = true;
        }
        else { Debug.LogError("Cannot enable input - GridInputHandler reference missing!", this); }

        // Hide UI Panels that shouldn't be visible at start
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);

        // Transition to Playing state
        SetState(GameState.Playing);
        Debug.Log("GameManager: New Game Started. State set to Playing.", this);
    }

    private void EndGame(bool timeout = false, bool noMoves = false)
    {
        // Prevent timeout end if timer is disabled
        if (timeout && !isTimerEnabled)
        {
            // This case should ideally not happen if UpdateTimer isn't called, but check defensively
            Debug.LogWarning("EndGame called with timeout=true, but timer is disabled. Ignoring timeout reason.");
            return;
        }

        if (currentState == GameState.GameOver) return; // Prevent multiple calls

        string reason = timeout ? "Time Ran Out" : (noMoves ? "No Moves Left" : "Manual Trigger");
        Debug.Log($"Game Over! Reason: {reason}");
        SetState(GameState.GameOver);

        // Show Game Over screen
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        else { Debug.LogWarning("GameManager: GameOver Panel reference not set!", this); }

        // Optional: Display final score on the Game Over panel
        // TextMeshProUGUI finalScoreText = gameOverPanel?.GetComponentInChildren<TextMeshProUGUI>();
        // if (finalScoreText != null) finalScoreText.text = "Final Score: " + currentScore;
    }

    // --- Score Handling (Supports both modes) ---
    public void AddScoreForWord(string word)
    {
        if (currentState != GameState.Playing) return; // Only add score while playing
        if (string.IsNullOrEmpty(word)) return;

        int scoreToAdd = 0;

        // Calculate score based on selected mode
        switch (currentScoringMode)
        {
            case ScoringMode.LengthBased:
                scoreToAdd = word.Length * pointsPerLetter;
                // Debug.Log($"Scoring (LengthBased): Word '{word}', Length {word.Length}, Score {scoreToAdd}");
                break;

            case ScoringMode.ScrabbleBased:
                scoreToAdd = 0; // Start score at 0 for this word
                foreach (char letter in word)
                {
                    // Ensure dictionary lookup uses uppercase
                    char upperLetter = char.ToUpperInvariant(letter);
                    if (scrabbleLetterValues.TryGetValue(upperLetter, out int letterValue))
                    {
                        scoreToAdd += letterValue;
                    }
                    else
                    {
                        // Optional: Handle unexpected characters
                        // Debug.LogWarning($"Scrabble scoring: Character '{upperLetter}' not found in value dictionary.");
                    }
                }
                // Debug.Log($"Scoring (ScrabbleBased): Word '{word}', Score {scoreToAdd}");
                break;

            default:
                Debug.LogError($"Unknown Scoring Mode: {currentScoringMode}");
                break;
        }

        // Add calculated score and update UI
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
        else
        {
            // Debug.LogWarning("GameManager: Score Text UI element not assigned!"); // Keep commented unless needed
        }
    }

    // --- Timer Handling ---

    private void UpdateTimer()
    {
        // This method is only called if isTimerEnabled is true (checked in Update)
        if (currentTimeRemaining > 0)
        {
            currentTimeRemaining -= Time.deltaTime;
            UpdateTimerUI(); // Update the display

            if (currentTimeRemaining <= 0)
            {
                currentTimeRemaining = 0; // Clamp to zero
                UpdateTimerUI(); // Update UI one last time to show 00:00
                EndGame(timeout: true); // End game due to time running out
            }
        }
    }

    private void UpdateTimerUI()
    {
        // Only update the text if the text component is assigned AND the parent group is active
        // (The parent group check is implicitly handled by StartGame setting its active state)
        if (timerText != null)
        {
            // Check if the parent group is actually active before trying to update text.
            // This prevents potential errors if UpdateTimerUI was somehow called while disabled.
            if (timerDisplayGroup != null && timerDisplayGroup.activeSelf)
            {
                int minutes = Mathf.FloorToInt(currentTimeRemaining / 60);
                int seconds = Mathf.FloorToInt(currentTimeRemaining % 60);
                timerText.text = $"{minutes:00}:{seconds:00}"; // Format M:SS
            }
        }
        // else { Debug.LogWarning("GameManager: Timer Text UI element not assigned!"); } // Keep commented unless needed
    }


    // --- Moves Handling ---

    public void DecrementMoves() // Can be called by GridInputHandler or other actions
    {
        if (currentState != GameState.Playing) return; // Only decrement while playing

        currentMovesRemaining--;
        UpdateMovesUI();

        if (currentMovesRemaining <= 0)
        {
            currentMovesRemaining = 0; // Clamp
            UpdateMovesUI(); // Update UI one last time
            EndGame(noMoves: true); // End game due to running out of moves
        }
    }

    private void UpdateMovesUI()
    {
        if (movesText != null)
        {
            movesText.text = "Moves: " + currentMovesRemaining.ToString();
        }
        // else { Debug.LogWarning("GameManager: Moves Text UI element not assigned!"); } // Keep commented unless needed
    }


    // --- UI Button Actions ---

    public void RestartGame()
    {
        Debug.Log("Restarting Game...");
        // Ensure time scale is reset before loading scene, especially if paused
        Time.timeScale = 1f;
        // Reload the current scene
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

    public void QuitGame() // Example Quit button action
    {
        Debug.Log("Quitting Game...");
        Application.Quit(); // Works in standalone builds
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Stops playing in the editor
#endif
    }

} // End of GameManager class