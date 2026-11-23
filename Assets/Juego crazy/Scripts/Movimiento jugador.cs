using System.Collections;
using UnityEngine;
using Mirror;

public class PlayerMovement2D : NetworkBehaviour
{
    [Header("Configuración de Movimiento")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float bounceForce = 7f;

    public Rigidbody2D rb;
    private Camera mainCamera;
    private Vector2 moveInput;
    private TeamMember teamMember;

    [Header("Pase (Clic Derecho) y Balonazo Cargado (Clic Izquierdo)")]
    [SerializeField] private float grabRadius = 1.5f;
    [SerializeField] private LayerMask itemLayer;
    [SerializeField] private float passForce = 13f;
    [SerializeField] private float minShotForce = 14f;
    [SerializeField] private float maxShotForce = 26f;
    [SerializeField] private float maxChargeTime = 1.2f;

    private bool isChargingShot = false;
    private float chargeTimer = 0f;

    [Header("Quite Sano (Clic Izq) y Golpe Agresivo (Clic Der)")]
    [SerializeField] private float actionRange = 1.6f;
    [SerializeField] private float actionCooldown = 0.6f;
    [SerializeField] private float stunDuration = 1.6f;
    [SerializeField] private float attackerPenaltyDuration = 1.0f;
    private float nextActionTime = 0f;

    [SyncVar]
    public bool isStunned = false;

    [Header("Sonidos y Animación")]
    [SerializeField] private AudioClip grabSound;
    [SerializeField] private AudioClip throwSound;
    [SerializeField] private AudioClip bumpSound;
    private AudioSource audioSource;
    private Animator anim;
    private NetworkAnimator networkAnimator;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        mainCamera = Camera.main;
        anim = GetComponent<Animator>();
        networkAnimator = GetComponent<NetworkAnimator>();
        teamMember = GetComponent<TeamMember>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        Camera playerCam = GetComponentInChildren<Camera>(true);
        if (playerCam != null)
        {
            playerCam.gameObject.SetActive(true);
        }
    }

    public GrabbableItem GetHeldBall()
    {
        GrabbableItem ball = FindFirstObjectByType<GrabbableItem>();
        if (ball != null && ball.currentHolder == netIdentity)
        {
            return ball;
        }
        return null;
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        // Si está stuneado o penalizado, se queda parado
        if (isStunned)
        {
            isChargingShot = false;
            chargeTimer = 0f;
            moveInput = Vector2.zero;
            if (anim != null) anim.SetFloat("Movimiento", 0f);
            return;
        }

        // --- 1. MOVIMIENTO ---
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");
        moveInput = new Vector2(moveX, moveY).normalized;

        if (anim != null)
        {
            anim.SetFloat("Movimiento", moveInput.sqrMagnitude);
        }

        // --- 2. ROTACIÓN AL MOUSE ---
        RotateTowardsMouse();

        GrabbableItem heldItem = GetHeldBall();

        // --- 3. AGARRAR BALÓN SUELTO CON 'E' ---
        if (Input.GetKeyDown(KeyCode.E) && heldItem == null)
        {
            TryGrabNearbyItem();
        }

        // ==========================================================
        // ACCIONES CON EL BALÓN EN POSESIÓN
        // ==========================================================
        if (heldItem != null)
        {
            // CLIC DERECHO: Pase limpio (no hace daño/stun a compañeros ni rivales)
            if (Input.GetMouseButtonDown(1) && Time.time >= nextActionTime)
            {
                nextActionTime = Time.time + actionCooldown;
                isChargingShot = false;
                chargeTimer = 0f;

                Vector2 aimDir = GetMouseDirection();
                CmdThrowItem(heldItem.gameObject, aimDir, passForce, false);
                TriggerAnim("Patear");
            }

            // CLIC IZQUIERDO (MANTENER): Cargar intensidad del balonazo
            if (Input.GetMouseButtonDown(0) && Time.time >= nextActionTime)
            {
                isChargingShot = true;
                chargeTimer = 0f;
            }

            if (isChargingShot && Input.GetMouseButton(0))
            {
                chargeTimer = Mathf.Clamp(chargeTimer + Time.deltaTime, 0f, maxChargeTime);
            }

            // CLIC IZQUIERDO (SOLTAR): Disparar balonazo con la intensidad medida
            if (isChargingShot && Input.GetMouseButtonUp(0))
            {
                isChargingShot = false;
                nextActionTime = Time.time + actionCooldown;

                float chargePercent = chargeTimer / maxChargeTime;
                float finalShotForce = Mathf.Lerp(minShotForce, maxShotForce, chargePercent);

                Vector2 aimDir = GetMouseDirection();
                CmdThrowItem(heldItem.gameObject, aimDir, finalShotForce, true);
                TriggerAnim("Patear");
                chargeTimer = 0f;
            }
        }
        // ==========================================================
        // ACCIONES SIN EL BALÓN EN POSESIÓN
        // ==========================================================
        else
        {
            isChargingShot = false;
            chargeTimer = 0f;

            // CLIC IZQUIERDO: Quite normal / "sano" al contrincante
            if (Input.GetMouseButtonDown(0) && Time.time >= nextActionTime)
            {
                nextActionTime = Time.time + actionCooldown;
                CmdTryStealBall();
                TriggerAnim("Patear");
            }

            // CLIC DERECHO: Golpe agresivo (ambos se quedan parados: víctima stuneada, atacante penalizado)
            if (Input.GetMouseButtonDown(1) && Time.time >= nextActionTime)
            {
                nextActionTime = Time.time + actionCooldown;
                CmdTryAggressiveTackle();
                TriggerAnim("Patear");
            }
        }
    }

