using UnityEngine;
using Mirror;

public class PlayerMovement2D : NetworkBehaviour
{
    [Header("Configuración")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float bounceForce = 7f;

    [SyncVar(hook = nameof(OnColorChanged))]
    private Color playerColor = Color.white;

    public Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private AudioSource audioSource;
    private Vector2 moveInput;

    [Header("Efectos de Sonido")]
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip grabSound;
    [SerializeField] private AudioClip throwSound;

    [Header("Interacción y Pase")]
    [SerializeField] private float grabRadius = 1.5f;
    [SerializeField] private LayerMask itemLayer;
    [SerializeField] private float throwForce = 12f;

    private GrabbableItem heldItem;
    private Vector2 lastFacingDir = Vector2.right;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        moveInput = new Vector2(moveX, moveY).normalized;

        if (moveInput != Vector2.zero)
        {
            lastFacingDir = moveInput;
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (heldItem == null)
            {
                TryGrabNearbyItem();
            }
            else
            {
                Vector2 passDirection = moveInput != Vector2.zero ? moveInput : lastFacingDir;
                CmdThrowItem(heldItem.gameObject, passDirection, throwForce);
                heldItem = null;
            }
        }
    }

    private void TryGrabNearbyItem()
    {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, grabRadius, itemLayer);
        if (hit != null && hit.TryGetComponent<GrabbableItem>(out GrabbableItem item))
        {
            if (item.currentHolder == null)
            {
                heldItem = item;
                CmdGrabItem(item.gameObject);
            }
        }
    }

    [Command]
    private void CmdGrabItem(GameObject itemObject)
    {
        if (itemObject != null && itemObject.TryGetComponent<GrabbableItem>(out GrabbableItem item))
        {
            if (item.currentHolder == null)
            {
                item.Grab(netIdentity);
                RpcPlaySound(1); // 1 = Sonido de agarrar
            }
        }
    }

    [Command]
    private void CmdDropItem(GameObject itemObject)
    {
        if (itemObject != null && itemObject.TryGetComponent<GrabbableItem>(out GrabbableItem item))
        {
            if (item.currentHolder == netIdentity)
            {
                item.Drop();
            }
        }
    }

    [Command]
    private void CmdThrowItem(GameObject itemObject, Vector2 direction, float force)
    {
        if (itemObject != null && itemObject.TryGetComponent<GrabbableItem>(out GrabbableItem item))
        {
            if (item.currentHolder == netIdentity)
            {
                item.Throw(direction, force);
                RpcPlaySound(2); // 2 = Sonido de pase/lanzamiento
            }
        }
    }

    private void FixedUpdate()
    {
        if (!isLocalPlayer) return;

        if (moveInput != Vector2.zero)
        {
            rb.linearVelocity = moveInput * moveSpeed;
        }
        else
        {
            if (rb.linearVelocity.magnitude < moveSpeed)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }
    }

    private void OnColorChanged(Color oldColor, Color newColor)
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.color = newColor;
    }

    [ServerCallback]
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.TryGetComponent<PlayerMovement2D>(out PlayerMovement2D otherPlayer))
        {
            Color nuevoColor = new Color(Random.value, Random.value, Random.value, 1f);
            playerColor = nuevoColor;

            Vector2 pushDirection = (transform.position - collision.transform.position).normalized;
            RpcApplyKnockback(pushDirection * bounceForce);
            RpcPlaySound(0); // 0 = Sonido de golpe/choque
        }
    }

    [ClientRpc]
    private void RpcApplyKnockback(Vector2 force)
    {
        if (rb != null)
        {
            rb.AddForce(force, ForceMode2D.Impulse);
        }
    }

    // Este ClientRpc ejecuta el sonido en todas las ventanas a la vez
    [ClientRpc]
    private void RpcPlaySound(int soundId)
    {
        if (audioSource == null) return;

        switch (soundId)
        {
            case 0: // Choque
                if (hitSound != null) audioSource.PlayOneShot(hitSound);
                break;
            case 1: // Agarrar
                if (grabSound != null) audioSource.PlayOneShot(grabSound);
                break;
            case 2: // Pase
                if (throwSound != null) audioSource.PlayOneShot(throwSound);
                break;
        }
    }
}