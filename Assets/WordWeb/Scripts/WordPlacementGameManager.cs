using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using WordScroll.Modifiers;
using DG.Tweening;

/// <summary>
/// Main game manager for the strategic word placement game.
/// Orchestrates all game systems and handles game flow.
/// </summary>
public class WordPlacementGameManager : MonoBehaviour
{
    [Header("Game Systems")]
    [SerializeField] private DynamicGridManager gridManager;
    [SerializeField] private WordListPanel wordListPanel;
    [SerializeField] private WordPlacementUI gameUI;
    [SerializeField] private PlacementValidator placementValidator;
    [SerializeField] private WordPlacementScorer scorer;

    [Header("Existing Systems")]
    [SerializeField] private ModifierManager modifierManager;
    [SerializeField] private LevelManager levelManager;
    [SerializeField] private AnimatedScoringSystem animatedScoring;

    [Header("Game Settings")]
    [SerializeField] private WordListScriptableObject defaultWordList;
    [SerializeField] private float gameTimeLimit = 300f; // 5 minutes
    [SerializeField] private int targetScore = 500;
    [SerializeField] private int minWordsToWin = 5;
    [SerializeField] private bool requireCenterStart = true;

    [Header("Scoring")]
    [SerializeField] private int baseWordScore = 10;
    [SerializeField] private float difficultyMultiplier = 1.5f;
    [SerializeField] private float lengthBonus = 2f;
    [SerializeField] private float intersectionBonus = 5f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip wordPlacedSound;
    [SerializeField] private AudioClip wordRemovedSound;
    [SerializeField] private AudioClip gameWinSound;
    [SerializeField] private AudioClip gameOverSound;

    // Game state
    private GameState currentGameState = GameState.NotStarted;
    private float gameTimer = 0f;
    private int currentScore = 0;
    private int wordsPlaced = 0;
    private bool isFirstWordPlaced = false;
    private bool isGamePaused = false;

    // Placed words tracking
    private List<PlacedWord> placedWords = new List<PlacedWord>();
    private Dictionary<Vector2Int, char> gridLetters = new Dictionary<Vector2Int, char>();

    // Events
    public System.Action<GameState> OnGameStateChanged;
    public System.Action<int> OnScoreChanged;
    public System.Action<float> OnTimeChanged;
    public System.Action<int> OnWordsPlacedChanged;
    public System.Action OnGameWon;
    public System.Action OnGameLost;

    #region Initialization

    void Awake()
    {
        ValidateReferences();
        SetupEventListeners();
    }

    void Start()
    {
        InitializeGame();
    }

    private void ValidateReferences()
    {
        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<DynamicGridManager>();
        }

        if (wordListPanel == null)
        {
            wordListPanel = FindFirstObjectByType<WordListPanel>();
        }

        if (gameUI == null)
        {
            gameUI = FindFirstObjectByType<WordPlacementUI>();
        }

        if (placementValidator == null)
        {
            placementValidator = FindFirstObjectByType<PlacementValidator>();
        }

        if (scorer == null)
        {
            scorer = FindFirstObjectByType<WordPlacementScorer>();
        }

        // Try to find existing systems
        if (modifierManager == null)
        {
            modifierManager = ModifierManager.Instance;
        }

        if (levelManager == null)
        {
            levelManager = FindFirstObjectByType<LevelManager>();
        }

