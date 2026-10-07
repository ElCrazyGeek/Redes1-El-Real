using UnityEngine;
using Mirror;

public class Camara : MonoBehaviour
{
    [SerializeField] private float smoothSpeed = 10f;
    [SerializeField] private Vector3 offset = new Vector3(0, 0, -10f);

    private Transform targetTransform;

    private void LateUpdate()
    {
        // Si aún no hemos encontrado al jugador local, lo buscamos
        if (targetTransform == null)
        {
            if (NetworkClient.localPlayer != null)
            {
                targetTransform = NetworkClient.localPlayer.transform;
            }
            return;
        }

        // Seguir suavemente la posición X, Y del jugador local manteniendo Z = -10
        Vector3 targetPos = targetTransform.position + offset;
        transform.position = Vector3.Lerp(transform.position, targetPos, smoothSpeed * Time.deltaTime);
    }
}