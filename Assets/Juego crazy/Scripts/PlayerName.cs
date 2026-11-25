using UnityEngine;
using Mirror;
using TMPro;

public class ScoreManager : NetworkBehaviour
{
    public static ScoreManager Instance;

    [Header("Configuración de Partida")]
    [SyncVar(hook = nameof(OnTimeChanged))]
    public float timeRemaining = 180f; // 3 minutos

    [SyncVar]
    public bool isMatchRunning = true;

    [Header("Marcadores")]
    [SyncVar(hook = nameof(OnScoreAChanged))]
    public int scoreTeamA = 0; // Equipo Azul (1)

    [SyncVar(hook = nameof(OnScoreBChanged))]
    public int scoreTeamB = 0; // Equipo Rojo (2)

    [Header("Referencias UI - HUD")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI scoreTeamAText;
    [SerializeField] private TextMeshProUGUI scoreTeamBText;

    [Header("Referencias UI - Panel de Victoria")]
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
        if (victoryPanel != null)
            victoryPanel.SetActive(false);

        UpdateTimerUI(timeRemaining);
        UpdateScoreUI();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        UpdateTimerUI(timeRemaining);
        UpdateScoreUI();
    }

    private void Update()
    {
        if (!isServer || !isMatchRunning) return;

        timeRemaining -= Time.deltaTime;

        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            isMatchRunning = false;
            EvaluateWinner();
        }
    }

    // Llamar a este método desde el script de la portería cuando entre el balón
    // teamId: 1 = Equipo Azul, 2 = Equipo Rojo
    [Server]
    public void AddGoal(int teamId)
    {
        if (!isMatchRunning) return;

        if (teamId == 1) scoreTeamA++;
        else if (teamId == 2) scoreTeamB++;
    }

    // --- Hooks de Sincronización ---

    private void OnTimeChanged(float oldTime, float newTime)
    {
        UpdateTimerUI(newTime);
    }

    private void OnScoreAChanged(int oldScore, int newScore)
    {
        UpdateScoreUI();
    }

    private void OnScoreBChanged(int oldScore, int newScore)
    {
        UpdateScoreUI();
    }

    // --- Actualización de UI ---

    private void UpdateTimerUI(float timeToDisplay)
    {
        if (timerText == null) return;

        float t = Mathf.Max(0f, timeToDisplay);
        int minutes = Mathf.FloorToInt(t / 60f);
        int seconds = Mathf.FloorToInt(t % 60f);

        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    private void UpdateScoreUI()
    {
        if (scoreTeamAText != null) scoreTeamAText.text = scoreTeamA.ToString();
        if (scoreTeamBText != null) scoreTeamBText.text = scoreTeamB.ToString();
    }

    // --- Fin de Partida y Pantalla de Victoria ---

    [Server]
    private void EvaluateWinner()
    {
        int winnerTeamId = 0; // Empate

        if (scoreTeamA > scoreTeamB) winnerTeamId = 1; // Azul
        else if (scoreTeamB > scoreTeamA) winnerTeamId = 2; // Rojo

        RpcShowVictoryScreen(winnerTeamId, scoreTeamA, scoreTeamB);
    }

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

    // Asignar al botón "Volver al Inicio"
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
        else if (NetworkServer.active)
        {
            NetworkManager.singleton.StopServer();
        }
    }
}