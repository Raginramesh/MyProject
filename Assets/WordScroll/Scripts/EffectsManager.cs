using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using System;

public class EffectsManager : MonoBehaviour
{
    [Header("Prefabs & Targets")]
    [SerializeField] private GameObject flyingLetterPrefab;
    [SerializeField] private RectTransform scoreTargetRectTransform;

    [Header("Animation Parameters - General")]
    [SerializeField] private float flyingLetterInitialScale = 1.0f;
    [SerializeField] private int activelyFlyingLetterSortOrder = 100; // NEW: For Canvas sorting

    [Header("Animation Parameters - Lift Off (Global or Per Word)")] // Renamed header for clarity
    [SerializeField] private float liftOffDistance = 0.2f;
    [SerializeField] private float liftOffDuration = 0.15f;
    [SerializeField] private Ease liftOffEase = Ease.OutQuad;

    [Header("Animation Parameters - Flying to Score (Per Letter)")] // Renamed header
    [SerializeField] private float flyToScoreDurationPerLetter = 0.5f;
    [SerializeField] private Ease flyToScoreEase = Ease.InOutQuad;
    [SerializeField] private float scaleDownFactorDuringFly = 0.5f;
    [SerializeField] private float delayBetweenLetterFlights = 0.1f;

    [Header("Parenting")]
    [SerializeField] private Transform flyingLetterParent;

    public bool IsAnimating { get; private set; } = false;

    void Awake()
    {
        if (flyingLetterPrefab == null)
        {
            Debug.LogError("EffectsManager: FlyingLetterPrefab is not set!", this);
        }
        if (scoreTargetRectTransform == null)
        {
            Debug.LogError("EffectsManager: ScoreTargetRectTransform is not set!", this);
        }
        if (flyingLetterParent == null)
        {
            flyingLetterParent = transform;
        }
    }

    public List<GameObject> SpawnAndFloatLetterPrefabs(List<RectTransform> sourceCellRects, string wordString)
    {
        if (sourceCellRects == null || sourceCellRects.Count != wordString.Length)
        {
            Debug.LogError("EffectsManager.SpawnAndFloat: Mismatch between sourceCellRects and wordString length or null input.");
            return new List<GameObject>();
        }

        List<GameObject> spawnedFloatingPrefabs = new List<GameObject>();

        for (int i = 0; i < wordString.Length; i++)
        {
            if (sourceCellRects[i] == null)
            {
                Debug.LogWarning($"EffectsManager.SpawnAndFloat: Null RectTransform at index {i} for word '{wordString}'. Skipping this letter.");
                continue;
            }

            GameObject instance = Instantiate(flyingLetterPrefab, flyingLetterParent);
            instance.transform.position = sourceCellRects[i].position;
            instance.transform.localScale = Vector3.one * flyingLetterInitialScale;

            TextMeshProUGUI letterText = instance.GetComponentInChildren<TextMeshProUGUI>();
            if (letterText != null)
            {
                letterText.text = wordString[i].ToString();
            }
            else
            {
                Debug.LogWarning("EffectsManager.SpawnAndFloat: FlyingLetterPrefab is missing TextMeshProUGUI component in children.", instance);
            }

            CanvasGroup cg = instance.GetComponent<CanvasGroup>();
            if (cg == null) cg = instance.AddComponent<CanvasGroup>();
            cg.alpha = 1f;

            // Ensure prefab has a Canvas for sorting later, disable override initially
            Canvas letterCanvas = instance.GetComponent<Canvas>();
            if (letterCanvas == null) letterCanvas = instance.AddComponent<Canvas>();
            letterCanvas.overrideSorting = false; // Will be enabled when it's the *actively* flying letter

            spawnedFloatingPrefabs.Add(instance);
        }
        return spawnedFloatingPrefabs;
    }

    /// <summary>
    /// Performs a simultaneous lift-off animation for all provided prefabs.
    /// Sets IsAnimating flag for the duration of this group lift-off.
    /// </summary>
    public IEnumerator PerformGlobalLiftOff(List<GameObject> allPrefabsToLift)
    {
        if (allPrefabsToLift == null || allPrefabsToLift.Count == 0 || !(liftOffDistance > 0 && liftOffDuration > 0))
        {
            if (!(liftOffDistance > 0 && liftOffDuration > 0))
            {
                // Only log if it was intended but params are zero, not if list is empty.
                // Debug.Log("EffectsManager.PerformGlobalLiftOff: Lift-off distance or duration is zero, skipping.");
            }
            yield break; // No lift-off to perform
        }

        IsAnimating = true;
        Debug.Log($"EffectsManager: Starting Global Lift-Off for {allPrefabsToLift.Count} prefabs.");

        Sequence masterLiftOffSequence = DOTween.Sequence();

        foreach (GameObject letterInstance in allPrefabsToLift)
        {
            if (letterInstance == null) continue;

            DOTween.Kill(letterInstance.transform, complete: false); // Kill any prior tweens

            Vector3 currentPos = letterInstance.transform.position;
            Vector3 upTargetPos = new Vector3(currentPos.x, currentPos.y + liftOffDistance, currentPos.z);

            masterLiftOffSequence.Insert(0, letterInstance.transform.DOMove(upTargetPos, liftOffDuration).SetEase(liftOffEase));
        }

        if (masterLiftOffSequence.IsActive() && masterLiftOffSequence.IsPlaying())
        {
            yield return masterLiftOffSequence.WaitForCompletion();
        }

        Debug.Log("EffectsManager: Global Lift-Off Finished.");
        IsAnimating = false;
    }


