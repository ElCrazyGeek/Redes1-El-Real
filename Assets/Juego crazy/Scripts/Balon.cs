using UnityEngine;
using Mirror;

public class GrabbableItem : NetworkBehaviour
{
    // Guarda qué jugador lo tiene cargado actualmente (null si está en el suelo)
    [SyncVar(hook = nameof(OnHolderChanged))]
    public NetworkIdentity currentHolder;

    private Rigidbody2D rb;
    private Collider2D col;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
    }

    // Hook: se ejecuta en todos los clientes cuando alguien agarra o suelta el objeto
    private void OnHolderChanged(NetworkIdentity oldHolder, NetworkIdentity newHolder)
    {
        if (newHolder != null)
        {
            // Se emparenta visualmente al jugador
            transform.SetParent(newHolder.transform);
            transform.localPosition = new Vector3(0, 0.7f, 0); // posición arriba de la cabeza
            if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
            if (col != null) col.enabled = false; // evitar chocar con quien lo carga
        }
        else
        {
            // Se suelta en el suelo
            transform.SetParent(null);
            if (rb != null) rb.bodyType = RigidbodyType2D.Dynamic;
            if (col != null) col.enabled = true;
        }
    }

    [Server]
    public void Grab(NetworkIdentity player)
    {
        currentHolder = player;
    }

    [Server]
    public void Drop()
    {
        currentHolder = null;
    }
}