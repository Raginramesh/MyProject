using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using System.Linq;
using System; // Required for System.Guid

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

    [Header("Effects")]
    [SerializeField] private float scoreShakeDuration = 0.2f;
    [SerializeField] private float scoreShakeStrength = 3f;
    [SerializeField] private int scoreShakeVibrato = 15;

    private float currentTimeRemaining;
    private int currentMovesRemaining;
    private int currentScore = 0;
    private static GameManager instance;

    private List<FoundWordData> currentPotentialWords = new List<FoundWordData>();
    private HashSet<System.Guid> idsOfWordsInCurrentSequence = new HashSet<System.Guid>();


    void Awake()
    {
        if (instance == null) { instance = this; }
        else if (instance != this) { Destroy(gameObject); return; }

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
        if (scoreTextRectTransform == null && effectsManager != null) Debug.LogError("GM: Score Text RectTransform is MISSING! EffectsManager needs this.", this);

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
        if (currentState == newState && newState != GameState.Initializing) return;
        currentState = newState;
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
        currentScore = 0; UpdateScoreUI();
        currentPotentialWords.Clear();
        idsOfWordsInCurrentSequence.Clear();

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
            Debug.Log("GM.AttemptTap: No connected candidates found after FindAllConnectedWords.");
            return false;
        }

        List<FoundWordData> wordsToProcessInSequence = FilterSubWordsFromBatch(allConnectedCandidates);

        // Final safety check for already processed words (should be minimal if logic above is correct)
        // and ensure uniqueness by ID.
        wordsToProcessInSequence = wordsToProcessInSequence
                                    .Where(w => !wordValidator.IsWordFoundThisSession(w.Word))
                                    .DistinctBy(w => w.ID)
                                    .ToList();

        if (wordsToProcessInSequence.Count == 0)
        {
            Debug.Log("GM.AttemptTap: No words left after final filtering.");
            return false;
        }

        Debug.Log($"GM.AttemptTap: Starting sequence for {wordsToProcessInSequence.Count} FINAL words: " +
                  $"{string.Join(", ", wordsToProcessInSequence.Select(w => w.Word + $"({w.ID.ToString().Substring(0, 4)})"))}");

        StartCoroutine(ProcessWordsSequentially(wordsToProcessInSequence));
        return true;
    }

    private bool AreCoordinatesPrefixSubsequence(List<Vector2Int> prefixCoords, List<Vector2Int> mainCoords)
    {
        if (prefixCoords == null || mainCoords == null || prefixCoords.Count == 0 || prefixCoords.Count > mainCoords.Count)
        {
            return false;
        }
        for (int k = 0; k < prefixCoords.Count; k++)
        {
            if (prefixCoords[k] != mainCoords[k])
            {
                return false;
            }
        }
        return true;
    }

    private List<FoundWordData> FilterSubWordsFromBatch(List<FoundWordData> candidates)
    {
        if (candidates == null || candidates.Count <= 1)
        {
            return candidates ?? new List<FoundWordData>();
        }

        // Primary sort: by length descending. Secondary: by ID for stable sort.
        var sortedCandidates = candidates
            .OrderByDescending(w => w.Word.Length)
            .ThenBy(w => w.ID) // Ensures consistent processing order for tie-breaking
            .ToList();

        List<FoundWordData> keptWords = new List<FoundWordData>();
        HashSet<System.Guid> discardedWordIds = new HashSet<System.Guid>();

        for (int i = 0; i < sortedCandidates.Count; i++)
        {
            FoundWordData currentWord = sortedCandidates[i];

            if (discardedWordIds.Contains(currentWord.ID))
            {
                continue; // Already discarded by a longer word.
            }

            // If not discarded, this word is a keeper (for now).
            // No need to add to keptWords yet, we'll build it from non-discarded items at the end.

            // Check against all *other* words in the original sorted list.
            // We are checking if 'currentWord' makes any *other* word a sub-word.
            // Or, more directly, if 'currentWord' is kept, should any *other* word be discarded?
            for (int j = 0; j < sortedCandidates.Count; j++)
            {
                if (i == j) continue; // Don't compare a word to itself.

                FoundWordData otherWord = sortedCandidates[j];
                if (discardedWordIds.Contains(otherWord.ID)) continue; // Other word already discarded.

                // If 'otherWord' is shorter than 'currentWord', is in the same orientation,
                // 'currentWord' starts with 'otherWord', and coordinates match as a prefix.
                if (otherWord.Word.Length < currentWord.Word.Length &&
                    currentWord.GetOrientation() == otherWord.GetOrientation() &&
                    currentWord.Word.StartsWith(otherWord.Word) &&
                    AreCoordinatesPrefixSubsequence(otherWord.Coordinates, currentWord.Coordinates))
                {
                    // 'otherWord' is a sub-word of 'currentWord' on the same axis. Discard 'otherWord'.
                    // Debug.Log($"FilterSubWords: Discarding '{otherWord.Word}' because it's a sub-word of '{currentWord.Word}'");
                    discardedWordIds.Add(otherWord.ID);
                }
            }
        }

        // Collect all words that were not discarded.
        foreach (var word in sortedCandidates) // Iterate through original sorted list to maintain some order
        {
            if (!discardedWordIds.Contains(word.ID))
            {
                keptWords.Add(word);
            }
        }

        // `keptWords` could potentially still have words that are identical if `FindAllConnectedWords` produced them,
        // so a final DistinctBy ID is good practice, although `ProcessWordsSequentially` also has a `IsWordFoundThisSession` check.
        return keptWords.DistinctBy(w => w.ID).ToList();
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
            Vector2Int intersectionPoint;

            foreach (var potentialWordOnGrid in currentPotentialWords)
            {
                if (potentialWordOnGrid.ID == currentWord.ID) continue;
                if (visitedWordIds.Contains(potentialWordOnGrid.ID)) continue;
                if (wordValidator.IsWordFoundThisSession(potentialWordOnGrid.Word)) continue;

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

    private FoundWordData? SelectPrimaryFromCandidates(List<FoundWordData> candidates)
    {
        if (candidates == null || candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0];
        return candidates.OrderByDescending(w => w.Word.Length)
                         .ThenBy(w => w.GetOrientation())
                         .ThenBy(w => w.Coordinates[0].x)
                         .ThenBy(w => w.Coordinates[0].y)
                         .First();
    }

    private IEnumerator ProcessWordsSequentially(List<FoundWordData> wordsToAnimate)
    {
        if (wordsToAnimate == null || wordsToAnimate.Count == 0)
        {
            yield break;
        }

        isProcessingSequentialWords = true;
        idsOfWordsInCurrentSequence.Clear();

        List<Vector2Int> allCellsAffectedInThisSequence = new List<Vector2Int>();

        foreach (var wordData in wordsToAnimate) // wordsToAnimate should be correctly filtered now
        {
            if (wordValidator.IsWordFoundThisSession(wordData.Word)) // Safeguard
            {
                continue;
            }

            Debug.Log($"GM.ProcessSeq: Processing word: {wordData.Word} (ID: {wordData.ID}) for animation.");
            idsOfWordsInCurrentSequence.Add(wordData.ID);
            allCellsAffectedInThisSequence.AddRange(wordData.Coordinates);

            if (wordGridManager != null)
            {
                wordGridManager.ClearHighlightForSpecificWord(wordData);
            }
            currentPotentialWords.RemoveAll(pwd => pwd.ID == wordData.ID);


            List<RectTransform> sourceCellRects = GetRectTransformsForCoords(wordData.Coordinates);
            if (sourceCellRects == null || sourceCellRects.Count != wordData.Word.Length)
            {
                Debug.LogError($"GM.ProcessSeq: Could not get valid RectTransforms for word '{wordData.Word}'. Skipping its animation.");
                wordValidator.MarkWordAsFoundInSession(wordData.Word);
                foreach (char letter in wordData.Word) HandleSingleLetterScore(CalculateScoreValueForLetter(letter));
                continue;
            }

            List<int> individualLetterScores = wordData.Word.Select(letter => CalculateScoreValueForLetter(letter)).ToList();

            if (effectsManager != null)
            {
                effectsManager.PlayFlyToScoreEffect(sourceCellRects, wordData.Word, individualLetterScores, HandleSingleLetterScore);
                yield return new WaitUntil(() => !effectsManager.IsAnimating);
            }
            else
            {
                Debug.LogWarning("GM.ProcessSeq: EffectsManager is null. Scoring word without animation.");
                foreach (int scoreValue in individualLetterScores) HandleSingleLetterScore(scoreValue);
            }
            wordValidator.MarkWordAsFoundInSession(wordData.Word);
        }

        yield return new WaitForSeconds(replacementDelayAfterEffectStart);

        if (wordGridManager != null && allCellsAffectedInThisSequence.Count > 0)
        {
            List<Vector2Int> uniqueCellsToReplace = allCellsAffectedInThisSequence.Distinct().ToList();
            wordGridManager.ReplaceLettersAt(uniqueCellsToReplace, true);

            yield return new WaitUntil(() => !wordGridManager.isAnimating);

            wordGridManager.TriggerValidationCheckAndHighlightUpdate();
        }

        isProcessingSequentialWords = false;
        idsOfWordsInCurrentSequence.Clear();
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
        if (wordGridManager == null || coords == null)
        {
            Debug.LogError("GM.GetRects: WordGridManager or coords null.");
            return null;
        }
        foreach (var coord in coords)
        {
            CellController cell = wordGridManager.GetCellController(coord);
            if (cell != null && cell.RectTransform != null && cell.gameObject.activeSelf)
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

    public List<FoundWordData> GetCurrentPotentialWords()
    {
        return new List<FoundWordData>(currentPotentialWords); // Return a copy
    }

    public bool IsWordInCurrentProcessingSequence(System.Guid wordId)
    {
        return idsOfWordsInCurrentSequence.Contains(wordId);
    }
}

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