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
    
    [Header("Tool Properties")]
    public ToolType toolType;
    public int maxDurability = 100;
    public bool isRepairable = true;
    
    [Header("Usage")]
    public bool canUseUnderwater = true;
    public float useCooldown = 1f;
    
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

