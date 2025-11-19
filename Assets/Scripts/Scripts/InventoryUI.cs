using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple UI manager for displaying inventory slots in the bottom right corner
/// Now supports both Image and RawImage components
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("UI Slots")]
    [SerializeField] private RawImage[] slotImages = new RawImage[3]; // Changed to RawImage
    [SerializeField] private GameObject[] slotBackgrounds = new GameObject[3];
    [SerializeField] private Text[] slotNumbers = new Text[3];
    
    [Header("Colors")]
    [SerializeField] private Color emptyColor = Color.gray;
    [SerializeField] private Color oxygenTankColor = Color.cyan;
    [SerializeField] private Color wrenchColor = Color.gray;
    [SerializeField] private Color flashlightColor = Color.yellow;
    [SerializeField] private Color repairKitColor = Color.green;
    [SerializeField] private Color ropeColor = new Color(0.8f, 0.4f, 0.2f);
    [SerializeField] private Color otherColor = Color.white;
    
    private MonoBehaviour playerController;
    
    void Start()
    {
        // Find player controller (try to avoid compilation order issues)
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            // Get all MonoBehaviour components and find the right one
            var controllers = player.GetComponents<MonoBehaviour>();
            foreach (var controller in controllers)
            {
                if (controller.GetType().Name == "PlayerSwimmingController")
                {
                    playerController = controller;
                    Debug.Log("[InventoryUI] Found PlayerSwimmingController");
                    break;
                }
            }
            
            // If not found, try fallback
            if (playerController == null)
            {
                playerController = player.GetComponent<MonoBehaviour>();
                Debug.LogWarning("[InventoryUI] PlayerSwimmingController not found, using fallback MonoBehaviour");
            }
        }
        else
        {
            Debug.LogError("[InventoryUI] No GameObject with 'Player' tag found!");
        }
        
        // Validate slot setup
        ValidateSlotSetup();
        
        InitializeSlots();
    }
    
    void ValidateSlotSetup()
    {
        Debug.Log("[InventoryUI] Validating slot setup...");
        
        if (slotImages == null)
        {
            Debug.LogError("[InventoryUI] Slot Images array is null!");
            return;
        }
        
        Debug.Log($"[InventoryUI] Slot Images array size: {slotImages.Length}");
        
        for (int i = 0; i < slotImages.Length; i++)
        {
            if (slotImages[i] == null)
            {
                Debug.LogError($"[InventoryUI] Slot RawImage {i} is null!");
            }
            else
            {
                Debug.Log($"[InventoryUI] Slot RawImage {i}: {slotImages[i].name} (GameObject: {slotImages[i].gameObject.name})");
            }
        }
    }
    
    void InitializeSlots()
    {
        for (int i = 0; i < 3; i++)
        {
            if (slotNumbers != null && i < slotNumbers.Length && slotNumbers[i] != null)
            {
                slotNumbers[i].text = (i + 1).ToString();
                slotNumbers[i].color = Color.white;
            }
            
            if (slotImages != null && i < slotImages.Length && slotImages[i] != null)
            {
                slotImages[i].color = emptyColor;
                slotImages[i].texture = null; // RawImage uses texture instead of sprite
                slotImages[i].enabled = false; // Start with RawImage disabled for empty slots
            }
        }
    }
    
    void Update()
    {
        UpdateSlots();
    }
    
    void UpdateSlots()
    {
        if (playerController == null)
        {
            Debug.LogWarning("[InventoryUI] Player controller not found");
            return;
        }
        
        // Try to get quick slots via reflection to avoid compilation dependencies
        try
        {
            // Debug: Log the controller type
            Debug.Log($"[InventoryUI] Player controller type: {playerController.GetType().Name}");
            
            var method = playerController.GetType().GetMethod("GetQuickSlots");
            if (method != null)
            {
                Debug.Log("[InventoryUI] GetQuickSlots method found, invoking...");
                var quickSlots = method.Invoke(playerController, null) as object[];
                if (quickSlots != null)
                {
                    Debug.Log($"[InventoryUI] Got {quickSlots.Length} quick slots");
                    for (int i = 0; i < 3 && i < quickSlots.Length; i++)
                    {
                        UpdateSlot(i, quickSlots[i]);
                    }
                }
                else
                {
                    Debug.LogWarning("[InventoryUI] GetQuickSlots returned null");
                }
            }
            else
            {
                // Only log this warning once per second to avoid spam
                if (Time.frameCount % 60 == 0)
                {
                    Debug.LogWarning($"[InventoryUI] GetQuickSlots method not found on {playerController.GetType().Name}");
                    
                    // Debug: List all methods available
                    var methods = playerController.GetType().GetMethods();
                    Debug.Log($"[InventoryUI] Available methods on {playerController.GetType().Name}:");
                    foreach (var m in methods)
                    {
                        if (m.Name.Contains("Quick") || m.Name.Contains("Slot") || m.Name.Contains("Inventory"))
                        {
                            Debug.Log($"  - {m.Name}");
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            // Only log significant errors, not every reflection attempt
            if (Time.frameCount % 60 == 0) // Only log once per second
            {
                Debug.LogError($"[InventoryUI] Reflection error: {e.Message}");
            }
        }
    }
    
    void UpdateSlot(int index, object toolData)
    {
        bool hasItem = toolData != null;

        if (slotImages != null && index < slotImages.Length && slotImages[index] != null)
        {
            if (hasItem)
            {
                slotImages[index].enabled = true; // Enable RawImage when there's an item
                
                // Try to get the icon sprite first
                Sprite iconSprite = GetItemIcon(toolData);
                if (iconSprite != null)
                {
                    // For RawImage, we need to convert Sprite to Texture2D
                    Texture2D iconTexture = SpriteToTexture2D(iconSprite);
                    if (iconTexture != null)
                    {
                        slotImages[index].texture = iconTexture;
                        slotImages[index].color = Color.white; // Use white to show the texture naturally
                        Debug.Log($"[InventoryUI] Successfully set texture from sprite '{iconSprite.name}' to RawImage slot {index}");
                    }
                    else
                    {
                        // Fallback to color if texture conversion fails
                        Color itemColor = GetItemColor(toolData);
                        slotImages[index].texture = null;
                        slotImages[index].color = itemColor;
                        Debug.LogWarning($"[InventoryUI] Failed to convert sprite to texture for slot {index}, using color fallback");
                    }
                }
                else
                {
                    // Fallback to color coding if no icon
                    Color itemColor = GetItemColor(toolData);
                    slotImages[index].texture = null;
                    slotImages[index].color = itemColor;
                    Debug.Log($"[InventoryUI] Using color fallback for slot {index}: {itemColor}. No sprite found.");
                }
            }
            else
            {
                // Empty slot - disable RawImage component
                slotImages[index].enabled = false; // Disable RawImage for empty slots
                slotImages[index].texture = null;
                slotImages[index].color = emptyColor;
                Debug.Log($"[InventoryUI] Slot {index} is empty - disabled RawImage");
            }
        }
        else
        {
            Debug.LogWarning($"[InventoryUI] Slot RawImage {index} is null or array not properly set!");
        }
        
        if (slotNumbers != null && index < slotNumbers.Length && slotNumbers[index] != null)
        {
            slotNumbers[index].color = hasItem ? Color.yellow : Color.white;
        }
    }
    
    /// <summary>
    /// Convert a Sprite to Texture2D for use with RawImage
    /// </summary>
    Texture2D SpriteToTexture2D(Sprite sprite)
    {
        if (sprite == null) return null;
        
        try
        {
            // If sprite already has a texture, try to use it directly
            if (sprite.texture != null)
            {
                // Check if we can read the texture
                if (sprite.texture.isReadable)
                {
                    return sprite.texture;
                }
                else
                {
                    Debug.LogWarning($"[InventoryUI] Sprite texture is not readable: {sprite.name}");
                    return sprite.texture; // Still try to use it, RawImage might handle it
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[InventoryUI] Error converting sprite to texture: {e.Message}");
        }
        
        return null;
    }
    
    Color GetItemColor(object toolData)
    {
        if (toolData == null) return emptyColor;
        
        try
        {
            // Use reflection to get tool item type
            var toolItemField = toolData.GetType().GetField("toolItem");
            if (toolItemField != null)
            {
                var toolItem = toolItemField.GetValue(toolData);
                if (toolItem != null)
                {
                    var toolTypeField = toolItem.GetType().GetField("toolType");
                    if (toolTypeField != null)
                    {
                        var toolType = toolTypeField.GetValue(toolItem);
                        string toolTypeName = toolType.ToString();
                        
                        switch (toolTypeName)
                        {
                            case "OxygenTank":
                                return oxygenTankColor;
                            case "Wrench":
                                return wrenchColor;
                            case "Flashlight":
                                return flashlightColor;
                            case "RepairKit":
                                return repairKitColor;
                            case "Rope":
                                return ropeColor;
                            default:
                                return otherColor;
                        }
                    }
                }
            }
        }
        catch
        {
            // Return default color if reflection fails
        }
        
        return otherColor;
    }
    
    Sprite GetItemIcon(object toolData)
    {
        if (toolData == null) 
        {
            return null;
        }
        
        try
        {
            // Use reflection to get tool item
            var toolItemField = toolData.GetType().GetField("toolItem");
            if (toolItemField != null)
            {
                var toolItem = toolItemField.GetValue(toolData);
                if (toolItem != null)
                {
                    Debug.Log($"[InventoryUI] Found toolItem: {toolItem}");
                    
                    // Try to call GetIconSprite method
                    var getIconMethod = toolItem.GetType().GetMethod("GetIconSprite");
                    if (getIconMethod != null)
                    {
                        var sprite = getIconMethod.Invoke(toolItem, null) as Sprite;
                        if (sprite != null)
                        {
                            Debug.Log($"[InventoryUI] GetIconSprite returned sprite: {sprite.name}");
                        }
                        else
                        {
                            Debug.LogWarning("[InventoryUI] GetIconSprite returned null");
                        }
                        return sprite;
                    }
                    
                    // Fallback: try to get icon field directly
                    var iconField = toolItem.GetType().GetField("icon");
                    if (iconField != null)
                    {
                        var directIcon = iconField.GetValue(toolItem) as Sprite;
                        if (directIcon != null)
                        {
                            Debug.Log($"[InventoryUI] Using direct icon: {directIcon.name}");
                        }
                        return directIcon;
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[InventoryUI] Error in GetItemIcon: {e.Message}");
        }
        
        return null;
    }
    
    /// <summary>
    /// Highlight a slot (called from player controller)
    /// </summary>
    public void HighlightSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < 3 && slotBackgrounds[slotIndex] != null)
        {
            StartCoroutine(PulseSlot(slotIndex));
        }
    }
    
    System.Collections.IEnumerator PulseSlot(int slotIndex)
    {
        Transform slotTransform = slotBackgrounds[slotIndex].transform;
        Vector3 originalScale = slotTransform.localScale;
        
        // Scale up
        float timer = 0f;
        while (timer < 0.1f)
        {
            timer += Time.deltaTime;
            float scale = Mathf.Lerp(1f, 1.2f, timer / 0.1f);
            slotTransform.localScale = originalScale * scale;
            yield return null;
        }
        
        // Scale down
        timer = 0f;
        while (timer < 0.1f)
        {
            timer += Time.deltaTime;
            float scale = Mathf.Lerp(1.2f, 1f, timer / 0.1f);
            slotTransform.localScale = originalScale * scale;
            yield return null;
        }
        
        slotTransform.localScale = originalScale;
    }
}