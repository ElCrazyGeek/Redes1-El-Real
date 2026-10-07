using UnityEngine;
using Mirror;

public class GrabbableItem : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnHolderChanged))]
    public NetworkIdentity currentHolder;

    private Rigidbody2D rb;
    private Collider2D col;

    [SerializeField] private AudioClip bounceSound;
    private AudioSource audioSource;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        audioSource = GetComponent<AudioSource>();
    }

    private void OnHolderChanged(NetworkIdentity oldHolder, NetworkIdentity newHolder)
    {
        if (newHolder != null)
        {
            transform.SetParent(newHolder.transform);
            transform.localPosition = new Vector3(0, 0.7f, 0);
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.linearVelocity = Vector2.zero;
            }
            if (col != null) col.enabled = false;
        }
        else
        {
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

    // Este es el método que te hace falta agregar:
    [Server]
    public void Throw(Vector2 throwDirection, float force)
    {
        currentHolder = null; // Libera el objeto para que caiga al suelo

        if (rb != null)
        {
            transform.position += (Vector3)(throwDirection * 0.4f);
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(throwDirection * force, ForceMode2D.Impulse);
        }
    }

    [ServerCallback]
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Si no lo tiene nadie agarrado y choca contra algo con velocidad
        if (currentHolder == null && collision.relativeVelocity.magnitude > 1.5f)
        {
            RpcPlayBounceSound();
        }
    }

    [ClientRpc]
    private void RpcPlayBounceSound()
    {
        if (audioSource != null && bounceSound != null)
        {
            audioSource.PlayOneShot(bounceSound);
        }
    }
}