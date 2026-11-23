using UnityEngine;
using Mirror;

public class TeamRoomManager : NetworkRoomManager
{
    [Header("Colores de Equipo")]
    public Color teamAColor = new Color(0.2f, 0.8f, 1f);
    public Color teamBColor = new Color(1f, 0.35f, 0.35f);

    public override bool OnRoomServerSceneLoadedForPlayer(NetworkConnectionToClient conn, GameObject roomPlayer, GameObject gamePlayer)
    {
        int chosenTeam = 1;

        if (roomPlayer != null && roomPlayer.TryGetComponent<CustomRoomPlayer>(out CustomRoomPlayer customRP))
        {
            chosenTeam = customRP.selectedTeam;
        }

        Color assignedColor = chosenTeam == 1 ? teamAColor : teamBColor;

        if (gamePlayer != null && gamePlayer.TryGetComponent<TeamMember>(out TeamMember teamMember))
        {
            teamMember.SetTeam(chosenTeam, assignedColor);
        }

        return true;
    }

    // Método para iniciar la partida desde tu botón "Iniciar Juego" (incluso con 1 solo jugador)
    [Server]
    public void ForceStartGame()
    {
        ServerChangeScene(GameplayScene);
    }
}