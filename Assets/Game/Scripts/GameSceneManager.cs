using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Photon.Pun;

public class GameSceneManager : MonoBehaviourPunCallbacks
{   
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GameObject player = PhotonNetwork.Instantiate($"Prefabs/Player", new Vector3(0, 250, 0), Quaternion.identity);
        GameObject MainCam = GameObject.FindGameObjectWithTag("MainCamera");
		MainCam.transform.SetParent(player.transform, false);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