    /// <summary>
    /// Coroutine that takes a list of prefabs (assumed to be already lifted if lift-off was performed)
    /// and animates them to score ONE BY ONE. Each flying letter gets top sort order.
    /// Sets IsAnimating flag for the duration of this word's letters flying.
    /// </summary>
    public IEnumerator FlyPrefabsToScoreSequentially(List<GameObject> letterPrefabs, List<int> individualLetterScores, Action<int> scoreCallback)
    {
        if (letterPrefabs == null || letterPrefabs.Count == 0)
        {
            Debug.LogWarning("EffectsManager.FlyPrefabsToScoreSequentially: No prefabs provided.");
            yield break;
        }
        if (scoreTargetRectTransform == null)
        {
            Debug.LogError("EffectsManager.FlyPrefabsToScoreSequentially: ScoreTargetRectTransform is null.");
            foreach (var prefab in letterPrefabs) { if (prefab != null) Destroy(prefab); }
            yield break;
        }

        IsAnimating = true;
        Vector3 scoreTargetWorldPosition = scoreTargetRectTransform.position;

        for (int i = 0; i < letterPrefabs.Count; i++)
        {
            GameObject letterInstance = letterPrefabs[i];
            if (letterInstance == null)
            {
                Debug.LogWarning($"EffectsManager.FlyPrefabsToScoreSequentially: Null prefab in list at index {i}. Skipping.");
                continue;
            }

            // Ensure any prior tweens are killed (e.g., if lift-off didn't run or was interrupted)
            DOTween.Kill(letterInstance.transform, complete: false);

            // Set Canvas sorting for top-most rendering
            Canvas letterCanvas = letterInstance.GetComponent<Canvas>(); // Should exist from SpawnAndFloat
            if (letterCanvas != null)
            {
                letterCanvas.overrideSorting = true;
                letterCanvas.sortingOrder = activelyFlyingLetterSortOrder;
            }
            else
            {
                Debug.LogWarning("EffectsManager: Flying letter prefab missing Canvas component for sorting.", letterInstance);
            }

            int scoreForThisLetter = (individualLetterScores != null && i < individualLetterScores.Count) ? individualLetterScores[i] : 0;
            Vector3 targetFlyScale = Vector3.one * flyingLetterInitialScale * scaleDownFactorDuringFly;

            Sequence flySequence = DOTween.Sequence();

            flySequence.Append(letterInstance.transform.DOMove(scoreTargetWorldPosition, flyToScoreDurationPerLetter).SetEase(flyToScoreEase))
                       .Join(letterInstance.transform.DOScale(targetFlyScale, flyToScoreDurationPerLetter * 0.8f).SetEase(Ease.InQuad));

            flySequence.OnComplete(() => {
                scoreCallback?.Invoke(scoreForThisLetter);
                // Canvas sorting will be gone when destroyed. If it were to return to pool, we'd reset it here.
                if (letterInstance != null) Destroy(letterInstance);
            });

            yield return flySequence.WaitForCompletion();

            if (i < letterPrefabs.Count - 1 && delayBetweenLetterFlights > 0)
            {
                yield return new WaitForSeconds(delayBetweenLetterFlights);
            }
        }

        IsAnimating = false;
    }

    public void ClearAllFloatingLetters(Dictionary<System.Guid, List<GameObject>> wordToFloatingPrefabsMap)
    {
        if (wordToFloatingPrefabsMap == null) return;
        foreach (var kvp in wordToFloatingPrefabsMap)
        {
            if (kvp.Value != null)
            {
                foreach (var prefab in kvp.Value)
                {
                    if (prefab != null)
                    {
                        DOTween.Kill(prefab.transform);
                        Destroy(prefab);
                    }
                }
            }
        }
        wordToFloatingPrefabsMap.Clear();
        if (IsAnimating)
        {
            Debug.LogWarning("EffectsManager.ClearAllFloatingLetters: Resetting IsAnimating flag.");
            IsAnimating = false;
        }
    }
}