using UnityEngine;
using Mirror;

public class FloorTile : MonoBehaviour
{
    private SpriteRenderer sr;

    // 0 = sin pintar, 1 = Equipo A, 2 = Equipo B
    public int paintedByTeam = 0;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!NetworkServer.active) return;

        // Validamos si lo que pasó encima es el balón
        if (collision.TryGetComponent<GrabbableItem>(out GrabbableItem ball))
        {
            int teamId = ball.lastTeamTouch;

            // Si el equipo es válido y la casilla aún no pertenece a ese equipo
            if (teamId > 0 && paintedByTeam != teamId)
            {
                paintedByTeam = teamId;

                if (FloorGridManager.Instance != null)
                {
                    FloorGridManager.Instance.PaintTileForTeam(transform.position, teamId);
                }
            }
        }
    }

    public void Paint(Color newColor, int teamId)
    {
        paintedByTeam = teamId;
        if (sr != null)
        {
            sr.color = newColor;
        }
    }
}