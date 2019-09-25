using System;
using System.Collections;
using System.Collections.Generic;
using RB_LANCHAT;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatBox : MonoBehaviour {
    [SerializeField] private TextMeshProUGUI m_UserNameText;
    [SerializeField] private TextMeshProUGUI m_ChatBoxTxt;
    [SerializeField] private Button m_SendBtn;
    [SerializeField] private TMP_InputField m_ChatEditBox;

    private NetworkPlayer mPlayerSocket;

    void Start () {
        m_ChatBoxTxt.text = "....";
        m_SendBtn.onClick.RemoveAllListeners ();
        m_SendBtn.onClick.AddListener (() => {
            if (string.IsNullOrEmpty (m_ChatEditBox.text)) {
                Debug.LogWarning ("Empty field!!");
                return;
            }

            Debug.Log ("Send btn clicked::");
            SimpleMessagePacket smp = new SimpleMessagePacket ();
            smp.msg = m_ChatEditBox.text;
            smp.fromIP = LobbyManager.HOST_IP;
            smp.fromName = LobbyManager.HOST_NAME;

            mPlayerSocket?.Send (JsonUtility.ToJson (smp));
        });
    }

    public void InitializeChat (NetworkPlayer _playerSocket) {
        mPlayerSocket = _playerSocket;
        m_UserNameText.text = LobbyManager.HOST_NAME;
    }

    public void AddToBox (string _msg) {
        m_ChatBoxTxt.text += "\n" + _msg;
    }
}