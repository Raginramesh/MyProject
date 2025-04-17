using UnityEngine;
using HutongGames.PlayMaker;
using TooltipAttribute = HutongGames.PlayMaker.TooltipAttribute;

[ActionCategory("Custom Actions")]
[Tooltip("Spawns game objects in a circular pattern based on the number of objects.")]
public class SpawnObjectsInCircle : FsmStateAction
{
    [RequiredField]
    [Tooltip("The prefab to spawn.")]
    public FsmGameObject prefab;

    [RequiredField]
    [Tooltip("The center position of the circle.")]
    public FsmVector3 center;

    [RequiredField]
    [Tooltip("The radius of the circle.")]
    public FsmFloat radius;

    [RequiredField]
    [Tooltip("The number of objects to spawn.")]
    public FsmInt objectCount;

    [Tooltip("The rotation offset for spawned objects.")]
    public FsmFloat rotationOffset;

    [Tooltip("Parent object for spawned objects.")]
    public FsmGameObject parent;

    public override void Reset()
    {
        prefab = null;
        center = Vector3.zero;
        radius = 5f;
        objectCount = 8;
        rotationOffset = 0f;
        parent = null;
    }

    public override void OnEnter()
    {
        SpawnObjects();
        Finish();
    }

    private void SpawnObjects()
    {
        if (prefab.Value == null)
        {
            Debug.LogError("Prefab is null! Assign a valid prefab.");
            return;
        }

        float angleStep = 360f / objectCount.Value;

        for (int i = 0; i < objectCount.Value; i++)
        {
            float angle = i * angleStep + rotationOffset.Value;
            float radian = angle * Mathf.Deg2Rad;
            Vector3 spawnPosition = new Vector3(
                center.Value.x + radius.Value * Mathf.Cos(radian),
                center.Value.y,
                center.Value.z + radius.Value * Mathf.Sin(radian)
            );

            GameObject newObject = GameObject.Instantiate(prefab.Value, spawnPosition, Quaternion.identity);
            if (parent.Value != null)
            {
                newObject.transform.parent = parent.Value.transform;
            }
        }
    }
}
