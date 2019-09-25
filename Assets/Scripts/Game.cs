using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Game : MonoBehaviour
{
    public Button ServerButton;
    public Button ClientButton;
    public Button SendMsgToServerBtn;
    public Button BroadcastMsgBtn;
    public Button GetMessagePendingBtn;
    public Button ScanBtn;

    private LanManager mLanManager;

    void Start()
    {
        mLanManager = new LanManager();

        ServerButton.onClick.AddListener(() =>
        {
            Debug.Log("starting as server");
            mLanManager.StartServer();
        });

        ClientButton.onClick.AddListener(() =>
        {
            mLanManager.Log("starting as client");
            mLanManager.StartClientLocal();
        });

        SendMsgToServerBtn.onClick.AddListener(() =>
        {
            mLanManager.Log("sending message");
            mLanManager.SendMessageToServer(mLanManager.GetPrivateClientSocket(), Time.deltaTime.ToString());
        });

        BroadcastMsgBtn.onClick.AddListener(() =>
        {
            mLanManager.Log("broadcast msg button");
            mLanManager.BroadcastMsg(Time.deltaTime.ToString(), null);
        });

        //Polling
        GetMessagePendingBtn.onClick.AddListener(() =>
        {
            mLanManager.GetPendingMessage(mLanManager.GetPrivateClientSocket());
        });

        ScanBtn.onClick.AddListener(() =>
        {
            mLanManager.ScanHost();
        });
    }

}
