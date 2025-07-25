using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simple UI component for displaying individual letter feedback in Wordle-style gameplay
/// This script should be attached to a prefab that contains:
/// - An Image component for the background color
/// - A TextMeshProUGUI component for the letter text
/// </summary>
public class LetterFeedbackDisplay : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI letterText;
    
    [Header("Colors")]
    [SerializeField] private Color correctColor = Color.green;
    [SerializeField] private Color presentColor = Color.yellow;
    [SerializeField] private Color incorrectColor = Color.gray;
    [SerializeField] private Color textColor = Color.white;
    
    private void Awake()
    {
        // Auto-find components if not assigned
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();
            
        if (letterText == null)
            letterText = GetComponentInChildren<TextMeshProUGUI>();
    }
    
    /// <summary>
    /// Set the letter and feedback for this display
    /// </summary>
    public void SetLetterFeedback(char letter, LetterFeedback feedback)
    {
        // Set letter text
        if (letterText != null)
        {
            letterText.text = letter.ToString().ToUpper();
            letterText.color = textColor;
        }
        
        // Set background color based on feedback
        if (backgroundImage != null)
        {
            backgroundImage.color = GetColorForFeedback(feedback);
        }
    }
    
    /// <summary>
    /// Get the appropriate color for the given feedback type
    /// </summary>
    private Color GetColorForFeedback(LetterFeedback feedback)
    {
        switch (feedback)
        {
            case LetterFeedback.Correct:
                return correctColor;
            case LetterFeedback.Present:
                return presentColor;
            case LetterFeedback.None:
            default:
                return incorrectColor;
        }
    }
    
    /// <summary>
    /// Manually set colors (useful for customization)
    /// </summary>
    public void SetColors(Color correct, Color present, Color incorrect, Color text)
    {
        correctColor = correct;
        presentColor = present;
        incorrectColor = incorrect;
        textColor = text;
    }
}