    private void TriggerAnim(string triggerName)
    {
        if (networkAnimator != null) networkAnimator.SetTrigger(triggerName);
        else if (anim != null) anim.SetTrigger(triggerName);
    }

    private Vector2 GetMouseDirection()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return transform.right;

        Vector3 mouseScreen = Input.mousePosition;
        mouseScreen.z = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(mouseScreen);

        Vector2 dir = ((Vector2)mouseWorldPos - (Vector2)transform.position).normalized;
        return dir.sqrMagnitude > 0.001f ? dir : (Vector2)transform.right;
    }

    private void RotateTowardsMouse()
    {
        Vector2 lookDir = GetMouseDirection();
        float angle = Mathf.Atan2(lookDir.y, lookDir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
        if (rb != null) rb.rotation = angle;
    }

    private void TryGrabNearbyItem()
    {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, grabRadius, itemLayer);
        if (hit != null && hit.TryGetComponent<GrabbableItem>(out GrabbableItem item))
        {
            if (item.currentHolder == null)
            {
                CmdGrabItem(item.gameObject);
            }
        }
    }

    [Command]
    private void CmdGrabItem(GameObject itemObject)
    {
        if (isStunned) return;
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
    private void CmdThrowItem(GameObject itemObject, Vector2 direction, float force, bool isPowerShot)
    {
        if (isStunned) return;
        if (itemObject != null && itemObject.TryGetComponent<GrabbableItem>(out GrabbableItem item))
        {
            if (item.currentHolder == netIdentity)
            {
                item.Throw(direction, force, isPowerShot);
                RpcPlayThrowSound();
            }
        }
    }

    // --- QUITE NORMAL AL CONTRINCANTE (Clic Izquierdo sin balón) ---
    // --- CLIC IZQUIERDO SIN BALÓN: Quite limpio si hay rival cerca, o pedir pase al bot ---
    [Command]
    private void CmdTryStealBall()
    {
        if (isStunned) return;

        GrabbableItem ball = FindAnyObjectByType<GrabbableItem>();
        if (ball == null || ball.currentHolder == null || ball.currentHolder == netIdentity) return;

        int myTeam = teamMember != null ? teamMember.teamId : 1;
        int holderTeam = myTeam;
        if (ball.currentHolder.TryGetComponent<TeamMember>(out TeamMember holderTM))
        {
            holderTeam = holderTM.teamId;
        }

        // 1. Si el balón lo tiene un RIVAL y está cerca -> Quite limpio
        if (myTeam != holderTeam)
        {
            float dist = Vector2.Distance(transform.position, ball.currentHolder.transform.position);
            if (dist <= actionRange && ball.CanBeStolen())
            {
                ball.Grab(netIdentity);
                RpcPlayGrabSound();
            }
        }
        // 2. Si el balón lo tiene un BOT ALIADO (y no tienes rival cerca) -> Pedirle el pase
        else if (ball.currentHolder.TryGetComponent<BotAIController>(out BotAIController allyBot))
        {
            allyBot.ReceivePassRequest(transform);
        }
    }
    // --- GOLPE AGRESIVO (Clic Derecho sin balón: ambos quedan parados) ---
    [Command]
    private void CmdTryAggressiveTackle()
    {
        if (isStunned) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, actionRange);
        PlayerMovement2D targetPlayer = null;
        float closestDist = float.MaxValue;

        foreach (var col in hits)
        {
            if (col.gameObject == gameObject) continue;
            if (col.TryGetComponent<PlayerMovement2D>(out PlayerMovement2D other))
            {
                float d = Vector2.Distance(transform.position, other.transform.position);
                if (d < closestDist)
                {
                    closestDist = d;
                    targetPlayer = other;
                }
            }
        }

        if (targetPlayer != null)
        {
            // 1. La víctima suelta el balón y queda stuneada en el sitio
            targetPlayer.ServerApplyStunAndDrop(Vector2.zero, stunDuration);

            // 2. El atacante también queda parado como penalización (y suelta balón si tuviera)
            ServerApplyStunAndDrop(Vector2.zero, attackerPenaltyDuration);
        }
    }

    [Server]
    public void ServerApplyStunAndDrop(Vector2 knockbackForce, float duration)
    {
        GrabbableItem ball = FindFirstObjectByType<GrabbableItem>();
        if (ball != null && ball.currentHolder == netIdentity)
        {
            ball.Drop();
        }

        StartCoroutine(StunRoutine(duration));
        RpcApplyStunEffects(knockbackForce);
    }

    [Server]
    private IEnumerator StunRoutine(float duration)
    {
        isStunned = true;
        yield return new WaitForSeconds(duration);
        isStunned = false;
    }

    [ClientRpc]
    private void RpcPlayGrabSound()
    {
        if (grabSound != null && audioSource != null) audioSource.PlayOneShot(grabSound);
    }

    [ClientRpc]
    private void RpcPlayThrowSound()
    {
        if (throwSound != null && audioSource != null) audioSource.PlayOneShot(throwSound);
    }

    private void FixedUpdate()
    {
        if (!isLocalPlayer) return;

        // Si está stuneado o penalizado, queda completamente parado
        if (isStunned)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // Si está cargando el balonazo, camina un poco más lento (70% de velocidad)
        float currentSpeed = isChargingShot ? moveSpeed * 0.7f : moveSpeed;

        if (moveInput != Vector2.zero)
        {
            rb.linearVelocity = moveInput * currentSpeed;
        }
        else
        {
            if (rb.linearVelocity.magnitude < moveSpeed)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }
    }

    [ServerCallback]
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.TryGetComponent<PlayerMovement2D>(out PlayerMovement2D otherPlayer))
        {
            Vector2 pushDirection = (transform.position - collision.transform.position).normalized;
            RpcApplyKnockbackOnly(pushDirection * (bounceForce * 0.6f));
        }
    }

    [ClientRpc]
    private void RpcApplyStunEffects(Vector2 force)
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            if (force != Vector2.zero) rb.AddForce(force, ForceMode2D.Impulse);
        }

        if (anim != null) anim.SetTrigger("Golpe");
        if (bumpSound != null && audioSource != null) audioSource.PlayOneShot(bumpSound);
    }

    [ClientRpc]
    private void RpcApplyKnockbackOnly(Vector2 force)
    {
        if (rb != null) rb.AddForce(force, ForceMode2D.Impulse);
        if (bumpSound != null && audioSource != null) audioSource.PlayOneShot(bumpSound);
    }
}