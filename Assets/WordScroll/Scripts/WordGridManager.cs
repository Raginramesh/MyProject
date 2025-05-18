using UnityEngine;
using UnityEngine.UI; // For Image
using TMPro; // For TextMeshProUGUI
using DG.Tweening; // For DOTween
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class WordGridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int _gridSize = 4;
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
    [SerializeField] private Color cellColorAlternate = new Color(0.9f, 0.9f, 0.9f, 1f);
    [Tooltip("Color used to highlight cells that form a potentially valid word.")]
    [SerializeField] public Color potentialWordHighlightColor = Color.yellow;


    [Header("References")]
    [SerializeField] private WordValidator wordValidator; // Should be assigned in Inspector
    [SerializeField] private GameManager gameManager; // Should be assigned in Inspector or found


    public char[,] gridData { get; private set; }
    private CellController[,] gridCells; // Stores CellController components
    private LetterCell[,] gridCellComponents; // Stores LetterCell (from your original script)

    private List<char> WeightedLetters = new List<char>();
    public bool isAnimating { get; private set; } = false; // For grid animations like scroll/replace

    void Awake()
    {
        isAnimating = false;
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>(); // Find if not assigned
        if (wordValidator == null) wordValidator = FindFirstObjectByType<WordValidator>(); // Find if not assigned

        if (wordValidator == null) { Debug.LogError("WGM: WordValidator reference not set or found!", this); enabled = false; return; }
        if (gameManager == null) Debug.LogWarning("WGM: GameManager not found in Awake (may be set by GM later).", this);
    }

    void Start()
    {
        isAnimating = false; // Ensure it's false at start
        PopulateWeightedLettersList();
        // InitializeGrid(); // GameManager will call this in its StartGame
    }

    void OnEnable()
    {
        isAnimating = false; // Reset on enable
    }
    public void SetGameManager(GameManager manager) // Called by GameManager
    {
        gameManager = manager;
    }


    public void InitializeGrid()
    {
        isAnimating = false;
        if (letterCellPrefab == null) { Debug.LogError("WGM: Letter Cell Prefab missing!", this); return; }
        if (gridParent == null) { gridParent = this.transform; } // Default to this object if not set

        // Clear previous grid if any
        if (gridCells != null)
        {
            for (int r = 0; r < gridCells.GetLength(0); r++)
            {
                for (int c = 0; c < gridCells.GetLength(1); c++)
                {
                    if (gridCells[r, c] != null)
                    {
                        // Kill any tweens associated with the cell before destroying
                        DOTween.Kill(gridCells[r, c].transform); // Kills tweens on transform
                        if (gridCells[r, c].TryGetComponent<CanvasGroup>(out var cg)) { DOTween.Kill(cg); } // Kills tweens on CanvasGroup

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
        gridCellComponents = new LetterCell[gridSize, gridSize]; // Initialize this too

        PopulateGridData(); // Fill gridData with letters

        // UI Setup: Calculate start offset for centering grid
        float totalGridSizeUI = gridSize * cellSize + (gridSize - 1) * spacing;
        float startOffset = -totalGridSizeUI / 2f + cellSize / 2f;

        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                GameObject cellGO = Instantiate(letterCellPrefab, gridParent);
                CellController cellController = cellGO.GetComponent<CellController>();
                LetterCell cellComponent = cellGO.GetComponent<LetterCell>(); // Get LetterCell component
                Image cellImage = cellGO.GetComponent<Image>(); // For background color

                if (cellController == null) { Debug.LogError($"WGM: Prefab '{letterCellPrefab.name}' is missing CellController component!", cellGO); Destroy(cellGO); continue; }
                // LetterCell is optional based on your original script's usage, so null check it if needed for specific features

                RectTransform cellRect = cellController.RectTransform; // CellController should provide this
                if (cellRect == null) { Debug.LogError($"WGM: CellController on prefab '{letterCellPrefab.name}' does not have a RectTransform or it's not accessible.", cellGO); Destroy(cellGO); continue; }

                // Position and size the cell
                float posX = startOffset + c * (cellSize + spacing);
                float posY = startOffset + (gridSize - 1 - r) * (cellSize + spacing); // Y is often inverted in UI (0,0 at top-left or bottom-left)
                cellRect.anchoredPosition = new Vector2(posX, posY);
                cellRect.sizeDelta = new Vector2(cellSize, cellSize);
                cellRect.localScale = Vector3.one; // Ensure scale is reset

                cellController.SetLetter(gridData[r, c]); // Set the letter text via CellController
                cellController.SetAlpha(1f); // Make sure it's visible

                if (cellImage != null)
                {
                    cellImage.color = (r + c) % 2 == 0 ? cellColorPrimary : cellColorAlternate;
                    cellController.StoreDefaultColor(); // CellController stores its default bg color
                }

                gridCells[r, c] = cellController;
                gridCellComponents[r, c] = cellComponent; // Store LetterCell
            }
        }
    }


    void PopulateWeightedLettersList()
    {
        WeightedLetters.Clear();
        // Standard English letter distribution (Scrabble-like frequencies)
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
        if (gridData == null) { Debug.LogError("WGM: PopulateGridData called but gridData array is null!", this); return; }
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
        if (WeightedLetters == null || WeightedLetters.Count == 0)
        {
            Debug.LogWarning("WGM: WeightedLetters list is empty. Returning '?'.");
            return '?'; // Fallback
        }
        return WeightedLetters[Random.Range(0, WeightedLetters.Count)];
    }

    public void RequestRowScroll(int rowIndex, int direction, float scrollAmount)
    {
        if (!enabled || isAnimating || gameManager == null || gameManager.CurrentStatePublic != GameManager.GameState.Playing) return;
        isAnimating = true;
        ShiftRowData(rowIndex, direction);
        AnimateRowScroll(rowIndex, direction, scrollAmount); // scrollAmount not used in current animation
    }

    public void RequestColumnScroll(int colIndex, int direction, float scrollAmount)
    {
        if (!enabled || isAnimating || gameManager == null || gameManager.CurrentStatePublic != GameManager.GameState.Playing) return;
        isAnimating = true;
        ShiftColumnData(colIndex, direction);
        AnimateColumnScroll(colIndex, direction, scrollAmount); // scrollAmount not used
    }


    void ShiftRowData(int rowIndex, int direction)
    {
        if (rowIndex < 0 || rowIndex >= gridSize || gridData == null || gridCells == null) return;

        if (direction == 1) // Right
        {
            char tempData = gridData[rowIndex, gridSize - 1];
            CellController tempCellController = gridCells[rowIndex, gridSize - 1];
            LetterCell tempLetterCell = gridCellComponents?[rowIndex, gridSize - 1]; // Null-conditional
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
        else // Left (direction == -1)
        {
            char tempData = gridData[rowIndex, 0];
            CellController tempCellController = gridCells[rowIndex, 0];
            LetterCell tempLetterCell = gridCellComponents?[rowIndex, 0]; // Null-conditional
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
            LetterCell tempLetterCell = gridCellComponents?[gridSize - 1, colIndex]; // Null-conditional
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
        else // Up (direction == -1)
        {
            char tempData = gridData[0, colIndex];
            CellController tempCellController = gridCells[0, colIndex];
            LetterCell tempLetterCell = gridCellComponents?[0, colIndex]; // Null-conditional
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
            if (c < 0 || c >= gridCells.GetLength(1) || gridCells[rowIndex, c] == null || gridCells[rowIndex, c].RectTransform == null) continue;
            RectTransform cellRect = gridCells[rowIndex, c].RectTransform;
            seq.Join(cellRect.DOAnchorPosX(cellRect.anchoredPosition.x + singleCellMove, 0.3f).SetEase(Ease.OutCubic));
        }

        // Handle wrap-around cell positioning
        int wrapIndex = (direction == 1) ? 0 : gridSize - 1; // Cell that needs to jump
        if (wrapIndex >= 0 && wrapIndex < gridCells.GetLength(1) && gridCells[rowIndex, wrapIndex] != null && gridCells[rowIndex, wrapIndex].RectTransform != null)
        {
            RectTransform wrapCellRect = gridCells[rowIndex, wrapIndex].RectTransform;
            Vector2 wrapStartPos = wrapCellRect.anchoredPosition;
            // Position it off-screen just before the sequence ends, so it appears to slide in
            seq.InsertCallback(0.29f, () => { // Adjust timing if needed
                if (wrapCellRect != null) // Check again, might be destroyed
                    wrapCellRect.anchoredPosition = new Vector2(wrapStartPos.x - direction * totalMoveDistance, wrapStartPos.y);
            });
        }
        seq.OnKill(() => ResetAnimationFlag("RowScroll Killed")); // In case it's killed early
        seq.OnComplete(() => {
            try { SnapToGridPositions(); } catch (System.Exception e) { Debug.LogError($"Error in SnapToGridPositions after row scroll: {e.Message}", this); }
            ResetAnimationFlag("RowScroll Complete");
        });
    }

    void AnimateColumnScroll(int colIndex, int direction, float scrollAmount)
    {
        float totalMoveDistance = gridSize * (cellSize + spacing);
        float singleCellMove = -direction * (cellSize + spacing); // Y-axis scroll direction adjustment
        Sequence seq = DOTween.Sequence();

        if (gridCells == null || colIndex < 0 || colIndex >= gridCells.GetLength(1)) { ResetAnimationFlag("ColScroll Error"); return; }

        for (int r = 0; r < gridSize; r++)
        {
            if (r < 0 || r >= gridCells.GetLength(0) || gridCells[r, colIndex] == null || gridCells[r, colIndex].RectTransform == null) continue;
            RectTransform cellRect = gridCells[r, colIndex].RectTransform;
            seq.Join(cellRect.DOAnchorPosY(cellRect.anchoredPosition.y + singleCellMove, 0.3f).SetEase(Ease.OutCubic));
        }

        // Handle wrap-around
        int wrapIndex = (direction == 1) ? 0 : gridSize - 1; // If going "down" (dir=1), top cell (index 0) wraps to bottom
        if (wrapIndex >= 0 && wrapIndex < gridCells.GetLength(0) && gridCells[wrapIndex, colIndex] != null && gridCells[wrapIndex, colIndex].RectTransform != null)
        {
            RectTransform wrapCellRect = gridCells[wrapIndex, colIndex].RectTransform;
            Vector2 wrapStartPos = wrapCellRect.anchoredPosition;
            seq.InsertCallback(0.29f, () => {
                if (wrapCellRect != null)
                    wrapCellRect.anchoredPosition = new Vector2(wrapStartPos.x, wrapStartPos.y + direction * totalMoveDistance);
            });
        }
        seq.OnKill(() => ResetAnimationFlag("ColScroll Killed"));
        seq.OnComplete(() => {
            try { SnapToGridPositions(); } catch (System.Exception e) { Debug.LogError($"Error in SnapToGridPositions after col scroll: {e.Message}", this); }
            ResetAnimationFlag("ColScroll Complete");
        });
    }

    private void ResetAnimationFlag(string reason)
    {
        if (isAnimating)
        {
            isAnimating = false;
            // Debug.Log($"WGM ResetAnimationFlag: {reason}");
        }
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
                if (gridCells[r, c] != null && gridCells[r, c].RectTransform != null)
                {
                    float targetX = startOffset + c * (cellSize + spacing);
                    float targetY = startOffset + (gridSize - 1 - r) * (cellSize + spacing);
                    gridCells[r, c].RectTransform.anchoredPosition = new Vector2(targetX, targetY);
                }
            }
        }
    }

    public void ReplaceLettersAt(List<Vector2Int> coordinates, bool fadeIn = false)
    {
        if (coordinates == null || coordinates.Count == 0 || gridData == null)
        {
            ResetAnimationFlag("ReplaceLettersAt - No Coords/Data");
            return;
        }
        isAnimating = true;
        Sequence replacementSequence = DOTween.Sequence();
        float maxFadeDuration = 0f;

        foreach (Vector2Int coord in coordinates)
        {
            if (coord.x >= 0 && coord.x < gridSize && coord.y >= 0 && coord.y < gridSize)
            {
                char newLetter = GetRandomLetter();
                gridData[coord.x, coord.y] = newLetter; // Update data model

                CellController cellController = GetCellController(coord);
                if (cellController != null)
                {
                    if (!cellController.gameObject.activeSelf)
                    {
                        // This case should ideally not happen if cells are just having letters replaced
                        // But if they were deactivated, ensure they are active for the new letter
                        cellController.gameObject.SetActive(true);
                        Debug.LogWarning($"WGM.ReplaceLettersAt: Cell {coord} was inactive. Reactivated.");
                    }
                    cellController.SetLetter(newLetter); // Update visual letter

                    if (fadeIn)
                    {
                        CanvasGroup cg = cellController.GetComponent<CanvasGroup>();
                        if (cg == null) cg = cellController.gameObject.AddComponent<CanvasGroup>(); // Ensure CanvasGroup

                        cg.alpha = 0f; // Start transparent
                        cellController.RectTransform.localScale = Vector3.one * 0.8f; // Initial smaller scale for pop

                        // Create a sequence for this cell's pop-in
                        Sequence cellPopInSequence = DOTween.Sequence();
                        cellPopInSequence.Append(cg.DOFade(1f, cellFadeInDuration));
                        cellPopInSequence.Join(cellController.RectTransform.DOScale(Vector3.one, cellFadeInDuration).SetEase(Ease.OutBack));

                        replacementSequence.Join(cellPopInSequence); // Join to master sequence for parallel execution

                        if (cellFadeInDuration > maxFadeDuration) maxFadeDuration = cellFadeInDuration;
                    }
                    else
                    {
                        cellController.SetAlpha(1f); // Ensure fully visible
                        cellController.RectTransform.localScale = Vector3.one; // Reset scale
                    }
                }
            }
        }

        if (fadeIn && coordinates.Count > 0 && maxFadeDuration > 0)
        {
            replacementSequence.OnComplete(() => ResetAnimationFlag("LetterReplacement Complete"));
            replacementSequence.Play();
        }
        else
        {
            ResetAnimationFlag("LetterReplacement Immediate (No Fade/No Coords)");
        }
    }

    public void TriggerValidationCheckAndHighlightUpdate()
    {
        if (gameManager == null || wordValidator == null)
        {
            Debug.LogError("WGM: Missing GameManager or WordValidator reference for Validation/Highlight!", this);
            return;
        }
        // Allow validation even if GM is animating, as it might be a different type of animation (e.g. effects)
        // GM's IsAnyAnimationPlaying flag will gate input, this just updates visuals if possible.
        // if (gameManager.IsAnyAnimationPlaying && gameManager.CurrentStatePublic == GameManager.GameState.Playing)
        // {
        //     Debug.Log("WGM: GM is animating, delaying validation check.");
        //     return;
        // }
        List<FoundWordData> potentialWords = wordValidator.FindAllPotentialWords();
        gameManager.UpdatePotentialWordsDisplay(potentialWords); // GM handles displaying these
    }


    public void HighlightPotentialWordCells(List<FoundWordData> potentialWords)
    {
        if (gridCells == null) return;
        ClearAllCellHighlights(false); // Clear previous highlights first (false = don't tell GM to clear its list)

        if (potentialWords == null) return;

        foreach (FoundWordData wordData in potentialWords)
        {
            foreach (Vector2Int coord in wordData.Coordinates)
            {
                CellController cell = GetCellController(coord);
                if (cell != null && cell.gameObject.activeSelf)
                {
                    cell.SetHighlightState(true, potentialWordHighlightColor);
                }
            }
        }
    }

    public void ClearHighlightForSpecificWord(FoundWordData wordDataToClear)
    {
        if (gridCells == null || wordDataToClear.Coordinates == null || gameManager == null) return;

        foreach (Vector2Int coord in wordDataToClear.Coordinates)
        {
            CellController cell = GetCellController(coord);
            if (cell != null && cell.gameObject.activeSelf)
            {
                bool partOfOtherPotentialWordInCurrentSequence = false;
                List<FoundWordData> currentGMWords = gameManager.GetCurrentPotentialWords();

                foreach (var potentialWord in currentGMWords)
                {
                    // Skip if it's the word we are currently clearing OR if it's another word that GM is also about to process in this same sequence
                    if (potentialWord.ID == wordDataToClear.ID || gameManager.IsWordInCurrentProcessingSequence(potentialWord.ID))
                    {
                        if (potentialWord.ID != wordDataToClear.ID && potentialWord.Coordinates.Contains(coord)) // It's another word in the sequence sharing this cell
                        {
                            // If the other word is also in the current processing sequence, the cell will be "accounted for".
                            // The highlight can be removed now. If this logic is too aggressive (e.g. shared cell goes dark too soon),
                            // this is where it would be adjusted.
                        }
                        continue;
                    }
                    // If it's a different potential word (not in the current processing sequence) that shares this cell
                    if (potentialWord.Coordinates.Contains(coord))
                    {
                        partOfOtherPotentialWordInCurrentSequence = true;
                        break;
                    }
                }

                if (!partOfOtherPotentialWordInCurrentSequence)
                {
                    cell.SetHighlightState(false, cell.GetDefaultColor());
                }
                // else: Debug.Log($"WGM: Cell {coord} for '{wordDataToClear.Word}' is part of another word not in current sequence. Highlight retained.");
            }
        }
    }

    public void ClearAllCellHighlights(bool fullReset = true) // fullReset tells GM to clear its list
    {
        if (gridCells == null) return;
        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                if (gridCells[r, c] != null && gridCells[r, c].gameObject.activeSelf)
                {
                    gridCells[r, c].SetHighlightState(false, gridCells[r, c].GetDefaultColor());
                }
            }
        }
        if (fullReset && gameManager != null)
        {
            gameManager.ClearPotentialWords(); // Tell GM to clear its list of potential words
        }
    }

    public void ApplyPendingMoveReduction(int row, int col)
    {
        // This method seems related to a different mechanic (LetterCell moves)
        // from your original script. Keeping it as is.
        if ((row < 0 && col < 0) || (row >= 0 && col >= 0)) return; // Invalid if both or neither are specified
        if (gameManager != null) gameManager.DecrementMoves();

        bool wasRowScroll = row >= 0;
        if (gridCellComponents != null) // Check if this array is used/initialized
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

    public LetterCell GetLetterCellAt(int row, int col) // From your original script
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