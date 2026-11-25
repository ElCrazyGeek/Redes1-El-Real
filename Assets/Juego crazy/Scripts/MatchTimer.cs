using UnityEngine;
using Mirror;
using TMPro;

public class MatchTimer : NetworkBehaviour
{
    public static MatchTimer Instance;

    [Header("1. Temporizador Autoritativo")]
    [SyncVar(hook = nameof(OnTimeChanged))]
    public float timeRemaining = 180f; // 3 minutos (180 segundos)

    [SyncVar]
    public bool isMatchRunning = true;

    [Header("2. Marcador Sincronizado")]
    [SyncVar] public int scoreTeamA = 0; // Equipo Azul (1)
    [SyncVar] public int scoreTeamB = 0; // Equipo Rojo (2)

    [Header("UI - Reloj en Pantalla")]
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("UI - Panel de Victoria")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private TextMeshProUGUI finalScoreText;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (victoryPanel != null && isMatchRunning)
            victoryPanel.SetActive(false);

        UpdateTimerUI(timeRemaining);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        UpdateTimerUI(timeRemaining);
    }

    private void Update()
    {
        // Solo el servidor descuenta el tiempo mientras la partida está activa
        if (!isServer || !isMatchRunning) return;

        timeRemaining -= Time.deltaTime;

        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            isMatchRunning = false;
            EvaluateWinner();
        }
    }

    // Hook de Mirror: actualiza el texto MM:SS en todos los clientes cuando cambia el tiempo
    private void OnTimeChanged(float oldTime, float newTime)
    {
        UpdateTimerUI(newTime);
    }

    private void UpdateTimerUI(float timeToDisplay)
    {
        if (timerText == null) return;

        float t = Mathf.Max(0f, timeToDisplay);
        int minutes = Mathf.FloorToInt(t / 60f);
        int seconds = Mathf.FloorToInt(t % 60f);

        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    // Métodos para sumar goles desde el servidor (desde tu script Marcador o las porterías)
    [Server]
    public void AddGoalTeamA()
    {
        if (isMatchRunning) scoreTeamA++;
    }

    [Server]
    public void AddGoalTeamB()
    {
        if (isMatchRunning) scoreTeamB++;
    }

    // Evaluación del ganador en el servidor
    [Server]
    private void EvaluateWinner()
    {
        int winnerTeamId = 0; // 0 = Empate

        if (scoreTeamA > scoreTeamB)
            winnerTeamId = 1; // Gana Equipo Azul
        else if (scoreTeamB > scoreTeamA)
            winnerTeamId = 2; // Gana Equipo Rojo

        RpcShowVictoryScreen(winnerTeamId, scoreTeamA, scoreTeamB);
    }

    // Pantalla de victoria sincronizada en todas las PC
    [ClientRpc]
    private void RpcShowVictoryScreen(int winnerTeamId, int finalScoreA, int finalScoreB)
    {
        if (victoryPanel != null)
            victoryPanel.SetActive(true);

        if (winnerText != null)
        {
            switch (winnerTeamId)
            {
                case 1:
                    winnerText.text = "¡GANÓ EL EQUIPO AZUL!";
                    winnerText.color = Color.blue;
                    break;
                case 2:
                    winnerText.text = "¡GANÓ EL EQUIPO ROJO!";
                    winnerText.color = Color.red;
                    break;
                default:
                    winnerText.text = "¡EMPATE!";
                    winnerText.color = Color.yellow;
                    break;
            }
        }

        if (finalScoreText != null)
        {
            finalScoreText.text = $"Azul {finalScoreA} - {finalScoreB} Rojo";
        }
    }

    // Botón "Volver al Inicio" con limpieza de red en Mirror
    public void ReturnToMainMenu()
    {
        if (NetworkServer.active && NetworkClient.isConnected)
        {
            NetworkManager.singleton.StopHost();
        }
        else if (NetworkClient.isConnected)
        {
            NetworkManager.singleton.StopClient();
        }
    }
}