using UnityEngine;
using Mirror;
using TMPro;

public class PlayerName : NetworkBehaviour
{
    
    [SyncVar(hook = nameof(OnNameChanged))]
    public string playerName;

    [Header("UI")]
    [SerializeField] private TMP_Text nameText; 

    public override void OnStartServer()
    {
        base.OnStartServer();
        playerName = $"Player{connectionToClient.connectionId + 1}";
    }

    private void OnNameChanged(string oldName, string newName)
    {
        if (nameText != null)
        {
            nameText.text = newName;
        }
    }
}
