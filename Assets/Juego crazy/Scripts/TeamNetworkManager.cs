using System.Collections;
using UnityEngine;
using Mirror;

public class TeamRoomManager : NetworkRoomManager
{
    [Header("Colores de Equipo")]
    public Color teamAColor = new Color(0.2f, 0.8f, 1f);
    public Color teamBColor = new Color(1f, 0.35f, 0.35f);

    [Header("Configuración de Bots")]
    [SerializeField] private GameObject botPrefab;
    [SerializeField] private int targetPlayersPerTeam = 3;
    [SerializeField]
    private Vector2[] teamASpawnPoints = new Vector2[]
    {
        new Vector2(-4f, 2f),
        new Vector2(-4f, 0f),
        new Vector2(-4f, -2f)
    };
    [SerializeField]
    private Vector2[] teamBSpawnPoints = new Vector2[]
    {
        new Vector2(4f, 2f),
        new Vector2(4f, 0f),
        new Vector2(4f, -2f)
    };

    private int humanCountTeamA = 0;
    private int humanCountTeamB = 0;

    public override void OnRoomServerPlayersReady()
    {
        // Evita que arranque solo antes de darle a tu botón Iniciar si así lo prefieres
    }

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

        // Posicionar al jugador humano en su lado de la cancha
        if (chosenTeam == 1)
        {
            int idx = Mathf.Clamp(humanCountTeamA, 0, teamASpawnPoints.Length - 1);
            gamePlayer.transform.position = teamASpawnPoints[idx];
            humanCountTeamA++;
        }
        else
        {
            int idx = Mathf.Clamp(humanCountTeamB, 0, teamBSpawnPoints.Length - 1);
            gamePlayer.transform.position = teamBSpawnPoints[idx];
            humanCountTeamB++;
        }

        return true;
    }

    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);

        // Reiniciar contadores al entrar a la cancha y lanzar el spawner de bots
        if (sceneName == GameplayScene)
        {
            humanCountTeamA = 0;
            humanCountTeamB = 0;
            StartCoroutine(SpawnBotsAfterPlayersRoutine());
        }
    }

    [Server]
    private IEnumerator SpawnBotsAfterPlayersRoutine()
    {
        // Dar un pequeño margen para que Mirror termine de spawnear a los jugadores humanos
        yield return new WaitForSeconds(0.15f);

        if (botPrefab == null)
        {
            Debug.LogWarning("Falta asignar el Bot Prefab en TeamRoomManager.");
            yield break;
        }

        // Contar únicamente los jugadores humanos (tienen PlayerMovement2D)
        int currentTeamA = 0;
        int currentTeamB = 0;

        PlayerMovement2D[] humanPlayers = FindObjectsByType<PlayerMovement2D>(FindObjectsSortMode.None);
        foreach (var human in humanPlayers)
        {
            if (human.TryGetComponent<TeamMember>(out TeamMember tm))
            {
                if (tm.teamId == 1) currentTeamA++;
                else if (tm.teamId == 2) currentTeamB++;
            }
        }

        // Rellenar Equipo 1 (Azul) hasta llegar a 3
        for (int i = currentTeamA; i < targetPlayersPerTeam; i++)
        {
            Vector2 spawnPos = teamASpawnPoints[i % teamASpawnPoints.Length];
            SpawnSingleBot(1, teamAColor, spawnPos);
        }

        // Rellenar Equipo 2 (Rojo) hasta llegar a 3
        for (int i = currentTeamB; i < targetPlayersPerTeam; i++)
        {
            Vector2 spawnPos = teamBSpawnPoints[i % teamBSpawnPoints.Length];
            SpawnSingleBot(2, teamBColor, spawnPos);
        }
    }

    [Server]
    private void SpawnSingleBot(int teamId, Color teamColor, Vector2 position)
    {
        GameObject botInstance = Instantiate(botPrefab, position, Quaternion.identity);

        if (botInstance.TryGetComponent<TeamMember>(out TeamMember tm))
        {
            tm.SetTeam(teamId, teamColor);
        }

        // Instanciar en red para que todos los clientes vean al bot moverse y animarse
        NetworkServer.Spawn(botInstance);
    }

    [Server]
    public void ForceStartGame()
    {
        humanCountTeamA = 0;
        humanCountTeamB = 0;
        ServerChangeScene(GameplayScene);
    }
}