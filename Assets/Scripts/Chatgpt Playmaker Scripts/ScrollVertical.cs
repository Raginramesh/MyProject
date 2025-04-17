using UnityEngine;
using HutongGames.PlayMaker;
using TooltipAttribute = HutongGames.PlayMaker.TooltipAttribute;

[ActionCategory("Custom Actions")]
[Tooltip("Scrolls a list of 2D assets vertically at a given speed.")]
public class ScrollVertical : FsmStateAction
{
    [ArrayEditor(VariableType.GameObject)]
    [Tooltip("List of GameObjects to scroll.")]
    public FsmArray assets;

    [RequiredField]
    [Tooltip("Scroll speed (positive for up, negative for down).")]
    public FsmFloat scrollSpeed;

    [Tooltip("Enable looping (objects reappear at the start when they go out of view).")]
    public FsmBool loop;

    [Tooltip("Y position threshold to reset objects when looping.")]
    public FsmFloat resetThreshold;

    public override void Reset()
    {
        assets = null;
        scrollSpeed = 2f;
        loop = true;
        resetThreshold = -10f;
    }

    public override void OnUpdate()
    {
        foreach (var obj in assets.Values)
        {
            GameObject asset = obj as GameObject;
            if (asset != null)
            {
                asset.transform.position += Vector3.up * scrollSpeed.Value * Time.deltaTime;

                if (loop.Value && asset.transform.position.y >= resetThreshold.Value)
                {
                    ResetPosition(asset);
                }
            }
        }
    }

    private void ResetPosition(GameObject asset)
    {
        float minY = float.MaxValue;

        // Find the lowest positioned asset
        foreach (var obj in assets.Values)
        {
            GameObject otherAsset = obj as GameObject;
            if (otherAsset != null)
            {
                minY = Mathf.Min(minY, otherAsset.transform.position.y);
            }
        }

        // Move asset below the lowest asset
        asset.transform.position = new Vector3(asset.transform.position.x, minY - Mathf.Abs(resetThreshold.Value), asset.transform.position.z);
    }
}
