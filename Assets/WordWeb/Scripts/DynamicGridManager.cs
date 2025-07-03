using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Dynamic Grid Manager for the strategic word placement game.
/// Handles resolution-independent grid scaling, cell management, and placement validation.
/// </summary>
public class DynamicGridManager : MonoBehaviour
{
    [Header("Grid Configuration")]
    [SerializeField] private int gridSize = 15;
    [SerializeField] private float baseCellSize = 50f;
    [SerializeField] private float gridPadding = 20f;
    [SerializeField] private Color defaultCellColor = Color.white;
    [SerializeField] private Color centerCellColor = Color.yellow;
    [SerializeField] private Color occupiedCellColor = Color.gray;

    [Header("Scaling")]
    [SerializeField] private bool autoScale = true;
    [SerializeField] private float maxGridScreenPercentage = 0.6f;
    [SerializeField] private float minCellSize = 30f;
    [SerializeField] private float maxCellSize = 80f;

    [Header("Prefab References")]
    [SerializeField] private GameObject gridCellPrefab;
    [SerializeField] private Transform gridParent;

    [Header("Visual")]
    [SerializeField] private GridLayoutGroup gridLayoutGroup;
    [SerializeField] private RectTransform gridRectTransform;

    // Grid state
    private GridCell[,] gridCells;
    private char[,] gridLetters;
    private bool[,] gridOccupied;
    private Vector2Int centerPosition;
    private float currentCellSize;
    private bool gridInitialized = false;

    // Events
    public System.Action<Vector2Int, char> OnCellPlaced;
    public System.Action<Vector2Int> OnCellCleared;
    public System.Action OnGridCleared;

    #region Initialization

    void Awake()
    {
        ValidateReferences();
        InitializeGrid();
    }

    void Start()
    {
        if (autoScale)
        {
            CalculateOptimalCellSize();
            RefreshGridLayout();
        }
    }

    void OnRectTransformDimensionsChange()
    {
        if (gridInitialized && autoScale)
        {
            CalculateOptimalCellSize();
            RefreshGridLayout();
        }
    }

    private void ValidateReferences()
    {
        if (gridCellPrefab == null)
        {
            Debug.LogError("DynamicGridManager: Grid Cell Prefab not assigned!");
            return;
        }

        if (gridParent == null)
        {
            Debug.LogError("DynamicGridManager: Grid Parent not assigned!");
            return;
        }

        if (gridLayoutGroup == null)
        {
            gridLayoutGroup = gridParent.GetComponent<GridLayoutGroup>();
            if (gridLayoutGroup == null)
            {
                Debug.LogError("DynamicGridManager: No GridLayoutGroup found on gridParent!");
                return;
            }
        }

        if (gridRectTransform == null)
        {
            gridRectTransform = gridParent.GetComponent<RectTransform>();
        }
    }

    private void InitializeGrid()
    {
        // Initialize grid arrays
        gridCells = new GridCell[gridSize, gridSize];
        gridLetters = new char[gridSize, gridSize];
        gridOccupied = new bool[gridSize, gridSize];
        centerPosition = new Vector2Int(gridSize / 2, gridSize / 2);

        // Clear existing cells
        ClearGridCells();

        // Create grid cells
        CreateGridCells();

        gridInitialized = true;
    }

    #endregion

    #region Grid Creation

    private void ClearGridCells()
    {
        for (int i = gridParent.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
            {
                Destroy(gridParent.GetChild(i).gameObject);
            }
            else
            {
                DestroyImmediate(gridParent.GetChild(i).gameObject);
            }
        }
    }

