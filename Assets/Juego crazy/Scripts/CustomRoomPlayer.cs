using UnityEngine;
using Mirror;

public class CustomRoomPlayer : NetworkRoomPlayer
{
    [Header("Selección de Equipo en Lobby")]
    [SyncVar(hook = nameof(OnTeamSelectedChanged))]
    public int selectedTeam = 1; // 1 = Equipo Azul, 2 = Equipo Rojo

    public override void OnStartClient()
    {
        base.OnStartClient();
        // Si es nuestro jugador local del lobby, le avisamos a la UI para que se conecte con nosotros
        if (isLocalPlayer && LobbyUIManager.Instance != null)
        {
            LobbyUIManager.Instance.RegisterLocalRoomPlayer(this);
        }
        RefreshLobbyUI();
    }

    public override void OnClientEnterRoom()
    {
        base.OnClientEnterRoom();
        RefreshLobbyUI();
    }

    public override void OnClientExitRoom()
    {
        base.OnClientExitRoom();
        RefreshLobbyUI();
    }

    public override void ReadyStateChanged(bool oldReadyState, bool newReadyState)
    {
        base.ReadyStateChanged(oldReadyState, newReadyState);
        RefreshLobbyUI();
    }

    private void OnTeamSelectedChanged(int oldTeam, int newTeam)
    {
        RefreshLobbyUI();
    }

    // Llamado desde los botones de la UI del Lobby
    [Command]
    public void CmdSelectTeam(int teamId)
    {
        selectedTeam = teamId;
    }

    private void RefreshLobbyUI()
    {
        if (LobbyUIManager.Instance != null)
        {
            LobbyUIManager.Instance.UpdatePlayerList();
        }
    }
}