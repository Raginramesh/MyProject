using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TileData
{
    public Vector2Int Coordinates { get; private set; }
    public char Letter { get; private set; } // The actual placed letter
    public bool IsOccupied => Letter != '\0' && Letter != ' '; // Or your definition of empty

    public GameObject VisualTile { get; private set; } // Reference to the instantiated tile GameObject

    // Visual Components (fetched from VisualTile)
    private Image backgroundImage;
    private TextMeshProUGUI mainTextComponent; // For displaying the actual or preview letter

    // --- Preview State ---
    public bool IsPreviewed { get; private set; }
    public char PreviewLetterChar { get; private set; }
    public bool IsPreviewPlacementValid { get; private set; }

    // --- Configurable Colors (Consider moving to a Theme/Settings script for easier changes) ---
    private Color defaultTileBackgroundColor = Color.white; // Example: light gray
    private Color previewValidTileColor = new Color(0.8f, 1f, 0.8f, 1f); // Light green
    private Color previewInvalidTileColor = new Color(1f, 0.8f, 0.8f, 1f); // Light red
    private Color defaultLetterColor = Color.black;
    private Color previewLetterColor = new Color(0.2f, 0.2f, 0.2f, 0.7f); // Dark semi-transparent

    public TileData(Vector2Int coordinates, GameObject visualTileInstance)
    {
        Coordinates = coordinates;
        VisualTile = visualTileInstance;
        Letter = '\0'; // Initialize as empty

        if (VisualTile != null)
        {
            backgroundImage = VisualTile.GetComponent<Image>();
            // GetComponentInChildren<TextMeshProUGUI>(true) ensures we find it even if the child object holding text is initially inactive.
            mainTextComponent = VisualTile.GetComponentInChildren<TextMeshProUGUI>(true);

            if (backgroundImage != null)
            {
                defaultTileBackgroundColor = backgroundImage.color; // Store initial color from prefab
            }
            else
            {
                Debug.LogWarning($"TileData for {coordinates}: VisualTile '{VisualTile.name}' is missing an Image component.");
            }

            if (mainTextComponent == null)
            {
                Debug.LogWarning($"TileData for {coordinates}: VisualTile '{VisualTile.name}' is MISSING a TextMeshProUGUI child component. Text will not appear.");
            }
            else
            {
                Debug.Log($"TileData for {coordinates}: TextMeshProUGUI component found on child: {mainTextComponent.gameObject.name}");
                if (mainTextComponent.font == null)
                {
                    Debug.LogError($"TileData for {coordinates}: TextMeshProUGUI component on '{mainTextComponent.gameObject.name}' is MISSING a Font Asset!");
                }
            }
        }
        else
        {
            Debug.LogError($"TileData for {coordinates}: visualTileInstance was NULL!");
        }
        ResetTile(); // Ensure it starts visually and logically empty
    }

    // Call this to set the actual, confirmed letter on the tile
    public void SetPlacedLetter(char newLetter)
    {
        Debug.Log($"TileData ({Coordinates}): SetPlacedLetter CALLED with '{newLetter}'. Previous letter: '{Letter}'. Is mainTextComponent null? {mainTextComponent == null}");
        Letter = char.ToUpper(newLetter);
        IsPreviewed = false; // Clear any preview state
        UpdateVisuals();
    }

    // Call this to show a temporary preview letter
    public void SetPreviewState(char previewChar, bool isValidPlacement)
    {
        IsPreviewed = true;
        PreviewLetterChar = char.ToUpper(previewChar);
        IsPreviewPlacementValid = isValidPlacement;
        UpdateVisuals();
    }

    // Call this to remove the temporary preview
    public void ClearPreviewState()
    {
        if (!IsPreviewed) return; // No need to update if not in preview mode
        IsPreviewed = false;
        PreviewLetterChar = '\0';
        UpdateVisuals();
    }

    // Call this to reset the tile to its initial empty state (e.g., for new level or grid creation)
    public void ResetTile()
    {
        // Debug.Log($"TileData ({Coordinates}): ResetTile CALLED."); // Can be noisy if called for every tile on grid creation
        Letter = '\0';
        IsPreviewed = false;
        PreviewLetterChar = '\0';
        UpdateVisuals();
    }

    // Central method to update the tile's appearance based on its current state
    public void UpdateVisuals()
    {
        // This log can be very noisy, enable only if specifically debugging UpdateVisuals calls.
        // Debug.Log($"TileData ({Coordinates}): UpdateVisuals CALLED. IsPreviewed: {IsPreviewed}, Letter: '{Letter}', IsOccupied: {IsOccupied}");

        if (VisualTile == null)
        {
            Debug.LogError($"TileData ({Coordinates}): UpdateVisuals - VisualTile is NULL. Cannot update visuals.");
            return;
        }

        // Handle background color
        if (backgroundImage != null)
        {
            if (IsPreviewed)
            {
                backgroundImage.color = IsPreviewPlacementValid ? previewValidTileColor : previewInvalidTileColor;
            }
            else
            {
                backgroundImage.color = defaultTileBackgroundColor; // Revert to default tile color
            }
        }

        // Handle text
        if (mainTextComponent != null)
        {
            if (IsPreviewed)
            {
                mainTextComponent.text = PreviewLetterChar.ToString();
                mainTextComponent.color = previewLetterColor;
                mainTextComponent.gameObject.SetActive(PreviewLetterChar != '\0' && PreviewLetterChar != ' '); // Show only if there's a preview char
            }
            else // Not in preview mode, show actual placed letter or empty
            {
                if (IsOccupied)
                {
                    // Debug.Log($"TileData ({Coordinates}): UpdateVisuals - IsOccupied. Setting text to '{Letter}'. Current TMP text: '{mainTextComponent.text}'");
                    mainTextComponent.text = Letter.ToString();
                    mainTextComponent.color = defaultLetterColor;
                    mainTextComponent.gameObject.SetActive(true); // Ensure text object is active
                }
                else
                {
                    // Debug.Log($"TileData ({Coordinates}): UpdateVisuals - Not Occupied. Clearing text. Current TMP text: '{mainTextComponent.text}'");
                    mainTextComponent.text = ""; // Empty
                    // Decide if empty text objects should be active or inactive. Usually active but empty is fine.
                    mainTextComponent.gameObject.SetActive(true);
                }
            }
        }
        else if (IsOccupied || (IsPreviewed && PreviewLetterChar != '\0')) // Log warning only if we expected to show text
        {
            Debug.LogWarning($"TileData ({Coordinates}): UpdateVisuals - mainTextComponent is NULL when trying to display a letter ('{(IsPreviewed ? PreviewLetterChar : Letter)}').");
        }
    }
}