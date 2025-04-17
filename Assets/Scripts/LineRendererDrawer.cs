using UnityEngine;

public class LineRendererDrawer : MonoBehaviour
{
    public LineRenderer lineRenderer;
    public Camera mainCamera;
    public bool isDrawing = false;

    void Start()
    {
        // Get or add LineRenderer component
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();

        // Configure LineRenderer properties
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;
        lineRenderer.positionCount = 0;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.red;
        lineRenderer.endColor = Color.red;

        mainCamera = Camera.main;
    }

    void Update()
    {
        // Detect if the user is touching or clicking
#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0)) isDrawing = true;
        if (Input.GetMouseButtonUp(0)) isDrawing = false;
#else
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began) isDrawing = true;
            if (touch.phase == TouchPhase.Ended) isDrawing = false;
        }
#endif

        // Perform raycast and draw line
        if (isDrawing)
        {
            Vector3 worldPoint = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector2 touchPosition = new Vector2(worldPoint.x, worldPoint.y);

            RaycastHit2D hit = Physics2D.Raycast(touchPosition, Vector2.zero);
            if (hit.collider != null)
            {
                AddPoint(hit.point);
                Debug.Log(hit.distance);
            }
        }
    }

    private void AddPoint(Vector3 point)
    {
        // Add a new point to the line
        lineRenderer.positionCount++;
        lineRenderer.SetPosition(lineRenderer.positionCount - 1, point);
    }
}
