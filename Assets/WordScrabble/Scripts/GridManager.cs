using UnityEngine;
using UnityEngine.UI; // Required for GridLayoutGroup, LayoutRebuilder
using System.Collections.Generic;
using TMPro; // If using TextMeshPro for tile letters

// Represents the data stored for each tile on the grid
public class TileData
{
    public Vector2Int Coordinates { get; private set; }
    public char Letter { get; set; } = '\0'; // '\0' indicates empty
    public bool IsOccupied => Letter != '\0'; // Property to check if occupied
    public GameObject VisualTile { get; set; } // Reference to the instantiated tile GameObject
    // Add other properties later: IsBlocker, BonusType (e.g., DL, TW), etc.

    public TileData(Vector2Int coords)
    {
        Coordinates = coords;
    }
}

public class GridManager : MonoBehaviour
{
    [Header("References (Assign in Inspector)")]
    [SerializeField] private GameObject tilePrefab;         // The prefab for a single visual tile
    [SerializeField] private RectTransform gridPanelParent; // The UI Panel GameObject containing the GridLayoutGroup

    // Runtime Data
    private Dictionary<Vector2Int, TileData> gridState = new Dictionary<Vector2Int, TileData>();
    private int gridWidth;
    private int gridHeight;

    /// <summary>
    /// Clears the existing grid and creates a new one based on LevelData.
    /// Relies on a GridLayoutGroup component attached to gridPanelParent for layout.
    /// </summary>
    /// <param name="levelData">The data defining the level's grid dimensions.</param>
    public void CreateGrid(LevelData levelData)
    {
        if (tilePrefab == null || gridPanelParent == null)
        {
            Debug.LogError("GridManager Error: Missing Tile Prefab or Grid Panel Parent reference in the Inspector!", this);
            return;
        }

        // --- Check for the essential GridLayoutGroup component ---
        GridLayoutGroup gridLayout = gridPanelParent.GetComponent<GridLayoutGroup>();
        if (gridLayout == null)
        {
            Debug.LogError("GridManager Error: The assigned Grid Panel Parent is MISSING the GridLayoutGroup component! Please add it in the Inspector.", gridPanelParent.gameObject);
            return; // Cannot proceed without the layout group
        }

        gridWidth = levelData.gridWidth;
        gridHeight = levelData.gridHeight;

        ClearGrid(); // Remove any old tiles first

        // --- Ensure GridLayoutGroup Constraint matches Level Data (Optional but Recommended) ---
        // This helps if you reuse the same panel for different sized grids.
        // Configure the GridLayoutGroup primarily in the Inspector (Padding, Spacing, Start Corner, Cell Size).
        if (gridLayout.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
        {
            if (gridLayout.constraintCount != gridWidth)
            {
                Debug.LogWarning($"GridManager: Updating GridLayoutGroup constraintCount to {gridWidth} to match LevelData.", gridPanelParent.gameObject);
                gridLayout.constraintCount = gridWidth;
            }
        }
        else if (gridLayout.constraint == GridLayoutGroup.Constraint.FixedRowCount)
        {
            if (gridLayout.constraintCount != gridHeight)
            {
                Debug.LogWarning($"GridManager: Updating GridLayoutGroup constraintCount to {gridHeight} to match LevelData.", gridPanelParent.gameObject);
                gridLayout.constraintCount = gridHeight;
            }
        }
        // We don't calculate or set Cell Size here - rely on Inspector settings.

        // --- Instantiate Tiles ---
        // The GridLayoutGroup will automatically position and size them.
        int totalTilesToCreate = gridWidth * gridHeight;
        for (int i = 0; i < totalTilesToCreate; i++)
        {
            // Calculate coordinates based on index and grid width
            // Assumes GridLayoutGroup settings: Start Corner=UpperLeft, Start Axis=Horizontal
            int x = i % gridWidth;
            int y = i / gridWidth;
            Vector2Int coords = new Vector2Int(x, y);

            // Instantiate the tile as a child of the panel
            GameObject tileInstance = Instantiate(tilePrefab, gridPanelParent);
            tileInstance.name = $"Tile_{x}_{y}";

            // Create and store the logical data for this tile
            TileData newTileData = new TileData(coords);
            newTileData.VisualTile = tileInstance;
            gridState.Add(coords, newTileData);

            // Optional: Add AspectRatioFitter if needed for square tiles, configured on prefab is better
            // Ensure the Tile Prefab itself doesn't have a LayoutElement overriding the grid layout.
        }

        // Optional: Force layout rebuild if needed, though usually not necessary
        // LayoutRebuilder.ForceRebuildLayoutImmediate(gridPanelParent);

        Debug.Log($"GridManager: Grid created successfully. Dimensions: {gridWidth}x{gridHeight}. Tiles instantiated: {gridState.Count}");
    }

    /// <summary>
    /// Destroys all child GameObjects under the grid panel parent and clears the grid state data.
    /// </summary>
    private void ClearGrid()
    {
        // Efficiently destroy existing children
        foreach (Transform child in gridPanelParent)
        {
            Destroy(child.gameObject);
        }
        // Clear the dictionary holding the logical tile data
        gridState.Clear();
    }

    /// <summary>
    /// Checks if a given coordinate is within the bounds of the current grid.
    /// </summary>
    /// <param name="coordinate">The coordinate to check.</param>
    /// <returns>True if the coordinate is valid, false otherwise.</returns>
    public bool IsValidCoordinate(Vector2Int coordinate)
    {
        return coordinate.x >= 0 && coordinate.x < gridWidth &&
               coordinate.y >= 0 && coordinate.y < gridHeight;
    }

    /// <summary>
    /// Retrieves the TileData for a specific coordinate.
    /// </summary>
    /// <param name="coordinate">The coordinate of the tile.</param>
    /// <returns>The TileData object if the coordinate is valid, otherwise null.</returns>
    public TileData GetTileData(Vector2Int coordinate)
    {
        gridState.TryGetValue(coordinate, out TileData tileData);
        return tileData; // Returns null if the key (coordinate) doesn't exist
    }

    /// <summary>
    /// Attempts to place a letter onto the grid at the specified coordinate.
    /// Updates the TileData and the visual representation (Text).
    /// </summary>
    /// <param name="coordinate">The target coordinate.</param>
    /// <param name="letter">The letter to place.</param>
    /// <returns>True if the letter was successfully placed or matched an existing letter, false otherwise.</returns>
    public bool TrySetLetter(Vector2Int coordinate, char letter)
    {
        TileData tileData = GetTileData(coordinate);
        if (tileData != null) // Check if coordinate is valid first
        {
            // Allow placement if the tile is empty OR if the letter matches what's already there (for overlaps)
            if (!tileData.IsOccupied || tileData.Letter == char.ToUpper(letter))
            {
                bool wasEmpty = !tileData.IsOccupied; // Track if this is a new placement vs overlap confirmation
                tileData.Letter = char.ToUpper(letter);

                // --- Update Visual Tile ---
                // Find the TextMeshProUGUI component expected to be on the tile prefab (likely a child)
                TextMeshProUGUI textComponent = tileData.VisualTile.GetComponentInChildren<TextMeshProUGUI>();
                if (textComponent != null)
                {
                    textComponent.text = tileData.Letter.ToString();
                    // Optional: Change text color or tile background based on placement/overlap?
                }
                else
                {
                    // Log warning only once per unique tile prefab instance if issue persists
                    Debug.LogWarning($"GridManager: Tile at {coordinate} has no TextMeshProUGUI child to display the letter '{letter}'. Check the Tile Prefab.", tileData.VisualTile);
                }
                // ---

                if (wasEmpty)
                {
                    // Debug.Log($"GridManager: Set letter '{tileData.Letter}' at {coordinate}"); // Reduce log spam
                }
                else
                {
                    // Debug.Log($"GridManager: Letter '{tileData.Letter}' at {coordinate} confirmed (overlap)."); // Reduce log spam
                }
                return true; // Placement successful or overlap confirmed
            }
            else
            {
                // Tile exists, is occupied, and the new letter is different
                Debug.LogWarning($"GridManager: Cannot set letter '{letter}' at {coordinate}. Tile already occupied with '{tileData.Letter}'.");
                return false; // Placement blocked
            }
        }
        else
        {
            // GetTileData returned null, meaning the coordinate was invalid
            Debug.LogWarning($"GridManager: Cannot set letter '{letter}' at {coordinate}. Invalid coordinate.");
            return false; // Invalid coordinate
        }
    }

    /// <summary>
    /// Converts a screen point (like mouse position) to grid coordinates.
    /// Requires the gridPanelParent to have a correctly configured GridLayoutGroup.
    /// </summary>
    /// <param name="screenPoint">The screen point (e.g., Input.mousePosition).</param>
    /// <param name="uiCamera">The camera associated with the Canvas (null for ScreenSpaceOverlay).</param>
    /// <param name="gridCoords">The calculated grid coordinates (output).</param>
    /// <returns>True if the screen point maps to a valid coordinate within the grid, false otherwise.</returns>
    public bool ScreenPointToGridCoords(Vector2 screenPoint, Camera uiCamera, out Vector2Int gridCoords)
    {
        gridCoords = Vector2Int.zero; // Default output
        GridLayoutGroup gridLayout = gridPanelParent.GetComponent<GridLayoutGroup>();
        if (gridLayout == null)
        {
            Debug.LogError("GridManager.ScreenPointToGridCoords: Cannot perform conversion, GridPanelParent is missing GridLayoutGroup component.", gridPanelParent);
            return false;
        }

        // Convert screen point to the local coordinate system of the grid panel
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(gridPanelParent, screenPoint, uiCamera, out Vector2 localPoint))
        {
            // Adjust the local point based on the panel's pivot point to get position relative to the panel's bottom-left corner (conceptually)
            Rect panelRect = gridPanelParent.rect;
            localPoint.x += panelRect.width * gridPanelParent.pivot.x;
            localPoint.y += panelRect.height * gridPanelParent.pivot.y;

            // Get layout properties directly from the GridLayoutGroup
            Vector2 cellSize = gridLayout.cellSize;
            Vector2 spacing = gridLayout.spacing;
            RectOffset padding = gridLayout.padding;

            // Calculate the total size occupied by a cell plus its spacing
            // Handle potential zero values gracefully, though layout usually ensures > 0
            float effectiveCellWidth = cellSize.x + spacing.x;
            float effectiveCellHeight = cellSize.y + spacing.y;

            // Prevent division by zero if the grid layout results in zero-sized cells
            if (effectiveCellWidth <= 0 || effectiveCellHeight <= 0)
            {
                Debug.LogWarning("GridManager.ScreenPointToGridCoords: Effective cell size is zero or negative. Check GridLayoutGroup settings (Cell Size, Spacing).", gridPanelParent);
                return false;
            }

            // Adjust the local point for the grid's padding from the edges
            localPoint.x -= padding.left;
            // Adjust Y coordinate: In UI space, Y typically increases upwards.
            // We need to calculate from the top edge considering padding.
            localPoint.y = (panelRect.height - padding.top) - localPoint.y; // Invert Y axis relative to top padding

            // Clamp localPoint to be within the padded area to avoid negative indices from clicks in padding
            localPoint.x = Mathf.Max(0, localPoint.x);
            localPoint.y = Mathf.Max(0, localPoint.y);


            // Calculate the grid indices by dividing the adjusted local point by the effective cell size
            int x = Mathf.FloorToInt(localPoint.x / effectiveCellWidth);
            int y = Mathf.FloorToInt(localPoint.y / effectiveCellHeight);

            // Assign the calculated coordinates to the output parameter
            gridCoords = new Vector2Int(x, y);

            // Final check: Is the calculated coordinate actually within the logical grid bounds?
            return IsValidCoordinate(gridCoords);
        }

        // ScreenPointToLocalPointInRectangle failed (e.g., point is outside the RectTransform)
        return false;
    }

    /// <summary>
    /// Placeholder method for visually highlighting tiles on the grid.
    /// </summary>
    /// <param name="coordinates">A list of grid coordinates to highlight.</param>
    /// <param name="color">The color to use for highlighting.</param>
    public void HighlightTiles(List<Vector2Int> coordinates, Color color)
    {
        // TODO: Implement visual highlighting logic.
        // Example: Iterate through coordinates, get TileData, get VisualTile, change its Image color.
        // Remember to have a way to reset the highlight later.
        foreach (Vector2Int coord in coordinates)
        {
            TileData tileData = GetTileData(coord);
            if (tileData != null && tileData.VisualTile != null)
            {
                Image tileImage = tileData.VisualTile.GetComponent<Image>(); // Or GetComponentInChildren<Image>()
                if (tileImage != null)
                {
                    // tileImage.color = color; // Apply highlight
                }
            }
        }
        Debug.Log($"GridManager: Placeholder - Highlighting {coordinates.Count} tiles with {color}.");
    }
}