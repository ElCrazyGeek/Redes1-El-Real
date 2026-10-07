using UnityEngine;
using Mirror;

public class GrabbableItem : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnHolderChanged))]
    public NetworkIdentity currentHolder;

    [Header("Sonido")]
    [SerializeField] private AudioClip bounceSound;
    private AudioSource audioSource;

    private Rigidbody2D rb;
    private Collider2D col;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    private void Update()
    {
        // Mientras esté agarrado, el balón se posiciona frente al jugador
        if (currentHolder != null)
        {
            // Tomamos la rotación actual del jugador y lo colocamos 0.8 unidades hacia su frente
            Vector3 forwardOffset = currentHolder.transform.right * 0.8f;
            // Si tu personaje usa transform.up en vez de right, cambia a: currentHolder.transform.up * 0.8f

            transform.position = currentHolder.transform.position + forwardOffset;
            transform.rotation = Quaternion.identity; // Mantener el balón sin rotaciones raras
        }
    }

    private void OnHolderChanged(NetworkIdentity oldHolder, NetworkIdentity newHolder)
    {
        if (newHolder != null)
        {
            // Desactivamos la física mientras lo carga
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
            if (col != null) col.enabled = false;
        }
        else
        {
            // Reactivamos física al soltarlo
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
            }
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

    [Server]
    public void Throw(Vector2 throwDirection, float force)
    {
        if (currentHolder != null)
        {
            // Colocar el balón un paso por delante para que no choque con el cuerpo del jugador
            transform.position = currentHolder.transform.position + (Vector3)(throwDirection * 0.9f);
        }

        currentHolder = null; // Esto dispara OnHolderChanged en todos los clientes

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;

            // Asignación directa de velocidad de salida:
            rb.linearVelocity = throwDirection * force;
            // (Si tu versión de Unity no reconoce linearVelocity, usa: rb.velocity = throwDirection * force;)
        }
    }

    [ServerCallback]
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (currentHolder == null && collision.relativeVelocity.magnitude > 1f)
        {
            RpcPlayBounceSound();
        }
    }

    [ClientRpc]
    private void RpcPlayBounceSound()
    {
        if (bounceSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(bounceSound);
        }
    }
}