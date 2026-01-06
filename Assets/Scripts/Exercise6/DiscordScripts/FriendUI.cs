using System.Collections;
using Discord.Sdk;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class FriendUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI friendNameText;

    [SerializeField] private TextMeshProUGUI friendStatusText;

    [SerializeField] private Image friendAvatarImage;

    [SerializeField] private Button inviteButton;

    private Client client;
    public RelationshipHandle relationshipHandle { get; private set; }

    public void Initialize(Client client, RelationshipHandle relationshipHandle)
    {
        this.client = client;
        this.relationshipHandle = relationshipHandle;
        friendNameText.text = relationshipHandle.User().DisplayName();
        friendStatusText.text = relationshipHandle.User().Status().ToString();
        StartCoroutine(LoadAvatarFromUrl(relationshipHandle.User()
            .AvatarUrl(UserHandle.AvatarType.Png, UserHandle.AvatarType.Png)));
        inviteButton.onClick.AddListener(OnInviteButtonClick);
    }

    public void UpdateFriend()
    {
        friendNameText.text = relationshipHandle.User().DisplayName();
        friendStatusText.text = relationshipHandle.User().Status().ToString();
    }

    private IEnumerator LoadAvatarFromUrl(string url)
    {
        using (var request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var texture = DownloadHandlerTexture.GetContent(request);
                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f));
                friendAvatarImage.sprite = sprite;
            }
            else
            {
                Debug.LogError($"Failed to load profile image from URL: {url}. Error: {request.error}");
            }
        }
    }

    private void OnInviteButtonClick()
    {
        if (relationshipHandle != null)
        {
            var discordManager = FindFirstObjectByType<DiscordManager>();
            if (discordManager != null)
                discordManager.SendInvite(relationshipHandle.User().Id());
            else
                Debug.LogError("DiscordManager not found!");
        }
    }
}