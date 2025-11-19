using UnityEngine;
using Photon.Pun;

/// <summary>
/// Handles 3D swimming and diving movement for the player.
/// Movement is relative to where the player is looking - swim in the direction you face!
/// Also handles oxygen tank interactions.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[DefaultExecutionOrder(-100)] // Run before PhotonAnimatorView (default is 0) to ensure animation parameters are set before Photon reads them
public class PlayerSwimmingController : MonoBehaviour, IPunObservable
{
    // Compatibility enum for systems that reference FirstPersonController.MoveState
    public enum MoveState
    {
        Idle,
        Walk,
        Run,
        Swim
    }
    [Header("Movement Settings")]
    [SerializeField] private float swimSpeed = 5f;
    [SerializeField] private float fastSwimMultiplier = 1.5f;
    
    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxLookAngle = 80f;
    
    [Header("Water Physics")]
    [SerializeField] private float waterDrag = 8f; // Higher = stops faster (reduces sliding)
    
    [Header("Character Rotation")]
    [SerializeField] private Transform characterBody; // The character's body/root transform
    [SerializeField] private Transform characterHead; // Optional: specific head bone (leave null to use body)
    [SerializeField] private float rotationSpeed = 5f; // How fast the character rotates to face camera direction
    [SerializeField] private bool rotateBodyToCamera = true; // Whether to rotate body towards camera
    [SerializeField] private float headRotationLimit = 60f; // Max angle head can rotate up/down (degrees)
    
    [Header("First Person Camera")]
    [SerializeField] private bool firstPersonMode = true; // Enable first person view
    [SerializeField] private Vector3 cameraOffset = new Vector3(0, 0.1f, 0); // Camera offset from head position
    [SerializeField] private LayerMask cullingLayers; // Layers to hide in first person (usually character layer)
    
    [Header("Animation")]
    [SerializeField] private Animator animator; // Character's animator component
    [SerializeField] private string swimSpeedParameter = "SwimSpeed"; // Animator parameter for swim speed
    [SerializeField] private string isSwimmingParameter = "IsSwimming"; // Animator parameter for swimming state
    [SerializeField] private string isTreadingParameter = "IsTreading"; // Animator parameter for treading state
    [SerializeField] private string verticalAngleParameter = "VerticalAngle"; // Animator parameter for vertical swim direction (-1 to 1)
    
