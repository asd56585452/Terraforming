using UnityEngine;
using Photon.Pun;

/// <summary>
/// Handles 3D swimming and diving movement for the player.
/// Movement is relative to where the player is looking - swim in the direction you face!
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
    
    void Start()
    {
        controller = GetComponent<CharacterController>();
        
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
        waterSystem = FindObjectOfType<Water>();
        
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
        
        // Update animations at the end of Update() so PhotonAnimatorView can read correct values
        // PhotonAnimatorView reads parameters in its Update(), so we need to set them before that
        if (isLocalPlayer)
        {
            UpdateAnimations(); // This sets animator parameters which PhotonAnimatorView will sync
        }
    }
    
    void LateUpdate()
    {
        // Only update camera for local player
        if (!isLocalPlayer || playerCamera == null)
        {
            return;
        }
        
        // Update camera position in LateUpdate to avoid wobbling
        // This runs after all animations and rotations are applied
        if (firstPersonMode && characterHead != null)
        {
            playerCamera.transform.position = characterHead.position + characterHead.TransformDirection(cameraOffset);
        }
    }
    
    void UpdateUnderwaterStatus()
    {
        if (waterSystem != null && playerCamera != null)
        {
            // Use camera position for underwater detection (same as FirstPersonController)
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
            // This ensures W+A/D moves at the same speed as just W
            float normalizedHorizontal = horizontal / inputMagnitude;
            float normalizedVertical = vertical / inputMagnitude;
            
            // Clamp input magnitude to 1 (Unity's Input.GetAxis can sometimes exceed 1)
            inputMagnitude = Mathf.Clamp01(inputMagnitude);
            
            // Get camera-relative directions
            // Forward/backward relative to where camera is looking (includes vertical component)
            Vector3 cameraForward = playerCamera.transform.forward;
            // Left/right relative to camera
            Vector3 cameraRight = playerCamera.transform.right;
            
            // Calculate movement direction in 3D space
            // This properly handles diagonal movement in all directions
            Vector3 moveDirection = (cameraForward * normalizedVertical + cameraRight * normalizedHorizontal);
            
            // Normalize the final direction to ensure consistent speed in all directions
            // This is crucial for smooth diagonal movement
            moveDirection.Normalize();
            
            // Calculate speed
            float currentSpeed = swimSpeed;
            if (isFastSwimming)
            {
                currentSpeed *= fastSwimMultiplier;
            }
            
            // Apply movement with input magnitude to preserve analog input feel
            velocity = moveDirection * currentSpeed * inputMagnitude;
            currentMoveDirection = moveDirection; // Store for animation
            isMoving = true;
            
            // Check if moving backward (S key - negative vertical input)
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
        // This prevents drag from fighting against player input and causing stuttering
        if (!isMoving)
        {
            // Apply water drag - higher drag value = faster deceleration
            // This reduces the "sliding on ice" effect
            float dragFactor = Mathf.Clamp01(waterDrag * Time.deltaTime);
            velocity *= (1f - dragFactor);
        }
        
        // Move the character
        controller.Move(velocity * Time.deltaTime);
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