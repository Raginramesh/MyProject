using UnityEngine;
using HutongGames.PlayMaker;
using TooltipAttribute = HutongGames.PlayMaker.TooltipAttribute;


[ActionCategory("Custom Actions")]
[Tooltip("Scrolls a list of 2D assets vertically based on swipe input.")]
public class ScrollVerticalSwipe : FsmStateAction
{
    [ArrayEditor(VariableType.GameObject)]
    [Tooltip("List of GameObjects to scroll.")]
    public FsmArray assets;

    [Tooltip("Sensitivity of swipe detection.")]
    public FsmFloat swipeSensitivity;

    [Tooltip("Enable looping (objects reappear at the start when they go out of view).")]
    public FsmBool loop;

    [Tooltip("Y position threshold to reset objects when looping.")]
    public FsmFloat resetThreshold;

    private Vector2 startTouchPosition;
    private Vector2 endTouchPosition;
    private float scrollSpeed;

    public override void Reset()
    {
        assets = null;
        swipeSensitivity = 0.5f;
        loop = true;
        resetThreshold = -10f;
        scrollSpeed = 0;
    }

    public override void OnUpdate()
    {
        DetectSwipe();

        foreach (var obj in assets.Values)
        {
            GameObject asset = obj as GameObject;
            if (asset != null)
            {
                asset.transform.position += Vector3.up * scrollSpeed * Time.deltaTime;

                if (loop.Value && asset.transform.position.y >= resetThreshold.Value)
                {
                    ResetPosition(asset);
                }
            }
        }

        // Gradually slow down scrolling for a smooth feel
        scrollSpeed *= 0.95f;
    }

    private void DetectSwipe()
    {
        if (Input.GetMouseButtonDown(0))
        {
            startTouchPosition = Input.mousePosition;
        }

        if (Input.GetMouseButton(0))
        {
            endTouchPosition = Input.mousePosition;
            float deltaY = (endTouchPosition.y - startTouchPosition.y) * swipeSensitivity.Value;
            scrollSpeed = deltaY * 0.1f;
            startTouchPosition = endTouchPosition; // Update for smooth tracking
        }
    }

    private void ResetPosition(GameObject asset)
    {
        float minY = float.MaxValue;

        foreach (var obj in assets.Values)
        {
            GameObject otherAsset = obj as GameObject;
            if (otherAsset != null)
            {
                minY = Mathf.Min(minY, otherAsset.transform.position.y);
            }
        }

        asset.transform.position = new Vector3(asset.transform.position.x, minY - Mathf.Abs(resetThreshold.Value), asset.transform.position.z);
    }
}
