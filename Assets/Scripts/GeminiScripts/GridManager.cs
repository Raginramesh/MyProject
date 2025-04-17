using UnityEngine;
using System.Collections.Generic; // Needed for List

#if UNITY_EDITOR
using UnityEditor; // Needed for DestroyImmediate in editor context
#endif

// Enum to define the plane the grid will be generated on
public enum GridPlane { XY_2D, XZ_3D }

// Enum to define the shape of the grid cells
// *** UPDATED ENUM ***
public enum CellShape { Square, HexPointyTop, HexFlatTop } // Added HexFlatTop

public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    [Min(1)] public int rows = 10;
    [Min(1)] public int columns = 10;
    public GridPlane gridPlane = GridPlane.XY_2D;
    public CellShape cellShape = CellShape.Square;

    [Header("Cell Visuals")]
    public GameObject cellPrefab; // The prefab to instantiate for each cell

    [Header("Spacing")]
    [Tooltip("Horizontal distance between adjacent cell centers (Columns).")]
    [Min(0.1f)] public float cellSpacingX = 1.1f;
    [Tooltip("Vertical distance between adjacent cell centers (Rows on XY plane).")]
    [Min(0.1f)] public float cellSpacingY = 1.1f;
    [Tooltip("Depth distance between adjacent cell centers (Rows on XZ plane).")]
    [Min(0.1f)] public float cellSpacingZ = 1.1f;

    // *** UPDATED HEX FACTORS ***
    [Header("Hex Specific Factors")]
    [Tooltip("Vertical distance multiplier for Pointy Top hex rows (typically sqrt(3)/2 ≈ 0.866). Affects Y spacing in XY plane, Z spacing in XZ plane.")]
    [Range(0.1f, 1.0f)] public float hexPointyTopVerticalFactor = 0.866f;

    [Tooltip("Horizontal distance multiplier for Flat Top hex columns (typically sqrt(3)/2 ≈ 0.866). Affects X spacing.")]
    [Range(0.1f, 1.0f)] public float hexFlatTopHorizontalFactor = 0.866f;
    // *** END HEX FACTOR UPDATES ***

    [Header("Organization")]
    [Tooltip("(Optional) Assign a Transform to parent the generated cells.")]
    public Transform gridContainer;

    // --- Private Variables ---
    private List<GameObject> spawnedCells = new List<GameObject>(); // Keep track of created cells

    // --- Editor Functionality ---

    [ContextMenu("Generate Grid")]
    public void GenerateGrid()
    {
        ClearGrid(); // Clear previous grid first

        if (cellPrefab == null)
        {
            Debug.LogError("Cell Prefab is not assigned in the Grid Manager!", this);
            return;
        }

        Transform parent = (gridContainer != null) ? gridContainer : this.transform;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                Vector3 cellPosition = CalculateCellPosition(r, c);
                GameObject cellInstance = Instantiate(cellPrefab, cellPosition, Quaternion.identity, parent);
                cellInstance.name = $"{cellShape}_Cell_{r}_{c}";
                spawnedCells.Add(cellInstance);

                // Optional: Initialize a script on the cell
                // GridCell gridCellScript = cellInstance.GetComponent<GridCell>();
                // if (gridCellScript != null) { gridCellScript.Initialize(r, c); }
            }
        }

        Debug.Log($"Generated {cellShape} grid ({rows}x{columns}) on plane {gridPlane}.", this);
    }

    [ContextMenu("Clear Grid")]
    public void ClearGrid()
    {
        Transform parent = (gridContainer != null) ? gridContainer : this.transform;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            if (spawnedCells.Contains(child))
            {
                if (Application.isPlaying) Destroy(child);
                else
                {
#if UNITY_EDITOR
                    DestroyImmediate(child);
#else
                    Destroy(child);
#endif
                }
            }
            // Optional: else if (gridContainer != null && parent == gridContainer) { DestroyImmediate(child); } // Clear all under container
        }
        spawnedCells.Clear();
    }

    // --- Helper Function ---

    // *** UPDATED POSITION CALCULATION ***
    private Vector3 CalculateCellPosition(int row, int col)
    {
        float xPos = 0f;
        float yPos = 0f;
        float zPos = 0f;

        bool isPointyHex = cellShape == CellShape.HexPointyTop;
        bool isFlatHex = cellShape == CellShape.HexFlatTop;

        if (gridPlane == GridPlane.XZ_3D) // 3D Grid on XZ Plane
        {
            yPos = 0; // Flat on the XZ plane

            if (isPointyHex)
            {
                bool isOffsetColumn = (col % 2 != 0);
                xPos = col * cellSpacingX;
                // Use Z spacing and the pointy-top vertical factor for the Z axis
                zPos = row * cellSpacingZ * hexPointyTopVerticalFactor;
                if (isOffsetColumn) zPos += (cellSpacingZ * hexPointyTopVerticalFactor) / 2.0f;
            }
            else if (isFlatHex)
            {
                bool isOffsetRow = (row % 2 != 0);
                // Use X spacing and the flat-top horizontal factor for the X axis
                xPos = col * cellSpacingX * hexFlatTopHorizontalFactor;
                // Use Z spacing directly for the Z axis
                zPos = row * cellSpacingZ;
                if (isOffsetRow) xPos += (cellSpacingX * hexFlatTopHorizontalFactor) / 2.0f;
            }
            else // Square on XZ plane
            {
                xPos = col * cellSpacingX;
                zPos = row * cellSpacingZ;
            }
        }
        else // 2D Grid on XY Plane
        {
            zPos = 0; // Flat on the XY plane

            if (isPointyHex)
            {
                bool isOffsetColumn = (col % 2 != 0);
                xPos = col * cellSpacingX;
                // Use Y spacing and the pointy-top vertical factor for the Y axis
                yPos = row * cellSpacingY * hexPointyTopVerticalFactor;
                if (isOffsetColumn) yPos += (cellSpacingY * hexPointyTopVerticalFactor) / 2.0f;
            }
            else if (isFlatHex)
            {
                bool isOffsetRow = (row % 2 != 0);
                // Use X spacing and the flat-top horizontal factor for the X axis
                xPos = col * cellSpacingX * hexFlatTopHorizontalFactor;
                // Use Y spacing directly for the Y axis
                yPos = row * cellSpacingY;
                if (isOffsetRow) xPos += (cellSpacingX * hexFlatTopHorizontalFactor) / 2.0f;
            }
            else // Square on XY plane
            {
                xPos = col * cellSpacingX;
                yPos = row * cellSpacingY;
            }
        }

        // Return position relative to the GridManager's GameObject position
        return transform.position + new Vector3(xPos, yPos, zPos);
    }
    // *** END POSITION CALCULATION UPDATE ***

    /*
    // Optional: Auto-update in editor (use with caution)
    void OnValidate() {
        if (!Application.isPlaying && cellPrefab != null ) { // && toggleAutoUpdate) {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => {
                if (this != null) GenerateGrid();
            };
            #endif
        }
    }
    */
}