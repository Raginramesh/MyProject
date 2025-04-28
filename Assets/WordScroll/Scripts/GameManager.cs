using UnityEngine;
using TMPro; // Required for TextMeshPro UI elements
using UnityEngine.SceneManagement; // Required for scene reloading (like RestartGame)

public class GameManager : MonoBehaviour
{
    public enum GameState { Initializing, Playing, Paused, GameOver }

    [SerializeField] private GameState currentState = GameState.Initializing;
    public GameState CurrentState => currentState; // Public read-only property

    [Header("Game Settings")]
    [SerializeField] private float gameTimeLimit = 120f; // Seconds
    [SerializeField] private int startingMoves = 50;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI movesText;
    [SerializeField] private GameObject gameOverPanel; // Assign your Game Over UI panel
    [SerializeField] private GameObject pausePanel; // Assign your Pause Menu UI panel

    [Header("Component References")]
    [SerializeField] private WordGridManager wordGridManager;
    [SerializeField] private WordValidator wordValidator;
    [SerializeField] private GridInputHandler gridInputHandler;

    [Header("Scoring")]
    [SerializeField] private int pointsPerLetter = 10; // Points awarded for each letter in a valid word

    // Internal State
    private float currentTimeRemaining;
    private int currentMovesRemaining;
    private int currentScore = 0;

    // --- Unity Lifecycle Methods ---

    void Awake()
    {
        // Attempt to find references if not set in Inspector (robustness)
        if (wordGridManager == null) wordGridManager = FindFirstObjectByType<WordGridManager>();
        if (wordValidator == null) wordValidator = FindFirstObjectByType<WordValidator>();
        if (gridInputHandler == null) gridInputHandler = FindFirstObjectByType<GridInputHandler>();

        // Error checking for critical components
        if (wordGridManager == null) Debug.LogError("GameManager: WordGridManager not found!", this);
        if (wordValidator == null) Debug.LogError("GameManager: WordValidator not found!", this);
        if (gridInputHandler == null) Debug.LogError("GameManager: GridInputHandler not found!", this);

        // Ensure UI panels are initially hidden (or set active state based on design)
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);

        Debug.Log("GameManager Awake: Initializing references.", this);
    }

    void Start()
    {
        // Set initial state and start the game setup
        SetState(GameState.Initializing);
        StartGame(); // Start the game immediately or wait for player input
    }

    void Update()
    {
        // Only update timer if playing
        if (currentState == GameState.Playing)
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
                // Handled mostly in Awake/Start
                Time.timeScale = 1f; // Ensure time isn't stopped from a previous game over/pause
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
                Time.timeScale = 1f; // Keep time normal for game over animations/UI, but disable input
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

        // Reset Timer
        currentTimeRemaining = gameTimeLimit;
        UpdateTimerUI();

        // Reset Moves
        currentMovesRemaining = startingMoves;
        UpdateMovesUI();

        // Reset/Initialize Grid
        if (wordGridManager != null)
        {
            wordGridManager.InitializeGrid();
            // Pass reference to WordGridManager (if needed, though it finds GameManager)
            // wordGridManager.SetGameManager(this);
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

        // Hide UI Panels
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);

        // Transition to Playing state
        SetState(GameState.Playing);
        Debug.Log("GameManager: New Game Started. State set to Playing.", this);
    }

    private void EndGame(bool timeout = false, bool noMoves = false)
    {
        if (currentState == GameState.GameOver) return; // Prevent multiple calls

        string reason = timeout ? "Time Ran Out" : (noMoves ? "No Moves Left" : "Manual Trigger");
        Debug.Log($"Game Over! Reason: {reason}");
        SetState(GameState.GameOver);

        // Show Game Over screen
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        else { Debug.LogWarning("GameManager: GameOver Panel reference not set!", this); }

        // Optional: Display final score on the Game Over panel if it has a text element for it
        // Example: Find a component like FinalScoreDisplay on the panel and set its text
        // TextMeshProUGUI finalScoreText = gameOverPanel?.GetComponentInChildren<TextMeshProUGUI>(); // Be more specific if needed
        // if (finalScoreText != null) finalScoreText.text = "Final Score: " + currentScore;
    }

    // --- NEW: Score Handling ---
    /// <summary>
    /// Called by WordValidator when a valid new word is found.
    /// Calculates score and updates the total score and UI.
    /// </summary>
    /// <param name="word">The found word.</param>
    public void AddScoreForWord(string word)
    {
        if (currentState != GameState.Playing) return; // Only add score while playing
        if (string.IsNullOrEmpty(word)) return;

        // Basic scoring: points per letter
        int scoreToAdd = word.Length * pointsPerLetter;

        // Optional: Add bonus for longer words, etc.
        // if (word.Length > 5) scoreToAdd *= 2; // Example bonus

        currentScore += scoreToAdd;
        Debug.Log($"Added {scoreToAdd} points for word '{word}'. New Score: {currentScore}");

        UpdateScoreUI();
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + currentScore.ToString();
        }
        else
        {
            // Only log warning once perhaps, or use a flag
            // Debug.LogWarning("GameManager: Score Text UI element not assigned!", this);
        }
    }

    // --- Timer Handling ---

    private void UpdateTimer()
    {
        if (currentTimeRemaining > 0)
        {
            currentTimeRemaining -= Time.deltaTime;
            UpdateTimerUI();

            if (currentTimeRemaining <= 0)
            {
                currentTimeRemaining = 0; // Clamp to zero
                UpdateTimerUI(); // Update UI one last time
                EndGame(timeout: true); // End game due to time running out
            }
        }
    }

    private void UpdateTimerUI()
    {
        if (timerText != null)
        {
            // Format as minutes:seconds
            int minutes = Mathf.FloorToInt(currentTimeRemaining / 60);
            int seconds = Mathf.FloorToInt(currentTimeRemaining % 60);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
        // else { Debug.LogWarning("GameManager: Timer Text UI element not assigned!"); }
    }

    // --- Moves Handling ---

    public void DecrementMoves() // Made public if called externally, though usually internal
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
        // else { Debug.LogWarning("GameManager: Moves Text UI element not assigned!"); }
    }


    // --- UI Button Actions ---

    public void RestartGame()
    {
        Debug.Log("Restarting Game...");
        // Ensure time scale is reset before loading scene, especially if paused
        Time.timeScale = 1f;
        // Reload the current scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        // Alternatively, call StartGame() directly if you want to reset without reloading
        // StartGame();
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
        Application.Quit(); // Note: This only works in standalone builds, not the editor
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Stop playing in the editor
#endif
    }

} // End of GameManager class