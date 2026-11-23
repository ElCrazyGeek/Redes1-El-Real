using UnityEngine;
using Mirror;

public class GrabbableItem : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnHolderChanged))]
    public NetworkIdentity currentHolder;

    [SyncVar]
    public int lastTeamTouch = 1;

    [SyncVar]
    public bool isPowerShot = false;

    [Header("Sonido y Balonazo")]
    [SerializeField] private AudioClip bounceSound;
    [SerializeField] private float minPowerShotSpeed = 9f; // Velocidad mínima para seguir stuneando
    private AudioSource audioSource;

    private Rigidbody2D rb;
    private Collider2D col;

    [Header("Protección Anti-Robo")]
    [SerializeField] private float stealProtectionDuration = 0.9f;
    private float lastGrabTime = -10f;

    public bool CanBeStolen()
    {
        return Time.time >= lastGrabTime + stealProtectionDuration;
    }

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
        if (currentHolder != null)
        {
            Vector3 forwardOffset = currentHolder.transform.right * 0.8f;
            transform.position = currentHolder.transform.position + forwardOffset;
            transform.rotation = Quaternion.identity;
        }
        else if (isServer && isPowerShot && rb != null)
        {
            // Cuando el balonazo pierde velocidad, deja de stunear
            if (rb.linearVelocity.magnitude < minPowerShotSpeed)
            {
                isPowerShot = false;
            }
        }
    }

    private void OnHolderChanged(NetworkIdentity oldHolder, NetworkIdentity newHolder)
    {
        if (newHolder != null)
        {
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
        // Si alguien ya tenía el balón y se lo acaban de quitar, le avisamos al bot anterior
        if (currentHolder != null && currentHolder != player)
        {
            if (currentHolder.TryGetComponent<BotAIController>(out BotAIController previousBot))
            {
                previousBot.OnBallLostOrStolen();
            }
        }

        if (player.TryGetComponent<TeamMember>(out TeamMember tm))
        {
            lastTeamTouch = tm.teamId;
        }
        isPowerShot = false;
        currentHolder = player;
        lastGrabTime = Time.time;
    }

    [Server]
    public void Drop()
    {
        isPowerShot = false;
        currentHolder = null;
    }

    [Server]
    public void Throw(Vector2 throwDirection, float force, bool powerShot = false)
    {
        if (currentHolder != null)
        {
            // Margen seguro para no golpear al lanzador en el instante 0
            transform.position = currentHolder.transform.position + (Vector3)(throwDirection * 1.1f);
        }

        currentHolder = null;
        isPowerShot = powerShot;

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.linearVelocity = throwDirection * force;
        }
    }

    [ServerCallback]
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (currentHolder != null) return;

        // Solo el balonazo (isPowerShot == true) stunea a compañeros, rivales o al mismo jugador
        if (isPowerShot)
        {
            if (collision.gameObject.TryGetComponent<PlayerMovement2D>(out PlayerMovement2D hitPlayer))
            {
                isPowerShot = false;
                Vector2 knockDir = (hitPlayer.transform.position - transform.position).normalized;
                hitPlayer.ServerApplyStunAndDrop(knockDir * 9f, 1.6f);
            }
            else if (collision.gameObject.TryGetComponent<BotAIController>(out BotAIController hitBot))
            {
                isPowerShot = false;
                Vector2 knockDir = (hitBot.transform.position - transform.position).normalized;
                hitBot.ServerApplyStunAndDrop(knockDir * 9f, 1.6f);
            }
        }

        if (collision.relativeVelocity.magnitude > 1f)
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