using UnityEngine;

/// <summary>
/// Attach this to a GameObject to make it a collectible tool that the player can pick up.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CollectibleTool : MonoBehaviour
{
    [Header("Tool Settings")]
    [SerializeField] private ToolItem toolItem;
    
    [Header("Pickup Settings")]
    [SerializeField] private float pickupRange = 2f;
    [SerializeField] private KeyCode pickupKey = KeyCode.E;
    [SerializeField] private GameObject pickupPrompt;
    
    private bool isPlayerNearby = false;
    private ToolInventory playerInventory;
    private bool isBeingHeld = false;
    private bool isInInventory = false;
    private Vector3 originalScale;
    
    public ToolItem GetToolItem() => toolItem;
    public bool IsBeingHeld => isBeingHeld;
    public bool IsInInventory => isInInventory;
    
    public void SetToolItem(ToolItem item)
    {
        toolItem = item;
    }
    
    public void SetBeingHeld(bool held)
    {
        isBeingHeld = held;
        // Disable collider when being held to prevent interference
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = !held;
        }
    }
    
    public void SetInInventory(bool inInventory)
    {
        isInInventory = inInventory;
        
        if (inInventory)
        {
            // Hide the object when in inventory
            gameObject.SetActive(false);
            // Disable collider
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
            }
        }
        else
        {
            // Show the object when taken from inventory
            gameObject.SetActive(true);
            // Enable collider if not being held
            if (!isBeingHeld)
            {
                Collider col = GetComponent<Collider>();
                if (col != null)
                {
                    col.enabled = true;
                }
            }
        }
    }
    
    public void StoreOriginalScale()
    {
        originalScale = transform.localScale;
    }
    
    public void RestoreOriginalScale()
    {
        if (originalScale != Vector3.zero)
        {
            transform.localScale = originalScale;
        }
    }
    
    void Start()
    {
        // Store original scale
        originalScale = transform.localScale;
        
        // Make sure collider is a trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
        
        // Find player inventory
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerInventory = player.GetComponent<ToolInventory>();
        }
        
        if (pickupPrompt != null)
        {
            pickupPrompt.SetActive(false);
        }
        
        if (toolItem == null)
        {
            Debug.LogError($"CollectibleTool on {gameObject.name} has no ToolItem assigned!");
        }
    }
    
    void Update()
    {
        // Only process input and effects when not being held and not in inventory
        if (isBeingHeld || isInInventory) return;
        
        // Check for pickup input (legacy support - new system uses PlayerInteractionSystem)
        if (isPlayerNearby && Input.GetKeyDown(pickupKey))
        {
            TryPickup();
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (isBeingHeld || isInInventory) return;
        
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = true;
            if (pickupPrompt != null)
            {
                pickupPrompt.SetActive(true);
            }
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;
            if (pickupPrompt != null)
            {
                pickupPrompt.SetActive(false);
            }
        }
    }
    
    void TryPickup()
    {
        if (playerInventory != null && toolItem != null)
        {
            if (playerInventory.AddTool(toolItem))
            {
                // Successfully picked up
                Debug.Log($"Picked up {toolItem.toolName}!");
                Destroy(gameObject);
            }
            else
            {
                Debug.Log("Inventory is full!");
            }
        }
    }
}

