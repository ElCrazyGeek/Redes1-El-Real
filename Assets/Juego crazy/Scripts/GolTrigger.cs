using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(BoxCollider2D))]
public class GoalTrigger : NetworkBehaviour
{
    [Tooltip("1 para la portería donde anota Equipo A, 2 para donde anota Equipo B")]
    [SerializeField] private int teamThatScores = 1;

    [Header("Feedback Visual")]
    [SerializeField] private Color lockedColor = new Color(0.35f, 0.35f, 0.35f, 0.45f);

    [SyncVar(hook = nameof(OnGoalStateChanged))]
    private bool isGoalEnabled = false;

    private BoxCollider2D goalCollider;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        goalCollider = GetComponent<BoxCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ApplyGoalState(isGoalEnabled);
    }

    [ServerCallback]
    private void Update()
    {
        if (FloorGridManager.Instance == null) return;

        // Solo se habilita si el equipo que ataca este arco cubrió TODA la cancha con su color
        bool teamDominatesCourt = FloorGridManager.Instance.IsCourtFullyCoveredByTeam(teamThatScores);
        if (isGoalEnabled != teamDominatesCourt)
        {
            isGoalEnabled = teamDominatesCourt;
        }
    }

    private void OnGoalStateChanged(bool oldState, bool newState)
    {
        ApplyGoalState(newState);
    }

    private void ApplyGoalState(bool enabledState)
    {
        if (goalCollider == null) goalCollider = GetComponent<BoxCollider2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        // Trigger abierto si el equipo dominó la cancha; muro sólido si no
        if (goalCollider != null)
        {
            goalCollider.isTrigger = enabledState;
        }

        if (spriteRenderer != null)
        {
            if (enabledState && FloorGridManager.Instance != null)
            {
                // Se pinta del mismo color del equipo que desbloqueó la portería
                Color teamCol = FloorGridManager.Instance.GetTeamColor(teamThatScores);
                teamCol.a = 0.9f; // Bien visible al abrirse
                spriteRenderer.color = teamCol;
            }
            else
            {
                spriteRenderer.color = lockedColor;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!NetworkServer.active) return;
        if (!isGoalEnabled) return;

        if (collision.TryGetComponent<GrabbableItem>(out GrabbableItem item))
        {
            if (Marcador.Instance != null)
            {
                Marcador.Instance.AddGoal(teamThatScores, item.gameObject);
            }
        }
    }
}