using UnityEngine;
using Mirror;

public class TeamMember : NetworkBehaviour
{
    [Header("Configuración de Equipo")]
    [Tooltip("1 = Equipo A (ataca Portería B), 2 = Equipo B (ataca Portería A)")]
    [SyncVar(hook = nameof(OnTeamChanged))]
    public int teamId = 1;

    [SyncVar]
    public Color teamColor = Color.cyan;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ApplyTeamVisuals();
    }

    private void OnTeamChanged(int oldTeam, int newTeam)
    {
        ApplyTeamVisuals();
    }

    private void ApplyTeamVisuals()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            // Tinte sutil para distinguir equipos sin perder el sprite
            spriteRenderer.color = teamId == 1 ? new Color(0.7f, 0.9f, 1f) : new Color(1f, 0.75f, 0.75f);
        }
    }

    [Server]
    public void SetTeam(int newTeamId, Color newColor)
    {
        teamId = newTeamId;
        teamColor = newColor;
    }
}