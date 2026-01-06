using System;
using System.Collections;
using Discord.Sdk;
using UnityEngine;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour
{
    [SerializeField] private Button createLobbyButton;
    [SerializeField] private Button leaveLobbyButton;
    [SerializeField] private int maxLobbySize = 4;
    private string lobbySecret;
    private ulong currentLobby;
    private RichPresence richPresence;
    private Client client;

    private void Start()
    {
        richPresence = FindFirstObjectByType<RichPresence>();

        createLobbyButton.onClick.AddListener(CreateLobby);
        leaveLobbyButton.onClick.AddListener(LeaveLobby);

        createLobbyButton.gameObject.SetActive(false);
        leaveLobbyButton.gameObject.SetActive(false);
    }

    public void InitializeLobbyCreation(Client client)
    {
        this.client = client;
        createLobbyButton.gameObject.SetActive(true);
    }

    public void CreateLobby()
    {
        StopAllCoroutines();
        createLobbyButton.gameObject.SetActive(false);
        lobbySecret = Guid.NewGuid().ToString();
        client.CreateOrJoinLobby(lobbySecret, OnCreateOrJoinLobby);
    }

    public void JoinLobby(string lobbySecret)
    {
        StopAllCoroutines();
        createLobbyButton.gameObject.SetActive(false);
        StartCoroutine(JoinLobbyCoroutine(lobbySecret));
    }

    private IEnumerator JoinLobbyCoroutine(string lobbySecret)
    {
        yield return new WaitUntil(() => { return client.GetStatus() == Client.Status.Ready; });
        this.lobbySecret = lobbySecret;
        client.CreateOrJoinLobby(this.lobbySecret, OnCreateOrJoinLobby);
    }

    private void OnCreateOrJoinLobby(ClientResult clientResult, ulong lobbyId)
    {
        if (clientResult.Successful())
        {
            currentLobby = lobbyId;

            leaveLobbyButton.gameObject.SetActive(true);

            if (richPresence != null)
                richPresence.UpdateRichPresenceLobby(client, "In Lobby", "Waiting for players", lobbySecret,
                    lobbyId.ToString(), maxLobbySize);

            Debug.Log($"Successfully created or joined lobby {lobbyId}");
        }
        else
        {
            createLobbyButton.gameObject.SetActive(true);

            Debug.LogError($"Failed to create or join lobby: {clientResult}");
        }
    }

    public void LeaveLobby()
    {
        leaveLobbyButton.gameObject.SetActive(false);

        client.LeaveLobby(currentLobby, OnLeaveLobby);
    }

    private void OnLeaveLobby(ClientResult clientResult)
    {
        if (clientResult.Successful())
        {
            currentLobby = 0;
            lobbySecret = string.Empty;

            createLobbyButton.gameObject.SetActive(true);

            if (richPresence != null) richPresence.UpdateRichPresence(client);

            Debug.Log($"Successfully left lobby {currentLobby}");
        }
        else
        {
            leaveLobbyButton.gameObject.SetActive(true);

            Debug.LogError($"Failed to leave lobby: {clientResult}");
        }
    }
}