using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Handles UI feedback for Wordle-style target word discovery
/// </summary>
public class TargetWordFeedbackUI : MonoBehaviour
{
    [Header("Target Word Display")]
    [SerializeField] private TextMeshProUGUI targetWordText;
    
    [Header("Letter Discovery Settings")]
    [SerializeField] private bool enableLetterDiscovery = true;
    [Tooltip("When enabled, dominant words will start as dashes and reveal letters as they are discovered. When disabled, shows the full word immediately.")]
    
    [Header("Letter Feedback Display")]
    [SerializeField] private Transform letterFeedbackContainer;
    [SerializeField] private GameObject letterFeedbackPrefab; // Prefab for individual letter feedback
    
    [Header("Animation Settings")]
    [SerializeField] private float letterAnimationDelay = 0.1f;
    [SerializeField] private float feedbackDisplayDuration = 2.0f;
    
    [Header("Colors")]
    [SerializeField] private Color correctLetterColor = Color.green;
    [SerializeField] private Color presentLetterColor = Color.yellow;
    [SerializeField] private Color incorrectLetterColor = Color.gray;
    [SerializeField] private Color targetWordFoundColor = Color.green;
    
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip targetWordFoundSound;
    [SerializeField] private AudioClip letterRevealSound;
    
    private Coroutine currentFeedbackAnimation;
    private bool isAnimating = false;
    
