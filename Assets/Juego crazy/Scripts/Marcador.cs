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

    [Header("Efectos de Partículas")]
    [SerializeField] private ParticleSystem goalParticlesA;
    [SerializeField] private ParticleSystem goalParticlesB;

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
        if (isHandlingGoal) return;
        isHandlingGoal = true;

        if (teamId == 1)
        {
            scoreTeamA++;
            if (MatchTimer.Instance != null) MatchTimer.Instance.AddGoalTeamA();
        }
        else
        {
            scoreTeamB++;
            if (MatchTimer.Instance != null) MatchTimer.Instance.AddGoalTeamB();
        }

        RpcPlayGoalEffect(teamId);
        StartCoroutine(GoalDelayRoutine(ball, goalResetDelay));
    }

    [Server]
    private IEnumerator GoalDelayRoutine(GameObject ball, float delay)
    {
        try
        {
            // 1. Soltar el balón y congelar su física
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
                    rb.simulated = false;
                }

                RpcSetBallVisibility(ball, false);
            }

            // 2. Limpiar toda la pintura del piso (cierra las porterías)
            if (FloorGridManager.Instance != null)
            {
                FloorGridManager.Instance.ResetAllTiles();
            }

            // 3. Esperar el tiempo de festejo
            yield return new WaitForSeconds(delay);

            // 4. Regresar el balón al centro (0,0) y reactivar físicas
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
        }
        finally
        {
            isHandlingGoal = false;
        }
    }

    [ClientRpc]
    private void RpcPlayGoalEffect(int teamId)
    {
        // 1. Sonido de gol en todas las pantallas
        if (goalSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(goalSound);
        }

        // 2. Detonar las partículas en la portería correspondiente
        if (teamId == 1 && goalParticlesA != null)
        {
            goalParticlesA.Play();
        }
        else if (teamId == 2 && goalParticlesB != null)
        {
            goalParticlesB.Play();
        }
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