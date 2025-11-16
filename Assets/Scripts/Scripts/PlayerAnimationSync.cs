using UnityEngine;
using Photon.Pun;

/// <summary>
/// Automatically sets up PhotonAnimatorView to sync animation parameters for multiplayer.
/// This ensures remote players see the correct animations.
/// </summary>
[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(Animator))]
public class PlayerAnimationSync : MonoBehaviour
{
    private PhotonAnimatorView photonAnimatorView;
    private Animator animator;
    
    void Awake()
    {
        // Find Animator - check children first (common for character models)
        animator = GetComponentInChildren<Animator>();
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        
        if (animator == null)
        {
            Debug.LogError("PlayerAnimationSync: No Animator found on " + gameObject.name + " or its children!");
            return;
        }
        
        // Get or add PhotonAnimatorView - must be on same GameObject as PhotonView
        photonAnimatorView = GetComponent<PhotonAnimatorView>();
        if (photonAnimatorView == null)
        {
            photonAnimatorView = gameObject.AddComponent<PhotonAnimatorView>();
        }
        
        // PhotonAnimatorView only looks for Animator on the same GameObject (GetComponent)
        // If Animator is on a child, we need to set it manually via reflection
        if (animator != null && animator.gameObject != gameObject)
        {
            // Animator is on a child - set it in PhotonAnimatorView using reflection
            var animatorField = typeof(PhotonAnimatorView).GetField("m_Animator", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (animatorField != null)
            {
                animatorField.SetValue(photonAnimatorView, animator);
                Debug.Log($"PlayerAnimationSync: Set Animator reference from child object: {animator.gameObject.name}");
            }
            else
            {
                Debug.LogWarning("PlayerAnimationSync: Could not set Animator reference in PhotonAnimatorView (reflection failed)");
            }
        }
        
        SetupAnimationSync();
    }
    
    void SetupAnimationSync()
    {
        if (photonAnimatorView == null || animator == null)
        {
            Debug.LogWarning("PlayerAnimationSync: Missing PhotonAnimatorView or Animator component!");
            return;
        }
        
        // Get PhotonView to add PhotonAnimatorView to ObservedComponents
        PhotonView pv = GetComponent<PhotonView>();
        if (pv != null && pv.ObservedComponents != null)
        {
            // Add PhotonAnimatorView to ObservedComponents if not already there
            if (!pv.ObservedComponents.Contains(photonAnimatorView))
            {
                pv.ObservedComponents.Add(photonAnimatorView);
            }
        }
        
        // Sync animation parameters that are commonly used
        // These will be set up automatically if they exist in the Animator Controller
        
        // SwimSpeed (Float) - for swim speed animation
        if (HasParameter("SwimSpeed"))
        {
            photonAnimatorView.SetParameterSynchronized(
                "SwimSpeed", 
                PhotonAnimatorView.ParameterType.Float, 
                PhotonAnimatorView.SynchronizeType.Continuous
            );
        }
        
        // IsSwimming (Bool) - swimming state
        if (HasParameter("IsSwimming"))
        {
            photonAnimatorView.SetParameterSynchronized(
                "IsSwimming", 
                PhotonAnimatorView.ParameterType.Bool, 
                PhotonAnimatorView.SynchronizeType.Discrete
            );
        }
        
        // IsTreading (Bool) - treading water state
        if (HasParameter("IsTreading"))
        {
            photonAnimatorView.SetParameterSynchronized(
                "IsTreading", 
                PhotonAnimatorView.ParameterType.Bool, 
                PhotonAnimatorView.SynchronizeType.Discrete
            );
        }
        
        // VerticalAngle (Float) - vertical swim direction
        if (HasParameter("VerticalAngle"))
        {
            photonAnimatorView.SetParameterSynchronized(
                "VerticalAngle", 
                PhotonAnimatorView.ParameterType.Float, 
                PhotonAnimatorView.SynchronizeType.Continuous
            );
        }
        
        // Speed Percent (Float) - from CharacterAnimator
        if (HasParameter("Speed Percent"))
        {
            photonAnimatorView.SetParameterSynchronized(
                "Speed Percent", 
                PhotonAnimatorView.ParameterType.Float, 
                PhotonAnimatorView.SynchronizeType.Continuous
            );
        }
        
        // Air (Bool) - grounded/airborne state
        if (HasParameter("Air"))
        {
            photonAnimatorView.SetParameterSynchronized(
                "Air", 
                PhotonAnimatorView.ParameterType.Bool, 
                PhotonAnimatorView.SynchronizeType.Discrete
            );
        }
        
        // Debug: Log what parameters were found and configured
        int paramCount = 0;
        foreach (var param in animator.parameters)
        {
            var syncType = photonAnimatorView.GetParameterSynchronizeType(param.name);
            if (syncType != PhotonAnimatorView.SynchronizeType.Disabled)
            {
                paramCount++;
                Debug.Log($"PlayerAnimationSync: Configured '{param.name}' ({param.type}) as {syncType}");
            }
        }
        
        if (paramCount == 0)
        {
            Debug.LogWarning("PlayerAnimationSync: No animation parameters were configured! Check if parameters exist in Animator Controller.");
            Debug.LogWarning($"PlayerAnimationSync: Available parameters in Animator: {string.Join(", ", System.Array.ConvertAll(animator.parameters, p => p.name))}");
        }
        else
        {
            Debug.Log($"PlayerAnimationSync: {paramCount} animation parameter(s) configured for Photon synchronization.");
        }
    }
    
    /// <summary>
    /// Checks if the Animator has a parameter with the given name
    /// </summary>
    bool HasParameter(string parameterName)
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
}