    private void CreateGridCells()
    {
        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                GameObject cellObj = Instantiate(gridCellPrefab, gridParent);
                GridCell cell = cellObj.GetComponent<GridCell>();
                
                if (cell == null)
                {
                    cell = cellObj.AddComponent<GridCell>();
                }

                // Initialize cell
                cell.Initialize(x, y, this);
                gridCells[x, y] = cell;

                // Set cell appearance
                Vector2Int position = new Vector2Int(x, y);
                if (position == centerPosition)
                {
                    cell.SetCellType(GridCellType.Center);
                }
                else
                {
                    cell.SetCellType(GridCellType.Empty);
                }
            }
        }
    }

    #endregion

    #region Scaling & Layout

    private void CalculateOptimalCellSize()
    {
        // Get available screen space
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("DynamicGridManager: No Canvas found in parent hierarchy!");
            return;
        }

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        Vector2 canvasSize = canvasRect.rect.size;

        // Calculate available space for grid
        float availableWidth = canvasSize.x * maxGridScreenPercentage;
        float availableHeight = canvasSize.y * maxGridScreenPercentage;
        float availableSpace = Mathf.Min(availableWidth, availableHeight);

        // Calculate optimal cell size
        float calculatedCellSize = (availableSpace - gridPadding * 2) / gridSize;
        currentCellSize = Mathf.Clamp(calculatedCellSize, minCellSize, maxCellSize);

        Debug.Log($"Grid scaling: Canvas={canvasSize}, Available={availableSpace}, CellSize={currentCellSize}");
    }

    private void RefreshGridLayout()
    {
        if (gridLayoutGroup == null) return;

        // Update grid layout
        gridLayoutGroup.cellSize = new Vector2(currentCellSize, currentCellSize);
        gridLayoutGroup.spacing = new Vector2(2f, 2f);
        gridLayoutGroup.constraintCount = gridSize;

        // Update grid container size
        float totalSize = (currentCellSize * gridSize) + (gridLayoutGroup.spacing.x * (gridSize - 1));
        if (gridRectTransform != null)
        {
            gridRectTransform.sizeDelta = new Vector2(totalSize, totalSize);
        }
    }

    #endregion

    #region Grid Operations

    public bool IsPositionValid(Vector2Int position)
    {
        return position.x >= 0 && position.x < gridSize && 
               position.y >= 0 && position.y < gridSize;
    }

    public bool IsPositionOccupied(Vector2Int position)
    {
        if (!IsPositionValid(position)) return true;
        return gridOccupied[position.x, position.y];
    }

    public char GetLetterAt(Vector2Int position)
    {
        if (!IsPositionValid(position)) return '\0';
        return gridLetters[position.x, position.y];
    }

    public bool PlaceLetter(Vector2Int position, char letter)
    {
        if (!IsPositionValid(position) || IsPositionOccupied(position))
        {
            return false;
        }

        gridLetters[position.x, position.y] = letter;
        gridOccupied[position.x, position.y] = true;
        
        GridCell cell = gridCells[position.x, position.y];
        cell.SetLetter(letter);
        cell.SetCellType(GridCellType.Occupied);

        OnCellPlaced?.Invoke(position, letter);
        return true;
    }

    public bool ClearLetter(Vector2Int position)
    {
        if (!IsPositionValid(position) || !IsPositionOccupied(position))
        {
            return false;
        }

        gridLetters[position.x, position.y] = '\0';
        gridOccupied[position.x, position.y] = false;
        
        GridCell cell = gridCells[position.x, position.y];
        cell.ClearLetter();
        
        // Restore cell type
        if (position == centerPosition)
        {
            cell.SetCellType(GridCellType.Center);
        }
        else
        {
            cell.SetCellType(GridCellType.Empty);
        }

        OnCellCleared?.Invoke(position);
        return true;
    }

    public void ClearAllLetters()
    {
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                if (gridOccupied[x, y])
                {
                    ClearLetter(new Vector2Int(x, y));
                }
            }
        }
        
        OnGridCleared?.Invoke();
    }

    #endregion

    #region Word Placement

    public bool CanPlaceWord(string word, Vector2Int startPosition, PlacementOrientation orientation)
    {
        if (string.IsNullOrEmpty(word) || !IsPositionValid(startPosition))
        {
            return false;
        }

        Vector2Int direction = GetDirectionVector(orientation);
        Vector2Int currentPos = startPosition;

        for (int i = 0; i < word.Length; i++)
        {
            if (!IsPositionValid(currentPos))
            {
                return false;
            }

            // Check if position is occupied by different letter
            if (IsPositionOccupied(currentPos))
            {
                char existingLetter = GetLetterAt(currentPos);
                if (existingLetter != word[i])
                {
                    return false;
                }
            }

            currentPos += direction;
        }

        return true;
    }

    public bool PlaceWord(string word, Vector2Int startPosition, PlacementOrientation orientation)
    {
        if (!CanPlaceWord(word, startPosition, orientation))
        {
            return false;
        }

        Vector2Int direction = GetDirectionVector(orientation);
        Vector2Int currentPos = startPosition;

        for (int i = 0; i < word.Length; i++)
        {
            if (!IsPositionOccupied(currentPos))
            {
                PlaceLetter(currentPos, word[i]);
            }
            currentPos += direction;
        }

        return true;
    }

    private Vector2Int GetDirectionVector(PlacementOrientation orientation)
    {
        switch (orientation)
        {
            case PlacementOrientation.Horizontal:
                return Vector2Int.right;
            case PlacementOrientation.Vertical:
                return Vector2Int.up;
            default:
                return Vector2Int.right;
        }
    }

    #endregion

    #region Getters

    public int GridSize => gridSize;
    public Vector2Int CenterPosition => centerPosition;
    public float CurrentCellSize => currentCellSize;
    public GridCell GetGridCell(Vector2Int position)
    {
        if (!IsPositionValid(position)) return null;
        return gridCells[position.x, position.y];
    }

    public GridCell GetGridCell(int x, int y)
    {
        return GetGridCell(new Vector2Int(x, y));
    }

    #endregion

    #region Debug

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    void OnDrawGizmos()
    {
        if (!gridInitialized) return;

        // Draw grid bounds
        Gizmos.color = Color.blue;
        Vector3 center = transform.position;
        Vector3 size = Vector3.one * (currentCellSize * gridSize);
        Gizmos.DrawWireCube(center, size);

        // Draw center position
        Gizmos.color = Color.red;
        Vector3 centerWorldPos = GetWorldPosition(centerPosition);
        Gizmos.DrawWireSphere(centerWorldPos, currentCellSize * 0.5f);
    }

    public Vector3 GetWorldPosition(Vector2Int gridPosition)
    {
        if (gridCells == null || !IsPositionValid(gridPosition))
        {
            return Vector3.zero;
        }

        GridCell cell = gridCells[gridPosition.x, gridPosition.y];
        return cell != null ? cell.transform.position : Vector3.zero;
    }

    #endregion
}

/// <summary>
/// Enum for word placement orientation
/// </summary>
public enum PlacementOrientation
{
    Horizontal,
    Vertical
}

/// <summary>
/// Enum for grid cell types
/// </summary>
public enum GridCellType
{
    Empty,
    Center,
    Occupied,
    Highlighted
}
