using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Photon.Pun;

public class StartSceneManager : MonoBehaviourPunCallbacks
{   
    public GameObject RoleSelectCanvas, ReadyCanvas;
    public GameObject StartButton;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        if (!PhotonNetwork.IsConnected) PhotonNetwork.ConnectUsingSettings();
        RoleSelectCanvas.SetActive(true);
        ReadyCanvas.SetActive(false);
    }
    void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
    }
    public void MasterClientCreateRoom()
    {
        PhotonNetwork.CreateRoom("Room");
    }
    public void ClientJoinRoom()
    {
        PhotonNetwork.JoinRoom("Room");
    }
    public override void OnCreatedRoom()
    {
        RoleSelectCanvas.SetActive(false);
        ReadyCanvas.SetActive(true);
    }
    public override void OnJoinedRoom()
    {
        RoleSelectCanvas.SetActive(false);
        ReadyCanvas.SetActive(true);
        if (!PhotonNetwork.IsMasterClient) StartButton.SetActive(false);
    }
    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogWarning("Create room failed: " + message);
        return;
    }
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogWarning("Join room failed: " + message);
        return;
    }
    public void LoadGameScene()
    {   
        if (PhotonNetwork.IsMasterClient) PhotonNetwork.LoadLevel("GameScene");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
