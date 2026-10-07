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
    private Camera mainCamera;
    private Vector2 moveInput;

    [Header("Interacción y Pase")]
    [SerializeField] private float grabRadius = 1.5f;
    [SerializeField] private LayerMask itemLayer;
    [SerializeField] private float throwForce = 14f; // Fuerza del pase con clic

    private GrabbableItem heldItem;

    [Header("Sonidos")]
    [SerializeField] private AudioClip grabSound;
    [SerializeField] private AudioClip throwSound;
    [SerializeField] private AudioClip bumpSound;
    private AudioSource audioSource;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        mainCamera = Camera.main;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    private void Update()
    {
        // Solo la máquina dueña de este personaje procesa input y mira hacia su propio mouse
        if (!isLocalPlayer) return;

        // --- 1. MOVIMIENTO DE EJES ---
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");
        moveInput = new Vector2(moveX, moveY).normalized;

        // --- 2. ROTAR HACIA EL CURSOR DEL MOUSE ---
        RotateTowardsMouse();

        // --- 3. AGARRAR CON TECLA 'E' ---
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (heldItem == null)
            {
                TryGrabNearbyItem();
            }
        }

        // --- 4. LANZAR CON CLIC IZQUIERDO ---
        if (Input.GetMouseButtonDown(0)) // 0 = Clic izquierdo
        {
            if (heldItem != null)
            {
                ThrowItemTowardsMouse();
            }
        }
    }

    private void RotateTowardsMouse()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        // Obtenemos la posición del mouse proyectada en el plano Z = 0 del juego
        Vector3 mouseScreen = Input.mousePosition;
        mouseScreen.z = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(mouseScreen);

        Vector2 lookDir = (Vector2)mouseWorldPos - (Vector2)transform.position;

        if (lookDir.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(lookDir.y, lookDir.x) * Mathf.Rad2Deg;

            // Asignamos la rotación directo al transform y al Rigidbody
            transform.rotation = Quaternion.Euler(0, 0, angle);
            if (rb != null) rb.rotation = angle;
        }
    }

    private void ThrowItemTowardsMouse()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        Vector3 mouseScreen = Input.mousePosition;
        mouseScreen.z = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(mouseScreen);

        Vector2 aimDirection = ((Vector2)mouseWorldPos - (Vector2)transform.position).normalized;

        CmdThrowItem(heldItem.gameObject, aimDirection, throwForce);
        heldItem = null;
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
                RpcPlayGrabSound();
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
                RpcPlayThrowSound();
            }
        }
    }

    [ClientRpc]
    private void RpcPlayGrabSound()
    {
        if (grabSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(grabSound);
        }
    }

    [ClientRpc]
    private void RpcPlayThrowSound()
    {
        if (throwSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(throwSound);
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
        }
    }

    [ClientRpc]
    private void RpcApplyKnockback(Vector2 force)
    {
        if (rb != null)
        {
            rb.AddForce(force, ForceMode2D.Impulse);
        }

        if (bumpSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(bumpSound);
        }
    }
}