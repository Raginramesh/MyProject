using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;
using System.Linq;

public class WordGridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int _gridSize = 4;
    [SerializeField] private float _cellSize = 100f;
    [SerializeField] private float _spacing = 10f;
    [SerializeField] private GameObject letterCellPrefab;
    [SerializeField] private Transform gridParent;

    public int gridSize => _gridSize;
    public float cellSize => _cellSize;
    public float spacing => _spacing;

    [Header("Appearance")]
    [SerializeField] private float cellFadeInDuration = 0.3f;
    public float CellFadeInDuration => cellFadeInDuration;

    [SerializeField] private Color cellColorPrimary = Color.white;
    [SerializeField] private Color cellColorAlternate = new Color(0.9f, 0.9f, 0.9f, 1f);
    [Tooltip("Color used to highlight cells that form a potentially valid word.")]
    [SerializeField] public Color potentialWordHighlightColor = Color.yellow; // Public for GM access or direct use

    [Header("References")]
    [SerializeField] private WordValidator wordValidator;
    [SerializeField] private GameManager gameManager;

    public char[,] gridData { get; private set; }
    private CellController[,] gridCells;
    private LetterCell[,] gridCellComponents; // For move reduction if LetterCell.cs is used

    private List<char> WeightedLetters = new List<char>();
    public bool isAnimating { get; private set; } = false;

    void Awake()
    {
        isAnimating = false;
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        if (wordValidator == null) wordValidator = FindFirstObjectByType<WordValidator>();

        if (wordValidator == null) { Debug.LogError("WGM: WordValidator reference not set or found!", this); enabled = false; return; }
        if (gameManager == null) Debug.LogWarning("WGM: GameManager not found in Awake (may be set later).", this);
    }

    void Start()
    {
        isAnimating = false;
        PopulateWeightedLettersList();
    }
    void OnEnable() { isAnimating = false; }
    public void SetGameManager(GameManager manager) { gameManager = manager; }

    public void InitializeGrid()
    {
        isAnimating = false;
        if (letterCellPrefab == null) { Debug.LogError("WGM: Letter Cell Prefab missing!", this); return; }
        if (gridParent == null) { gridParent = this.transform; }

        if (gridCells != null)
        {
            for (int r = 0; r < gridCells.GetLength(0); r++)
            {
                for (int c = 0; c < gridCells.GetLength(1); c++)
                {
                    if (gridCells[r, c] != null)
                    {
                        DOTween.Kill(gridCells[r, c].transform);
                        if (gridCells[r, c].TryGetComponent<CanvasGroup>(out var cg)) { DOTween.Kill(cg); }
                        if (gridCells[r, c].gameObject != null)
                        {
                            if (Application.isEditor && !Application.isPlaying) { DestroyImmediate(gridCells[r, c].gameObject); }
                            else { Destroy(gridCells[r, c].gameObject); }
                        }
                    }
                }
            }
        }
        gridData = new char[gridSize, gridSize];
        gridCells = new CellController[gridSize, gridSize];
        gridCellComponents = new LetterCell[gridSize, gridSize]; // If using LetterCell for moves

        PopulateGridData();

        float totalGridSizeUI = gridSize * cellSize + (gridSize - 1) * spacing;
        float startOffset = -totalGridSizeUI / 2f + cellSize / 2f;

        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                GameObject cellGO = Instantiate(letterCellPrefab, gridParent);
                CellController cellController = cellGO.GetComponent<CellController>();
                LetterCell cellComponent = cellGO.GetComponent<LetterCell>(); // For LetterCell.cs
                Image cellImage = cellGO.GetComponent<Image>();

                if (cellController == null) { Debug.LogError($"Prefab missing CellController!", cellGO); Destroy(cellGO); continue; }

                RectTransform cellRect = cellController.RectTransform;
                float posX = startOffset + c * (cellSize + spacing);
                float posY = startOffset + (gridSize - 1 - r) * (cellSize + spacing);
                cellRect.anchoredPosition = new Vector2(posX, posY);
                cellRect.sizeDelta = new Vector2(cellSize, cellSize);
                cellRect.localScale = Vector3.one;

                cellController.SetLetter(gridData[r, c]);
                cellController.SetAlpha(1f);

                if (cellImage != null)
                {
                    cellImage.color = (r + c) % 2 == 0 ? cellColorPrimary : cellColorAlternate;
                    cellController.StoreDefaultColor(); // Store this initial color in CellController
                }

                gridCells[r, c] = cellController;
                gridCellComponents[r, c] = cellComponent; // Store LetterCell
            }
        }
    }

    void PopulateWeightedLettersList()
    {
        WeightedLetters.Clear();
        WeightedLetters.AddRange(Enumerable.Repeat('E', 12)); WeightedLetters.AddRange(Enumerable.Repeat('A', 9));
        WeightedLetters.AddRange(Enumerable.Repeat('I', 9)); WeightedLetters.AddRange(Enumerable.Repeat('O', 8));
        WeightedLetters.AddRange(Enumerable.Repeat('N', 6)); WeightedLetters.AddRange(Enumerable.Repeat('R', 6));
        WeightedLetters.AddRange(Enumerable.Repeat('T', 6)); WeightedLetters.AddRange(Enumerable.Repeat('L', 4));
        WeightedLetters.AddRange(Enumerable.Repeat('S', 4)); WeightedLetters.AddRange(Enumerable.Repeat('U', 4));
        WeightedLetters.AddRange(Enumerable.Repeat('D', 4)); WeightedLetters.AddRange(Enumerable.Repeat('G', 3));
        WeightedLetters.AddRange(Enumerable.Repeat('B', 2)); WeightedLetters.AddRange(Enumerable.Repeat('C', 2));
        WeightedLetters.AddRange(Enumerable.Repeat('M', 2)); WeightedLetters.AddRange(Enumerable.Repeat('P', 2));
        WeightedLetters.AddRange(Enumerable.Repeat('F', 2)); WeightedLetters.AddRange(Enumerable.Repeat('H', 2));
        WeightedLetters.AddRange(Enumerable.Repeat('V', 2)); WeightedLetters.AddRange(Enumerable.Repeat('W', 2));
        WeightedLetters.AddRange(Enumerable.Repeat('Y', 2)); WeightedLetters.AddRange(Enumerable.Repeat('K', 1));
        WeightedLetters.AddRange(Enumerable.Repeat('J', 1)); WeightedLetters.AddRange(Enumerable.Repeat('X', 1));
        WeightedLetters.AddRange(Enumerable.Repeat('Q', 1)); WeightedLetters.AddRange(Enumerable.Repeat('Z', 1));
    }

    void PopulateGridData()
    {
        if (gridData == null) { Debug.LogError("PopulateGridData: gridData array is null!", this); return; }
        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                gridData[r, c] = GetRandomLetter();
            }
        }
    }

    char GetRandomLetter()
    {
        if (WeightedLetters == null || WeightedLetters.Count == 0) { return '?'; }
        return WeightedLetters[Random.Range(0, WeightedLetters.Count)];
    }

    public void RequestRowScroll(int rowIndex, int direction, float scrollAmount)
    {
        if (!enabled || isAnimating || gameManager == null || gameManager.CurrentStatePublic != GameManager.GameState.Playing) return;
        isAnimating = true;
        ShiftRowData(rowIndex, direction);
        AnimateRowScroll(rowIndex, direction, scrollAmount);
    }
    public void RequestColumnScroll(int colIndex, int direction, float scrollAmount)
    {
        if (!enabled || isAnimating || gameManager == null || gameManager.CurrentStatePublic != GameManager.GameState.Playing) return;
        isAnimating = true;
        ShiftColumnData(colIndex, direction);
        AnimateColumnScroll(colIndex, direction, scrollAmount);
    }

    void ShiftRowData(int rowIndex, int direction)
    {
        if (rowIndex < 0 || rowIndex >= gridSize || gridData == null || gridCells == null) return;
        if (direction == 1) // Right
        {
            char tempData = gridData[rowIndex, gridSize - 1];
            CellController tempCellController = gridCells[rowIndex, gridSize - 1];
            LetterCell tempLetterCell = gridCellComponents?[rowIndex, gridSize - 1];
            for (int c = gridSize - 1; c > 0; c--)
            {
                gridData[rowIndex, c] = gridData[rowIndex, c - 1];
                gridCells[rowIndex, c] = gridCells[rowIndex, c - 1];
                if (gridCellComponents != null) gridCellComponents[rowIndex, c] = gridCellComponents[rowIndex, c - 1];
            }
            gridData[rowIndex, 0] = tempData;
            gridCells[rowIndex, 0] = tempCellController;
            if (gridCellComponents != null) gridCellComponents[rowIndex, 0] = tempLetterCell;
        }
        else // Left
        {
            char tempData = gridData[rowIndex, 0];
            CellController tempCellController = gridCells[rowIndex, 0];
            LetterCell tempLetterCell = gridCellComponents?[rowIndex, 0];
            for (int c = 0; c < gridSize - 1; c++)
            {
                gridData[rowIndex, c] = gridData[rowIndex, c + 1];
                gridCells[rowIndex, c] = gridCells[rowIndex, c + 1];
                if (gridCellComponents != null) gridCellComponents[rowIndex, c] = gridCellComponents[rowIndex, c + 1];
            }
            gridData[rowIndex, gridSize - 1] = tempData;
            gridCells[rowIndex, gridSize - 1] = tempCellController;
            if (gridCellComponents != null) gridCellComponents[rowIndex, gridSize - 1] = tempLetterCell;
        }
    }
    void ShiftColumnData(int colIndex, int direction)
    {
        if (colIndex < 0 || colIndex >= gridSize || gridData == null || gridCells == null) return;
        if (direction == 1) // Down
        {
            char tempData = gridData[gridSize - 1, colIndex];
            CellController tempCellController = gridCells[gridSize - 1, colIndex];
            LetterCell tempLetterCell = gridCellComponents?[gridSize - 1, colIndex];
            for (int r = gridSize - 1; r > 0; r--)
            {
                gridData[r, colIndex] = gridData[r - 1, colIndex];
                gridCells[r, colIndex] = gridCells[r - 1, colIndex];
                if (gridCellComponents != null) gridCellComponents[r, colIndex] = gridCellComponents[r - 1, colIndex];
            }
            gridData[0, colIndex] = tempData;
            gridCells[0, colIndex] = tempCellController;
            if (gridCellComponents != null) gridCellComponents[0, colIndex] = tempLetterCell;
        }
        else // Up
        {
            char tempData = gridData[0, colIndex];
            CellController tempCellController = gridCells[0, colIndex];
            LetterCell tempLetterCell = gridCellComponents?[0, colIndex];
            for (int r = 0; r < gridSize - 1; r++)
            {
                gridData[r, colIndex] = gridData[r + 1, colIndex];
                gridCells[r, colIndex] = gridCells[r + 1, colIndex];
                if (gridCellComponents != null) gridCellComponents[r, colIndex] = gridCellComponents[r + 1, colIndex];
            }
            gridData[gridSize - 1, colIndex] = tempData;
            gridCells[gridSize - 1, colIndex] = tempCellController;
            if (gridCellComponents != null) gridCellComponents[gridSize - 1, colIndex] = tempLetterCell;
        }
    }

    void AnimateRowScroll(int rowIndex, int direction, float scrollAmount)
    {
        float totalMoveDistance = gridSize * (cellSize + spacing);
        float singleCellMove = direction * (cellSize + spacing);
        Sequence seq = DOTween.Sequence();
        if (gridCells == null || rowIndex < 0 || rowIndex >= gridCells.GetLength(0)) { ResetAnimationFlag("RowScroll Error"); return; }

        for (int c = 0; c < gridSize; c++)
        {
            if (c < 0 || c >= gridCells.GetLength(1) || gridCells[rowIndex, c] == null) continue;
            RectTransform cellRect = gridCells[rowIndex, c].RectTransform;
            seq.Join(cellRect.DOAnchorPosX(cellRect.anchoredPosition.x + singleCellMove, 0.3f).SetEase(Ease.OutCubic));
        }
        int wrapIndex = (direction == 1) ? 0 : gridSize - 1;
        if (wrapIndex >= 0 && wrapIndex < gridCells.GetLength(1) && gridCells[rowIndex, wrapIndex] != null)
        {
            RectTransform wrapCellRect = gridCells[rowIndex, wrapIndex].RectTransform;
            Vector2 wrapStartPos = wrapCellRect.anchoredPosition;
            seq.InsertCallback(0.29f, () => {
                if (wrapCellRect != null) wrapCellRect.anchoredPosition = new Vector2(wrapStartPos.x - direction * totalMoveDistance, wrapStartPos.y);
            });
        }
        seq.OnKill(() => ResetAnimationFlag("RowScroll Killed"));
        seq.OnComplete(() => {
            try { SnapToGridPositions(); } catch (System.Exception e) { Debug.LogError($"Error SnapToGridPositions: {e.Message}", this); }
            ResetAnimationFlag("RowScroll Complete");
        });
    }

    void AnimateColumnScroll(int colIndex, int direction, float scrollAmount)
    {
        float totalMoveDistance = gridSize * (cellSize + spacing);
        float singleCellMove = -direction * (cellSize + spacing);
        Sequence seq = DOTween.Sequence();
        if (gridCells == null || colIndex < 0 || colIndex >= gridCells.GetLength(1)) { ResetAnimationFlag("ColScroll Error"); return; }

        for (int r = 0; r < gridSize; r++)
        {
            if (r < 0 || r >= gridCells.GetLength(0) || gridCells[r, colIndex] == null) continue;
            RectTransform cellRect = gridCells[r, colIndex].RectTransform;
            seq.Join(cellRect.DOAnchorPosY(cellRect.anchoredPosition.y + singleCellMove, 0.3f).SetEase(Ease.OutCubic));
        }
        int wrapIndex = (direction == 1) ? 0 : gridSize - 1;
        if (wrapIndex >= 0 && wrapIndex < gridCells.GetLength(0) && gridCells[wrapIndex, colIndex] != null)
        {
            RectTransform wrapCellRect = gridCells[wrapIndex, colIndex].RectTransform;
            Vector2 wrapStartPos = wrapCellRect.anchoredPosition;
            seq.InsertCallback(0.29f, () => {
                if (wrapCellRect != null) wrapCellRect.anchoredPosition = new Vector2(wrapStartPos.x, wrapStartPos.y + direction * totalMoveDistance);
            });
        }
        seq.OnKill(() => ResetAnimationFlag("ColScroll Killed"));
        seq.OnComplete(() => {
            try { SnapToGridPositions(); } catch (System.Exception e) { Debug.LogError($"Error SnapToGridPositions: {e.Message}", this); }
            ResetAnimationFlag("ColScroll Complete");
        });
    }

    private void ResetAnimationFlag(string reason)
    {
        if (isAnimating) { isAnimating = false; /* Debug.Log($"ResetAnimationFlag: {reason}"); */ }
    }

    void SnapToGridPositions()
    {
        if (gridCells == null) return;
        float totalGridSizeUI = gridSize * cellSize + (gridSize - 1) * spacing;
        float startOffset = -totalGridSizeUI / 2f + cellSize / 2f;
        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                if (gridCells[r, c] != null)
                {
                    float targetX = startOffset + c * (cellSize + spacing);
                    float targetY = startOffset + (gridSize - 1 - r) * (cellSize + spacing);
                    RectTransform cellRect = gridCells[r, c].RectTransform;
                    if (cellRect != null) cellRect.anchoredPosition = new Vector2(targetX, targetY);
                }
            }
        }
    }

    public void ReplaceLettersAt(List<Vector2Int> coordinates, bool fadeIn = false)
    {
        if (coordinates == null || coordinates.Count == 0 || gridData == null) return;
        foreach (Vector2Int coord in coordinates)
        {
            if (coord.x >= 0 && coord.x < gridSize && coord.y >= 0 && coord.y < gridSize)
            {
                char newLetter = GetRandomLetter();
                gridData[coord.x, coord.y] = newLetter;
                CellController cellController = GetCellController(coord);
                if (cellController != null)
                {
                    cellController.SetLetter(newLetter);
                    if (fadeIn) cellController.FadeIn(cellFadeInDuration);
                    else cellController.SetAlpha(1f);
                }
            }
        }
    }

    /// <summary>
    /// Triggers validation and tells GameManager to update potential word highlights.
    /// </summary>
    public void TriggerValidationCheckAndHighlightUpdate()
    {
        if (gameManager == null || wordValidator == null) { Debug.LogError("WGM: Missing refs for Validation/Highlight!", this); return; }

        // We can call this even if animating, as highlighting is a visual update,
        // but GameManager will decide if it acts on taps based on its IsAnyAnimationPlaying.
        // However, it's generally better to validate when the grid is stable.
        if (gameManager.IsAnyAnimationPlaying && gameManager.CurrentStatePublic == GameManager.GameState.Playing)
        {
            // Debug.Log("WGM: Validation highlight update deferred as an animation is playing.");
            // Optionally, set a flag to run this once animations complete.
            return;
        }

        List<FoundWordData> potentialWords = wordValidator.FindAllPotentialWords();
        gameManager.UpdatePotentialWordsDisplay(potentialWords); // New method in GameManager
    }

    public void HighlightPotentialWordCells(List<FoundWordData> potentialWords)
    {
        if (gridCells == null) return;
        ClearAllCellHighlights(false); // Clear previous without affecting current potential words list in GM

        foreach (FoundWordData wordData in potentialWords)
        {
            foreach (Vector2Int coord in wordData.Coordinates)
            {
                CellController cell = GetCellController(coord);
                if (cell != null)
                {
                    cell.SetHighlightState(true, potentialWordHighlightColor);
                }
            }
        }
    }

    public void ClearAllCellHighlights(bool fullReset = true)
    {
        if (gridCells == null) return;
        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                if (gridCells[r, c] != null)
                {
                    // Pass the cell's own default color for reset
                    gridCells[r, c].SetHighlightState(false, gridCells[r, c].GetDefaultColor());
                }
            }
        }
        if (fullReset && gameManager != null)
        {
            // If it's a full reset, also clear game manager's list of potential words
            // gameManager.ClearPotentialWords(); // This method would need to be added to GM
        }
    }

    public void ApplyPendingMoveReduction(int row, int col)
    {
        if ((row < 0 && col < 0) || (row >= 0 && col >= 0)) return; // Invalid state
        if (gameManager != null) gameManager.DecrementMoves();

        // LetterCell move reduction logic (if LetterCell.cs is used and configured on prefabs)
        bool wasRowScroll = row >= 0;
        if (gridCellComponents != null) // Check if LetterCell components are being tracked
        {
            if (wasRowScroll)
            {
                for (int c = 0; c < gridSize; c++)
                {
                    LetterCell cell = GetLetterCellAt(row, c);
                    if (cell != null && cell.EnableMoves) cell.ReduceMove();
                }
            }
            else // Column scroll
            {
                for (int r = 0; r < gridSize; r++)
                {
                    LetterCell cell = GetLetterCellAt(r, col);
                    if (cell != null && cell.EnableMoves) cell.ReduceMove();
                }
            }
        }
    }

    public LetterCell GetLetterCellAt(int row, int col)
    {
        if (gridCellComponents != null && row >= 0 && row < gridSize && col >= 0 && col < gridSize)
        {
            return gridCellComponents[row, col];
        }
        return null;
    }

    public CellController GetCellController(Vector2Int coord)
    {
        if (gridCells != null && coord.x >= 0 && coord.x < gridSize && coord.y >= 0 && coord.y < gridSize)
        {
            return gridCells[coord.x, coord.y];
        }
        return null;
    }
}