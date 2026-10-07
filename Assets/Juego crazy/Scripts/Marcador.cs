using UnityEngine;
using TMPro; // Usando TextMeshPro para UI nítida
using Mirror;

public class Marcador : NetworkBehaviour
{
    public static Marcador Instance;

    [Header("Marcador")]
    // SyncVars: el servidor las modifica y se replican a todos los jugadores
    [SyncVar(hook = nameof(OnScoreAChanged))]
    private int scoreTeamA = 0;

    [SyncVar(hook = nameof(OnScoreBChanged))]
    private int scoreTeamB = 0;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI scoreText; // Asignar el texto en el canvas

    [Header("Audio de Gol")]
    [SerializeField] private AudioClip goalSound;
    private AudioSource audioSource;

    private void Awake()
    {
        Instance = this;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
    }

    // Actualizar visualmente la UI en los clientes
    private void OnScoreAChanged(int oldVal, int newVal)
    {
        UpdateScoreDisplay(newVal, scoreTeamB);
    }

    private void OnScoreBChanged(int oldVal, int newVal)
    {
        UpdateScoreDisplay(scoreTeamA, newVal);
    }

    private void UpdateScoreDisplay(int teamA, int teamB)
    {
        if (scoreText != null)
        {
            scoreText.text = $"{teamA}  -  {teamB}";
        }
    }

    // Solo el servidor otorga puntos
    [Server]
    public void AddGoal(int teamId, GameObject ball)
    {
        if (teamId == 1) scoreTeamA++;
        else scoreTeamB++;

        // Sonido de gol a todos los clientes
        RpcPlayGoalEffect();

        // Reiniciar la posición del balón al centro
        if (ball != null)
        {
            ResetBall(ball);
        }
    }

    [Server]
    private void ResetBall(GameObject ball)
    {
        // Si alguien lo tenía agarrado, soltarlo
        if (ball.TryGetComponent<GrabbableItem>(out GrabbableItem item))
        {
            item.Drop();
        }

        // Posicionar en el centro (0, 0) y quitar velocidad
        ball.transform.position = Vector3.zero;
        if (ball.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    [ClientRpc]
    private void RpcPlayGoalEffect()
    {
        if (goalSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(goalSound);
        }
    }
}