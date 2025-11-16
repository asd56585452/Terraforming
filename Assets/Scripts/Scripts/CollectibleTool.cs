using UnityEngine;

/// <summary>
/// Attach this to a GameObject to make it a collectible tool that the player can pick up.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CollectibleTool : MonoBehaviour
{
    [Header("Tool Settings")]
    [SerializeField] private ToolItem toolItem;
    
    [Header("Visual Effects")]
    [SerializeField] private float rotationSpeed = 90f;
    [SerializeField] private float floatSpeed = 1f;
    [SerializeField] private float floatAmount = 0.5f;
    
    [Header("Pickup Settings")]
    [SerializeField] private float pickupRange = 2f;
    [SerializeField] private KeyCode pickupKey = KeyCode.E;
    [SerializeField] private GameObject pickupPrompt;
    
    private Vector3 startPosition;
    private bool isPlayerNearby = false;
    private ToolInventory playerInventory;
    
    void Start()
    {
        startPosition = transform.position;
        
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
        // Rotate and float animation
        transform.Rotate(0, rotationSpeed * Time.deltaTime, 0);
        transform.position = startPosition + Vector3.up * Mathf.Sin(Time.time * floatSpeed) * floatAmount;
        
        // Check for pickup input
        if (isPlayerNearby && Input.GetKeyDown(pickupKey))
        {
            TryPickup();
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
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