    [Header("Oxygen Tank Interaction")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private Transform handTransform; // Where to hold items
    [SerializeField] private Vector3 handOffset = new Vector3(0.3f, -0.2f, 0.5f);
    
    private CharacterController controller;
    private Camera playerCamera;
    private Vector3 velocity;
    private float verticalRotation = 0f;
    private bool isFastSwimming = false;
    private bool isMoving = false;
    private Vector3 currentMoveDirection = Vector3.zero;
    private bool isMovingBackward = false; // Track if moving backward (S key)
    
    // Water system integration
    private Water waterSystem;
    private bool isUnderwater = false;
    
    // Photon networking
    private PhotonView photonView;
    private bool isLocalPlayer = false;
    
    // Synced rotation for remote players
    private Quaternion syncedCharacterBodyRotation = Quaternion.identity;
    private float rotationSyncSpeed = 10f; // How fast to interpolate rotation for remote players
    
    // Oxygen tank interaction
    private ToolInventory.ToolData heldTool = null;
    private GameObject heldToolObject = null;
    private CollectibleTool nearbyCollectible = null;
    private ToolInventory toolInventory;
    private OxygenSystem oxygenSystem;
    
    // Quick inventory slots (3 slots) - now stores both ToolData and GameObject
    [System.Serializable]
    public class InventorySlot
    {
        public ToolInventory.ToolData toolData;
        public GameObject gameObject;
        
        public InventorySlot(ToolInventory.ToolData data, GameObject obj)
        {
            toolData = data;
            gameObject = obj;
        }
        
        public bool IsEmpty => toolData == null && gameObject == null;
        
        public void Clear()
        {
            toolData = null;
            gameObject = null;
        }
    }
    
    private InventorySlot[] quickSlots = new InventorySlot[3];
    
    // Compatibility properties for systems that reference FirstPersonController
    public MoveState currentMoveState
    {
        get
        {
            if (!isMoving) return MoveState.Idle;
            if (isFastSwimming) return MoveState.Run;
            return MoveState.Swim; // Your controller is swimming-focused
        }
    }
    
    public bool grounded
    {
        get { return controller != null && controller.isGrounded; }
    }
    
    public bool underwater
    {
        get { return isUnderwater; }
    }
    
    public bool HasItemInHand => heldTool != null;
    
    public ToolInventory.ToolData[] GetQuickSlots() 
    {
        ToolInventory.ToolData[] toolDataArray = new ToolInventory.ToolData[3];
        for (int i = 0; i < quickSlots.Length; i++)
        {
            toolDataArray[i] = quickSlots[i]?.toolData;
        }
        return toolDataArray;
    }
    
    void Start()
    {
        controller = GetComponent<CharacterController>();
        toolInventory = GetComponent<ToolInventory>();
        oxygenSystem = GetComponent<OxygenSystem>();
        
        // Check if this is a networked player (Photon)
        photonView = GetComponent<PhotonView>();
        if (photonView != null)
        {
            isLocalPlayer = photonView.IsMine;
            
            // Ensure PlayerSwimmingController is in ObservedComponents for rotation sync
            if (photonView.ObservedComponents != null && !photonView.ObservedComponents.Contains(this))
            {
                photonView.ObservedComponents.Add(this);
            }
        }
        else
        {
            // If no PhotonView, assume single player (local)
            isLocalPlayer = true;
        }
        
        // Only setup camera and input for local player
        if (isLocalPlayer)
        {
            playerCamera = GetComponentInChildren<Camera>();
            
            if (playerCamera == null)
            {
                Debug.LogWarning("No Camera found in children. Creating one...");
                GameObject camObj = new GameObject("PlayerCamera");
                camObj.transform.SetParent(transform);
                camObj.transform.localPosition = new Vector3(0, 0.5f, 0);
                playerCamera = camObj.AddComponent<Camera>();
            }
            
            // Setup first person camera
            if (firstPersonMode)
            {
                SetupFirstPersonCamera();
            }
            
            // Setup hand transform for holding items
            SetupHandTransform();
            
            // Lock cursor to center of screen (only for local player)
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            // For remote players, disable camera and input
            playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera != null)
            {
                playerCamera.enabled = false;
                // Also disable audio listener if present
                AudioListener audioListener = playerCamera.GetComponent<AudioListener>();
                if (audioListener != null)
                {
                    audioListener.enabled = false;
                }
            }
        }
        
        // Find water system for underwater detection (all players need this)
        waterSystem = FindFirstObjectByType<Water>();
        
        // Find character body if not assigned
        if (characterBody == null)
        {
            characterBody = transform;
        }
        
        // Find animator if not assigned
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
        }
        
