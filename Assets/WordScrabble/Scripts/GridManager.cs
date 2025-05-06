using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq; // Required for .Except()
using TMPro;

public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int gridWidth = 10;
    [SerializeField] private int gridHeight = 10;
    [SerializeField] private GameObject tilePrefab; // Your visual tile prefab

    [Header("References")]
    [SerializeField] private RectTransform gridPanelParent; // The UI Panel that will hold the grid tiles

    [Header("Debug")]
    [SerializeField] private bool logCoordinateConversion = false;
    [Tooltip("Pixel offset to correct pointer-to-cell mapping. If highlight is (X_offset_cells, Y_offset_cells) away from pointer, set this to approx (X_offset_cells * cellWidthWithSpacing, Y_offset_cells * cellHeightWithSpacing). Positive values shift highlight left/up.")]
    [SerializeField] private Vector2 coordinatePixelOffsetCorrection = Vector2.zero;


    private TileData[,] gridData;
    private GridLayoutGroup gridLayoutGroup;
    private List<Vector2Int> _currentPreviewCoords = new List<Vector2Int>();

    void Awake()
    {
        if (gridPanelParent == null)
        {
            Debug.LogError("GridManager: gridPanelParent is not assigned in Inspector!", this);
            return;
        }
        gridLayoutGroup = gridPanelParent.GetComponent<GridLayoutGroup>();
        if (gridLayoutGroup == null)
        {
            Debug.LogError("GridManager: gridPanelParent is missing a GridLayoutGroup component!", this);
        }
    }

    public void CreateGrid() // Overload for default size
    {
        CreateGrid(gridWidth, gridHeight);
    }

    public void CreateGrid(int width, int height)
    {
        this.gridWidth = width;
        this.gridHeight = height;

        if (gridPanelParent == null) { Debug.LogError("GridPanelParent not set in GridManager", this); return; }
        if (gridLayoutGroup == null) { Debug.LogError("GridLayoutGroup not found on GridPanelParent", this); return; }
        if (tilePrefab == null) { Debug.LogError("GridManager: tilePrefab is not assigned in Inspector!", this); return; }


        // 1. Clear existing visual tiles from the panel
        foreach (Transform child in gridPanelParent)
        {
            Destroy(child.gameObject);
        }
        _currentPreviewCoords.Clear(); // Clear any lingering preview data

        // 2. Initialize gridData array
        gridData = new TileData[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                GameObject tileInstance = Instantiate(tilePrefab, gridPanelParent);
                tileInstance.name = $"Tile_{x}_{y}";
                gridData[x, y] = new TileData(new Vector2Int(x, y), tileInstance);
            }
        }
        Debug.Log($"GridManager: Grid created ({width}x{height}) with fresh tiles. First tile (0,0) TextMeshPro: {gridData[0, 0]?.VisualTile?.GetComponentInChildren<TextMeshProUGUI>(true)?.gameObject.name}");
    }

    // Call this from LevelManager or similar when a level starts/restarts
    public void ClearAndResetGridVisuals()
    {
        if (gridData == null) return;
        _currentPreviewCoords.Clear();

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                gridData[x, y]?.ResetTile();
            }
        }
        Debug.Log("GridManager: Grid visuals cleared and reset.");
    }

    public TileData GetTileData(Vector2Int coordinate)
    {
        if (coordinate.x >= 0 && coordinate.x < gridWidth && coordinate.y >= 0 && coordinate.y < gridHeight)
        {
            return gridData[coordinate.x, coordinate.y];
        }
        // Debug.LogWarning($"GridManager.GetTileData: Coordinate {coordinate} is out of bounds.");
        return null;
    }

    public bool TrySetLetter(Vector2Int coordinate, char letter)
    {
        Debug.Log($"GridManager.TrySetLetter: Attempting to set '{letter}' at {coordinate}.");
        TileData tileData = GetTileData(coordinate);
        if (tileData != null)
        {
            Debug.Log($"GridManager.TrySetLetter: TileData found for {coordinate}. IsOccupied: {tileData.IsOccupied}, CurrentLetter: '{tileData.Letter}'");
            // Allow placement if tile is not occupied OR if it's occupied by the *same letter* (for overlaps)
            if (!tileData.IsOccupied || tileData.Letter == char.ToUpper(letter))
            {
                Debug.Log($"GridManager.TrySetLetter: Placement condition met for '{letter}' at {coordinate}. Calling tileData.SetPlacedLetter.");
                tileData.SetPlacedLetter(char.ToUpper(letter)); // This will call TileData.UpdateVisuals()
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
            TileData tile = GetTileData(coord);
            tile?.ClearPreviewState();
        }

        for (int i = 0; i < wordCoords.Count; i++)
        {
            Vector2Int coord = wordCoords[i];
            char letterToShow = (i < word.Length) ? word[i] : '?'; // Fallback for safety

            TileData tile = GetTileData(coord);
            tile?.SetPreviewState(letterToShow, isValidPlacement);
        }
        _currentPreviewCoords = new List<Vector2Int>(wordCoords); // Update current preview list
    }

    public void ClearWordPreview()
    {
        foreach (Vector2Int coord in _currentPreviewCoords)
        {
            TileData tile = GetTileData(coord);
            tile?.ClearPreviewState();
        }
        _currentPreviewCoords.Clear();
    }

    public bool ScreenPointToGridCoords(Vector2 screenPoint, Camera uiCamera, out Vector2Int gridCoords)
    {
        gridCoords = Vector2Int.zero;
        if (gridPanelParent == null || gridLayoutGroup == null)
        {
            if (logCoordinateConversion) Debug.LogError("GridManager: gridPanelParent or gridLayoutGroup is null.");
            return false;
        }

        Vector2 rawLocalPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(gridPanelParent, screenPoint, uiCamera, out rawLocalPoint))
        {
            if (logCoordinateConversion) Debug.Log($"ScreenPoint: {screenPoint}, RawLocalPoint (Pivot Relative): {rawLocalPoint}");

            // Adjust for pivot to get coordinates relative to the top-left corner of the gridPanelParent's rect.
            float xFromPanelLeft = rawLocalPoint.x + gridPanelParent.pivot.x * gridPanelParent.rect.width;
            float yFromPanelTop = (1.0f - gridPanelParent.pivot.y) * gridPanelParent.rect.height - rawLocalPoint.y;

            if (logCoordinateConversion) Debug.Log($"PanelPivot: {gridPanelParent.pivot}, PanelRect: {gridPanelParent.rect}, xFromPanelLeft: {xFromPanelLeft}, yFromPanelTop: {yFromPanelTop}");

            // Adjust for padding within the GridLayoutGroup to get coordinates relative to the content area.
            float contentX = xFromPanelLeft - gridLayoutGroup.padding.left;
            float contentY = yFromPanelTop - gridLayoutGroup.padding.top;

            if (logCoordinateConversion) Debug.Log($"Padding: L{gridLayoutGroup.padding.left} T{gridLayoutGroup.padding.top}, ContentX (pre-correction): {contentX}, ContentY (pre-correction): {contentY}");

            // Apply the explicit pixel offset correction
            contentX -= coordinatePixelOffsetCorrection.x;
            contentY -= coordinatePixelOffsetCorrection.y;

            if (logCoordinateConversion) Debug.Log($"coordinatePixelOffsetCorrection: {coordinatePixelOffsetCorrection}, ContentX (post-correction): {contentX}, ContentY (post-correction): {contentY}");

            float cellWidthWithSpacing = gridLayoutGroup.cellSize.x + gridLayoutGroup.spacing.x;
            float cellHeightWithSpacing = gridLayoutGroup.cellSize.y + gridLayoutGroup.spacing.y;

            if (cellWidthWithSpacing <= 0 || cellHeightWithSpacing <= 0)
            {
                if (logCoordinateConversion) Debug.LogError($"GridManager: Invalid cell size or spacing. Width: {cellWidthWithSpacing}, Height: {cellHeightWithSpacing}. Check GridLayoutGroup settings.");
                return false;
            }
            if (logCoordinateConversion) Debug.Log($"CellSize: {gridLayoutGroup.cellSize}, Spacing: {gridLayoutGroup.spacing}, CellW_Spacing: {cellWidthWithSpacing}, CellH_Spacing: {cellHeightWithSpacing}");

            int x = Mathf.FloorToInt(contentX / cellWidthWithSpacing);
            int y = Mathf.FloorToInt(contentY / cellHeightWithSpacing);

            if (logCoordinateConversion) Debug.Log($"Pre-division: contentX = {contentX}, contentY = {contentY}");
            if (logCoordinateConversion) Debug.Log($"Pre-division: cellWidthWithSpacing = {cellWidthWithSpacing}, cellHeightWithSpacing = {cellHeightWithSpacing}");
            if (logCoordinateConversion) Debug.Log($"Pre-division: Calculated Grid X (from contentX) = {Mathf.FloorToInt(contentX / cellWidthWithSpacing)}, Calculated Grid Y (from contentY) = {Mathf.FloorToInt(contentY / cellHeightWithSpacing)}");
            if (logCoordinateConversion) Debug.Log($"Calculated Grid Coords (before boundary check): ({x}, {y})");


            if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight)
            {
                gridCoords = new Vector2Int(x, y);
                if (logCoordinateConversion) Debug.Log($"Final Grid Coords: {gridCoords}");
                return true;
            }
            else
            {
                if (logCoordinateConversion) Debug.LogWarning($"Calculated coords ({x},{y}) are out of grid bounds ({gridWidth}x{gridHeight}).");
            }
        }
        else
        {
            if (logCoordinateConversion) Debug.LogWarning($"ScreenPointToLocalPointInRectangle failed for screenPoint: {screenPoint}");
        }
        return false;
    }
}