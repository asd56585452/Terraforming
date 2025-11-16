using UnityEngine;
using Photon.Pun;

/// <summary>
/// Attach this to a GameObject to create an oxygen refill station.
/// Player can interact with it to refill their oxygen.
/// </summary>
[RequireComponent(typeof(Collider))]
public class OxygenRefillStation : MonoBehaviour
{
    [Header("Refill Settings")]
    [SerializeField] private float refillRate = 50f; // Oxygen per second
    [SerializeField] private bool instantRefill = false;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject interactPrompt;
    [SerializeField] private ParticleSystem refillEffect;
    [SerializeField] private AudioSource refillSound;
    
    private bool isPlayerNearby = false;
    private OxygenSystem playerOxygen;
    private bool isRefilling = false;
    
    // Track which player is nearby (for multiplayer)
    private GameObject nearbyPlayer;
    
    void Start()
    {
        // Make sure collider is a trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
        
        if (interactPrompt != null)
        {
            interactPrompt.SetActive(false);
        }
    }
    
    void Update()
    {
        // Only process for local player
        if (nearbyPlayer != null)
        {
            PhotonView pv = nearbyPlayer.GetComponent<PhotonView>();
            if (pv != null && !pv.IsMine)
            {
                // Remote player, don't process
                return;
            }
        }
        
        if (isPlayerNearby && Input.GetKeyDown(interactKey))
        {
            StartRefilling();
        }
        
        if (isRefilling)
        {
            // Re-check player oxygen in case player left
            if (playerOxygen == null && nearbyPlayer != null)
            {
                playerOxygen = nearbyPlayer.GetComponent<OxygenSystem>();
                if (playerOxygen == null)
                {
                    playerOxygen = nearbyPlayer.GetComponentInParent<OxygenSystem>();
                }
            }
            
            if (playerOxygen != null)
            {
                if (instantRefill)
                {
                    playerOxygen.RefillOxygen();
                    StopRefilling();
                }
                else
                {
                    // Continuous refill
                    float refillAmount = refillRate * Time.deltaTime;
                    playerOxygen.AddOxygen(refillAmount);
                    
                    // Stop if oxygen is full
                    if (playerOxygen.OxygenPercentage >= 1f)
                    {
                        StopRefilling();
                    }
                }
            }
            else
            {
                // Lost reference to player oxygen, stop refilling
                StopRefilling();
            }
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        // Check if this is a player (by tag or by having OxygenSystem component)
        bool isPlayer = other.CompareTag("Player");
        OxygenSystem oxygen = other.GetComponent<OxygenSystem>();
        
        // Also check parent in case OxygenSystem is on parent GameObject
        if (oxygen == null)
        {
            oxygen = other.GetComponentInParent<OxygenSystem>();
        }
        
        if (isPlayer || oxygen != null)
        {
            // In multiplayer, only interact with local player
            PhotonView pv = other.GetComponent<PhotonView>();
            if (pv != null && !pv.IsMine)
            {
                // This is a remote player, ignore
                return;
            }
            
            // Check parent PhotonView too
            if (pv == null)
            {
                pv = other.GetComponentInParent<PhotonView>();
                if (pv != null && !pv.IsMine)
                {
                    return;
                }
            }
            
            isPlayerNearby = true;
            playerOxygen = oxygen;
            nearbyPlayer = other.gameObject;
            
            if (interactPrompt != null)
            {
                interactPrompt.SetActive(true);
            }
            
            if (playerOxygen == null)
            {
                Debug.LogWarning($"OxygenRefillStation: Found player but no OxygenSystem component on {other.gameObject.name}");
            }
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        // Check if this is the player we're tracking
        if (other.gameObject == nearbyPlayer || other.CompareTag("Player"))
        {
            // Verify it's the same player
            PhotonView pv = other.GetComponent<PhotonView>();
            if (pv != null && !pv.IsMine)
            {
                // Remote player, ignore
                return;
            }
            
            isPlayerNearby = false;
            nearbyPlayer = null;
            StopRefilling();
            
            if (interactPrompt != null)
            {
                interactPrompt.SetActive(false);
            }
        }
    }
    
    void StartRefilling()
    {
        if (playerOxygen != null && !playerOxygen.IsDead)
        {
            isRefilling = true;
            
            if (refillEffect != null)
            {
                refillEffect.Play();
            }
            
            if (refillSound != null)
            {
                refillSound.Play();
            }
        }
    }
    
    void StopRefilling()
    {
        isRefilling = false;
        
        if (refillEffect != null)
        {
            refillEffect.Stop();
        }
        
        if (refillSound != null)
        {
            refillSound.Stop();
        }
    }
}

