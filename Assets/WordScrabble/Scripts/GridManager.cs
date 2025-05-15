using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class GridManager : MonoBehaviour
{
    [Header("Grid Structure Configuration")]
    [Tooltip("Desired number of columns. Will be adjusted to an odd number if 'Enforce Odd Dimensions' is true.")]
    [SerializeField] private int desiredGridWidth = 15;
    [Tooltip("Desired number of rows. Will be adjusted to an odd number if 'Enforce Odd Dimensions' is true.")]
    [SerializeField] private int desiredGridHeight = 15;
    [Tooltip("Desired size (width, height) of each individual cell.")]
    [SerializeField] private Vector2 desiredCellSize = new Vector2(70, 70);
    [Tooltip("Desired spacing (horizontal, vertical) between cells.")]
    [SerializeField] private Vector2 desiredCellSpacing = new Vector2(5, 5);
    [Tooltip("If true, width and height will be incremented to the next odd number if an even number is entered.")]
    [SerializeField] private bool enforceOddDimensions = true;

    // --- Added Cell Color Fields ---
    [Header("Cell Colors")]
    [Tooltip("Primary color for the grid cells (e.g., for even cells in checkerboard).")]
    [SerializeField] private Color primaryCellColor = Color.white; // Example: white
    [Tooltip("Secondary color for alternating grid cells (e.g., for odd cells in checkerboard).")]
    [SerializeField] private Color secondaryCellColor = new Color(0.9f, 0.9f, 0.9f, 1f); // Example: light gray
    // --- End Added Cell Color Fields ---
    [Tooltip("Color for the automatically designated center tile of the grid.")]
    [SerializeField] private Color centerTileColor = Color.cyan;


    [Header("References")]
    [SerializeField] private RectTransform gridPanelParent;
    [SerializeField] private GameObject tilePrefab;

    [Header("Debug")]
    [SerializeField] private bool logCoordinateConversion = false;
    [SerializeField] private Vector2 coordinatePixelOffsetCorrection = Vector2.zero;

    private TileData[,] gridData;
    private GridLayoutGroup gridLayoutGroup;
    private List<Vector2Int> _currentPreviewCoords = new List<Vector2Int>();

    private int runtimeGridWidth;
    private int runtimeGridHeight;

    public int CurrentGridWidth => runtimeGridWidth;
    public int CurrentGridHeight => runtimeGridHeight;

    void Awake()
    {
        if (gridPanelParent == null)
        {
            Debug.LogError("GridManager: gridPanelParent is not assigned!", this);
            return;
        }
        gridLayoutGroup = gridPanelParent.GetComponent<GridLayoutGroup>();
        if (gridLayoutGroup == null)
        {
            Debug.LogError("GridManager: gridPanelParent is MISSING a GridLayoutGroup component!", this);
        }
        CreateGrid(); // Create the grid using Inspector settings on Awake
    }

    public void CreateGrid()
    {
        // Use a local copy for potential modification if enforcing odd dimensions
        int targetWidth = desiredGridWidth;
        int targetHeight = desiredGridHeight;

        if (enforceOddDimensions)
        {
            if (targetWidth % 2 == 0)
            {
                targetWidth++;
                Debug.Log($"GridManager: Adjusted desiredGridWidth from {desiredGridWidth} to {targetWidth} to be an odd number.");
            }
            if (targetHeight % 2 == 0)
            {
                targetHeight++;
                Debug.Log($"GridManager: Adjusted desiredGridHeight from {desiredGridHeight} to {targetHeight} to be an odd number.");
            }
        }
        CreateGrid(targetWidth, targetHeight); // Call the main creation logic
    }

    public void CreateGrid(int width, int height)
    {
        this.runtimeGridWidth = width;
        this.runtimeGridHeight = height;

        if (gridPanelParent == null) { Debug.LogError("GridManager.CreateGrid: gridPanelParent is null!", this); return; }
        if (tilePrefab == null) { Debug.LogError("GridManager.CreateGrid: tilePrefab is not assigned!", this); return; }
        if (gridLayoutGroup == null) { Debug.LogError("GridManager.CreateGrid: GridLayoutGroup on gridPanelParent is null.", this); return; }

        Debug.Log($"GridManager: Starting grid creation. Final WxH: {runtimeGridWidth}x{runtimeGridHeight}, CellSize: {desiredCellSize}, Spacing: {desiredCellSpacing}");

        gridLayoutGroup.cellSize = desiredCellSize;
        gridLayoutGroup.spacing = desiredCellSpacing;

        if (gridLayoutGroup.startAxis == GridLayoutGroup.Axis.Horizontal && gridLayoutGroup.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
        {
            gridLayoutGroup.constraintCount = this.runtimeGridWidth;
        }
        else if (gridLayoutGroup.startAxis == GridLayoutGroup.Axis.Vertical && gridLayoutGroup.constraint == GridLayoutGroup.Constraint.FixedRowCount)
        {
            gridLayoutGroup.constraintCount = this.runtimeGridHeight;
        }

        foreach (Transform child in gridPanelParent) { Destroy(child.gameObject); }
        _currentPreviewCoords.Clear(); // Preserved this line
        gridData = new TileData[this.runtimeGridWidth, this.runtimeGridHeight];

        // --- Modified Tile Instantiation Loop ---
        for (int y = 0; y < this.runtimeGridHeight; y++)
        {
            for (int x = 0; x < this.runtimeGridWidth; x++)
            {
                GameObject tileInstance = Instantiate(tilePrefab, gridPanelParent);
                tileInstance.name = $"Tile_{x}_{y}";

                // Determine the cell's default color for checkerboard pattern
                Color defaultCellColorForTile = (x + y) % 2 == 0 ? primaryCellColor : secondaryCellColor;

                // Pass the defaultCellColorForTile to the TileData constructor
                // This assumes your TileData constructor is: TileData(Vector2Int coords, GameObject visualInstance, Color defaultCellColor)
                gridData[x, y] = new TileData(new Vector2Int(x, y), tileInstance, defaultCellColorForTile);
            }
        }
        // --- End Modified Tile Instantiation Loop ---

        // Designate and color the center tile (This logic remains the same and is correct)
        if (this.runtimeGridWidth > 0 && this.runtimeGridHeight > 0)
        {
            Vector2Int centerCoordinate = new Vector2Int(this.runtimeGridWidth / 2, this.runtimeGridHeight / 2);
            TileData centerTile = GetTileData(centerCoordinate);
            if (centerTile != null)
            {
                centerTile.SetAsDesignatedCenterTile(centerTileColor); // TileData.UpdateVisuals will use this
                Debug.Log($"GridManager: Center tile at {centerCoordinate} designated with color {centerTileColor}.");
            }
            else { Debug.LogWarning($"GridManager: Could not get TileData for center coordinate {centerCoordinate} to designate it."); }
        }
        else { Debug.LogWarning("GridManager: Grid dimensions are zero, cannot designate center tile."); }

        Debug.Log($"GridManager: Grid creation complete ({runtimeGridWidth}x{runtimeGridHeight}).");
    }

    // --- All methods below this line are preserved from your original script ---
    public void ClearAndResetGridVisuals()
    {
        if (gridData == null) return;
        _currentPreviewCoords.Clear();
        for (int y = 0; y < runtimeGridHeight; y++)
        {
            for (int x = 0; x < runtimeGridWidth; x++)
            {
                gridData[x, y]?.ResetTile(); // ResetTile in TileData will now preserve center color if set
            }
        }
        Debug.Log("GridManager: Grid visuals cleared and reset.");
    }

    public TileData GetTileData(Vector2Int coordinate)
    {
        if (coordinate.x >= 0 && coordinate.x < runtimeGridWidth &&
            coordinate.y >= 0 && coordinate.y < runtimeGridHeight)
        {
            return gridData[coordinate.x, coordinate.y];
        }
        return null;
    }

    public bool TrySetLetter(Vector2Int coordinate, char letter)
    {
        TileData tileData = GetTileData(coordinate);
        if (tileData != null)
        {
            if (!tileData.IsOccupied || tileData.Letter == char.ToUpper(letter))
            {
                tileData.SetPlacedLetter(char.ToUpper(letter));
                return true;
            }
            else
            {
                Debug.LogWarning($"GridManager.TrySetLetter: CANNOT set letter '{letter}' at {coordinate}. Tile already occupied with DIFFERENT letter '{tileData.Letter}'.");
                return false;
            }
        }
        else
        {
            Debug.LogWarning($"GridManager.TrySetLetter: CANNOT set letter '{letter}' at {coordinate}. Invalid coordinate (TileData is NULL).");
            return false;
        }
    }

    public void ShowWordPreview(List<Vector2Int> wordCoords, string word, bool isValidPlacement)
    {
        if (wordCoords == null) wordCoords = new List<Vector2Int>();
        List<Vector2Int> coordsToClearPreview = _currentPreviewCoords.Except(wordCoords).ToList();
        foreach (Vector2Int coord in coordsToClearPreview)
        {
            GetTileData(coord)?.ClearPreviewState();
        }
        for (int i = 0; i < wordCoords.Count; i++)
        {
            char letterToShow = (i < word.Length) ? word[i] : '?';
            GetTileData(wordCoords[i])?.SetPreviewState(letterToShow, isValidPlacement);
        }
        _currentPreviewCoords = new List<Vector2Int>(wordCoords);
    }

    public void ClearWordPreview()
    {
        foreach (Vector2Int coord in _currentPreviewCoords)
        {
            GetTileData(coord)?.ClearPreviewState();
        }
        _currentPreviewCoords.Clear();
    }

    public bool ScreenPointToGridCoords(Vector2 screenPoint, Camera uiCamera, out Vector2Int gridCoords)
    {
        gridCoords = Vector2Int.zero;
        if (gridPanelParent == null || gridLayoutGroup == null)
        {
            if (logCoordinateConversion) Debug.LogError("GridManager.ScreenPointToGridCoords: gridPanelParent or gridLayoutGroup is null.");
            return false;
        }

        Vector2 rawLocalPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(gridPanelParent, screenPoint, uiCamera, out rawLocalPoint))
        {
            float xFromPanelLeft = rawLocalPoint.x + gridPanelParent.pivot.x * gridPanelParent.rect.width;
            float yFromPanelTop = (1.0f - gridPanelParent.pivot.y) * gridPanelParent.rect.height - rawLocalPoint.y;
            float contentX = xFromPanelLeft - gridLayoutGroup.padding.left - coordinatePixelOffsetCorrection.x;
            float contentY = yFromPanelTop - gridLayoutGroup.padding.top - coordinatePixelOffsetCorrection.y;

            float cellWidthWithSpacing = gridLayoutGroup.cellSize.x + gridLayoutGroup.spacing.x;
            float cellHeightWithSpacing = gridLayoutGroup.cellSize.y + gridLayoutGroup.spacing.y;

            if (cellWidthWithSpacing <= 0 || cellHeightWithSpacing <= 0)
            {
                if (logCoordinateConversion) Debug.LogError($"GridManager.ScreenPointToGridCoords: Invalid cell size or spacing from GridLayoutGroup. W: {cellWidthWithSpacing}, H: {cellHeightWithSpacing}.");
                return false;
            }

            int xCoord = Mathf.FloorToInt(contentX / cellWidthWithSpacing); // Renamed to avoid conflict with loop variable
            int yCoord = Mathf.FloorToInt(contentY / cellHeightWithSpacing); // Renamed to avoid conflict with loop variable

            if (xCoord >= 0 && xCoord < runtimeGridWidth && yCoord >= 0 && yCoord < runtimeGridHeight)
            {
                gridCoords = new Vector2Int(xCoord, yCoord);
                return true;
            }
            else { if (logCoordinateConversion) Debug.LogWarning($"Calculated coords ({xCoord},{yCoord}) are out of grid bounds ({runtimeGridWidth}x{runtimeGridHeight})."); }
        }
        else { if (logCoordinateConversion) Debug.LogWarning($"ScreenPointToLocalPointInRectangle failed for screenPoint: {screenPoint}"); }
        return false;
    }
}