    public bool IsAnimating => isAnimating;
    public bool IsLetterDiscoveryEnabled => enableLetterDiscovery;
    
    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }
    
    /// <summary>
    /// Shows feedback for a target word that was found
    /// </summary>
    public void ShowTargetWordFound(string word)
    {
        if (isAnimating)
        {
            Debug.LogWarning("TargetWordFeedbackUI: Already showing feedback");
            return;
        }
        
        if (currentFeedbackAnimation != null)
            StopCoroutine(currentFeedbackAnimation);
            
        currentFeedbackAnimation = StartCoroutine(AnimateTargetWordFound(word));
    }
    
    /// <summary>
    /// Shows letter feedback for a word validation
    /// </summary>
    public void ShowLetterFeedback(string word, LetterFeedback[] feedbacks)
    {
        if (isAnimating)
        {
            Debug.LogWarning("TargetWordFeedbackUI: Already showing feedback");
            return;
        }
        
        if (currentFeedbackAnimation != null)
            StopCoroutine(currentFeedbackAnimation);
            
        currentFeedbackAnimation = StartCoroutine(AnimateLetterFeedback(word, feedbacks));
    }
    
    /// <summary>
    /// Shows overall progress update for Wordle-style levels
    /// </summary>
    public void ShowProgressUpdate(int foundCount, int totalCount)
    {
        // Simple progress log without UI interference
        Debug.Log($"🎯 PROGRESS: {foundCount}/{totalCount} target words found");
    }
    
    private IEnumerator AnimateTargetWordFound(string word)
    {
        isAnimating = true;
        
        // Set up display
        targetWordText.text = word.ToUpper();
        targetWordText.color = targetWordFoundColor;
        
        // Simple scale animation for target word text
        if (targetWordText != null)
        {
            targetWordText.transform.localScale = Vector3.one * 0.8f;
            targetWordText.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
        }
        
        // Play sound
        PlayTargetWordFoundSound();
        
        // Hold display
        yield return new WaitForSeconds(feedbackDisplayDuration);
        
        isAnimating = false;
    }
    
    private IEnumerator AnimateLetterFeedback(string word, LetterFeedback[] feedbacks)
    {
        isAnimating = true;
        
        // Clear any existing letter feedback
        ClearLetterFeedback();
        
        // Set up display
        targetWordText.text = word.ToUpper();
        targetWordText.color = Color.white;
        
        // Create and animate letter feedback
        yield return StartCoroutine(AnimateLetterByLetter(word, feedbacks));
        
        // Hold display
        yield return new WaitForSeconds(feedbackDisplayDuration);
        
        isAnimating = false;
    }
    
    private IEnumerator AnimateLetterByLetter(string word, LetterFeedback[] feedbacks)
    {
        if (letterFeedbackContainer == null || letterFeedbackPrefab == null)
            yield break;
        
        for (int i = 0; i < word.Length && i < feedbacks.Length; i++)
        {
            // Create letter feedback UI element
            GameObject letterObj = Instantiate(letterFeedbackPrefab, letterFeedbackContainer);
            
            // Try to use the LetterFeedbackDisplay component first
            var letterFeedbackDisplay = letterObj.GetComponent<LetterFeedbackDisplay>();
            if (letterFeedbackDisplay != null)
            {
                letterFeedbackDisplay.SetLetterFeedback(word[i], feedbacks[i]);
            }
            else
            {
                // Fallback to manual setup if LetterFeedbackDisplay component is not present
                var letterText = letterObj.GetComponent<TextMeshProUGUI>();
                var letterImage = letterObj.GetComponent<Image>();
                
                if (letterText != null)
                {
                    letterText.text = word[i].ToString().ToUpper();
                }
                
                if (letterImage != null)
                {
                    letterImage.color = GetColorForFeedback(feedbacks[i]);
                }
            }
            
            // Animate letter appearance
            letterObj.transform.localScale = Vector3.zero;
            letterObj.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBounce);
            
            // Play sound
            PlayLetterRevealSound();
            
            yield return new WaitForSeconds(letterAnimationDelay);
        }
    }
    
    private Color GetColorForFeedback(LetterFeedback feedback)
    {
        switch (feedback)
        {
            case LetterFeedback.Correct:
                return correctLetterColor;
            case LetterFeedback.Present:
                return presentLetterColor;
            case LetterFeedback.None:
            default:
                return incorrectLetterColor;
        }
    }
    
    private void ClearLetterFeedback()
    {
        if (letterFeedbackContainer == null) return;
        
        foreach (Transform child in letterFeedbackContainer)
        {
            if (child != null)
                Destroy(child.gameObject);
        }
    }
    
    private void PlayTargetWordFoundSound()
    {
        if (audioSource != null && targetWordFoundSound != null)
        {
            audioSource.PlayOneShot(targetWordFoundSound);
        }
    }
    
    private void PlayLetterRevealSound()
    {
        if (audioSource != null && letterRevealSound != null)
        {
            audioSource.PlayOneShot(letterRevealSound);
        }
    }

    /// <summary>
    /// Shows the current dominant word (called from GameManager)
    /// </summary>
    public void ShowDominantWord(string dominantWord)
    {
        if (targetWordText == null) return;
        
        Debug.Log($"🎯 DOMINANT DISPLAY: Showing '{dominantWord}' (Discovery: {enableLetterDiscovery})");
        
        // Check if letter discovery is enabled
        if (enableLetterDiscovery)
        {
            // Use discovery system - GameManager will call ShowDominantWordWithDiscovery instead
            return;
        }
        
        // Letter discovery is disabled - hide the display completely
        HideDominantWord();
    }

    /// <summary>
    /// Shows the dominant word with letter discovery state (called from GameManager)
    /// </summary>
    public void ShowDominantWordWithDiscovery(string displayText)
    {
        if (targetWordText == null) return;
        
        Debug.Log($"🎯 DOMINANT DISCOVERY: Showing '{displayText}'");
        
        // Set the text with discovery state (includes dashes and spaces)
        targetWordText.text = string.IsNullOrEmpty(displayText) ? "" : displayText;
        
        // Ensure the target word text is visible
        if (!string.IsNullOrEmpty(displayText))
        {
            targetWordText.color = new Color(targetWordText.color.r, targetWordText.color.g, targetWordText.color.b, 1f);
            
            // Simple fade in animation with a subtle scale effect
            targetWordText.transform.localScale = Vector3.one * 0.9f;
            targetWordText.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutQuad);
            targetWordText.DOFade(1f, 0.2f);
        }
    }

    /// <summary>
    /// Hides the dominant word display (called from GameManager)
    /// </summary>
    public void HideDominantWord()
    {
        if (targetWordText == null) return;
        
        Debug.Log($"🎯 DOMINANT DISPLAY: Hiding dominant word");
        
        // Simple fade out animation
        targetWordText.DOFade(0f, 0.15f).OnComplete(() => {
            targetWordText.text = "";
        });
    }
}
