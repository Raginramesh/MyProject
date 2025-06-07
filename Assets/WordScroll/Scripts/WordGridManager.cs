using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

public class WordGridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int _gridSize = 7;
    public int gridSize => _gridSize;
    [SerializeField] private float _cellSize = 100f;
    public float cellSize => _cellSize;
    [SerializeField] private float _spacing = 10f;
    public float spacing => _spacing;   
    [SerializeField] private GameObject letterCellPrefab;
    [SerializeField] private Transform gridParent;

    [Header("Appearance")]
    [SerializeField] private float cellFadeInDuration = 0.3f;
    public float CellFadeInDuration => cellFadeInDuration;
    [SerializeField] private Color cellColorPrimary = Color.white;
    [SerializeField] private Color cellColorAlternate = Color.grey;

    [Header("Highlighting")]
    [Tooltip("Colors to cycle through for highlighting different potential words. Define at least one.")]
    [SerializeField]
    private Color[] wordHighlightPalette = { Color.yellow, Color.cyan, Color.magenta, Color.green, Color.blue, Color.red };

    [Header("References")]
    [SerializeField] private WordValidator wordValidator;
    [SerializeField] private GameManager gameManager;

    private CellController[,] gridCells;
    public char[,] gridData { get; private set; }

    public bool isAnimating { get; private set; } = false;

    private List<char> WeightedLetters = new List<char>();
    private Vector2 gridCenterOffset;

    void Awake()
    {
        if (letterCellPrefab == null) { Debug.LogError("WGM: Letter Cell Prefab not assigned!", this); enabled = false; return; }
        if (gridParent == null) { Debug.LogError("WGM: Grid Parent not assigned!", this); enabled = false; return; }
        if (wordValidator == null) wordValidator = FindFirstObjectByType<WordValidator>();
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        if (wordValidator == null) Debug.LogWarning("WGM: WordValidator not found!", this);
        if (gameManager == null) Debug.LogWarning("WGM: GameManager not found!", this);

        gridCells = new CellController[gridSize, gridSize];
        gridData = new char[gridSize, gridSize];
        PopulateWeightedLettersList();
        CalculateGridCenterOffset();
    }

    public void SetGameManager(GameManager manager)
    {
        this.gameManager = manager;
    }

    private void CalculateGridCenterOffset()
    {
        float totalGridWidth = gridSize * cellSize + (gridSize - 1) * spacing;
        gridCenterOffset = new Vector2(totalGridWidth / 2f - cellSize / 2f, totalGridWidth / 2f - cellSize / 2f);
    }

    public Vector2 GetBaseCellPosition(int r, int c)
    {
        float xPos = c * (cellSize + spacing) - gridCenterOffset.x;
        float yPos = -(r * (cellSize + spacing) - gridCenterOffset.y);
        return new Vector2(xPos, yPos);
    }

    public void InitializeGrid()
    {
        isAnimating = true; // Temporarily set for initial fade-in
        PopulateGridData();

        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                GameObject cellGO;
                if (gridCells[r, c] == null)
                {
                    cellGO = Instantiate(letterCellPrefab, gridParent);
                    gridCells[r, c] = cellGO.GetComponent<CellController>();
                    if (gridCells[r, c] == null)
                    {
                        Debug.LogError($"WGM: Cell prefab {letterCellPrefab.name} is missing CellController script.", this);
                        Destroy(cellGO);
                        isAnimating = false;
                        return;
                    }
                }
                else
                {
                    cellGO = gridCells[r, c].gameObject;
                }

                CellController cell = gridCells[r, c];
                cell.gameObject.name = $"Cell_{r}_{c}";

                RectTransform cellRectTransform = cellGO.GetComponent<RectTransform>();
                if (cellRectTransform != null)
                {
                    cellRectTransform.sizeDelta = new Vector2(cellSize, cellSize);
                }
                else
                {
                    Debug.LogWarning($"WGM: Cell_{r}_{c} prefab instance is missing RectTransform.", this);
                }

                cell.transform.localPosition = GetBaseCellPosition(r, c);
                cell.SetLetter(gridData[r, c]);
                Image bgImage = cell.GetComponent<Image>();
                if (bgImage != null)
                {
                    bgImage.color = (r + c) % 2 == 0 ? cellColorPrimary : cellColorAlternate;
                }
                cell.StoreDefaultColor(); // Store color after setting it
                cell.FadeIn(cellFadeInDuration);
            }
        }

        DOVirtual.DelayedCall(cellFadeInDuration + 0.1f, () =>
        {
            ResetAnimationFlag("InitializeGrid");
            TriggerValidationCheckAndHighlightUpdate();
        }, false);
    }

    void PopulateWeightedLettersList()
    {
        WeightedLetters.Clear();
        AddLetters("E", 12); AddLetters("A", 9); AddLetters("I", 9); AddLetters("O", 8);
        AddLetters("N", 6); AddLetters("R", 6); AddLetters("T", 6); AddLetters("L", 4);
        AddLetters("S", 4); AddLetters("U", 4); AddLetters("D", 4); AddLetters("G", 3);
        AddLetters("B", 2); AddLetters("C", 2); AddLetters("M", 2); AddLetters("P", 2);
        AddLetters("F", 2); AddLetters("H", 2); AddLetters("V", 2); AddLetters("W", 2); AddLetters("Y", 2);
        AddLetters("K", 1); AddLetters("J", 1); AddLetters("X", 1); AddLetters("Q", 1); AddLetters("Z", 1);
    }

    void AddLetters(string letters, int count)
    {
        foreach (char letter in letters)
        {
            for (int i = 0; i < count; i++) { WeightedLetters.Add(letter); }
        }
    }

    void PopulateGridData()
    {
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
        if (WeightedLetters.Count == 0) return 'A';
        return WeightedLetters[UnityEngine.Random.Range(0, WeightedLetters.Count)];
    }

    public void SetRowVisualOffset(int rowIndex, float currentFrameVisualOffset)
    {
        if (rowIndex < 0 || rowIndex >= gridSize) return;
        for (int c = 0; c < gridSize; c++)
        {
            if (gridCells[rowIndex, c] == null) continue;
            Vector2 basePos = GetBaseCellPosition(rowIndex, c);
            gridCells[rowIndex, c].transform.localPosition = new Vector3(basePos.x + currentFrameVisualOffset, basePos.y, 0);
        }
    }

    public void SetColumnVisualOffset(int colIndex, float currentFrameVisualOffset)
    {
        if (colIndex < 0 || colIndex >= gridSize) return;
        for (int r = 0; r < gridSize; r++)
        {
            if (gridCells[r, colIndex] == null) continue;
            Vector2 basePos = GetBaseCellPosition(r, colIndex);
            gridCells[r, colIndex].transform.localPosition = new Vector3(basePos.x, basePos.y + currentFrameVisualOffset, 0);
        }
    }

    public void SnapRowToGrid(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= gridSize) return;
        for (int c = 0; c < gridSize; c++)
        {
            if (gridCells[rowIndex, c] != null)
            {
                gridCells[rowIndex, c].transform.localPosition = GetBaseCellPosition(rowIndex, c);
                gridCells[rowIndex, c].SetLetter(gridData[rowIndex, c]); // Ensure letter is correct
            }
        }
    }

    public void SnapColumnToGrid(int colIndex)
    {
        if (colIndex < 0 || colIndex >= gridSize) return;
        for (int r = 0; r < gridSize; r++)
        {
            if (gridCells[r, colIndex] != null)
            {
                gridCells[r, colIndex].transform.localPosition = GetBaseCellPosition(r, colIndex);
                gridCells[r, colIndex].SetLetter(gridData[r, colIndex]); // Ensure letter is correct
            }
        }
    }

    // Shifts data and refreshes cell letters
    public void ShiftRowDataAndRefresh(int rowIndex, int cellsToShift)
    {
        if (rowIndex < 0 || rowIndex >= gridSize || cellsToShift == 0) return;
        int direction = Math.Sign(cellsToShift);
        for (int i = 0; i < Math.Abs(cellsToShift); i++)
        {
            ShiftRowDataInternal(rowIndex, direction);
        }
        RefreshCellLettersInRow(rowIndex);
    }

    // Shifts data and refreshes cell letters
    public void ShiftColumnDataAndRefresh(int colIndex, int cellsToShift)
    {
        if (colIndex < 0 || colIndex >= gridSize || cellsToShift == 0) return;
        int direction = Math.Sign(cellsToShift);
        for (int i = 0; i < Math.Abs(cellsToShift); i++)
        {
            ShiftColumnDataInternal(colIndex, direction);
        }
        RefreshCellLettersInColumn(colIndex);
    }

    // Internal method for single step data shift
    private void ShiftRowDataInternal(int rowIndex, int direction)
    {
        if (direction == 0) return;
        char[] tempRow = new char[gridSize];
        for (int c = 0; c < gridSize; c++) tempRow[c] = gridData[rowIndex, c];

        for (int c = 0; c < gridSize; c++)
        {
            int prevC = (c - direction + gridSize) % gridSize; // direction 1 means data from left moves to current
            gridData[rowIndex, c] = tempRow[prevC];
        }
    }

    private void ShiftColumnDataInternal(int colIndex, int direction)
    {
        if (direction == 0) return;
        char[] tempCol = new char[gridSize];
        for (int r = 0; r < gridSize; r++) tempCol[r] = gridData[r, colIndex];

        for (int r = 0; r < gridSize; r++)
        {
            int prevR = (r - direction + gridSize) % gridSize; // direction 1 means data from top moves to current
            gridData[r, colIndex] = tempCol[prevR];
        }
    }

    private void RefreshCellLettersInRow(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= gridSize) return;
        for (int c = 0; c < gridSize; c++)
        {
            if (gridCells[rowIndex, c] != null)
            {
                gridCells[rowIndex, c].SetLetter(gridData[rowIndex, c]);
            }
        }
    }

    private void RefreshCellLettersInColumn(int colIndex)
    {
        if (colIndex < 0 || colIndex >= gridSize) return;
        for (int r = 0; r < gridSize; r++)
        {
            if (gridCells[r, colIndex] != null)
            {
                gridCells[r, colIndex].SetLetter(gridData[r, colIndex]);
            }
        }
    }

    private void ResetAnimationFlag(string reason = "")
    {
        isAnimating = false;
    }

    public void ReplaceLettersAt(List<Vector2Int> coordinates, bool fadeIn = false)
    {
        if (coordinates == null || coordinates.Count == 0) return;
        isAnimating = true;
        Sequence replacementSequence = DOTween.Sequence();

        foreach (var coord in coordinates)
        {
            if (coord.x >= 0 && coord.x < gridSize && coord.y >= 0 && coord.y < gridSize)
            {
                gridData[coord.x, coord.y] = GetRandomLetter();
                CellController cell = gridCells[coord.x, coord.y];
                if (cell != null)
                {
                    if (fadeIn)
                    {
                        cell.FadeOutImmediate();
                        replacementSequence.AppendCallback(() => cell.SetLetter(gridData[coord.x, coord.y]));
                        replacementSequence.AppendInterval(0.01f);
                        replacementSequence.Append(cell.GetComponent<CanvasGroup>().DOFade(1f, cellFadeInDuration * 0.5f));
                    }
                    else
                    {
                        cell.SetLetter(gridData[coord.x, coord.y]);
                    }
                }
            }
        }
        replacementSequence.OnComplete(() => ResetAnimationFlag("ReplaceLettersAt"));
    }

    public void TriggerValidationCheckAndHighlightUpdate()
    {
        if (wordValidator == null) return;
        if (gameManager != null && isAnimating && gameManager.CurrentStatePublic != GameManager.GameState.Initializing)
        {
            return;
        }
        List<FoundWordData> potentialWords = wordValidator.FindAllPotentialWords();
        Dictionary<System.Guid, Color> appliedColors = HighlightPotentialWordCells(potentialWords);
        if (gameManager != null)
        {
            gameManager.UpdatePotentialWordsDisplay(potentialWords, appliedColors);
        }
    }

    public Dictionary<System.Guid, Color> HighlightPotentialWordCells(List<FoundWordData> potentialWords)
    {
        ClearAllCellHighlights(false);
        Dictionary<System.Guid, Color> appliedColors = new Dictionary<System.Guid, Color>();
        int colorIndex = 0;

        if (potentialWords == null) return appliedColors;

        foreach (var wordData in potentialWords)
        {
            if (wordData.Coordinates == null || wordData.Coordinates.Count == 0) continue;

            Color highlightColor = (wordHighlightPalette.Length > 0) ? wordHighlightPalette[colorIndex % wordHighlightPalette.Length] : Color.yellow;
            appliedColors[wordData.ID] = highlightColor;

            foreach (var coord in wordData.Coordinates)
            {
                if (coord.x >= 0 && coord.x < gridSize && coord.y >= 0 && coord.y < gridSize)
                {
                    CellController cell = gridCells[coord.x, coord.y];
                    if (cell != null)
                    {
                        cell.SetHighlightState(true, highlightColor);
                    }
                }
            }
            colorIndex++;
        }
        return appliedColors;
    }

    public void ClearHighlightForSpecificWord(FoundWordData wordDataToClear)
    {
        if (wordDataToClear.Coordinates == null) return;
        foreach (var coord in wordDataToClear.Coordinates)
        {
            if (coord.x >= 0 && coord.x < gridSize && coord.y >= 0 && coord.y < gridSize)
            {
                CellController cell = gridCells[coord.x, coord.y];
                if (cell != null)
                {
                    cell.SetHighlightState(false, cell.GetDefaultColor());
                }
            }
        }
    }

    public void ClearAllCellHighlights(bool fullReset = true)
    {
        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                if (gridCells[r, c] != null)
                {
                    if (fullReset) gridCells[r, c].StoreDefaultColor();
                    gridCells[r, c].SetHighlightState(false, gridCells[r, c].GetDefaultColor());
                }
            }
        }
    }

    // Modified to accept a count of moves to reduce
    public void ApplyPendingMoveReduction(int row, int col, int count = 1)
    {
        if (gameManager != null && gameManager.CurrentGameDisplayMode == GameManager.DisplayMode.Moves)
        {
            for (int i = 0; i < count; i++)
            {
                gameManager.DecrementMoves();
            }
        }
    }

    public CellController GetCellController(Vector2Int coord)
    {
        if (coord.x >= 0 && coord.x < gridSize && coord.y >= 0 && coord.y < gridSize)
        {
            return gridCells[coord.x, coord.y];
        }
        return null;
    }

    public char[] GetRowData(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= gridSize)
        {
            Debug.LogError($"WGM.GetRowData: Invalid rowIndex {rowIndex}");
            return null;
        }
        char[] row = new char[gridSize];
        for (int c = 0; c < gridSize; c++)
        {
            row[c] = gridData[rowIndex, c];
        }
        return row;
    }

    public char[] GetColumnData(int colIndex)
    {
        if (colIndex < 0 || colIndex >= gridSize)
        {
            Debug.LogError($"WGM.GetColumnData: Invalid colIndex {colIndex}");
            return null;
        }
        char[] col = new char[gridSize];
        for (int r = 0; r < gridSize; r++)
        {
            col[r] = gridData[r, colIndex];
        }
        return col;
    }
}