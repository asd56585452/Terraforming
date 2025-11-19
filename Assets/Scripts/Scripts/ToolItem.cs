using UnityEngine;

/// <summary>
/// ScriptableObject that defines a tool item with its properties.
/// Create these assets in the Unity Editor to define different tools.
/// </summary>
[CreateAssetMenu(fileName = "New Tool", menuName = "Ocean Game/Tool Item")]
public class ToolItem : ScriptableObject
{
    [Header("Tool Information")]
    public string toolName;
    public string description;
    public Sprite icon;
    public Texture2D iconTexture; // Alternative texture support
    
    [Header("Tool Properties")]
    public ToolType toolType;
    public int maxDurability = 100;
    public bool isRepairable = true;
    
    [Header("Usage")]
    public bool canUseUnderwater = true;
    public float useCooldown = 1f;
    
    [Header("Oxygen Tank Settings")]
    [SerializeField] private float oxygenAmount = 50f; // Amount of oxygen this tank provides
    
    public float OxygenAmount => oxygenAmount;
    
    /// <summary>
    /// Get the icon as a Sprite, converting from Texture2D if needed
    /// </summary>
    public Sprite GetIconSprite()
    {
        if (icon != null)
        {
            return icon;
        }
        else if (iconTexture != null)
        {
            try
            {
                // Create sprite from texture2D with error checking
                if (iconTexture.width <= 0 || iconTexture.height <= 0)
                {
                    Debug.LogError($"Invalid texture dimensions for {toolName}: {iconTexture.width}x{iconTexture.height}");
                    return null;
                }
                
                Sprite generatedSprite = Sprite.Create(
                    iconTexture, 
                    new Rect(0, 0, iconTexture.width, iconTexture.height), 
                    new Vector2(0.5f, 0.5f),
                    100.0f // pixels per unit - important for UI scaling
                );
                
                if (generatedSprite != null)
                {
                    // Set a proper name for debugging
                    generatedSprite.name = $"{toolName}_Icon";
                }
                
                return generatedSprite;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error creating sprite for {toolName}: {e.Message}");
                return null;
            }
        }
        
        return null;
    }
    
    public enum ToolType
    {
        Wrench,
        Flashlight,
        RepairKit,
        OxygenTank,
        Rope,
        Other
    }
}

