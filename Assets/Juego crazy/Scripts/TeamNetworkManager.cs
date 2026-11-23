using UnityEngine;
using Mirror;

public class TeamRoomManager : NetworkRoomManager
{
    [Header("Colores de Equipo")]
    [SerializeField] private Color teamAColor = new Color(0.2f, 0.8f, 1f);
    [SerializeField] private Color teamBColor = new Color(1f, 0.35f, 0.35f);

    // Este método se ejecuta en el servidor por cada jugador al cargar la escena de juego (RoomScene)
    public override bool OnRoomServerSceneLoadedForPlayer(NetworkConnectionToClient conn, GameObject roomPlayer, GameObject gamePlayer)
    {
        // Usamos el índice del jugador en la sala (0, 1, 2, 3...) para alternar equipos
        int index = 0;
        if (roomPlayer != null && roomPlayer.TryGetComponent<NetworkRoomPlayer>(out NetworkRoomPlayer nrp))
        {
            index = nrp.index;
        }

        int assignedTeam = (index % 2 == 0) ? 1 : 2;
        Color assignedColor = assignedTeam == 1 ? teamAColor : teamBColor;

        if (gamePlayer != null && gamePlayer.TryGetComponent<TeamMember>(out TeamMember teamMember))
        {
            teamMember.SetTeam(assignedTeam, assignedColor);
        }

        return true;
    }
}