        // Initialize quick inventory slots
        for (int i = 0; i < quickSlots.Length; i++)
        {
            quickSlots[i] = new InventorySlot(null, null);
        }
    }
    
    void SetupHandTransform()
    {
        if (handTransform == null)
        {
            GameObject handPoint = new GameObject("HandPoint");
            handPoint.transform.SetParent(playerCamera.transform);
            handPoint.transform.localPosition = handOffset;
            handTransform = handPoint.transform;
        }
    }
    
    void SetupFirstPersonCamera()
    {
        // In first person, we want the camera to be independent of body rotation
        // to prevent wobbling. We'll position it relative to the head but not parent it.
        
        // Find head position
        Transform headTransform = characterHead;
        if (headTransform == null && characterBody != null)
        {
            // Try to find head in character hierarchy
            headTransform = FindChildRecursive(characterBody, "Head");
            if (headTransform == null)
            {
                headTransform = FindChildRecursive(characterBody, "head");
            }
        }
        
        if (headTransform != null)
        {
            characterHead = headTransform;
            // Position camera at head but don't parent it - this prevents wobbling
            // We'll update position in LateUpdate instead
            playerCamera.transform.position = headTransform.position + headTransform.TransformDirection(cameraOffset);
        }
        else if (characterBody != null)
        {
            // Fallback: position at estimated head height
            playerCamera.transform.position = characterBody.position + new Vector3(0, 1.6f, 0) + cameraOffset;
        }
        
        // Make camera a child of the Player root (not body/head) to avoid rotation issues
        playerCamera.transform.SetParent(transform);
    }
    
    Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Contains(name))
            {
                return child;
            }
            Transform found = FindChildRecursive(child, name);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }
    
    void Update()
    {
        // Only process input and movement for local player
        if (!isLocalPlayer)
        {
            // Remote players: animations are synced via PhotonAnimatorView
            // Character body rotation is synced via IPunObservable
            // We still update underwater status for visual effects if needed
            if (waterSystem != null)
            {
                UpdateUnderwaterStatus();
            }
            // Apply synced rotation to character body for remote players
            ApplySyncedRotation();
            return;
        }
        
        // Check if underwater (using Water system if available)
        UpdateUnderwaterStatus();
        
        HandleMouseLook();
        HandleMovement();
        ApplyWaterPhysics();
        HandleCharacterRotation();
        
        // Handle oxygen tank interactions
        HandleOxygenTankInteractions();
        
        // Update animations at the end of Update() so PhotonAnimatorView can read correct values
        // PhotonAnimatorView reads parameters in its Update(), so we need to set them before that
        if (isLocalPlayer)
        {
            UpdateAnimations(); // This sets animator parameters which PhotonAnimatorView will sync
        }
    }
    
    void UpdateUnderwaterStatus()
    {
        if (waterSystem != null && playerCamera != null)
        {
            // Use camera position for underwater detection
            float distanceFromCenter = (playerCamera.transform.position - Vector3.zero).magnitude;
            isUnderwater = distanceFromCenter < waterSystem.radius + 0.25f;
        }
        else
        {
            // Fallback: assume always underwater for swimming gameplay
            isUnderwater = true;
        }
    }
    
    void HandleMouseLook()
    {
        // Get mouse input
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        
        // Rotate player horizontally
        transform.Rotate(0, mouseX, 0);
        
        // Rotate camera vertically (clamped)
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);
        playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
    }
    
    void HandleMovement()
    {
        // Get input
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        // Check for fast swimming (hold Shift)
        isFastSwimming = Input.GetKey(KeyCode.LeftShift);
        
        // Calculate input magnitude for deadzone check
        float inputMagnitude = Mathf.Sqrt(horizontal * horizontal + vertical * vertical);
        
        // Only process movement if we have significant input
        if (inputMagnitude > 0.1f)
        {
            // Normalize input to prevent faster diagonal movement
            float normalizedHorizontal = horizontal / inputMagnitude;
            float normalizedVertical = vertical / inputMagnitude;
            
            // Clamp input magnitude to 1
            inputMagnitude = Mathf.Clamp01(inputMagnitude);
            
            // Get camera-relative directions
            Vector3 cameraForward = playerCamera.transform.forward;
            Vector3 cameraRight = playerCamera.transform.right;
            
            // Calculate movement direction in 3D space
            Vector3 moveDirection = (cameraForward * normalizedVertical + cameraRight * normalizedHorizontal);
            moveDirection.Normalize();
            
            // Calculate speed
            float currentSpeed = swimSpeed;
            if (isFastSwimming)
            {
                currentSpeed *= fastSwimMultiplier;
            }
            
            // Apply movement with input magnitude
            velocity = moveDirection * currentSpeed * inputMagnitude;
            currentMoveDirection = moveDirection;
            isMoving = true;
            
            // Check if moving backward
            isMovingBackward = normalizedVertical < -0.1f;
        }
        else
        {
            // When not moving, set Y velocity to 0, let drag handle X and Z
            velocity = new Vector3(velocity.x, 0, velocity.z);
            isMoving = false;
            isMovingBackward = false;
        }
    }
    
    void ApplyWaterPhysics()
    {
        // Only apply drag when NOT actively moving
        if (!isMoving)
        {
            // Apply water drag
            float dragFactor = Mathf.Clamp01(waterDrag * Time.deltaTime);
            velocity *= (1f - dragFactor);
        }
        
        // Move the character
        controller.Move(velocity * Time.deltaTime);
    }
    
    void LateUpdate()
    {
        // Only update camera for local player
        if (!isLocalPlayer || playerCamera == null)
        {
            return;
        }
        
        // Update camera position in LateUpdate to avoid wobbling
        if (firstPersonMode && characterHead != null)
        {
            playerCamera.transform.position = characterHead.position + characterHead.TransformDirection(cameraOffset);
        }
    }
    
    void HandleOxygenTankInteractions()
    {
        // Check for nearby interactables
        CheckForNearbyItems();
        
        // Handle E key - Pickup or Use
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (nearbyCollectible != null)
            {
                PickupItem(nearbyCollectible);
            }
            else if (heldTool != null)
            {
                UseHeldTool();
            }
        }
        
        // Handle Q key - Drop
        if (Input.GetKeyDown(KeyCode.Q) && heldTool != null)
        {
            DropHeldItem();
        }
        
        // Handle 1, 2, 3 keys - Inventory slots
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            HandleQuickSlot(0);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            HandleQuickSlot(1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            HandleQuickSlot(2);
        }
    }
    
    void CheckForNearbyItems()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit, interactionRange))
        {
            CollectibleTool collectible = hit.collider.GetComponent<CollectibleTool>();
            if (collectible != null)
            {
                nearbyCollectible = collectible;
            }
            else
            {
                nearbyCollectible = null;
            }
        }
        else
        {
            nearbyCollectible = null;
        }
    }
    
    void PickupItem(CollectibleTool collectible)
    {
        ToolItem toolItem = collectible.GetToolItem();
        if (toolItem == null) return;
        
        if (heldTool == null)
        {
            // Store original scale before modifying
            collectible.StoreOriginalScale();
            
            // Pick up directly to hand
            heldTool = new ToolInventory.ToolData(toolItem);
            
            // Use the actual GameObject instead of creating a cube
            heldToolObject = collectible.gameObject;
            
            // Set up the object for being held
            collectible.SetBeingHeld(true);
            heldToolObject.transform.SetParent(handTransform);
            
            // Set specific transform for oxygen tank
            if (toolItem.toolType == ToolItem.ToolType.OxygenTank)
            {
                heldToolObject.transform.localPosition = new Vector3(-0.2f, 0f, 0f);
                heldToolObject.transform.localRotation = Quaternion.Euler(0f, 10f, 90f);
            }
            else
            {
                // Default position and rotation for other tools
                heldToolObject.transform.localPosition = Vector3.zero;
                heldToolObject.transform.localRotation = Quaternion.identity;
            }
            
            // Scale down if needed
            heldToolObject.transform.localScale = heldToolObject.transform.localScale * 0.5f;
            
            Debug.Log($"Picked up {toolItem.toolName} to hand");
        }
        else
        {
            Debug.Log("Hands are full! Drop current item first or put it in inventory.");
        }
    }
    
    void UseHeldTool()
    {
        if (heldTool == null) return;
        
        if (heldTool.toolItem.toolType == ToolItem.ToolType.OxygenTank)
        {
            // Use oxygen tank
            if (oxygenSystem != null)
            {
                oxygenSystem.AddOxygen(heldTool.toolItem.OxygenAmount);
                Debug.Log($"Used oxygen tank! Added {heldTool.toolItem.OxygenAmount} oxygen.");
                
                // Remove the used oxygen tank
                if (heldToolObject != null)
                {
                    Destroy(heldToolObject);
                    heldToolObject = null;
                }
                heldTool = null;
            }
        }
        else
        {
            // Use other tools
            if (toolInventory != null)
            {
                toolInventory.UseTool(heldTool);
            }
        }
    }
    
    void DropHeldItem()
    {
        if (heldTool == null || heldToolObject == null) return;
        
        // Calculate drop position in front of the player
        Vector3 dropPosition = playerCamera.transform.position + playerCamera.transform.forward * 2f;
        
        // Unparent and position the object
        heldToolObject.transform.SetParent(null);
        heldToolObject.transform.position = dropPosition;
        heldToolObject.transform.rotation = Quaternion.identity;
        
        // Reset the CollectibleTool component and restore original scale
        CollectibleTool collectible = heldToolObject.GetComponent<CollectibleTool>();
        if (collectible != null)
        {
            collectible.SetBeingHeld(false);
            collectible.RestoreOriginalScale();
        }
        
        // Clear references
        heldToolObject = null;
        heldTool = null;
        
        Debug.Log("Dropped item");
    }
    
    void HandleQuickSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= quickSlots.Length) return;
        
        // Find UI to trigger animations
        InventoryUI inventoryUI = FindFirstObjectByType<InventoryUI>();
        
        if (heldTool == null)
        {
            // Take from slot to hand
            if (!quickSlots[slotIndex].IsEmpty)
            {
                heldTool = quickSlots[slotIndex].toolData;
                heldToolObject = quickSlots[slotIndex].gameObject;
                
                if (heldToolObject != null)
                {
                    // Restore the object from inventory
                    CollectibleTool collectible = heldToolObject.GetComponent<CollectibleTool>();
                    if (collectible != null)
                    {
                        collectible.SetInInventory(false);
                        collectible.SetBeingHeld(true);
                    }
                    
                    // Set up for being held
                    heldToolObject.transform.SetParent(handTransform);
                    
                    // Set specific transform for oxygen tank
                    if (heldTool.toolItem.toolType == ToolItem.ToolType.OxygenTank)
                    {
                        heldToolObject.transform.localPosition = new Vector3(-0.2f, 0f, 0f);
                        heldToolObject.transform.localRotation = Quaternion.Euler(0f, 10f, 90f);
                    }
                    else
                    {
                        // Default position and rotation for other tools
                        heldToolObject.transform.localPosition = Vector3.zero;
                        heldToolObject.transform.localRotation = Quaternion.identity;
                    }
                    
                    // Scale down for holding
                    heldToolObject.transform.localScale = heldToolObject.transform.localScale * 0.5f;
                }
                else
                {
                    // Fallback: create placeholder if GameObject is missing
                    CreateHeldToolObject(heldTool.toolItem);
                }
                
                // Clear slot
                quickSlots[slotIndex].Clear();
                
                // Trigger UI animation
                if (inventoryUI != null)
                {
                    inventoryUI.HighlightSlot(slotIndex);
                }
                
                Debug.Log($"Took {heldTool.toolItem.toolName} from slot {slotIndex + 1}");
            }
        }
        else
        {
            // Put from hand to slot
            if (quickSlots[slotIndex].IsEmpty)
            {
                if (heldToolObject != null)
                {
                    // Store the actual GameObject
                    CollectibleTool collectible = heldToolObject.GetComponent<CollectibleTool>();
                    if (collectible != null)
                    {
                        collectible.SetBeingHeld(false);
                        collectible.SetInInventory(true);
                        collectible.RestoreOriginalScale();
                    }
                    
                    // Unparent and hide
                    heldToolObject.transform.SetParent(null);
                    
                    // Store in slot
                    quickSlots[slotIndex] = new InventorySlot(heldTool, heldToolObject);
                }
                else
                {
                    // Only store tool data if no GameObject
                    quickSlots[slotIndex] = new InventorySlot(heldTool, null);
                }
                
                // Trigger UI animation
                if (inventoryUI != null)
                {
                    inventoryUI.HighlightSlot(slotIndex);
                }
                
                Debug.Log($"Put {heldTool.toolItem.toolName} into slot {slotIndex + 1}");
                
                // Clear hand
                heldTool = null;
                heldToolObject = null;
            }
            else
            {
                Debug.Log($"Slot {slotIndex + 1} is already occupied!");
            }
        }
    }
    
    void CreateHeldToolObject(ToolItem toolItem)
    {
        if (heldToolObject != null)
        {
            Destroy(heldToolObject);
        }
        
        // Create a visual representation of the held tool when taking from inventory
        // Since we can't restore the original GameObject, we'll create a placeholder
        heldToolObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        heldToolObject.transform.SetParent(handTransform);
        
        // Set specific transform for oxygen tank
        if (toolItem.toolType == ToolItem.ToolType.OxygenTank)
        {
            heldToolObject.transform.localPosition = new Vector3(-0.2f, 0f, 0f);
            heldToolObject.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }
        else
        {
            // Default position and rotation for other tools
            heldToolObject.transform.localPosition = Vector3.zero;
            heldToolObject.transform.localRotation = Quaternion.identity;
        }
        
        heldToolObject.transform.localScale = Vector3.one * 0.3f;
        
        // Remove collider from held object
        Collider col = heldToolObject.GetComponent<Collider>();
        if (col != null)
        {
            Destroy(col);
        }
        
        // Color code based on tool type
        Renderer renderer = heldToolObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            switch (toolItem.toolType)
            {
                case ToolItem.ToolType.OxygenTank:
                    renderer.material.color = Color.blue;
                    break;
                case ToolItem.ToolType.Wrench:
                    renderer.material.color = Color.gray;
                    break;
                case ToolItem.ToolType.Flashlight:
                    renderer.material.color = Color.yellow;
                    break;
                default:
                    renderer.material.color = Color.white;
                    break;
            }
        }
        
        // Add a note that this is a placeholder
        heldToolObject.name = $"Placeholder_{toolItem.toolName}";
    }
    
    /// <summary>
    /// Gets the current movement velocity (useful for other systems like oxygen consumption)
    /// </summary>
    public Vector3 GetVelocity()
    {
        return velocity;
    }
    
    /// <summary>
    /// Checks if player is fast swimming (for increased oxygen consumption)
    /// </summary>
    public bool IsFastSwimming()
    {
        return isFastSwimming;
    }
    
    /// <summary>
    /// Rotates the character's body/head towards the movement direction or camera direction
    /// </summary>
    void HandleCharacterRotation()
    {
        if (!rotateBodyToCamera || characterBody == null || playerCamera == null)
        {
            return;
        }
        
        Vector3 targetDirection;
        
        // When treading backward (S key), don't rotate - keep facing camera direction
        if (isMovingBackward)
        {
            // Stay facing camera direction when treading backward
            targetDirection = playerCamera.transform.forward;
        }
        else if (firstPersonMode)
        {
            // In first person: rotate body to face MOVEMENT direction (where they're swimming)
            // This makes the animation point in the correct direction
            if (isMoving && currentMoveDirection.magnitude > 0.1f)
            {
                // Use movement direction for body rotation
                targetDirection = currentMoveDirection;
            }
            else
            {
                // When not moving, use camera direction
                targetDirection = playerCamera.transform.forward;
            }
        }
        else
        {
            // In third person: rotate body to face camera look direction
            targetDirection = playerCamera.transform.forward;
        }
        
        // For swimming, use full 3D rotation so character can point up/down
        // Normalize the direction
        if (targetDirection.magnitude > 0.1f)
        {
            targetDirection.Normalize();
            
            // Calculate target rotation in 3D space
            // This allows the character to rotate to face up/down when swimming vertically
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            
            // Smoothly rotate towards target
            characterBody.rotation = Quaternion.Slerp(
                characterBody.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
        
        // Rotate head to face camera direction (where player is looking)
        if (characterHead != null)
        {
            Vector3 cameraForward = playerCamera.transform.forward;
            
            if (firstPersonMode)
            {
                // In first person: head rotates to match camera exactly (camera is attached to head)
                // But we can still apply slight head rotation for animation purposes
                Vector3 bodyForward = characterBody.forward;
                Vector3 cameraForwardLocal = characterBody.InverseTransformDirection(cameraForward);
                
                // Calculate pitch (up/down) angle
                float pitch = Mathf.Atan2(cameraForwardLocal.y, Mathf.Sqrt(cameraForwardLocal.x * cameraForwardLocal.x + cameraForwardLocal.z * cameraForwardLocal.z)) * Mathf.Rad2Deg;
                pitch = Mathf.Clamp(pitch, -headRotationLimit, headRotationLimit);
                
                // Apply head rotation relative to body
                Quaternion headLocalRotation = Quaternion.Euler(pitch, 0, 0);
                characterHead.localRotation = Quaternion.Slerp(
                    characterHead.localRotation,
                    headLocalRotation,
                    rotationSpeed * Time.deltaTime
                );
            }
            else
            {
                // In third person: head follows camera direction
                Vector3 bodyForward = characterBody.forward;
                Vector3 cameraForwardLocal = characterBody.InverseTransformDirection(cameraForward);
                
                float pitch = Mathf.Atan2(cameraForwardLocal.y, Mathf.Sqrt(cameraForwardLocal.x * cameraForwardLocal.x + cameraForwardLocal.z * cameraForwardLocal.z)) * Mathf.Rad2Deg;
                pitch = Mathf.Clamp(pitch, -headRotationLimit, headRotationLimit);
                
                Quaternion headLocalRotation = Quaternion.Euler(pitch, 0, 0);
                characterHead.localRotation = Quaternion.Slerp(
                    characterHead.localRotation,
                    headLocalRotation,
                    rotationSpeed * Time.deltaTime
                );
            }
        }
    }
    
    /// <summary>
    /// Updates animation parameters based on movement state
    /// </summary>
    void UpdateAnimations()
    {
        if (animator == null)
        {
            return;
        }
        
        // Update swimming/treading states
        // If moving backward (S key), use treading animation instead of swimming
        bool shouldSwim = isMoving && !isMovingBackward;
        bool shouldTread = !isMoving || isMovingBackward;
        
        if (!string.IsNullOrEmpty(isSwimmingParameter) && HasAnimatorParameter(isSwimmingParameter))
        {
            animator.SetBool(isSwimmingParameter, shouldSwim);
        }
        
        if (!string.IsNullOrEmpty(isTreadingParameter) && HasAnimatorParameter(isTreadingParameter))
        {
            animator.SetBool(isTreadingParameter, shouldTread);
        }
        
        // Update swim speed (for animation speed or blend trees)
        if (!string.IsNullOrEmpty(swimSpeedParameter) && HasAnimatorParameter(swimSpeedParameter))
        {
            float normalizedSpeed = velocity.magnitude / swimSpeed;
            if (isFastSwimming)
            {
                normalizedSpeed *= fastSwimMultiplier;
            }
            animator.SetFloat(swimSpeedParameter, normalizedSpeed);
        }
        
        // Update vertical angle for vertical swimming animations
        // This tells the animator if we're swimming up, down, or horizontally
        if (!string.IsNullOrEmpty(verticalAngleParameter) && HasAnimatorParameter(verticalAngleParameter))
        {
            if (isMoving)
            {
                // Calculate the vertical component of movement direction
                // -1 = straight down, 0 = horizontal, 1 = straight up
                float verticalAngle = currentMoveDirection.y;
                animator.SetFloat(verticalAngleParameter, verticalAngle);
            }
            else
            {
                // When not moving, reset to 0 (horizontal)
                animator.SetFloat(verticalAngleParameter, 0f);
            }
        }
    }
    
    /// <summary>
    /// Checks if animator has a parameter with the given name
    /// </summary>
    bool HasAnimatorParameter(string parameterName)
    {
        if (animator == null || string.IsNullOrEmpty(parameterName))
        {
            return false;
        }
        
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == parameterName)
            {
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// Compatibility method for terrain modification systems
    /// Called when terrain changes near the player
    /// </summary>
    public void NotifyTerrainChanged(Vector3 point, float radius)
    {
        // CharacterController doesn't need terrain updates like Rigidbody does
        // This method exists for compatibility with systems like Terraformer
        // You can add custom logic here if needed
    }
    
    /// <summary>
    /// Applies synced rotation to character body for remote players
    /// </summary>
    void ApplySyncedRotation()
    {
        if (characterBody != null && !isLocalPlayer)
        {
            // Smoothly interpolate to the synced rotation
            characterBody.rotation = Quaternion.Slerp(
                characterBody.rotation,
                syncedCharacterBodyRotation,
                rotationSyncSpeed * Time.deltaTime
            );
        }
    }
    
    /// <summary>
    /// Photon serialization - syncs character body rotation for remote players
    /// </summary>
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (characterBody == null)
        {
            return;
        }
        
        if (stream.IsWriting)
        {
            // Local player: send character body rotation
            stream.SendNext(characterBody.rotation);
        }
        else
        {
            // Remote player: receive character body rotation
            syncedCharacterBodyRotation = (Quaternion)stream.ReceiveNext();
        }
    }
}