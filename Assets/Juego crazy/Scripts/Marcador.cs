using System.Collections;
using UnityEngine;
using TMPro;
using Mirror;

public class Marcador : NetworkBehaviour
{
    public static Marcador Instance;

    [Header("Marcador")]
    [SyncVar(hook = nameof(OnScoreAChanged))]
    private int scoreTeamA = 0;

    [SyncVar(hook = nameof(OnScoreBChanged))]
    private int scoreTeamB = 0;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI scoreText;

    [Header("Audio y Tiempos de Gol")]
    [SerializeField] private AudioClip goalSound;
    [Tooltip("Tiempo de espera en segundos tras anotar")]
    [SerializeField] private float goalResetDelay = 3f; // Aquí eliges el tiempo
    private AudioSource audioSource;

    private bool isHandlingGoal = false; // Evita que se cobren goles dobles durante el festejo

    private void Awake()
    {
        Instance = this;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
    }

    private void Start()
    {
        UpdateScoreDisplay(scoreTeamA, scoreTeamB);
    }

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

    [Server]
    public void AddGoal(int teamId, GameObject ball)
    {
        // Si ya se está procesando un gol, ignoramos para no duplicar puntos
        if (isHandlingGoal) return;

        isHandlingGoal = true;

        if (teamId == 1) scoreTeamA++;
        else scoreTeamB++;

        // Sonido de gol a todos los clientes
        RpcPlayGoalEffect();

        // Si prefieres que el delay coincida exactamente con la duración del audio:
        // float delay = goalSound != null ? goalSound.length : goalResetDelay;

        // Arrancamos la pausa en el servidor
        StartCoroutine(GoalDelayRoutine(ball, goalResetDelay));
    }

    [Server]
    private IEnumerator GoalDelayRoutine(GameObject ball, float delay)
    {
        // 1. Pausar física y soltar el balón
        if (ball != null)
        {
            if (ball.TryGetComponent<GrabbableItem>(out GrabbableItem item))
            {
                item.Drop();
            }

            if (ball.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.simulated = false; // Desactiva físicas durante la pausa
            }

            // Opcional: Ocultar el balón o dejarlo quieto en la portería
            RpcSetBallVisibility(ball, false);
        }

        // 2. Esperar el tiempo configurado
        yield return new WaitForSeconds(delay);

        // 3. Reubicar al centro (0, 0) y reactivar físicas
        if (ball != null)
        {
            ball.transform.position = Vector3.zero;

            if (ball.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
            {
                rb.simulated = true;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            RpcSetBallVisibility(ball, true);
        }

        isHandlingGoal = false;
    }

    [ClientRpc]
    private void RpcSetBallVisibility(GameObject ball, bool visible)
    {
        if (ball != null)
        {
            if (ball.TryGetComponent<SpriteRenderer>(out SpriteRenderer sr))
            {
                sr.enabled = visible;
            }
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