using UnityEngine;
using Mirror;

public class CustomRoomPlayer : NetworkRoomPlayer
{
    public const int MAX_PLAYERS_PER_TEAM = 3;

    [Header("Selección de Equipo en Lobby")]
    [SyncVar(hook = nameof(OnTeamSelectedChanged))]
    public int selectedTeam = 1; // 1 = Equipo Azul, 2 = Equipo Rojo

    public override void OnStartServer()
    {
        base.OnStartServer();
        // Al entrar a la sala, asignarle automáticamente un equipo que tenga espacio (< 3)
        int countA = CountPlayersInTeam(1);
        int countB = CountPlayersInTeam(2);

        if (countA <= countB && countA < MAX_PLAYERS_PER_TEAM)
            selectedTeam = 1;
        else if (countB < MAX_PLAYERS_PER_TEAM)
            selectedTeam = 2;
        else
            selectedTeam = 1;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
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

    [Command]
    public void CmdSelectTeam(int teamId)
    {
        if (teamId != 1 && teamId != 2) return;
        if (selectedTeam == teamId) return;

        // Validar en el servidor que el equipo destino no tenga ya 3 jugadores
        if (CountPlayersInTeam(teamId) < MAX_PLAYERS_PER_TEAM)
        {
            selectedTeam = teamId;
        }
    }

    public static int CountPlayersInTeam(int teamId)
    {
        int count = 0;
        CustomRoomPlayer[] players = FindObjectsByType<CustomRoomPlayer>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p != null && p.selectedTeam == teamId)
            {
                count++;
            }
        }
        return count;
    }

    private void RefreshLobbyUI()
    {
        if (LobbyUIManager.Instance != null)
        {
            LobbyUIManager.Instance.UpdatePlayerList();
        }
    }
} 