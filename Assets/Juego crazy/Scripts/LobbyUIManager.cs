using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;

public class LobbyUIManager : MonoBehaviour
{
    public static LobbyUIManager Instance;

    [Header("Botones de Selección")]
    [SerializeField] private Button btnTeamA;
    [SerializeField] private Button btnTeamB;
    [SerializeField] private Button btnReady;
    [SerializeField] private TextMeshProUGUI btnReadyText;
    [SerializeField] private Button btnStartMatch; // Solo visible para el Host

    [Header("Lista de Jugadores en Sala")]
    [SerializeField] private TextMeshProUGUI teamAListText;
    [SerializeField] private TextMeshProUGUI teamBListText;

    private CustomRoomPlayer localRoomPlayer;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (btnTeamA != null) btnTeamA.onClick.AddListener(() => SelectTeam(1));
        if (btnTeamB != null) btnTeamB.onClick.AddListener(() => SelectTeam(2));
        if (btnReady != null) btnReady.onClick.AddListener(ToggleReady);
        if (btnStartMatch != null)
        {
            btnStartMatch.onClick.AddListener(OnStartMatchClicked);
            // Solo el servidor/Host puede forzar el inicio de partida
            btnStartMatch.gameObject.SetActive(NetworkServer.active);
        }

        UpdatePlayerList();
    }

    public void RegisterLocalRoomPlayer(CustomRoomPlayer player)
    {
        localRoomPlayer = player;
        if (btnStartMatch != null)
        {
            btnStartMatch.gameObject.SetActive(NetworkServer.active);
        }
        UpdatePlayerList();
    }

    private void SelectTeam(int teamId)
    {
        if (localRoomPlayer != null)
        {
            localRoomPlayer.CmdSelectTeam(teamId);
        }
    }

    private void ToggleReady()
    {
        if (localRoomPlayer != null)
        {
            localRoomPlayer.CmdChangeReadyState(!localRoomPlayer.readyToBegin);
        }
    }

    private void OnStartMatchClicked()
    {
        if (NetworkServer.active && NetworkManager.singleton is TeamRoomManager roomManager)
        {
            roomManager.ForceStartGame();
        }
    }

    public void UpdatePlayerList()
    {
        string listA = "<b>EQUIPO AZUL</b>\n";
        string listB = "<b>EQUIPO ROJO</b>\n";

        CustomRoomPlayer[] players = FindObjectsByType<CustomRoomPlayer>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            string status = p.readyToBegin ? "<color=#55FF55>[LISTO]</color>" : "<color=#AAAAAA>[Esperando]</color>";
            string tag = p.isLocalPlayer ? " (Tú)" : "";
            string line = $"Jugador {p.index + 1}{tag} {status}\n";

            if (p.selectedTeam == 1) listA += line;
            else listB += line;
        }

        if (teamAListText != null) teamAListText.text = listA;
        if (teamBListText != null) teamBListText.text = listB;

        if (localRoomPlayer != null && btnReadyText != null)
        {
            btnReadyText.text = localRoomPlayer.readyToBegin ? "Cancelar Listo" : "¡Estoy Listo!";
        }
    }
}