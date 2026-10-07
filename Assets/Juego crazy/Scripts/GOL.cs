using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GOL : MonoBehaviour
{
    [Tooltip("1 para Portería A, 2 para Portería B")]
    [SerializeField] private int teamThatScores = 1;

    // Solo el servidor procesa el gol para evitar trampas o dobles conteos
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!NetworkServer.active) return;

        // Comprobar si lo que entró fue el balón
        if (collision.TryGetComponent<GrabbableItem>(out GrabbableItem item))
        {
            Marcador.Instance.AddGoal(teamThatScores, item.gameObject);
        }
    }
}