using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Photon.Pun;

public class GameSceneManager : MonoBehaviourPunCallbacks
{   
    public static GameObject LocalPlayerInstance;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Only instantiate player if we're in a room
        if (PhotonNetwork.InRoom)
        {
            SpawnPlayer();
        }
        else
        {
            Debug.LogWarning("Not in a Photon room. Player will not be spawned.");
        }
    }
    
    void SpawnPlayer()
    {
        // Check if local player already exists (prevents duplicate spawning on scene reload)
        if (LocalPlayerInstance != null)
        {
            Debug.LogWarning("Local player already exists. Not spawning again.");
            return;
        }
        
        // Instantiate player via PhotonNetwork
        GameObject player = PhotonNetwork.Instantiate("Prefabs/Player", new Vector3(0, 250, 0), Quaternion.identity);
        
        if (player != null)
        {
            // Get PhotonView to check if this is the local player
            PhotonView pv = player.GetComponent<PhotonView>();
            if (pv != null && pv.IsMine)
            {
                LocalPlayerInstance = player;
                
                // Setup camera for local player only
                GameObject MainCam = GameObject.FindGameObjectWithTag("MainCamera");
                if (MainCam != null)
                {
                    // Disable the scene camera
                    Camera sceneCam = MainCam.GetComponent<Camera>();
                    if (sceneCam != null)
                    {
                        sceneCam.enabled = false;
                    }
                    
                    // The player's camera should already be set up in PlayerSwimmingController
                    // But if there's a scene camera, we can parent it as backup
                    // MainCam.transform.SetParent(player.transform, false);
                }
                
                Debug.Log("Local player spawned successfully!");
            }
        }
        else
        {
            Debug.LogError("Failed to instantiate player prefab. Make sure 'Prefabs/Player' exists in Resources folder.");
        }
    }
    
    // Called when player joins room (if scene loads before joining)
    public override void OnJoinedRoom()
    {
        if (LocalPlayerInstance == null)
        {
            SpawnPlayer();
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
