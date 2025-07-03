using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;

/// <summary>
/// Manages the scrollable panel containing word tiles for the word placement game.
/// Handles word list population, filtering, and tile management.
/// </summary>
public class WordListPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Transform contentParent;
    [SerializeField] private GameObject wordTilePrefab;
    [SerializeField] private GridLayoutGroup gridLayoutGroup;

    [Header("Layout Settings")]
    [SerializeField] private float tileSpacing = 10f;
    [SerializeField] private Vector2 tileSize = new Vector2(120, 40);
    [SerializeField] private int tilesPerRow = 2;
    [SerializeField] private RectOffset contentPadding = new RectOffset(10, 10, 10, 10);

    [Header("Word Management")]
    [SerializeField] private WordListScriptableObject currentWordList;
    [SerializeField] private int maxWordsToShow = 12;
    [SerializeField] private bool shuffleWords = true;
    [SerializeField] private bool autoRefreshOnEmpty = true;

    [Header("Filtering")]
    [SerializeField] private int minWordLength = 3;
    [SerializeField] private int maxWordLength = 10;
    [SerializeField] private List<int> allowedDifficulties = new List<int> { 1, 2, 3, 4, 5 };

    [Header("Animation")]
    [SerializeField] private float tileAnimationDuration = 0.3f;
    [SerializeField] private float tileAnimationDelay = 0.1f;
    [SerializeField] private Ease tileAnimationEase = Ease.OutBack;

    // Word tile management
    private List<WordTile> activeWordTiles = new List<WordTile>();
    private List<WordTile> unusedWordTiles = new List<WordTile>();
    private Queue<WordData> availableWords = new Queue<WordData>();
    private HashSet<string> usedWords = new HashSet<string>();

    // State
    private bool isInitialized = false;
    private int wordsPlacedCount = 0;

    // Events
    public System.Action<WordTile> OnWordTileCreated;
    public System.Action<WordTile> OnWordTilePlaced;
    public System.Action<WordTile> OnWordTileRemoved;
    public System.Action OnWordListEmpty;
    public System.Action OnWordListRefreshed;

    #region Initialization

    void Awake()
    {
        ValidateReferences();
    }

    void Start()
    {
        SetupLayout();
        
        if (currentWordList != null)
        {
            PopulateWordList(currentWordList);
        }
    }

    private void ValidateReferences()
    {
        if (scrollRect == null)
        {
            scrollRect = GetComponentInChildren<ScrollRect>();
        }

        if (contentParent == null && scrollRect != null)
        {
            contentParent = scrollRect.content;
        }

        if (gridLayoutGroup == null && contentParent != null)
        {
            gridLayoutGroup = contentParent.GetComponent<GridLayoutGroup>();
        }

        if (wordTilePrefab == null)
        {
            Debug.LogError("WordListPanel: Word Tile Prefab not assigned!");
        }

        if (contentParent == null)
        {
            Debug.LogError("WordListPanel: Content Parent not found!");
        }
    }

    private void SetupLayout()
    {
        if (gridLayoutGroup == null) return;

        // Configure grid layout
        gridLayoutGroup.cellSize = tileSize;
        gridLayoutGroup.spacing = new Vector2(tileSpacing, tileSpacing);
        gridLayoutGroup.padding = contentPadding;
        gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayoutGroup.constraintCount = tilesPerRow;
        gridLayoutGroup.childAlignment = TextAnchor.UpperLeft;
        gridLayoutGroup.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayoutGroup.startCorner = GridLayoutGroup.Corner.UpperLeft;

        // Configure scroll rect
        if (scrollRect != null)
        {
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 10f;
        }
    }

    #endregion

    #region Word List Management

    public void PopulateWordList(WordListScriptableObject wordList)
    {
        if (wordList == null)
        {
            Debug.LogWarning("WordListPanel: Attempted to populate with null word list!");
            return;
        }

        currentWordList = wordList;
        
        // Clear existing data
        ClearWordList();
        
        // Filter and prepare words
        var filteredWords = FilterWords(wordList.Words);
        
        if (shuffleWords)
        {
            filteredWords = ShuffleWords(filteredWords);
        }
        
        // Add words to available queue
        availableWords.Clear();
        foreach (var word in filteredWords)
        {
            availableWords.Enqueue(word);
        }
        
        // Create initial word tiles
        CreateInitialWordTiles();
        
        isInitialized = true;
        OnWordListRefreshed?.Invoke();
        
        Debug.Log($"WordListPanel: Populated with {filteredWords.Count} words, showing {activeWordTiles.Count} initially");
    }

    private List<WordData> FilterWords(WordData[] words)
    {
        return words.Where(word => 
            word.word.Length >= minWordLength && 
            word.word.Length <= maxWordLength &&
            allowedDifficulties.Contains(word.difficulty) &&
            !usedWords.Contains(word.word.ToUpper())
        ).ToList();
    }

    private List<WordData> ShuffleWords(List<WordData> words)
    {
        for (int i = 0; i < words.Count; i++)
        {
            WordData temp = words[i];
            int randomIndex = Random.Range(i, words.Count);
            words[i] = words[randomIndex];
            words[randomIndex] = temp;
        }
        return words;
    }

    private void CreateInitialWordTiles()
    {
        int tilesToCreate = Mathf.Min(maxWordsToShow, availableWords.Count);
        
        for (int i = 0; i < tilesToCreate; i++)
        {
            if (availableWords.Count > 0)
            {
                WordData wordData = availableWords.Dequeue();
                CreateWordTile(wordData, i);
            }
        }
    }

    #endregion

    #region Word Tile Management

    private void CreateWordTile(WordData wordData, int index)
    {
        if (wordTilePrefab == null || contentParent == null) return;

        // Instantiate tile
        GameObject tileObj = Instantiate(wordTilePrefab, contentParent);
        WordTile wordTile = tileObj.GetComponent<WordTile>();
        
        if (wordTile == null)
        {
            wordTile = tileObj.AddComponent<WordTile>();
        }

        // Calculate letter scores
        int[] letterScores = LetterBlock.CalculateWordScores(wordData.word);
        
        // Initialize tile
        wordTile.Initialize(wordData.word, letterScores, wordData.difficulty);
        
        // Subscribe to events
        wordTile.OnWordPlaced += OnWordTilePlacedHandler;
        wordTile.OnWordRemoved += OnWordTileRemovedHandler;
        
        // Add to active tiles
        activeWordTiles.Add(wordTile);
        
        // Animate tile entrance
        AnimateTileEntrance(wordTile, index);
        
        OnWordTileCreated?.Invoke(wordTile);
        
        Debug.Log($"Created word tile: {wordData.word} (Difficulty: {wordData.difficulty}, Score: {wordTile.TotalScore})");
    }

    private void AnimateTileEntrance(WordTile wordTile, int index)
    {
        if (wordTile == null) return;

        RectTransform tileRect = wordTile.GetComponent<RectTransform>();
        if (tileRect == null) return;

        // Set initial state
        tileRect.localScale = Vector3.zero;
        
        // Animate entrance
        float delay = index * tileAnimationDelay;
        
        tileRect.DOScale(Vector3.one, tileAnimationDuration)
            .SetDelay(delay)
            .SetEase(tileAnimationEase);
    }

    private void OnWordTilePlacedHandler(WordTile wordTile)
    {
        if (wordTile == null) return;

        // Mark word as used
        usedWords.Add(wordTile.Word.ToUpper());
        wordsPlacedCount++;
        
        // Remove from active tiles
        activeWordTiles.Remove(wordTile);
        
        // Try to replace with new word
        if (autoRefreshOnEmpty && availableWords.Count > 0)
        {
            WordData newWordData = availableWords.Dequeue();
            CreateWordTile(newWordData, activeWordTiles.Count);
        }
        
        // Check if we need more words
        if (activeWordTiles.Count == 0 && availableWords.Count == 0)
        {
            OnWordListEmpty?.Invoke();
        }
        
        OnWordTilePlaced?.Invoke(wordTile);
        
        Debug.Log($"Word placed: {wordTile.Word}, Remaining tiles: {activeWordTiles.Count}, Words placed: {wordsPlacedCount}");
    }

    private void OnWordTileRemovedHandler(WordTile wordTile)
    {
        if (wordTile == null) return;

        // Mark word as unused
        usedWords.Remove(wordTile.Word.ToUpper());
        wordsPlacedCount--;
        
        // Add back to active tiles if not already there
        if (!activeWordTiles.Contains(wordTile))
        {
            activeWordTiles.Add(wordTile);
        }
        
        OnWordTileRemoved?.Invoke(wordTile);
        
        Debug.Log($"Word removed: {wordTile.Word}, Active tiles: {activeWordTiles.Count}");
    }

    #endregion

    #region Public Methods

    public void ClearWordList()
    {
        // Clear active tiles
        foreach (var tile in activeWordTiles)
        {
            if (tile != null)
            {
                // Unsubscribe from events
                tile.OnWordPlaced -= OnWordTilePlacedHandler;
                tile.OnWordRemoved -= OnWordTileRemovedHandler;
                
                // Destroy tile
                if (Application.isPlaying)
                {
                    Destroy(tile.gameObject);
                }
                else
                {
                    DestroyImmediate(tile.gameObject);
                }
            }
        }
        
        activeWordTiles.Clear();
        unusedWordTiles.Clear();
        availableWords.Clear();
        usedWords.Clear();
        wordsPlacedCount = 0;
    }

    public void RefreshWordList()
    {
        if (currentWordList != null)
        {
            PopulateWordList(currentWordList);
        }
    }

    public void AddBonusWords(int count)
    {
        for (int i = 0; i < count && availableWords.Count > 0; i++)
        {
            WordData wordData = availableWords.Dequeue();
            CreateWordTile(wordData, activeWordTiles.Count);
        }
    }

    public void RemoveWordTile(WordTile wordTile)
    {
        if (wordTile == null) return;

        activeWordTiles.Remove(wordTile);
        
        // Animate removal
        RectTransform tileRect = wordTile.GetComponent<RectTransform>();
        if (tileRect != null)
        {
            tileRect.DOScale(Vector3.zero, tileAnimationDuration)
                .SetEase(Ease.InBack)
                .OnComplete(() => {
                    if (wordTile != null)
                    {
                        Destroy(wordTile.gameObject);
                    }
                });
        }
    }

    public void ResetAllWordTiles()
    {
        foreach (var tile in activeWordTiles)
        {
            if (tile != null)
            {
                tile.ResetTile();
            }
        }
        
        usedWords.Clear();
        wordsPlacedCount = 0;
    }

    #endregion

    #region Layout Management

    public void SetTilesPerRow(int count)
    {
        tilesPerRow = Mathf.Max(1, count);
        
        if (gridLayoutGroup != null)
        {
            gridLayoutGroup.constraintCount = tilesPerRow;
        }
    }

    public void SetTileSize(Vector2 size)
    {
        tileSize = size;
        
        if (gridLayoutGroup != null)
        {
            gridLayoutGroup.cellSize = tileSize;
        }
    }

    public void UpdateLayoutForScreenSize()
    {
        // Adjust layout based on screen size
        float screenWidth = Screen.width;
        
        if (screenWidth < 768f) // Mobile
        {
            SetTilesPerRow(1);
            SetTileSize(new Vector2(screenWidth * 0.8f, 50f));
        }
        else if (screenWidth < 1200f) // Tablet
        {
            SetTilesPerRow(2);
            SetTileSize(new Vector2(screenWidth * 0.35f, 50f));
        }
        else // Desktop
        {
            SetTilesPerRow(2);
            SetTileSize(new Vector2(200f, 50f));
        }
    }

    #endregion

    #region Getters

    public List<WordTile> ActiveWordTiles => activeWordTiles;
    public int AvailableWordsCount => availableWords.Count;
    public int WordsPlacedCount => wordsPlacedCount;
    public bool HasAvailableWords => availableWords.Count > 0 || activeWordTiles.Count > 0;
    public WordListScriptableObject CurrentWordList => currentWordList;

    #endregion

    #region Debug

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void OnGUI()
    {
        if (!isInitialized) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 100));
        GUILayout.Label($"Active Tiles: {activeWordTiles.Count}");
        GUILayout.Label($"Available Words: {availableWords.Count}");
        GUILayout.Label($"Words Placed: {wordsPlacedCount}");
        GUILayout.Label($"Used Words: {usedWords.Count}");
        GUILayout.EndArea();
    }

    #endregion
}
