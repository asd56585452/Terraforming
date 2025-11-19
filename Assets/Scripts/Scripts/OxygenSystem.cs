using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;

/// <summary>
/// Manages the player's oxygen levels. Oxygen depletes over time and faster when moving.
/// Player dies when oxygen reaches zero.
/// </summary>
public class OxygenSystem : MonoBehaviour
{
    [Header("Oxygen Settings")]
    [SerializeField] private float maxOxygen = 100f;
    [SerializeField] private float baseDepletionRate = 0.5f; // Oxygen per second
    [SerializeField] private float fastSwimDepletionMultiplier = 2f;
    [SerializeField] private float movementDepletionMultiplier = 1.5f;
    
    [Header("UI References")]
    [SerializeField] private GameObject oxygenBar;
    [SerializeField] private GameObject oxygenText;
    
    [Header("Color Settings")]
    [SerializeField] private Color brightRedColor = new Color(1f, 0.3f, 0.3f, 1f); // Bright red
    [SerializeField] private Color darkRedColor = new Color(0.1f, 0.03f, 0.03f, 1f); // Dark red
    
    [Header("Death Settings")]
    [SerializeField] private GameObject deathScreen;
    [SerializeField] private string deathMessage = "You ran out of oxygen!";
    
    private float currentOxygen;
    private PlayerSwimmingController playerController;
    private bool isDead = false;
    private PhotonView photonView;
    private bool isLocalPlayer = false;
    
    // UI component references
    private RectTransform oxygenBarRect;
    private Image oxygenBarImage;
    private TextMeshProUGUI oxygenTextComponent;
    private float originalWidth;
    
    public float CurrentOxygen => currentOxygen;
    public float MaxOxygen => maxOxygen;
    public float OxygenPercentage => currentOxygen / maxOxygen;
    public bool IsDead => isDead;
    
    void Start()
    {
        // Check if this is local player (for multiplayer)
        photonView = GetComponent<PhotonView>();
        if (photonView != null)
        {
            isLocalPlayer = photonView.IsMine;
        }
        else
        {
            // Single player mode
            isLocalPlayer = true;
        }
        
        currentOxygen = maxOxygen;
        playerController = GetComponent<PlayerSwimmingController>();
        
        // Initialize UI components
        InitializeUIComponents();
        
        // Only show UI for local player
        if (isLocalPlayer)
        {
            UpdateUI();
        }
        else
        {
            // Hide UI for remote players
            if (oxygenBar != null) oxygenBar.SetActive(false);
            if (oxygenText != null) oxygenText.SetActive(false);
        }
    }
    
    void InitializeUIComponents()
    {
        // Get RectTransform and Image components from oxygen bar
        if (oxygenBar != null)
        {
            oxygenBarRect = oxygenBar.GetComponent<RectTransform>();
            oxygenBarImage = oxygenBar.GetComponent<Image>();
            
            if (oxygenBarRect != null)
            {
                // Store original width
                originalWidth = oxygenBarRect.sizeDelta.x;
            }
        }
        
        // Get TextMeshProUGUI component from oxygen text
        if (oxygenText != null)
        {
            oxygenTextComponent = oxygenText.GetComponent<TextMeshProUGUI>();
        }
    }
    
    void Update()
    {
        // Only process oxygen for local player
        if (!isLocalPlayer) return;
        
        if (isDead) return;
        
        // Calculate depletion rate
        float depletionRate = baseDepletionRate;
        
        if (playerController != null)
        {
            Vector3 velocity = playerController.GetVelocity();
            bool isMoving = velocity.magnitude > 0.1f;
            
            if (isMoving)
            {
                depletionRate *= movementDepletionMultiplier;
            }
            
            if (playerController.IsFastSwimming())
            {
                depletionRate *= fastSwimDepletionMultiplier;
            }
        }
        
        // Deplete oxygen
        currentOxygen -= depletionRate * Time.deltaTime;
        currentOxygen = Mathf.Max(0, currentOxygen);
        
        // Check for death
        if (currentOxygen <= 0 && !isDead)
        {
            OnOxygenDepleted();
        }
        
        UpdateUI();
    }
    
    /// <summary>
    /// Refills oxygen to maximum
    /// </summary>
    public void RefillOxygen()
    {
        currentOxygen = maxOxygen;
        UpdateUI();
    }
    
    /// <summary>
    /// Adds a specific amount of oxygen
    /// </summary>
    public void AddOxygen(float amount)
    {
        currentOxygen = Mathf.Min(maxOxygen, currentOxygen + amount);
        UpdateUI();
    }
    
    /// <summary>
    /// Sets oxygen to a specific value
    /// </summary>
    public void SetOxygen(float amount)
    {
        currentOxygen = Mathf.Clamp(amount, 0, maxOxygen);
        UpdateUI();
    }
    
    void OnOxygenDepleted()
    {
        isDead = true;
        Debug.Log(deathMessage);
        
        // Disable player movement
        if (playerController != null)
        {
            playerController.enabled = false;
        }
        
        // Show death screen if available
        if (deathScreen != null)
        {
            deathScreen.SetActive(true);
        }
        
        // Unlock cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    void UpdateUI()
    {
        // Only update UI for local player
        if (!isLocalPlayer) return;
        
        float oxygenPercentage = OxygenPercentage;
        
        // Update oxygen bar width
        if (oxygenBarRect != null)
        {
            Vector2 sizeDelta = oxygenBarRect.sizeDelta;
            sizeDelta.x = originalWidth * oxygenPercentage;
            oxygenBarRect.sizeDelta = sizeDelta;
        }
        
        // Update oxygen bar color (bright red to dark red)
        if (oxygenBarImage != null)
        {
            // Lerp from bright red (high oxygen) to dark red (low oxygen)
            oxygenBarImage.color = Color.Lerp(darkRedColor, brightRedColor, oxygenPercentage);
        }
        
        // Update text
        if (oxygenTextComponent != null)
        {
            oxygenTextComponent.text = $"Oxygen: {currentOxygen:F1}%";
        }
    }
    
    /// <summary>
    /// Resets the oxygen system (for respawning)
    /// </summary>
    public void ResetOxygen()
    {
        isDead = false;
        currentOxygen = maxOxygen;
        
        if (playerController != null)
        {
            playerController.enabled = true;
        }
        
        if (deathScreen != null)
        {
            deathScreen.SetActive(false);
        }
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        UpdateUI();
    }
}

