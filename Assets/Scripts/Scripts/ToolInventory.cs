using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the player's tool inventory. Handles collecting, storing, and using tools.
/// Now works with PlayerInteractionSystem for hand-held items and quick slots.
/// </summary>
public class ToolInventory : MonoBehaviour
{
    [Header("Inventory Settings")]
    [SerializeField] private int maxInventorySize = 10;
    
    [Header("UI References")]
    [SerializeField] private Transform inventoryPanel;
    [SerializeField] private GameObject inventorySlotPrefab;
    [SerializeField] private TextMeshProUGUI inventoryText; // Use TextMeshPro for modern Unity
    // If using legacy Text, change to: private Text inventoryText;
    
    private List<ToolData> inventory = new List<ToolData>();
    private ToolData selectedTool = null;
    
    /// <summary>
    /// Data structure to hold tool information and its current state
    /// </summary>
    [System.Serializable]
    public class ToolData
    {
        public ToolItem toolItem;
        public int currentDurability;
        public float lastUseTime;
        
        public ToolData(ToolItem item)
        {
            toolItem = item;
            currentDurability = item.maxDurability;
            lastUseTime = -999f;
        }
        
        public bool IsUsable()
        {
            return currentDurability > 0 && 
                   (Time.time - lastUseTime) >= toolItem.useCooldown;
        }
        
        public bool IsBroken()
        {
            return currentDurability <= 0;
        }
    }
    
    void Start()
    {
        UpdateUI();
    }
    
    void Update()
    {
        // Handle tool selection with number keys (legacy support)
        // New system uses PlayerInteractionSystem for 1-3, this handles 4-9
        for (int i = 4; i <= 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + i))
            {
                SelectTool(i - 1);
            }
        }
        
        // Use tool with left mouse button (legacy support)
        if (Input.GetMouseButtonDown(0) && selectedTool != null)
        {
            UseTool(selectedTool);
        }
    }
    
    /// <summary>
    /// Adds a tool to the inventory
    /// </summary>
    public bool AddTool(ToolItem toolItem)
    {
        if (inventory.Count >= maxInventorySize)
        {
            Debug.Log("Inventory is full!");
            return false;
        }
        
        ToolData newTool = new ToolData(toolItem);
        inventory.Add(newTool);
        UpdateUI();
        
        Debug.Log($"Added {toolItem.toolName} to inventory!");
        return true;
    }
    
    /// <summary>
    /// Removes a tool from the inventory
    /// </summary>
    public bool RemoveTool(ToolData tool)
    {
        if (inventory.Remove(tool))
        {
            if (selectedTool == tool)
            {
                selectedTool = null;
            }
            UpdateUI();
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Uses a tool (reduces durability or applies effect)
    /// </summary>
    public void UseTool(ToolData tool)
    {
        if (tool == null || !tool.IsUsable())
        {
            return;
        }
        
        tool.lastUseTime = Time.time;
        
        // Handle different tool types
        switch (tool.toolItem.toolType)
        {
            case ToolItem.ToolType.OxygenTank:
                // Don't reduce durability for oxygen tanks - they get consumed
                UseOxygenTank(tool);
                break;
            
            case ToolItem.ToolType.RepairKit:
                // Repair kits might be single use
                tool.currentDurability--;
                break;
                
            default:
                // Most tools lose durability when used
                tool.currentDurability--;
                break;
        }
        
        Debug.Log($"Used {tool.toolItem.toolName}. Durability: {tool.currentDurability}/{tool.toolItem.maxDurability}");
        
        if (tool.IsBroken())
        {
            Debug.Log($"{tool.toolItem.toolName} is broken!");
        }
        
        UpdateUI();
    }
    
    /// <summary>
    /// Uses an oxygen tank specifically
    /// </summary>
    private void UseOxygenTank(ToolData oxygenTank)
    {
        OxygenSystem oxygenSystem = GetComponent<OxygenSystem>();
        if (oxygenSystem != null)
        {
            oxygenSystem.AddOxygen(oxygenTank.toolItem.OxygenAmount);
            Debug.Log($"Used oxygen tank! Added {oxygenTank.toolItem.OxygenAmount} oxygen.");
            
            // Remove the used oxygen tank from inventory
            RemoveTool(oxygenTank);
        }
    }
    
    /// <summary>
    /// Repairs a tool (restores durability)
    /// </summary>
    public bool RepairTool(ToolData tool, int repairAmount)
    {
        if (tool == null || !tool.toolItem.isRepairable)
        {
            return false;
        }
        
        tool.currentDurability = Mathf.Min(tool.toolItem.maxDurability, tool.currentDurability + repairAmount);
        Debug.Log($"Repaired {tool.toolItem.toolName}. Durability: {tool.currentDurability}/{tool.toolItem.maxDurability}");
        UpdateUI();
        return true;
    }
    
    /// <summary>
    /// Fully repairs a tool
    /// </summary>
    public bool FullyRepairTool(ToolData tool)
    {
        if (tool == null || !tool.toolItem.isRepairable)
        {
            return false;
        }
        
        tool.currentDurability = tool.toolItem.maxDurability;
        Debug.Log($"Fully repaired {tool.toolItem.toolName}!");
        UpdateUI();
        return true;
    }
    
    /// <summary>
    /// Selects a tool by index
    /// </summary>
    public void SelectTool(int index)
    {
        if (index >= 0 && index < inventory.Count)
        {
            selectedTool = inventory[index];
            Debug.Log($"Selected {selectedTool.toolItem.toolName}");
            UpdateUI();
        }
    }
    
    /// <summary>
    /// Gets the currently selected tool
    /// </summary>
    public ToolData GetSelectedTool()
    {
        return selectedTool;
    }
    
    /// <summary>
    /// Gets all tools in inventory
    /// </summary>
    public List<ToolData> GetAllTools()
    {
        return new List<ToolData>(inventory);
    }
    
    /// <summary>
    /// Checks if player has a specific tool type
    /// </summary>
    public bool HasToolType(ToolItem.ToolType toolType)
    {
        foreach (var tool in inventory)
        {
            if (tool.toolItem.toolType == toolType && !tool.IsBroken())
            {
                return true;
            }
        }
        return false;
    }
    
    void UpdateUI()
    {
        if (inventoryText != null)
        {
            string text = $"Inventory: {inventory.Count}/{maxInventorySize}\n";
            for (int i = 0; i < inventory.Count; i++)
            {
                var tool = inventory[i];
                string marker = (selectedTool == tool) ? "> " : "  ";
                text += $"{marker}[{i + 1}] {tool.toolItem.toolName} ({tool.currentDurability}/{tool.toolItem.maxDurability})\n";
            }
            inventoryText.text = text;
        }
        
        // Update inventory panel if using UI slots
        if (inventoryPanel != null && inventorySlotPrefab != null)
        {
            // Clear existing slots
            foreach (Transform child in inventoryPanel)
            {
                Destroy(child.gameObject);
            }
            
            // Create slots for each tool
            for (int i = 0; i < inventory.Count; i++)
            {
                GameObject slot = Instantiate(inventorySlotPrefab, inventoryPanel);
                // You can customize the slot UI here
                // For example, set icon, durability bar, etc.
            }
        }
    }
}