        if (animatedScoring == null)
        {
            animatedScoring = FindFirstObjectByType<AnimatedScoringSystem>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void SetupEventListeners()
    {
        // Grid events
        if (gridManager != null)
        {
            gridManager.OnCellPlaced += OnCellPlaced;
            gridManager.OnCellCleared += OnCellCleared;
            gridManager.OnGridCleared += OnGridCleared;
        }

        // Word list events
        if (wordListPanel != null)
        {
            wordListPanel.OnWordTilePlaced += OnWordTilePlaced;
            wordListPanel.OnWordTileRemoved += OnWordTileRemoved;
            wordListPanel.OnWordListEmpty += OnWordListEmpty;
        }

        // UI events
        if (gameUI != null)
        {
            gameUI.OnPauseClicked += PauseGame;
            gameUI.OnResumeClicked += ResumeGame;
            gameUI.OnRestartClicked += RestartGame;
            gameUI.OnQuitClicked += QuitGame;
        }

        // Modifier events
        if (modifierManager != null)
        {
            // Subscribe to modifier events if needed
        }
    }

    #endregion

    #region Game Flow

    public void InitializeGame()
    {
        Debug.Log("Initializing Word Placement Game...");

        // Reset game state
        ResetGameState();

        // Setup grid
        if (gridManager != null)
        {
            gridManager.ClearAllLetters();
        }

        // Setup word list
        if (wordListPanel != null && defaultWordList != null)
        {
            wordListPanel.PopulateWordList(defaultWordList);
        }

        // Apply modifiers
        ApplyModifierEffects();

        // Setup UI
        UpdateUI();

        // Set initial game state
        SetGameState(GameState.Ready);

        Debug.Log("Game initialized successfully");
    }

    public void StartGame()
    {
        if (currentGameState != GameState.Ready) return;

        Debug.Log("Starting Word Placement Game...");

        SetGameState(GameState.Playing);
        gameTimer = gameTimeLimit;

        // Start timer
        StartCoroutine(GameTimerCoroutine());

        // Show tutorial if first time
        if (gameUI != null && !HasPlayedBefore())
        {
            gameUI.ShowTutorial();
        }
    }

    public void PauseGame()
    {
        if (currentGameState != GameState.Playing) return;

        SetGameState(GameState.Paused);
        isGamePaused = true;
        Time.timeScale = 0f;

        if (gameUI != null)
        {
            gameUI.ShowPauseMenu();
        }
    }

    public void ResumeGame()
    {
        if (currentGameState != GameState.Paused) return;

        SetGameState(GameState.Playing);
        isGamePaused = false;
        Time.timeScale = 1f;

        if (gameUI != null)
        {
            gameUI.HidePauseMenu();
        }
    }

    public void RestartGame()
    {
        StopAllCoroutines();
        Time.timeScale = 1f;
        InitializeGame();
    }

    public void QuitGame()
    {
        StopAllCoroutines();
        Time.timeScale = 1f;
        
        // Return to home screen
        UnityEngine.SceneManagement.SceneManager.LoadScene("HomeScene");
    }

    private void EndGame(bool won)
    {
        if (currentGameState == GameState.GameOver) return;

        SetGameState(GameState.GameOver);
        StopAllCoroutines();

        if (won)
        {
            OnGameWon?.Invoke();
            PlaySound(gameWinSound);
            
            if (gameUI != null)
            {
                gameUI.ShowWinScreen(currentScore, wordsPlaced);
            }
        }
        else
        {
            OnGameLost?.Invoke();
            PlaySound(gameOverSound);
            
            if (gameUI != null)
            {
                gameUI.ShowLoseScreen(currentScore, wordsPlaced);
            }
        }

        // Save high score
        SaveHighScore();

        Debug.Log($"Game ended. Won: {won}, Score: {currentScore}, Words: {wordsPlaced}");
    }

    #endregion

    #region Game State Management

    private void SetGameState(GameState newState)
    {
        if (currentGameState == newState) return;

        GameState previousState = currentGameState;
        currentGameState = newState;

        Debug.Log($"Game state changed: {previousState} -> {newState}");

        OnGameStateChanged?.Invoke(newState);
        
        if (gameUI != null)
        {
            gameUI.UpdateGameState(newState);
        }
    }

    private void ResetGameState()
    {
        gameTimer = gameTimeLimit;
        currentScore = 0;
        wordsPlaced = 0;
        isFirstWordPlaced = false;
        isGamePaused = false;
        
        placedWords.Clear();
        gridLetters.Clear();

        OnScoreChanged?.Invoke(currentScore);
        OnTimeChanged?.Invoke(gameTimer);
        OnWordsPlacedChanged?.Invoke(wordsPlaced);
    }

    #endregion

    #region Word Placement Handling

    private void OnWordTilePlaced(WordTile wordTile)
    {
        if (currentGameState != GameState.Playing) return;

        // Validate placement
        if (!isFirstWordPlaced && requireCenterStart)
        {
            if (!DoesWordPassThroughCenter(wordTile))
            {
                Debug.LogWarning("First word must pass through center!");
                return;
            }
        }

        // Calculate score
        int wordScore = CalculateWordScore(wordTile);
        AddScore(wordScore);

        // Update placement tracking
        wordsPlaced++;
        OnWordsPlacedChanged?.Invoke(wordsPlaced);

        // Mark first word as placed
        if (!isFirstWordPlaced)
        {
            isFirstWordPlaced = true;
        }

        // Play sound
        PlaySound(wordPlacedSound);

        // Check win condition
        CheckWinCondition();

        Debug.Log($"Word placed: {wordTile.Word}, Score: {wordScore}, Total: {currentScore}");
    }

    private void OnWordTileRemoved(WordTile wordTile)
    {
        // Handle word removal (if implemented)
        PlaySound(wordRemovedSound);
        
        Debug.Log($"Word removed: {wordTile.Word}");
    }

    private void OnWordListEmpty()
    {
        Debug.Log("Word list is empty!");
        
        // Check if game should end
        if (currentGameState == GameState.Playing)
        {
            CheckWinCondition();
        }
    }

    #endregion

    #region Scoring

    private int CalculateWordScore(WordTile wordTile)
    {
        if (scorer != null)
        {
            return scorer.CalculateWordScore(wordTile);
        }

        // Basic scoring calculation
        int baseScore = wordTile.TotalScore;
        float difficultyBonus = 1f + (wordTile.Difficulty - 1) * difficultyMultiplier;
        float lengthBonusValue = wordTile.Word.Length * lengthBonus;
        
        int finalScore = Mathf.RoundToInt(baseScore * difficultyBonus + lengthBonusValue);
        
        // Apply modifier bonuses
        finalScore = ApplyModifierBonuses(finalScore);
        
        return finalScore;
    }

    private int ApplyModifierBonuses(int baseScore)
    {
        if (modifierManager == null) return baseScore;

        var activeModifiers = modifierManager.GetAllActiveModifiers();
        float multiplier = 1f;
        int additive = 0;

        foreach (var modifier in activeModifiers)
        {
            switch (modifier.effectType)
            {
                case ModifierEffectType.GeneralScoreBonusAndMoveReduction:
                    multiplier *= modifier.generalScoreMultiplier;
                    break;
                case ModifierEffectType.VowelCountBonus:
                    additive += modifier.vowelBonusPoints;
                    break;
            }
        }

        return Mathf.RoundToInt(baseScore * multiplier) + additive;
    }

    private void AddScore(int points)
    {
        currentScore += points;
        OnScoreChanged?.Invoke(currentScore);

        // Animate score if possible
        if (animatedScoring != null)
        {
            // Could integrate with existing animated scoring system
        }
    }

    #endregion

    #region Game Logic

    private bool DoesWordPassThroughCenter(WordTile wordTile)
    {
        // TODO: Implement center validation
        // Check if the placed word passes through the center cell
        return true; // Placeholder
    }

    private void CheckWinCondition()
    {
        bool hasWon = false;

        // Check multiple win conditions
        if (currentScore >= targetScore)
        {
            hasWon = true;
        }
        else if (wordsPlaced >= minWordsToWin)
        {
            hasWon = true;
        }

        if (hasWon)
        {
            EndGame(true);
        }
    }

    private void CheckLoseCondition()
    {
        bool hasLost = false;

        // Check lose conditions
        if (gameTimer <= 0f)
        {
            hasLost = true;
        }
        else if (!wordListPanel.HasAvailableWords && wordsPlaced < minWordsToWin)
        {
            hasLost = true;
        }

        if (hasLost)
        {
            EndGame(false);
        }
    }

    #endregion

    #region Timer

    private IEnumerator GameTimerCoroutine()
    {
        while (gameTimer > 0f && currentGameState == GameState.Playing)
        {
            yield return new WaitForSeconds(1f);
            
            if (!isGamePaused)
            {
                gameTimer -= 1f;
                OnTimeChanged?.Invoke(gameTimer);
                
                // Check for time warnings
                if (gameTimer <= 30f && gameUI != null)
                {
                    gameUI.ShowTimeWarning();
                }
            }
        }

        if (currentGameState == GameState.Playing)
        {
            CheckLoseCondition();
        }
    }

    #endregion

    #region Event Handlers

    private void OnCellPlaced(Vector2Int position, char letter)
    {
        gridLetters[position] = letter;
    }

    private void OnCellCleared(Vector2Int position)
    {
        gridLetters.Remove(position);
    }

    private void OnGridCleared()
    {
        gridLetters.Clear();
    }

    #endregion

    #region Modifier Integration

    private void ApplyModifierEffects()
    {
        if (modifierManager == null) return;

        var activeModifiers = modifierManager.GetAllActiveModifiers();
        
        foreach (var modifier in activeModifiers)
        {
            switch (modifier.effectType)
            {
                case ModifierEffectType.GeneralScoreBonusAndMoveReduction:
                    // Apply move reduction if needed
                    Debug.Log($"Applied GeneralScoreBonusAndMoveReduction modifier: {modifier.cardName}");
                    break;
                    
                case ModifierEffectType.SpecificWordLengthScoreBonus:
                    Debug.Log($"Applied SpecificWordLengthScoreBonus modifier: {modifier.cardName}");
                    break;
                    
                case ModifierEffectType.VowelCountBonus:
                    Debug.Log($"Applied VowelCountBonus modifier: {modifier.cardName}");
                    break;
            }
        }
    }

    #endregion

    #region UI Updates

    private void UpdateUI()
    {
        if (gameUI == null) return;

        gameUI.UpdateScore(currentScore);
        gameUI.UpdateTime(gameTimer);
        gameUI.UpdateWordsPlaced(wordsPlaced);
        gameUI.UpdateTargetScore(targetScore);
        gameUI.UpdateMinWords(minWordsToWin);
    }

    #endregion

    #region Audio

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    #endregion

    #region Save/Load

    private void SaveHighScore()
    {
        int highScore = PlayerPrefs.GetInt("WordPlacement_HighScore", 0);
        if (currentScore > highScore)
        {
            PlayerPrefs.SetInt("WordPlacement_HighScore", currentScore);
            PlayerPrefs.Save();
        }
    }

    private bool HasPlayedBefore()
    {
        return PlayerPrefs.GetInt("WordPlacement_HasPlayed", 0) == 1;
    }

    #endregion

    #region Public Properties

    public GameState CurrentGameState => currentGameState;
    public float GameTimer => gameTimer;
    public int CurrentScore => currentScore;
    public int WordsPlaced => wordsPlaced;
    public int TargetScore => targetScore;
    public int MinWordsToWin => minWordsToWin;
    public bool IsGameActive => currentGameState == GameState.Playing;

    #endregion
}

/// <summary>
/// Game state enumeration
/// </summary>
public enum GameState
{
    NotStarted,
    Ready,
    Playing,
    Paused,
    GameOver
}

/// <summary>
/// Data structure for tracking placed words
/// </summary>
[System.Serializable]
public class PlacedWord
{
    public string word;
    public Vector2Int startPosition;
    public PlacementOrientation orientation;
    public int score;
    public List<Vector2Int> occupiedCells;
    
    public PlacedWord(string word, Vector2Int start, PlacementOrientation orient, int score)
    {
        this.word = word;
        this.startPosition = start;
        this.orientation = orient;
        this.score = score;
        this.occupiedCells = new List<Vector2Int>();
    }
}
