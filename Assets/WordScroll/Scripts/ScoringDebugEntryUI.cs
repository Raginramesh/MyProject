using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// UI component for displaying individual debug entries in the scoring debug panel
/// </summary>
public class ScoringDebugEntryUI : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI timestampText;
    [SerializeField] private TextMeshProUGUI detailsText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Button expandButton;
    [SerializeField] private GameObject detailsPanel;
    [SerializeField] private Image iconImage;
    
    [Header("Expand/Collapse")]
    [SerializeField] private RectTransform detailsRect;
    [SerializeField] private float expandDuration = 0.3f;
    
    [Header("Icons")]
    [SerializeField] private Sprite scoringIcon;
    [SerializeField] private Sprite letterIcon;
    [SerializeField] private Sprite modifierIcon;
    [SerializeField] private Sprite errorIcon;
    [SerializeField] private Sprite warningIcon;
    [SerializeField] private Sprite infoIcon;
    
    private ScoringDebugEntry currentEntry;
    private bool isExpanded = false;
    private float collapsedHeight;
    private float expandedHeight;
    
    private void Awake()
    {
        // Setup expand button
        if (expandButton != null)
        {
            expandButton.onClick.AddListener(ToggleExpanded);
        }
        
        // Start collapsed
        if (detailsPanel != null)
        {
            detailsPanel.SetActive(false);
        }
    }
    
    public void Setup(ScoringDebugEntry entry, Color backgroundColor)
    {
        currentEntry = entry;
        
        // Set title
        if (titleText != null)
        {
            titleText.text = entry.title;
        }
        
        // Set timestamp
        if (timestampText != null)
        {
            timestampText.text = entry.timestamp.ToString("HH:mm:ss");
        }
        
        // Set details
        if (detailsText != null)
        {
            detailsText.text = entry.details;
        }
        
        // Set background color
        if (backgroundImage != null)
        {
            backgroundColor.a = 0.1f; // Make it subtle
            backgroundImage.color = backgroundColor;
        }
        
        // Set icon
        if (iconImage != null)
        {
            iconImage.sprite = GetIconForEntryType(entry.entryType);
            iconImage.color = backgroundColor;
        }
        
        // Calculate heights
        CalculateHeights();
    }
    
    private void CalculateHeights()
    {
        Canvas.ForceUpdateCanvases();
        
        // Get collapsed height (just title and timestamp)
        collapsedHeight = titleText != null ? titleText.preferredHeight + 40f : 60f;
        
        // Get expanded height (include details)
        if (detailsText != null)
        {
            expandedHeight = collapsedHeight + detailsText.preferredHeight + 20f;
        }
        else
        {
            expandedHeight = collapsedHeight + 100f;
        }
        
        // Set initial height
        var rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, collapsedHeight);
        }
    }
    
    private Sprite GetIconForEntryType(ScoringDebugEntry.EntryType entryType)
    {
        return entryType switch
        {
            ScoringDebugEntry.EntryType.ScoringBreakdown => scoringIcon,
            ScoringDebugEntry.EntryType.LetterBreakdown => letterIcon,
            ScoringDebugEntry.EntryType.ModifierInfo => modifierIcon,
            ScoringDebugEntry.EntryType.Error => errorIcon,
            ScoringDebugEntry.EntryType.Warning => warningIcon,
            ScoringDebugEntry.EntryType.Info => infoIcon,
            _ => infoIcon
        };
    }
    
    public void ToggleExpanded()
    {
        isExpanded = !isExpanded;
        
        // Show/hide details panel
        if (detailsPanel != null)
        {
            detailsPanel.SetActive(isExpanded);
        }
        
        // Animate height change
        var rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            float targetHeight = isExpanded ? expandedHeight : collapsedHeight;
            
            rectTransform.DOSizeDelta(new Vector2(rectTransform.sizeDelta.x, targetHeight), expandDuration)
                .SetEase(Ease.OutQuad);
        }
        
        // Rotate expand button
        if (expandButton != null)
        {
            float targetRotation = isExpanded ? 180f : 0f;
            expandButton.transform.DORotate(new Vector3(0, 0, targetRotation), expandDuration);
        }
        
        // Update layout
        StartCoroutine(RefreshLayoutDelayed());
    }
    
    private System.Collections.IEnumerator RefreshLayoutDelayed()
    {
        yield return new WaitForSeconds(expandDuration + 0.1f);
        
        // Force layout rebuild
        var layoutGroup = GetComponentInParent<VerticalLayoutGroup>();
        if (layoutGroup != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(layoutGroup.GetComponent<RectTransform>());
        }
    }
    
    /// <summary>
    /// Highlight this entry briefly
    /// </summary>
    public void Highlight()
    {
        if (backgroundImage != null)
        {
            Color originalColor = backgroundImage.color;
            Color highlightColor = originalColor;
            highlightColor.a = 0.3f;
            
            backgroundImage.DOColor(highlightColor, 0.2f)
                .OnComplete(() => backgroundImage.DOColor(originalColor, 0.5f));
        }
    }
    
    /// <summary>
    /// Get the entry data
    /// </summary>
    public ScoringDebugEntry GetEntry()
    {
        return currentEntry;
    }
}
