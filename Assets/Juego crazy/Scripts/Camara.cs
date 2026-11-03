using UnityEngine;

public class KeepCameraStraight : MonoBehaviour
{
    private void LateUpdate()
    {
        // Fuerza la rotación de la cámara a mantenerse recta sin importar cómo gire el jugador
        transform.rotation = Quaternion.identity;
    }
}