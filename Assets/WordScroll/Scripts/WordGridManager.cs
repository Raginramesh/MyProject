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

    [Header("Highlighting")]
    [Tooltip("Colors to cycle through for highlighting different potential words. Define at least one.")]
    [SerializeField]
    private Color[] wordHighlightPalette = new Color[] {
        new Color(1f, 1f, 0.6f, 0.75f),
        new Color(0.6f, 1f, 1f, 0.75f),
        new Color(1f, 0.6f, 1f, 0.75f),
        new Color(0.6f, 1f, 0.6f, 0.75f),
        new Color(1f, 0.8f, 0.6f, 0.75f)
    };


    [Header("References")]
    [SerializeField] private WordValidator wordValidator;
    [SerializeField] private GameManager gameManager;


    public char[,] gridData { get; private set; }
    private CellController[,] gridCells;
    private LetterCell[,] gridCellComponents;

    private List<char> WeightedLetters = new List<char>();
    public bool isAnimating { get; private set; } = false;

    void Awake()
    {
        isAnimating = false;
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        if (wordValidator == null) wordValidator = FindFirstObjectByType<WordValidator>();

        if (wordValidator == null) { Debug.LogError("WGM: WordValidator reference not set or found!", this); enabled = false; return; }
        if (gameManager == null) Debug.LogWarning("WGM: GameManager not found in Awake (may be set by GM later).", this);

        if (wordHighlightPalette == null || wordHighlightPalette.Length == 0)
        {
            wordHighlightPalette = new Color[] { Color.white };
        }
    }

    void Start()
    {
        isAnimating = false;
        PopulateWeightedLettersList();
    }

    void OnEnable()
    {
        isAnimating = false;
    }
    public void SetGameManager(GameManager manager)
    {
        gameManager = manager;
    }


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
        gridCellComponents = new LetterCell[gridSize, gridSize];

        PopulateGridData();

        float totalGridSizeUI = gridSize * cellSize + (gridSize - 1) * spacing;
        float startOffset = -totalGridSizeUI / 2f + cellSize / 2f;

        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                GameObject cellGO = Instantiate(letterCellPrefab, gridParent);
                CellController cellController = cellGO.GetComponent<CellController>();
                LetterCell cellComponent = cellGO.GetComponent<LetterCell>();
                Image cellImage = cellGO.GetComponent<Image>();

                if (cellController == null) { Debug.LogError($"WGM: Prefab '{letterCellPrefab.name}' is missing CellController component!", cellGO); Destroy(cellGO); continue; }

                RectTransform cellRect = cellController.RectTransform;
                if (cellRect == null) { Debug.LogError($"WGM: CellController on prefab '{letterCellPrefab.name}' does not have a RectTransform or it's not accessible.", cellGO); Destroy(cellGO); continue; }

                float posX = startOffset + c * (cellSize + spacing);
                float posY = startOffset + (gridSize - 1 - r) * (cellSize + spacing); // Adjusted for Y-down in typical UI
                cellRect.anchoredPosition = new Vector2(posX, posY);
                cellRect.sizeDelta = new Vector2(cellSize, cellSize);
                cellRect.localScale = Vector3.one;

                cellController.SetLetter(gridData[r, c]);
                cellController.SetAlpha(1f);

                if (cellImage != null)
                {
                    cellImage.color = (r + c) % 2 == 0 ? cellColorPrimary : cellColorAlternate;
                    cellController.StoreDefaultColor();
                }

                gridCells[r, c] = cellController;
                gridCellComponents[r, c] = cellComponent;
            }
        }
        // Initial validation after grid setup
        TriggerValidationCheckAndHighlightUpdate();
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
            // Fallback if list is empty, though PopulateWeightedLettersList should prevent this
            Debug.LogWarning("WGM: WeightedLetters list is empty. Returning '?'.");
            return '?';
        }
        return WeightedLetters[UnityEngine.Random.Range(0, WeightedLetters.Count)];
    }

    // MODIFIED: Now returns Sequence
    public Sequence RequestRowScroll(int rowIndex, int direction, float scrollAmount)
    {
        if (!enabled || isAnimating || gameManager == null || gameManager.CurrentStatePublic != GameManager.GameState.Playing) return null;
        isAnimating = true; // Set animating flag at the beginning of the operation
        ShiftRowData(rowIndex, direction);
        return AnimateRowScroll(rowIndex, direction, scrollAmount); // Return the sequence
    }

    // MODIFIED: Now returns Sequence
    public Sequence RequestColumnScroll(int colIndex, int direction, float scrollAmount)
    {
        if (!enabled || isAnimating || gameManager == null || gameManager.CurrentStatePublic != GameManager.GameState.Playing) return null;
        isAnimating = true; // Set animating flag
        ShiftColumnData(colIndex, direction);
        return AnimateColumnScroll(colIndex, direction, scrollAmount); // Return the sequence
    }


    void ShiftRowData(int rowIndex, int direction)
    {
        if (rowIndex < 0 || rowIndex >= gridSize || gridData == null || gridCells == null) return;

        if (direction == 1) // Right
        {
            char tempData = gridData[rowIndex, gridSize - 1];
            CellController tempCellController = gridCells[rowIndex, gridSize - 1];
            LetterCell tempLetterCell = gridCellComponents?[rowIndex, gridSize - 1]; // Null-conditional for safety
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

        if (direction == 1) // Down (data shifts "down", cells appear to move "up")
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
        else // Up (data shifts "up", cells appear to move "down")
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

    // MODIFIED: Returns Sequence, removed TriggerValidationCheckAndHighlightUpdate from OnComplete
    Sequence AnimateRowScroll(int rowIndex, int direction, float scrollAmount)
    {
        float totalMoveDistance = gridSize * (cellSize + spacing);
        float singleCellMove = direction * (cellSize + spacing);
        Sequence seq = DOTween.Sequence();

        if (gridCells == null || rowIndex < 0 || rowIndex >= gridCells.GetLength(0))
        {
            ResetAnimationFlag("RowScroll Error");
            return null;
        }

        for (int c = 0; c < gridSize; c++)
        {
            if (c < 0 || c >= gridCells.GetLength(1) || gridCells[rowIndex, c] == null || gridCells[rowIndex, c].RectTransform == null) continue;
            RectTransform cellRect = gridCells[rowIndex, c].RectTransform;
            seq.Join(cellRect.DOAnchorPosX(cellRect.anchoredPosition.x + singleCellMove, 0.3f).SetEase(Ease.OutCubic));
        }

        int wrapIndex = (direction == 1) ? 0 : gridSize - 1;
        if (wrapIndex >= 0 && wrapIndex < gridCells.GetLength(1) && gridCells[rowIndex, wrapIndex] != null && gridCells[rowIndex, wrapIndex].RectTransform != null)
        {
            RectTransform wrapCellRect = gridCells[rowIndex, wrapIndex].RectTransform;
            Vector2 wrapStartPos = wrapCellRect.anchoredPosition;
            // Position the wrapping cell off-screen just before the sequence makes it visible
            seq.InsertCallback(0.01f, () => { // Do this early in the sequence
                if (wrapCellRect != null) // Check might be redundant if check above passed, but good for safety
                    wrapCellRect.anchoredPosition = new Vector2(wrapStartPos.x - direction * totalMoveDistance, wrapStartPos.y);
            });
        }
        seq.OnKill(() => ResetAnimationFlag("RowScroll Killed"));
        seq.OnComplete(() => {
            try { SnapToGridPositions(); } catch (System.Exception e) { Debug.LogError($"Error in SnapToGridPositions after row scroll: {e.Message}", this); }
            ResetAnimationFlag("RowScroll Complete");
            // REMOVED: TriggerValidationCheckAndHighlightUpdate(); 
        });
        return seq;
    }

    // MODIFIED: Returns Sequence, removed TriggerValidationCheckAndHighlightUpdate from OnComplete
    Sequence AnimateColumnScroll(int colIndex, int direction, float scrollAmount)
    {
        float totalMoveDistance = gridSize * (cellSize + spacing);
        // For column scroll, positive 'direction' (e.g., 1 for "down" in data) means cells move visually "up" (positive Y in UI usually)
        // So, if direction is 1 (data down), singleCellMove is positive Y.
        // If direction is -1 (data up), singleCellMove is negative Y.
        float singleCellMove = -direction * (cellSize + spacing); // Corrected: Visual Y moves are inverted from data shift "direction"
        Sequence seq = DOTween.Sequence();

        if (gridCells == null || colIndex < 0 || colIndex >= gridCells.GetLength(1))
        {
            ResetAnimationFlag("ColScroll Error");
            return null;
        }

        for (int r = 0; r < gridSize; r++)
        {
            if (r < 0 || r >= gridCells.GetLength(0) || gridCells[r, colIndex] == null || gridCells[r, colIndex].RectTransform == null) continue;
            RectTransform cellRect = gridCells[r, colIndex].RectTransform;
            seq.Join(cellRect.DOAnchorPosY(cellRect.anchoredPosition.y + singleCellMove, 0.3f).SetEase(Ease.OutCubic));
        }

        // 'direction' 1 means data shifted "down", so the cell from the "bottom" (gridSize-1) wraps to the "top" (0)
        // Visually, this cell needs to be repositioned from its current (bottom) to above the top.
        int wrapIndex = (direction == 1) ? 0 : gridSize - 1;
        if (wrapIndex >= 0 && wrapIndex < gridCells.GetLength(0) && gridCells[wrapIndex, colIndex] != null && gridCells[wrapIndex, colIndex].RectTransform != null)
        {
            RectTransform wrapCellRect = gridCells[wrapIndex, colIndex].RectTransform;
            Vector2 wrapStartPos = wrapCellRect.anchoredPosition;
            seq.InsertCallback(0.01f, () => { // Early in sequence
                if (wrapCellRect != null)
                    // If direction = 1 (data down, visual up), cell from bottom (GS-1, now at 0) moves to visually top. Its start Y was low, needs to jump to high Y.
                    // totalMoveDistance needs to be ADDED to its Y if it's moving visually upwards from bottom.
                    // The 'direction' in ShiftColumnData (1 for data down) corresponds to visual movement 'up' (-singleCellMove).
                    // So, if direction is 1, the cell from bottom (GS-1) is now at index 0. It needs to jump from its original low Y to a high Y.
                    // The visual movement direction for the wrap is -direction.
                    wrapCellRect.anchoredPosition = new Vector2(wrapStartPos.x, wrapStartPos.y + direction * totalMoveDistance);
            });
        }
        seq.OnKill(() => ResetAnimationFlag("ColScroll Killed"));
        seq.OnComplete(() => {
            try { SnapToGridPositions(); } catch (System.Exception e) { Debug.LogError($"Error in SnapToGridPositions after col scroll: {e.Message}", this); }
            ResetAnimationFlag("ColScroll Complete");
            // REMOVED: TriggerValidationCheckAndHighlightUpdate();
        });
        return seq;
    }

    private void ResetAnimationFlag(string reason)
    {
        // Debug.Log($"WGM ResetAnimationFlag: {reason}. Current isAnimating: {isAnimating}");
        if (isAnimating)
        {
            isAnimating = false;
        }
    }


    void SnapToGridPositions()
    {
        // Debug.Log("WGM: Snapping to grid positions.");
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
                    float targetY = startOffset + (gridSize - 1 - r) * (cellSize + spacing); // Y-down
                    gridCells[r, c].RectTransform.anchoredPosition = new Vector2(targetX, targetY);
                    if (gridData != null && r < gridData.GetLength(0) && c < gridData.GetLength(1))
                    {
                        // Ensure letter is updated after data shift and snap
                        gridCells[r, c].SetLetter(gridData[r, c]);
                    }
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
                gridData[coord.x, coord.y] = newLetter;

                CellController cellController = GetCellController(coord);
                if (cellController != null)
                {
                    cellController.SetHighlightState(false, cellController.GetDefaultColor());

                    if (!cellController.gameObject.activeSelf)
                    {
                        cellController.gameObject.SetActive(true);
                    }
                    cellController.SetLetter(newLetter);

                    if (fadeIn)
                    {
                        CanvasGroup cg = cellController.GetComponent<CanvasGroup>();
                        if (cg == null) cg = cellController.gameObject.AddComponent<CanvasGroup>();

                        cg.alpha = 0f;
                        cellController.RectTransform.localScale = Vector3.one * 0.8f;

                        Sequence cellPopInSequence = DOTween.Sequence();
                        cellPopInSequence.Append(cg.DOFade(1f, cellFadeInDuration));
                        cellPopInSequence.Join(cellController.RectTransform.DOScale(Vector3.one, cellFadeInDuration).SetEase(Ease.OutBack));

                        replacementSequence.Join(cellPopInSequence);

                        if (cellFadeInDuration > maxFadeDuration) maxFadeDuration = cellFadeInDuration;
                    }
                    else
                    {
                        cellController.SetAlpha(1f);
                        cellController.RectTransform.localScale = Vector3.one;
                    }
                }
            }
        }

        if (fadeIn && coordinates.Count > 0 && maxFadeDuration > 0)
        {
            replacementSequence.OnComplete(() => {
                ResetAnimationFlag("LetterReplacement Complete");
                TriggerValidationCheckAndHighlightUpdate(); // Validate after replacement animation
            });
            replacementSequence.Play();
        }
        else // No fade, or no coordinates to fade.
        {
            ResetAnimationFlag("LetterReplacement Immediate");
            if (coordinates.Count > 0) // Only trigger if actual replacements happened
            {
                TriggerValidationCheckAndHighlightUpdate(); // Validate immediately
            }
        }
    }

    public void TriggerValidationCheckAndHighlightUpdate()
    {
        // Debug.Log($"WGM: Attempting TriggerValidationCheckAndHighlightUpdate. GM animating: {gameManager?.IsAnyAnimationPlaying}, WGM animating: {this.isAnimating}");
        if (gameManager == null || wordValidator == null || gameManager.CurrentStatePublic != GameManager.GameState.Playing)
        {
            return;
        }
        // IMPORTANT: Check if GM is busy with its own animations (like word processing)
        // OR if WGM itself is busy (e.g. from ReplaceLettersAt, or a scroll animation not yet fully reset)
        if (gameManager.IsAnyAnimationPlaying || this.isAnimating)
        {
            // Debug.Log("WGM.TriggerValidation: Suppressed due to ongoing animation elsewhere or WGM itself animating.");
            return;
        }

        // Debug.Log($"WGM.TriggerValidation: Proceeding at {Time.time}");
        List<FoundWordData> potentialWords = wordValidator.FindAllPotentialWords();
        Dictionary<System.Guid, Color> appliedHighlightColors = HighlightPotentialWordCells(potentialWords);
        gameManager.UpdatePotentialWordsDisplay(potentialWords, appliedHighlightColors);
    }

    public Dictionary<System.Guid, Color> HighlightPotentialWordCells(List<FoundWordData> potentialWords)
    {
        Dictionary<System.Guid, Color> appliedColors = new Dictionary<System.Guid, Color>();
        if (gridCells == null) return appliedColors;

        ClearAllCellHighlights(false); // false = don't clear GM's potential word list yet

        if (potentialWords == null || potentialWords.Count == 0 || wordHighlightPalette == null || wordHighlightPalette.Length == 0)
        {
            return appliedColors;
        }

        for (int i = 0; i < potentialWords.Count; i++)
        {
            FoundWordData wordData = potentialWords[i];
            Color wordSpecificHighlightColor = wordHighlightPalette[i % wordHighlightPalette.Length];

            appliedColors[wordData.ID] = wordSpecificHighlightColor;

            if (wordData.Coordinates == null) continue;

            foreach (Vector2Int coord in wordData.Coordinates)
            {
                CellController cell = GetCellController(coord);
                if (cell != null && cell.gameObject.activeSelf)
                {
                    cell.SetHighlightState(true, wordSpecificHighlightColor);
                }
            }
        }
        return appliedColors;
    }

    public void ClearHighlightForSpecificWord(FoundWordData wordDataToClear)
    {
        if (gridCells == null || wordDataToClear.Coordinates == null || gameManager == null) return;

        List<FoundWordData> remainingPotentialWordsFromGM = gameManager.GetCurrentPotentialWords();
        Dictionary<System.Guid, Color> currentHighlightColors = gameManager.GetCurrentAppliedHighlightColors();

        foreach (Vector2Int coord in wordDataToClear.Coordinates)
        {
            CellController cell = GetCellController(coord);
            if (cell != null && cell.gameObject.activeSelf)
            {
                bool stillNeedsHighlightByAnotherWord = false;

                // Check if this cell is part of any *other* remaining potential word
                for (int i = 0; i < remainingPotentialWordsFromGM.Count; i++)
                {
                    var otherWord = remainingPotentialWordsFromGM[i];
                    if (otherWord.ID == wordDataToClear.ID) continue; // Skip the word we are clearing

                    if (otherWord.Coordinates.Contains(coord))
                    {
                        // This cell is part of another word, re-apply that word's highlight
                        if (currentHighlightColors.TryGetValue(otherWord.ID, out Color otherWordSpecificHighlightColor))
                        {
                            cell.SetHighlightState(true, otherWordSpecificHighlightColor);
                        }
                        else // Should not happen if data is consistent
                        {
                            // Fallback: if somehow color is missing, just ensure it's not using the cleared word's color
                            cell.SetHighlightState(false, cell.GetDefaultColor());
                            // Or re-evaluate highlight from palette:
                            // int otherWordIndex = remainingPotentialWordsFromGM.IndexOf(otherWord);
                            // Color newHighlight = wordHighlightPalette[otherWordIndex % wordHighlightPalette.Length];
                            // cell.SetHighlightState(true, newHighlight);
                        }
                        stillNeedsHighlightByAnotherWord = true;
                        break;
                    }
                }

                if (!stillNeedsHighlightByAnotherWord)
                {
                    // This cell is not part of any other potential word, so clear its highlight
                    cell.SetHighlightState(false, cell.GetDefaultColor());
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
                if (gridCells[r, c] != null && gridCells[r, c].gameObject.activeSelf)
                {
                    gridCells[r, c].SetHighlightState(false, gridCells[r, c].GetDefaultColor());
                }
            }
        }
        if (fullReset && gameManager != null) // If fullReset, also clear the list in GameManager
        {
            gameManager.ClearPotentialWords();
        }
    }

    public void ApplyPendingMoveReduction(int row, int col)
    {
        if ((row < 0 && col < 0) || (row >= 0 && col >= 0)) return; // Only one should be valid
        if (gameManager != null) gameManager.DecrementMoves();

        bool wasRowScroll = row >= 0;
        if (gridCellComponents != null)
        {
            if (wasRowScroll)
            {
                for (int c = 0; c < gridSize; c++)
                {
                    LetterCell cellComp = GetLetterCellAt(row, c);
                    if (cellComp != null && cellComp.EnableMoves) cellComp.ReduceMove();
                }
            }
            else // Was column scroll
            {
                for (int r = 0; r < gridSize; r++)
                {
                    LetterCell cellComp = GetLetterCellAt(r, col);
                    if (cellComp != null && cellComp.EnableMoves) cellComp.ReduceMove();
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