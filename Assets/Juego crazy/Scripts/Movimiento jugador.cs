using UnityEngine;
using Mirror;

public class PlayerMovement2D : NetworkBehaviour
{

    [Header("Configuración")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float bounceForce = 7f;

    [SyncVar(hook = nameof(OnColorChanged))]
    private Color playerColor = Color.white;

    public  Rigidbody2D rb;

    private SpriteRenderer spriteRenderer;
    private Vector2 moveInput;

    [Header("Interacción")]
    [SerializeField] private float grabRadius = 1.5f;
    [SerializeField] private LayerMask itemLayer;
    private GrabbableItem heldItem;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        //Ojo,si este objeto NO le pertenece a la máquina que corre este frame, ignora el input.
        if (!isLocalPlayer) return;

        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        
        moveInput = new Vector2(moveX, moveY).normalized;

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (heldItem == null)
            {
                TryGrabNearbyItem();
            }
            else
            {
                CmdDropItem(heldItem.gameObject);
                heldItem = null;
            }
        }
    }

    private void TryGrabNearbyItem()
    {
        // Buscar objetos cercanos en el radio
        Collider2D hit = Physics2D.OverlapCircle(transform.position, grabRadius, itemLayer);
        if (hit != null && hit.TryGetComponent<GrabbableItem>(out GrabbableItem item))
        {
            // Solo si nadie más lo tiene agarrado
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
        if (itemObject.TryGetComponent<GrabbableItem>(out GrabbableItem item))
        {
            if (item.currentHolder == null)
            {
                item.Grab(netIdentity);
            }
        }
    }

    [Command]
    private void CmdDropItem(GameObject itemObject)
    {
        if (itemObject.TryGetComponent<GrabbableItem>(out GrabbableItem item))
        {
            if (item.currentHolder == netIdentity)
            {
                item.Drop();
            }
        }
    }

    private void FixedUpdate()
    {
        if (!isLocalPlayer) return;

        // Si el jugador está presionando teclas, asigna la velocidad de movimiento directo
        if (moveInput != Vector2.zero)
        {
            rb.linearVelocity = moveInput * moveSpeed;
        }
        else
        {
            // Si no presiona nada y la velocidad residual es baja (no está volando por un choque), frena a cero
            if (rb.linearVelocity.magnitude < moveSpeed)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }
    }

    // Hook: se ejecuta localmente en cada máquina cuando la red recibe un color nuevo
    private void OnColorChanged(Color oldColor, Color newColor)
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.color = newColor;
    }

    // Detección física: solo el servidor toma la decisión de la colisión
    [ServerCallback]
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Verificamos si el objeto con el que chocamos es otro jugador
        if (collision.gameObject.TryGetComponent<PlayerMovement2D>(out PlayerMovement2D otherPlayer))
        {
            // 1. Cambiar color a un tono aleatorio en el servidor (se sincronizará a todos)
            Color nuevoColor = new Color(Random.value, Random.value, Random.value, 1f);
            playerColor = nuevoColor;

            // 2. Calcular vector de empuje (en dirección opuesta al choque)
            Vector2 pushDirection = (transform.position - collision.transform.position).normalized;

            // 3. Aplicar empuje a los clientes a través de un ClientRpc
            RpcApplyKnockback(pushDirection * bounceForce);
        }
    }

    // ClientRpc: el servidor le ordena a todas las instancias ejecutar este impulso
    [ClientRpc]
    private void RpcApplyKnockback(Vector2 force)
    {
        if (rb != null)
        {
            rb.AddForce(force, ForceMode2D.Impulse);
        }
    }
}