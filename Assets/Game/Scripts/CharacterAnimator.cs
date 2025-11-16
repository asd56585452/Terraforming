using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class CharacterAnimator : MonoBehaviour
{
	Animator animator;
	// Support both old and new controller systems
	FirstPersonController oldController;
	PlayerSwimmingController newController;

	float speedPercent;
	
	// Photon networking
	private PhotonView photonView;
	private bool isLocalPlayer = false;

	void Start()
	{
		animator = GetComponentInChildren<Animator>();
		
		// Check if this is a networked player (Photon)
		photonView = GetComponent<PhotonView>();
		if (photonView != null)
		{
			isLocalPlayer = photonView.IsMine;
		}
		else
		{
			// If no PhotonView, assume single player (local)
			isLocalPlayer = true;
		}
		
		// Try to find new controller first (your system)
		newController = GetComponent<PlayerSwimmingController>();
		// Fallback to old controller if new one not found
		if (newController == null)
		{
			oldController = GetComponent<FirstPersonController>();
		}
	}


	void Update()
	{
		// Only update animations for local player
		// Remote players get animation parameters synced via PhotonAnimatorView
		if (!isLocalPlayer)
		{
			return;
		}
		
		// Use new controller if available, otherwise use old one
		PlayerSwimmingController.MoveState state;
		bool isGrounded;
		
		if (newController != null)
		{
			state = newController.currentMoveState;
			isGrounded = newController.grounded;
		}
		else if (oldController != null)
		{
			// Convert old enum to new enum (they have same values)
			state = (PlayerSwimmingController.MoveState)(int)oldController.currentMoveState;
			isGrounded = oldController.grounded;
		}
		else
		{
			// No controller found, skip animation updates
			return;
		}

		float targetSpeedPercent = 0;
		if (state == PlayerSwimmingController.MoveState.Walk)
		{
			targetSpeedPercent = 0.5f;
		}
		else if (state == PlayerSwimmingController.MoveState.Run)
		{
			targetSpeedPercent = 1;
		}
		else if (state == PlayerSwimmingController.MoveState.Swim)
		{
			targetSpeedPercent = 0.5f;
		}

		speedPercent = Mathf.Lerp(speedPercent, targetSpeedPercent, Time.deltaTime * 3);

		animator.SetFloat("Speed Percent", speedPercent);
		animator.SetBool("Air", !isGrounded);
	}
}
