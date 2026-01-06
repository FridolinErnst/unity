using Discord.Sdk;
using UnityEngine;
using TMPro;

public class MessageManager : MonoBehaviour
{
    [SerializeField]
    private GameObject messagePanel;
    [SerializeField]
    private GameObject messageUIPrefab;
    [SerializeField]
    private TMP_InputField messageInputField;
    [SerializeField]
    private Transform messageScrollContainer;

    private Client client;
    private ulong currentUserId;

    void Start()
    {
        messagePanel.SetActive(false);
        messageInputField.onEndEdit.AddListener(SendDirectMessage);
    }

    public void InitializeMessageManager(Client client)
    {
        this.client = client;
    }

    public void OpenMessageUI(ulong userId)
    {
        currentUserId = userId;
        messagePanel.SetActive(true);
        for (int i = messageScrollContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(messageScrollContainer.GetChild(i).gameObject);
        }
    }

    private void SendDirectMessage(string message)
    {
        if(message == string.Empty)
        {
            return;
        }
        client.SendUserMessage(currentUserId, message, OnMessageSent);
        messageInputField.text = string.Empty;
    }

    private void OnMessageSent(ClientResult result, ulong messageId)
    {
        if (result.Successful())
        {
            Debug.Log("Message sent successfully.");
        }
        else
        {
            Debug.LogError("Failed to send message: " + result.Error());
        }
    }

    public void MessageReceived(ulong messageId)
    {
        MessageHandle message = client.GetMessageHandle(messageId);
        if (message != null && (message.Author().Id() == currentUserId || message.Author().Id() == client.GetCurrentUserV2().Id()))
        {
            GameObject messageUI = Instantiate(messageUIPrefab, messageScrollContainer);
            TextMeshProUGUI messageUIText = messageUI.GetComponent<TextMeshProUGUI>();
            messageUIText.text = $"{message.Author().DisplayName()}: {message.Content()}";
        }
    }
}