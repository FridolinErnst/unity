using System;
using Discord.Sdk;
using UnityEngine;

public class RichPresence : MonoBehaviour
{
    [SerializeField] private string details = "In Unity";

    [SerializeField] private string state = "Building a game";

    private ulong startTimestamp;

    private void Start()
    {
        startTimestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public void UpdateRichPresence(Client client)
    {
        var activity = new Activity();

        activity.SetType(ActivityTypes.Playing);
        activity.SetDetails(details);
        activity.SetState(state);

        var activityTimestamp = new ActivityTimestamps();
        activityTimestamp.SetStart(startTimestamp);
        activity.SetTimestamps(activityTimestamp);

        client.UpdateRichPresence(activity, OnUpdateRichPresence);
    }

    private void OnUpdateRichPresence(ClientResult result)
    {
        if (result.Successful())
            Debug.Log("Rich presence updated!");
        else
            Debug.LogError($"Failed to update rich presence {result.Error()}");
    }

    public void UpdateRichPresenceLobby(Client client, string state, string details, string lobbySecret, string lobbyId,
        int maxLobbySize)
    {
        var activity = new Activity();

        activity.SetType(ActivityTypes.Playing);
        activity.SetState(state);
        activity.SetDetails(details);

        var activityTimestamp = new ActivityTimestamps();
        activityTimestamp.SetStart(startTimestamp);
        activity.SetTimestamps(activityTimestamp);

        var activityParty = new ActivityParty();
        activityParty.SetId(lobbyId);
        activityParty.SetCurrentSize(1);
        activityParty.SetMaxSize(maxLobbySize);
        activity.SetParty(activityParty);

        var activitySecrets = new ActivitySecrets();
        activitySecrets.SetJoin(lobbySecret);
        activity.SetSecrets(activitySecrets);

        client.UpdateRichPresence(activity, OnUpdateRichPresence);
    }
}