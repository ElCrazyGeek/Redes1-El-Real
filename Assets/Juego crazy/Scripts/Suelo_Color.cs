using UnityEngine;
using Mirror;

public class FloorTile : MonoBehaviour
{
    [SerializeField] private Color paintedColor = Color.cyan;
    private SpriteRenderer sr;
    private bool isPainted = false;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!NetworkServer.active) return;
        if (isPainted) return;

        // Si lo que colisiona es el balón
        if (collision.TryGetComponent<GrabbableItem>(out GrabbableItem ball))
        {
            isPainted = true;

            // Llamamos al manager de la malla si existe
            if (FloorGridManager.Instance != null)
            {
                FloorGridManager.Instance.RpcPaintTile(transform.position, paintedColor);
            }
        }
    }

    public void Paint(Color newColor)
    {
        if (sr != null)
        {
            sr.color = newColor;
        }
    }
}