using System.Collections.Generic;
using Discord.Sdk;
using UnityEngine;

public class FriendsList : MonoBehaviour
{
    [SerializeField] private GameObject friendUIPrefab;

    [SerializeField] private Transform friendListContentTransform;

    private readonly List<GameObject> friendUIObjects = new();

    public void LoadFriends(Client client)
    {
        var relationships = client.GetRelationships();
        foreach (var relationship in relationships)
        {
            var friendUI = Instantiate(friendUIPrefab, friendListContentTransform);
            friendUI.GetComponent<FriendUI>().Initialize(client, relationship);
            friendUIObjects.Add(friendUI);
        }

        SortFriends();
    }

    // Discord users can change their name or online status, use this to keep the UI up to date
    public void UpdateFriends()
    {
        foreach (var friendUIObject in friendUIObjects) friendUIObject.GetComponent<FriendUI>().UpdateFriend();
    }

    public void SortFriends()
    {
        // Sort friends by online status and then by display name
        friendUIObjects.Sort((a, b) =>
        {
            var friendA = a.GetComponent<FriendUI>();
            var friendB = b.GetComponent<FriendUI>();

            RelationshipHandle relationshipA = friendA.relationshipHandle;
            RelationshipHandle relationshipB = friendB.relationshipHandle;

            var statusA = relationshipA.User().Status();
            var statusB = relationshipB.User().Status();

            if (statusA != statusB)
                return statusA.CompareTo(statusB);

            return relationshipA.User().DisplayName().CompareTo(relationshipB.User().DisplayName());
        });

        // Reorder the friend UI elements in the hierarchy after sorting
        for (var i = 0; i < friendUIObjects.Count; i++) friendUIObjects[i].transform.SetSiblingIndex(i);
    }
}