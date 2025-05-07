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

    public TileData(Vector2Int coordinates, GameObject visualTileInstance)
    {
        Coordinates = coordinates;
        VisualTile = visualTileInstance;
        Letter = '\0';

        if (VisualTile != null)
        {
            backgroundImage = VisualTile.GetComponent<Image>();
            mainTextComponent = VisualTile.GetComponentInChildren<TextMeshProUGUI>(true);

            if (backgroundImage != null)
            {
                _initialPrefabBackgroundColor = backgroundImage.color; // Store initial color from prefab
            }
            else { Debug.LogWarning($"TileData for {coordinates}: VisualTile '{VisualTile.name}' is missing an Image component."); }

            if (mainTextComponent == null) { Debug.LogWarning($"TileData for {coordinates}: VisualTile '{VisualTile.name}' is MISSING a TextMeshProUGUI child component."); }
            else
            {
                if (mainTextComponent.font == null) Debug.LogError($"TileData for {coordinates}: TextMeshProUGUI on '{mainTextComponent.gameObject.name}' is MISSING a Font Asset!");
            }
        }
        else { Debug.LogError($"TileData for {coordinates}: visualTileInstance was NULL!"); }
        ResetTile(); // Initial visual state
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
        // Update visuals immediately to reflect this change if it wasn't already set by initial creation
        if (backgroundImage != null && backgroundImage.color != _designatedCenterTileColor && !IsPreviewed)
        {
            UpdateVisuals();
        }
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
            else // Not previewed, not special center - use initial prefab color
            {
                backgroundImage.color = _initialPrefabBackgroundColor;
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
                    mainTextComponent.gameObject.SetActive(true); // Or false if you prefer to hide empty text objects
                }
            }
        }
    }
}