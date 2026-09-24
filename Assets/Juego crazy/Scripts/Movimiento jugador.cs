using UnityEngine;
using Mirror;

public class PlayerMovement2D : NetworkBehaviour
{
    [Header("Configuración")]
    [SerializeField] private float moveSpeed = 5f;

    public  Rigidbody2D rb;
    private Vector2 moveInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        //Ojo,si este objeto NO le pertenece a la máquina que corre este frame, ignora el input.
        if (!isLocalPlayer) return;

        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        
        moveInput = new Vector2(moveX, moveY).normalized;
    }

    private void FixedUpdate()
    {
        // Solo el jugador local calcula su propia física inmediata
        if (!isLocalPlayer) return;

        rb.linearVelocity = moveInput * moveSpeed;
        // Si estás en versiones anteriores de Unity donde 'linearVelocity' no existe, 
        // usa: rb.velocity = moveInput * moveSpeed;
    }
}