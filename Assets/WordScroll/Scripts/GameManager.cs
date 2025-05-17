using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections; // Added for Coroutines
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

    // Modified to be a settable property for more direct control during combo processing
    private bool _isAnyAnimationPlaying = false;
    public bool IsAnyAnimationPlaying
    {
        get
        {
            return _isAnyAnimationPlaying ||
                   (wordGridManager != null && wordGridManager.isAnimating) ||
                   (effectsManager != null && effectsManager.IsAnimating) ||
                   (gridInputHandler != null && gridInputHandler.IsPerformingInertiaScroll);
        }
        private set
        { // Private setter for internal control, public getter for read-only access
            _isAnyAnimationPlaying = value;
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

    [Header("Timing & Combo Settings")] // Modified section title
    [SerializeField] private float replacementDelayAfterEffectStart = 0.4f; // Kept from original
    [Tooltip("Duration for each word's found/score animation in a combo sequence.")]
    [SerializeField] private float animationDurationPerWordInCombo = 0.5f; // New for combo

    [Header("Effects")]
    [SerializeField] private float scoreShakeDuration = 0.2f;
    [SerializeField] private float scoreShakeStrength = 3f;
    [SerializeField] private int scoreShakeVibrato = 15;

    private float currentTimeRemaining;
    private int currentMovesRemaining;
    private int currentScore = 0;
    private static GameManager instance;

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

        if (wordGridManager == null) Debug.LogError("GM: WordGridManager missing!", this);
        if (wordValidator == null) Debug.LogError("GM: WordValidator missing!", this);
        if (gridInputHandler == null) Debug.LogError("GM: GridInputHandler missing! Tapping will not work.", this);
        if (effectsManager == null) Debug.LogWarning("GM: EffectsManager missing! Word animations might not play correctly.", this); // Changed to Warning
        if (scoreText == null) Debug.LogError("GM: Score Text (TMP) missing!", this);

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);

        // Pass reference to WordValidator if it needs it
        if (wordValidator != null) wordValidator.SetGameManager(this);
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
        { SetState(GameState.Initializing); } // Ensure correct initial state
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
        if (currentState == newState && newState != GameState.Initializing) return; // Allow re-initializing
        currentState = newState;
        _isAnyAnimationPlaying = false; // Reset animation block on state change by default

        switch (currentState)
        {
            case GameState.Initializing:
                Time.timeScale = 1f;
                if (gridInputHandler != null) gridInputHandler.enabled = false;
                _isAnyAnimationPlaying = true; // Block input during setup
                break;
            case GameState.Playing:
                Time.timeScale = 1f;
                if (gridInputHandler != null) gridInputHandler.enabled = true;
                if (pausePanel != null) pausePanel.SetActive(false);
                _isAnyAnimationPlaying = false; // Allow input
                break;
            case GameState.Paused:
                Time.timeScale = 0f;
                if (gridInputHandler != null) gridInputHandler.enabled = false;
                if (pausePanel != null) pausePanel.SetActive(true);
                _isAnyAnimationPlaying = true; // Block game input
                break;
            case GameState.GameOver:
                Time.timeScale = 1f; // Or 0f if you want to freeze it
                if (gridInputHandler != null) gridInputHandler.enabled = false;
                _isAnyAnimationPlaying = true; // Block input
                break;
        }
    }

    private void StartGame()
    {
        SetState(GameState.Initializing);
        currentScore = 0; UpdateScoreUI();
        currentPotentialWords.Clear();

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
            // wordValidator.SetGameManager(this); // Already done in Awake
            if (wordGridManager != null)
            {
                wordGridManager.TriggerValidationCheckAndHighlightUpdate();
            }
        }
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        SetState(GameState.Playing); // Transition to Playing after setup
    }

    private void EndGame(bool timeout = false, bool noMoves = false)
    {
        if (currentState == GameState.GameOver) return;
        if ((timeout && currentDisplayMode != DisplayMode.Timer) || (noMoves && currentDisplayMode != DisplayMode.Moves)) return;

        string reason = timeout ? "Time Ran Out" : (noMoves ? "No Moves Left" : "Game Over");
        SetState(GameState.GameOver);
        currentPotentialWords.Clear();
        if (wordGridManager != null) wordGridManager.ClearAllCellHighlights();


        if (gameOverPanel != null) { gameOverPanel.SetActive(true); /* Populate reason/score */ }
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

    // <<< MODIFIED: AttemptTapValidation to handle Combos >>>
    public bool AttemptTapValidation(Vector2Int tappedCoordinate)
    {
        if (currentState != GameState.Playing || IsAnyAnimationPlaying)
        {
            return false;
        }

        List<FoundWordData> tappedCandidates = new List<FoundWordData>();
        foreach (var potentialWord in currentPotentialWords)
        {
            if (potentialWord.Coordinates.Contains(tappedCoordinate))
            {
                // Only consider words not yet found in the current game session for forming new combos.
                if (!wordValidator.IsWordFoundThisSession(potentialWord.Word))
                {
                    tappedCandidates.Add(potentialWord);
                }
            }
        }

        if (tappedCandidates.Count == 0)
        {
            return false; // Tapped cell not part of any new valid word
        }

        // Select the primary word based on prioritization (e.g., longest)
        FoundWordData primaryWord = SelectPrimaryFromCandidates(tappedCandidates);

        List<FoundWordData> intersectingWords = new List<FoundWordData>();
        Vector2Int intersectionPoint; // Output from CheckIntersection

        // Check for intersections against all other potential words on the grid
        foreach (var otherWord in currentPotentialWords)
        {
            if (otherWord.ID == primaryWord.ID) continue; // Don't check against itself
            if (wordValidator.IsWordFoundThisSession(otherWord.Word)) continue; // Skip if already processed

            if (wordValidator.CheckIntersection(primaryWord, otherWord, out intersectionPoint))
            {
                intersectingWords.Add(otherWord);
            }
        }

        List<FoundWordData> wordsToProcess = new List<FoundWordData> { primaryWord };
        if (intersectingWords.Count > 0)
        {
            wordsToProcess.AddRange(intersectingWords);
        }

        // Final filter for session-found words and unique IDs
        List<FoundWordData> finalWordsToProcess = wordsToProcess
                                                  .Where(w => !wordValidator.IsWordFoundThisSession(w.Word))
                                                  .DistinctBy(w => w.ID)
                                                  .ToList();

        if (finalWordsToProcess.Count > 0)
        {
            StartCoroutine(ProcessWordSetGameplay(finalWordsToProcess));
            return true;
        }
        else
        {
            // This case implies the primary tapped word itself was already found,
            // or all potential intersections were already found.
            // Potentially, if only the primary was tapped and it's new, but no new intersections,
            // it should have been in finalWordsToProcess.
            // If finalWordsToProcess is empty, it means no new words to process from this tap.
            return false;
        }
    }

    // <<< NEW HELPER: SelectPrimaryFromCandidates >>>
    private FoundWordData SelectPrimaryFromCandidates(List<FoundWordData> candidates)
    {
        if (candidates.Count == 1) return candidates[0];
        // Prioritize: Longest word, then by orientation (e.g., Horizontal), then positional tie-breaking.
        return candidates.OrderByDescending(w => w.Word.Length)
                         .ThenBy(w => w.GetOrientation()) // Assumes enum order: Horizontal, Vertical, SingleLetter
                         .ThenBy(w => w.Coordinates[0].x) // Top-most
                         .ThenBy(w => w.Coordinates[0].y) // Left-most
                         .First();
    }

    // <<< MODIFIED: ProcessSingleWordInternal is now part of ProcessWordSetGameplay coroutine >>>
    // The old ProcessSingleWordInternal is effectively replaced by the new coroutine.

    // <<< NEW COROUTINE: ProcessWordSetGameplay for single words or combos >>>
    private IEnumerator ProcessWordSetGameplay(List<FoundWordData> wordsInSet)
    {
        IsAnyAnimationPlaying = true; // Block further input

        // Remove processed words from tappable list and clear their immediate highlights
        foreach (var wordData in wordsInSet)
        {
            currentPotentialWords.RemoveAll(pwd => pwd.ID == wordData.ID);
            foreach (Vector2Int coord in wordData.Coordinates)
            {
                CellController cell = wordGridManager.GetCellController(coord);
                if (cell != null)
                {
                    cell.SetHighlightState(false, cell.GetDefaultColor()); // Reset highlight
                    // cell.FadeOutImmediate(); // Consider if immediate fade out is desired before animation
                }
            }
        }


        HashSet<string> uniqueWordStringsProcessedInThisSet = new HashSet<string>();
        List<Vector2Int> allCellsToClearFromThisSet = new List<Vector2Int>();

        foreach (var wordData in wordsInSet)
        {
            // 1. Score & UI Update (using existing HandleSingleLetterScore for Scrabble values)
            List<RectTransform> sourceCellRects = GetRectTransformsForCoords(wordData.Coordinates);
            if (sourceCellRects != null && sourceCellRects.Count > 0 && effectsManager != null)
            {
                List<int> individualLetterScores = wordData.Word.Select(letter => CalculateScoreValueForLetter(letter)).ToList();
                effectsManager.PlayFlyToScoreEffect(sourceCellRects, wordData.Word, individualLetterScores, HandleSingleLetterScore);
            }
            else
            { // Fallback for scoring if effect can't play
                foreach (char letter in wordData.Word) HandleSingleLetterScore(CalculateScoreValueForLetter(letter));
            }


            // 2. Mark as Found
            if (uniqueWordStringsProcessedInThisSet.Add(wordData.Word.ToUpperInvariant()))
            {
                wordValidator.MarkWordAsFoundInSession(wordData.Word);
            }

            // 3. Collect Cells for Clearing
            allCellsToClearFromThisSet.AddRange(wordData.Coordinates);

            // 4. Fade out cells for this specific word (if not done above)
            // This could be part of EffectsManager or a direct call to WordGridManager
            foreach (Vector2Int coord in wordData.Coordinates)
            {
                CellController cell = wordGridManager.GetCellController(coord);
                cell?.FadeOutImmediate(); // Or a more graceful animation
            }

            // 5. Wait for animation or fixed duration for this word in the combo
            // If PlayFlyToScoreEffect is not blocking, use a fixed delay.
            // If it IS blocking or has a callback, adjust this.
            yield return new WaitForSeconds(animationDurationPerWordInCombo);
        }

        // All words in set animated and scored.
        // 6. Clear All Processed Cells and Refill Grid (after a delay if needed)
        // The original ProcessSingleWordInternal had a DOVirtual.DelayedCall here.
        // We can adapt that if replacement should be explicitly delayed after all effects.
        yield return new WaitForSeconds(replacementDelayAfterEffectStart); // Use existing delay before replacing

        if (wordGridManager != null && allCellsToClearFromThisSet.Count > 0)
        {
            wordGridManager.ReplaceLettersAt(allCellsToClearFromThisSet.Distinct().ToList(), true);

            yield return new WaitUntil(() => !wordGridManager.isAnimating); // Wait for refill

            // 7. Trigger a new validation pass on the updated grid
            wordGridManager.TriggerValidationCheckAndHighlightUpdate();
        }

        IsAnyAnimationPlaying = false; // Release block
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
            else { return null; } // If any cell is invalid, the list is invalid for effects
        }
        return rects;
    }
}

// Helper Extensions (if not already available in your project's .NET version)
public static class LinqExtensions
{
    public static IEnumerable<TSource> DistinctBy<TSource, TKey>(
        this IEnumerable<TSource> source,
        System.Func<TSource, TKey> keySelector)
    {
        if (source == null) throw new System.ArgumentNullException(nameof(source));
        if (keySelector == null) throw new System.ArgumentNullException(nameof(keySelector));

        HashSet<TKey> seenKeys = new HashSet<TKey>();
        foreach (TSource element in source)
        {
            if (seenKeys.Add(keySelector(element)))
            {
                yield return element;
            }
        }
    }
}