using UnityEngine;
using UnityEngine.UI;

public class GridManagerGPT : MonoBehaviour
{
    public enum GridDimension { TwoD, ThreeD }
    public enum GridType { Square, Hex }

    [Header("Grid Settings")]
    public GridDimension gridDimension = GridDimension.TwoD;
    public GridType gridType = GridType.Square;
    public int rows = 5;
    public int columns = 5;
    public float cellSize = 100f;
    public float spacing = 10f;
    public Vector2 padding = new Vector2(20f, 20f);
    public GameObject cellPrefab;
    public RectTransform gridParent;
    public ScrollRect scrollRect;

    void Start()
    {
        GenerateGrid();
    }

    void GenerateGrid()
    {
        Vector2 gridSize = CalculateGridSize();

        gridParent.sizeDelta = gridSize;
        gridParent.anchoredPosition = Vector2.zero;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                Vector2 position = Vector2.zero;

                if (gridType == GridType.Square)
                {
                    position = GetSquareGridPosition(row, col);
                }
                else if (gridType == GridType.Hex)
                {
                    position = GetHexGridPosition(row, col);
                }

                GameObject cell = Instantiate(cellPrefab, gridParent);
                RectTransform rectTransform = cell.GetComponent<RectTransform>();
                rectTransform.anchoredPosition = position;
                rectTransform.sizeDelta = new Vector2(cellSize, cellSize);
                cell.name = $"Cell_{row}_{col}";
            }
        }
    }

    Vector2 GetSquareGridPosition(int row, int col)
    {
        float x = padding.x + col * (cellSize + spacing);
        float y = -padding.y - row * (cellSize + spacing);
        return new Vector2(x, y);
    }

    Vector2 GetHexGridPosition(int row, int col)
    {
        float width = cellSize * 0.866f; // cos(30°) ~ 0.866 for flat-top hex
        float height = cellSize;

        float x = padding.x + col * (width + spacing * 0.5f);
        float y = -padding.y - row * (height * 0.75f + spacing * 0.5f);

        if (col % 2 == 1)
        {
            y -= height * 0.375f;
        }

        return new Vector2(x, y);
    }

    Vector2 CalculateGridSize()
    {
        float width = 0f;
        float height = 0f;

        if (gridType == GridType.Square)
        {
            width = padding.x * 2 + columns * cellSize + (columns - 1) * spacing;
            height = padding.y * 2 + rows * cellSize + (rows - 1) * spacing;
        }
        else if (gridType == GridType.Hex)
        {
            float hexWidth = cellSize * 0.866f;
            float hexHeight = cellSize;
            width = padding.x * 2 + columns * hexWidth + (columns - 1) * spacing * 0.5f;
            height = padding.y * 2 + rows * hexHeight * 0.75f + (rows - 1) * spacing * 0.5f + hexHeight * 0.25f;
        }

        return new Vector2(width, height);
    }
}
