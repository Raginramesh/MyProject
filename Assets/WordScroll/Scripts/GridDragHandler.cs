using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using TMPro;

public class GridDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Dependencies")]
    [SerializeField] private WordGridManager gridManager;

    [Header("Drag Settings")]
    [Tooltip("Minimum drag distance to register a drag")]
    [SerializeField] private float minDragThreshold = 10f;
    [Tooltip("Factor to control the overall drag sensitivity")]
    [SerializeField] private float dragSensitivity = 1f;
    [Tooltip("Factor to specifically control the row scroll speed")]
    [SerializeField] public float rowScrollSpeedMultiplier = 0.1f; // Public variable for row scroll speed
    [Tooltip("Speed of the snap back animation")]
    [SerializeField] private float snapDuration = 0.1f;
    [Tooltip("Easing for the snap back animation")]
    [SerializeField] private Ease snapEase = Ease.OutCubic;
    [Header("Scrolling Bounds")]
    [Tooltip("The positive X boundary where cells will wrap to the left.")]
    [SerializeField] private float rightScrollBound = 200f;

    [Tooltip("The negative X boundary where cells will wrap to the right.")]
    [SerializeField] private float leftScrollBound = -200f;

    private Vector2 startTouchPosition;
    private bool isDragging = false;
    private int draggedRowIndex = -1;
    private int draggedColumnIndex = -1;

    private RectTransform gridContainerRect;
    private Vector2 cellSize;
    private Vector2 spacing;
    private Vector2 totalCellSize;

    private void Start()
    {
        if (gridManager == null)
        {
            Debug.LogError("GridDragHandler: WordGridManager not assigned!", this);
            enabled = false;
            return;
        }

        gridContainerRect = gridManager.GetComponent<RectTransform>();
        if (gridContainerRect == null)
        {
            Debug.LogError("GridDragHandler: WordGridManager doesn't have a RectTransform!", this);
            enabled = false;
            return;
        }

        cellSize = gridManager.CellSize;
        spacing = gridManager.Spacing;
        totalCellSize = cellSize + spacing;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (gridManager.IsAnimating) return;

        startTouchPosition = eventData.position;
        isDragging = false;
        draggedRowIndex = -1;
        draggedColumnIndex = -1;

        Vector2 localTouchPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(gridContainerRect, startTouchPosition, eventData.pressEventCamera, out localTouchPosition);

        float cellWidthWithSpacing = gridManager.CellSize.x + gridManager.Spacing.x;
        float cellHeightWithSpacing = gridManager.CellSize.y + gridManager.Spacing.y;

        // Calculate grid bounds in local space
        float gridLeft = -gridContainerRect.rect.width / 2f;
        float gridRight = gridContainerRect.rect.width / 2f;
        float gridTop = gridContainerRect.rect.height / 2f;
        float gridBottom = -gridContainerRect.rect.height / 2f;

        // Calculate touch position within the grid (0 to gridSize)
        float relativeX = (localTouchPosition.x - gridLeft) / cellWidthWithSpacing;
        float relativeY = (gridTop - localTouchPosition.y) / cellHeightWithSpacing;

        int col = Mathf.FloorToInt(relativeX);
        int row = Mathf.FloorToInt(relativeY);

        Debug.Log($"OnBeginDrag: Touch Position (Screen): {startTouchPosition}, Local: {localTouchPosition}, Relative (X, Y): ({relativeX}, {relativeY}), Calculated Cell (Row, Col): ({row}, {col})");

        if (row >= 0 && row < gridManager.GridSize && col >= 0 && col < gridManager.GridSize)
        {
            string letter = gridManager.GetLetterAt(row, col).ToString(); // Fixed the error here
            Debug.Log($"OnBeginDrag: Letter at touched cell ({row}, {col}): {letter}");
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (gridManager.IsAnimating) return;

        Vector2 currentTouchPosition = eventData.position;
        Vector2 delta = currentTouchPosition - startTouchPosition;

        if (!isDragging && delta.magnitude > minDragThreshold)
        {
            isDragging = true;
            draggedRowIndex = -1;
            draggedColumnIndex = -1;

            Vector2 localTouchPosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(gridContainerRect, startTouchPosition, eventData.pressEventCamera, out localTouchPosition);

            float cellWidthWithSpacing = gridManager.CellSize.x + gridManager.Spacing.x;
            float cellHeightWithSpacing = gridManager.CellSize.y + gridManager.Spacing.y;

            // Calculate grid bounds in local space
            float gridLeft = -gridContainerRect.rect.width / 2f;
            float gridRight = gridContainerRect.rect.width / 2f;
            float gridTop = gridContainerRect.rect.height / 2f;
            float gridBottom = -gridContainerRect.rect.height / 2f;

            // Calculate touch position within the grid (0 to gridSize)
            float relativeX = (localTouchPosition.x - gridLeft) / cellWidthWithSpacing;
            float relativeY = (gridTop - localTouchPosition.y) / cellHeightWithSpacing;

            int col = Mathf.FloorToInt(relativeX);
            int row = Mathf.FloorToInt(relativeY);

            if (row >= 0 && row < gridManager.GridSize && Mathf.Abs(delta.y) <= Mathf.Abs(delta.x)) // Prioritize row drag
            {
                draggedRowIndex = row;
                Debug.Log($"Starting drag on row: {draggedRowIndex} at local Y: {localTouchPosition.y}, relative Y: {relativeY}");
                draggedColumnIndex = -1; // Ensure we are not also dragging a column
            }
            else if (col >= 0 && col < gridManager.GridSize && Mathf.Abs(delta.x) < Mathf.Abs(delta.y)) // Prioritize column drag
            {
                draggedColumnIndex = col;
                Debug.Log($"Starting drag on column: {draggedColumnIndex} at local X: {localTouchPosition.x}, relative X: {relativeX}");
                draggedRowIndex = -1; // Ensure we are not also dragging a row
            }
            else
            {
                isDragging = false;
            }
        }

        if (isDragging && draggedRowIndex != -1)
        {
            float dragAmount = delta.x * dragSensitivity * rowScrollSpeedMultiplier; // Apply the row scroll speed multiplier
            ScrollRowVisual(draggedRowIndex, dragAmount);
        }
        // We'll handle column dragging later
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
        if (draggedRowIndex != -1)
        {
            SnapRow(draggedRowIndex);
            draggedRowIndex = -1;
        }
        // We'll handle column snapping later
    }

    private void ScrollRowVisual(int rowIndex, float dragAmount)
    {
        if (gridManager == null) return;

        float cellWidthWithSpacing = gridManager.CellSize.x + gridManager.Spacing.x;
        int gridSize = gridManager.GridSize;
        float halfCellWidth = gridManager.CellSize.x / 2f;

        for (int col = 0; col < gridSize; col++)
        {
            RectTransform cellRect = gridManager.GetCellRect(rowIndex, col);
            if (cellRect != null)
            {
                Vector2 currentPosition = cellRect.anchoredPosition;
                float targetX = currentPosition.x + dragAmount;
                cellRect.anchoredPosition = new Vector2(targetX, currentPosition.y);

                // Calculate the right and left edges of the cell
                float cellRightEdge = targetX + halfCellWidth;
                float cellLeftEdge = targetX - halfCellWidth;

                // Wrap to left only after the right edge crosses the right bound
                if (cellRightEdge > rightScrollBound)
                {
                    cellRect.anchoredPosition = new Vector2(targetX - gridSize * cellWidthWithSpacing, currentPosition.y);
                }
                // Wrap to right only after the left edge crosses the left bound
                else if (cellLeftEdge < leftScrollBound)
                {
                    cellRect.anchoredPosition = new Vector2(targetX + gridSize * cellWidthWithSpacing, currentPosition.y);
                }
            }
        }
    }

    private void ScrollColumnVisual(int colIndex, float dragAmount)
    {
        // We'll implement this later
    }

    private void SnapRow(int rowIndex)
    {
        // We'll implement this later
        Debug.Log($"Snapping row {rowIndex}");
    }

    private void SnapColumn(int colIndex)
    {
        // We'll implement this later
        Debug.Log($"Snapping column {colIndex}");
    }
}