using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class SafeAreaHandler : MonoBehaviour
{
    private RectTransform panelRectTransform;
    private Rect lastSafeArea = new Rect(0, 0, 0, 0);
    private ScreenOrientation lastOrientation = ScreenOrientation.AutoRotation; // Use AutoRotation as an initial dummy value

    void Awake()
    {
        panelRectTransform = GetComponent<RectTransform>();
        ApplySafeArea(); // Apply once on awake
    }

    void Update()
    {
        // Check if safe area or orientation has changed
        if (Screen.safeArea != lastSafeArea || Screen.orientation != lastOrientation)
        {
            ApplySafeArea();
        }
    }

    void ApplySafeArea()
    {
        Rect safeArea = Screen.safeArea;

        if (safeArea != lastSafeArea || Screen.orientation != lastOrientation) // Apply only if changed
        {
            lastSafeArea = safeArea;
            lastOrientation = Screen.orientation;

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            // Convert pixel coordinates to normalized anchor points (0 to 1)
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            // Apply the anchors to the RectTransform
            panelRectTransform.anchorMin = anchorMin;
            panelRectTransform.anchorMax = anchorMax;

            Debug.Log($"Applied Safe Area: Min({anchorMin.x:F2}, {anchorMin.y:F2}) Max({anchorMax.x:F2}, {anchorMax.y:F2})");
        }
    }
}