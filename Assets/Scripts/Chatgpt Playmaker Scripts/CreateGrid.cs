using UnityEngine;
using HutongGames.PlayMaker;
using TooltipAttribute = HutongGames.PlayMaker.TooltipAttribute;

[ActionCategory("Custom Actions")]
[HutongGames.PlayMaker.Tooltip("Creates a grid of GameObjects with Square or Hexagonal placement.")]
public class CreateGrid : FsmStateAction
{
    public enum GridType { Square, Hexagonal }

    [RequiredField]
    [Tooltip("The prefab to instantiate.")]
    public FsmGameObject prefab;

    [RequiredField]
    [Tooltip("Number of rows.")]
    public FsmInt rows;

    [RequiredField]
    [Tooltip("Number of columns.")]
    public FsmInt columns;

    [Tooltip("Spacing between grid elements.")]
    public FsmVector2 spacing;

    [Tooltip("Parent object for the grid (optional).")]
    public FsmGameObject parent;

    [Tooltip("Use 2D (XY plane) or 3D (XZ plane).")]
    public FsmBool use2D;

    [Tooltip("Grid type: Square or Hexagonal.")]
    public GridType gridType = GridType.Square;

    public override void Reset()
    {
        prefab = null;
        rows = 3;
        columns = 3;
        spacing = new Vector2(1, 1);
        parent = null;
        use2D = true;
        gridType = GridType.Square;
    }

    public override void OnEnter()
    {
        if (prefab.Value == null)
        {
            Debug.LogError("Prefab is not assigned!");
            Finish();
            return;
        }

        Transform parentTransform = parent.Value != null ? parent.Value.transform : null;

        for (int row = 0; row < rows.Value; row++)
        {
            for (int col = 0; col < columns.Value; col++)
            {
                Vector3 position = CalculatePosition(row, col);

                GameObject instance = GameObject.Instantiate(prefab.Value, position, Quaternion.identity);
                if (parentTransform != null)
                {
                    instance.transform.SetParent(parentTransform);
                }
            }
        }

        Finish();
    }

    private Vector3 CalculatePosition(int row, int col)
    {
        if (gridType == GridType.Square)
        {
            return use2D.Value
                ? new Vector3(col * spacing.Value.x, row * spacing.Value.y, 0)  // 2D (XY plane)
                : new Vector3(col * spacing.Value.x, 0, row * spacing.Value.y); // 3D (XZ plane)
        }
        else // Hexagonal Grid
        {
            float hexWidth = spacing.Value.x;
            float hexHeight = spacing.Value.y * 0.866f; // 0.866 = sqrt(3)/2, to maintain hexagonal shape

            float xOffset = (row % 2 == 0) ? 0 : hexWidth * 0.5f; // Offset for staggered rows

            return use2D.Value
                ? new Vector3(col * hexWidth + xOffset, row * hexHeight, 0)  // 2D (XY)
                : new Vector3(col * hexWidth + xOffset, 0, row * hexHeight); // 3D (XZ)
        }
    }
}
