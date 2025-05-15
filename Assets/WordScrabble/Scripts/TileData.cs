using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TileData
{
    public Vector2Int Coordinates { get; private set; }
    public char Letter { get; private set; }
    public bool IsOccupied => Letter != '\0' && Letter != ' ';

    public GameObject VisualTile { get; private set; }
    private Image backgroundImage;
    private TextMeshProUGUI mainTextComponent;

    public bool IsPreviewed { get; private set; }
    public char PreviewLetterChar { get; private set; }
    public bool IsPreviewPlacementValid { get; private set; }

    // Color fields
    private Color _initialPrefabBackgroundColor; // True color from the prefab
    private Color previewValidTileColor = new Color(0.8f, 1f, 0.8f, 1f);
    private Color previewInvalidTileColor = new Color(1f, 0.8f, 0.8f, 1f);
    private Color defaultLetterColor = Color.black;
    private Color previewLetterColor = new Color(0.2f, 0.2f, 0.2f, 0.7f);


    // Center Tile specific
    public bool IsDesignatedCenterTile { get; private set; } = false;
    private Color _designatedCenterTileColor;

    // Alternate cell color support
    private Color _defaultCellColor;


    public TileData(Vector2Int coordinates, GameObject visualTileInstance, Color defaultCellColor)
    {
        Coordinates = coordinates;
        VisualTile = visualTileInstance;
        Letter = '\0';
        _defaultCellColor = defaultCellColor; // Store this
        _initialPrefabBackgroundColor = defaultCellColor; // Use it as the base
        if (backgroundImage != null)
        {
            backgroundImage.color = _initialPrefabBackgroundColor;
        }

        if (VisualTile != null)
        {
            backgroundImage = VisualTile.GetComponent<Image>();
            mainTextComponent = VisualTile.GetComponentInChildren<TextMeshProUGUI>(true);

            if (backgroundImage != null)
            {
                _initialPrefabBackgroundColor = defaultCellColor; // Set the initial color to the provided default
                backgroundImage.color = _initialPrefabBackgroundColor; // Apply the initial color
            }
            else
            {
                Debug.LogWarning($"TileData for {coordinates}: VisualTile '{VisualTile.name}' is missing an Image component.");
            }

            if (mainTextComponent == null)
            {
                Debug.LogWarning($"TileData for {coordinates}: VisualTile '{VisualTile.name}' is MISSING a TextMeshProUGUI child component.");
            }
            else
            {
                if (mainTextComponent.font == null)
                    Debug.LogError($"TileData for {coordinates}: TextMeshProUGUI on '{mainTextComponent.gameObject.name}' is MISSING a Font Asset!");
            }
        }
        else
        {
            Debug.LogError($"TileData for {coordinates}: visualTileInstance was NULL!");
        }
        ResetTile(); // Initialize the visual state
    }

    public void SetPlacedLetter(char newLetter)
    {
        Letter = char.ToUpper(newLetter);
        IsPreviewed = false;
        UpdateVisuals();
    }

    public void SetPreviewState(char previewChar, bool isValidPlacement)
    {
        IsPreviewed = true;
        PreviewLetterChar = char.ToUpper(previewChar);
        IsPreviewPlacementValid = isValidPlacement;
        UpdateVisuals();
    }

    public void ClearPreviewState()
    {
        if (!IsPreviewed) return;
        IsPreviewed = false;
        PreviewLetterChar = '\0';
        UpdateVisuals();
    }

    // Method to mark this tile as the special center tile
    public void SetAsDesignatedCenterTile(Color centerColor)
    {
        IsDesignatedCenterTile = true;
        _designatedCenterTileColor = centerColor;
        UpdateVisuals(); // Update visuals to reflect the new center tile color
    }

    public void ResetTile()
    {
        Letter = '\0';
        IsPreviewed = false;
        PreviewLetterChar = '\0';
        // The IsDesignatedCenterTile status persists. UpdateVisuals will handle the color.
        UpdateVisuals();
    }

    public void UpdateVisuals()
    {
        if (VisualTile == null) return;

        // Background Color Logic
        if (backgroundImage != null)
        {
            if (IsPreviewed)
            {
                backgroundImage.color = IsPreviewPlacementValid ? previewValidTileColor : previewInvalidTileColor;
            }
            else if (IsDesignatedCenterTile) // Not previewed, but is it the special center tile?
            {
                backgroundImage.color = _designatedCenterTileColor;
            }
            else // Not previewed, not special center - use alternate or default color
            {
                backgroundImage.color = _defaultCellColor; // Use the alternating cell color based on initialization
            }
        }

        // Text Logic
        if (mainTextComponent != null)
        {
            if (IsPreviewed)
            {
                mainTextComponent.text = PreviewLetterChar.ToString();
                mainTextComponent.color = previewLetterColor;
                mainTextComponent.gameObject.SetActive(PreviewLetterChar != '\0' && PreviewLetterChar != ' ');
            }
            else
            {
                if (IsOccupied)
                {
                    mainTextComponent.text = Letter.ToString();
                    mainTextComponent.color = defaultLetterColor;
                    mainTextComponent.gameObject.SetActive(true);
                }
                else
                {
                    mainTextComponent.text = "";
                    mainTextComponent.gameObject.SetActive(false); // Hide empty text objects for a cleaner grid
                }
            }
        }
    }
}