using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightThrower : MonoBehaviour
{

	public StickyLight lightPrefab;
	public Transform spawnPoint;
	Rigidbody rb;

	// Support both old and new controller systems
	FirstPersonController oldController;
	PlayerSwimmingController newController;
	Terraformer terraformer;

	void Start()
	{
		// Try to find new controller first (your system)
		newController = GetComponent<PlayerSwimmingController>();
		// Fallback to old controller if new one not found
		if (newController == null)
		{
			oldController = GetComponent<FirstPersonController>();
		}
		rb = GetComponent<Rigidbody>();
		terraformer = FindObjectOfType<Terraformer>();
	}


	void Update()
	{
		if (Input.GetKeyDown(KeyCode.Q))
		{
			// Get velocity - use Rigidbody if available, otherwise zero
			Vector3 velocity = (rb != null) ? rb.linearVelocity : Vector3.zero;
			
			// Get gravity - use controller's gravity if available, otherwise use Physics.gravity
			float gravity = 0f;
			if (oldController != null)
			{
				gravity = oldController.gravity;
			}
			else
			{
				// Default gravity value (you can adjust this)
				gravity = Physics.gravity.y;
			}
			
			var l = Instantiate(lightPrefab, spawnPoint.position, spawnPoint.rotation);
			l.Init(velocity, gravity, terraformer);
		}
	}
}
