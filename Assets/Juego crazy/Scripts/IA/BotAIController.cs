using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

[RequireComponent(typeof(Rigidbody2D), typeof(TeamMember))]
public class BotAIController : NetworkBehaviour
{
    [Header("Movimiento y Físicas")]
    [SerializeField] private float moveSpeed = 4.6f;
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Interacción con Balón")]
    [SerializeField] private float grabRadius = 1.4f;
    [SerializeField] private float stealRange = 1.5f;
    [SerializeField] private float passForce = 13.5f;
    [SerializeField] private float goalShotForce = 21f;
    [SerializeField] private LayerMask obstacleLayer;

    [Header("Tiempos de Decisión y Pases Constantes")]
    [SerializeField] private float actionCooldown = 0.55f;
    [SerializeField] private float minHoldTimeBeforePass = 0.75f; // Pasan rápido tras pintar un par de casillas
    [SerializeField] private float stealRecoveryDelay = 2.5f;     // Delay tras perder el balón para ceder presión a otro compañero
    [SerializeField] private float stunDuration = 1.6f;

    [Header("Sonidos y Animación")]
    [SerializeField] private AudioClip grabSound;
    [SerializeField] private AudioClip throwSound;
    [SerializeField] private AudioClip bumpSound;

    private Rigidbody2D rb;
    private TeamMember teamMember;
    private Animator anim;
    private NetworkAnimator networkAnimator;
    private AudioSource audioSource;

    [SyncVar]
    public bool isStunned = false;

    private float nextActionTime = 0f;
    private float ballGrabbedTimestamp = 0f;
    private float canChaseBallAfterTime = 0f;
    private Vector2 currentMoveDir = Vector2.zero;

    private GrabbableItem ball;
    private Transform targetEnemyGoal;

    public bool IsInRecoveryDelay => Time.time < canChaseBallAfterTime;
    private Transform requestedPassTarget = null;
    private float passRequestExpireTime = 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        teamMember = GetComponent<TeamMember>();
        anim = GetComponent<Animator>();
        networkAnimator = GetComponent<NetworkAnimator>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        FindGoalsAndBall();
    }

    [Server]
    public void OnBallLostOrStolen()
    {
        canChaseBallAfterTime = Time.time + stealRecoveryDelay;
        nextActionTime = Time.time + 0.5f;
    }

    private void FindGoalsAndBall()
    {
        if (ball == null) ball = FindAnyObjectByType<GrabbableItem>();

        string goalName = (teamMember != null && teamMember.teamId == 1) ? "Porteria_B" : "Porteria_A";
        GameObject goalObj = GameObject.Find(goalName);
        if (goalObj != null) targetEnemyGoal = goalObj.transform;
    }

    [ServerCallback]
    private void Update()
    {
        if (isStunned)
        {
            currentMoveDir = Vector2.zero;
            UpdateAnimation(0f);
            return;
        }

        if (ball == null || targetEnemyGoal == null)
        {
            FindGoalsAndBall();
            if (ball == null) return;
        }

        int myTeam = teamMember != null ? teamMember.teamId : 1;
        List<Vector2> teammatePositions = GetTeammatePositions(myTeam);

        // ==========================================================
        // CASO 1: ESTE BOT TIENE EL BALÓN
        // ==========================================================
        if (ball.currentHolder == netIdentity)
        {
            HandlePossessionBehavior(myTeam, teammatePositions);
        }
        // ==========================================================
        // CASO 2: BALÓN SUELTO EN LA CANCHA
        // ==========================================================
        else if (ball.currentHolder == null)
        {
            if (ShouldIChaseBall(ball.transform.position, myTeam))
            {
                MoveTowards(ball.transform.position);
                float distToBall = Vector2.Distance(transform.position, ball.transform.position);
                if (distToBall <= grabRadius && Time.time >= nextActionTime)
                {
                    nextActionTime = Time.time + actionCooldown;
                    ball.Grab(netIdentity);
                    ballGrabbedTimestamp = Time.time;
                    RpcPlayGrabSound();
                }
            }
            else
            {
                SpreadOutAndCoverCourt(myTeam, teammatePositions);
            }
        }
        // ==========================================================
        // CASO 3: OTRO JUGADOR O BOT TIENE EL BALÓN
        // ==========================================================
        else
        {
            int holderTeam = GetHolderTeam(ball.currentHolder);

            if (holderTeam != myTeam)
            {
                if (ShouldIChaseBall(ball.currentHolder.transform.position, myTeam))
                {
                    MoveTowards(ball.currentHolder.transform.position);

                    float distToHolder = Vector2.Distance(transform.position, ball.currentHolder.transform.position);
                    if (distToHolder <= stealRange && ball.CanBeStolen() && Time.time >= nextActionTime)
                    {
                        nextActionTime = Time.time + actionCooldown;
                        ball.Grab(netIdentity);
                        ballGrabbedTimestamp = Time.time;
                        RpcPlayGrabSound();
                    }
                }
                else
                {
                    SpreadOutAndCoverCourt(myTeam, teammatePositions);
                }
            }
            else
            {
                SpreadOutAndCoverCourt(myTeam, teammatePositions);
            }
        }
    }

    private bool ShouldIChaseBall(Vector2 targetPos, int myTeam)
    {
        BotAIController[] allBots = FindObjectsByType<BotAIController>(FindObjectsSortMode.None);
        List<BotAIController> availableTeammateBots = new List<BotAIController>();

        foreach (var bot in allBots)
        {
            if (bot == null || bot.isStunned) continue;
            if (bot.teamMember != null && bot.teamMember.teamId == myTeam)
            {
                if (!bot.IsInRecoveryDelay)
                {
                    availableTeammateBots.Add(bot);
                }
            }
        }

        if (IsInRecoveryDelay && availableTeammateBots.Count > 0)
        {
            return false;
        }

        float myDist = Vector2.Distance(transform.position, targetPos);
        foreach (var candidate in availableTeammateBots)
        {
            if (candidate == this) continue;
            float otherDist = Vector2.Distance(candidate.transform.position, targetPos);
            if (otherDist < myDist - 0.1f)
            {
                return false;
            }
        }

        return true;
    }

    [Server]
    private void SpreadOutAndCoverCourt(int myTeam, List<Vector2> teammatePositions)
    {
        foreach (Vector2 matePos in teammatePositions)
        {
            float distToMate = Vector2.Distance(transform.position, matePos);
            if (distToMate < 3.5f)
            {
                Vector2 pushAway = ((Vector2)transform.position - matePos).normalized;
                if (pushAway.sqrMagnitude < 0.01f) pushAway = Random.insideUnitCircle.normalized;
                MoveTowards((Vector2)transform.position + pushAway * 3f);
                return;
            }
        }

        if (FloorGridManager.Instance != null)
        {
            Vector2 wideSpot = FloorGridManager.Instance.GetSpreadUnpaintedTile(myTeam, transform.position, teammatePositions);
            MoveTowards(wideSpot);
        }
    }

    [Server]
    private void HandlePossessionBehavior(int myTeam, List<Vector2> teammatePositions)
    {
        bool canScoreGoal = FloorGridManager.Instance != null &&
                            FloorGridManager.Instance.IsCourtFullyCoveredByTeam(myTeam);

        // 1. METER GOL SI YA CUBRIERON EL 100% DE LA CANCHA
        if (canScoreGoal && targetEnemyGoal != null)
        {
            Vector2 goalPos = targetEnemyGoal.position;
            float distToGoal = Vector2.Distance(transform.position, goalPos);

            if (distToGoal <= 9f && HasClearLineOfSight(transform.position, goalPos) && Time.time >= nextActionTime)
            {
                nextActionTime = Time.time + actionCooldown;
                requestedPassTarget = null;
                Vector2 shootDir = (goalPos - (Vector2)transform.position).normalized;
                RotateInstant(shootDir);
                ball.Throw(shootDir, goalShotForce, false);
                TriggerKickAnim();
                RpcPlayThrowSound();
                return;
            }
            else
            {
                MoveTowards(goalPos);
                return;
            }
        }

        // 2. RESPONDER SI EL JUGADOR HUMANO PIDIÓ PASE CON CLIC IZQUIERDO
        if (requestedPassTarget != null && Time.time <= passRequestExpireTime)
        {
            if (HasClearLineOfSight(transform.position, requestedPassTarget.position))
            {
                nextActionTime = Time.time + actionCooldown;
                Vector2 passDir = ((Vector2)requestedPassTarget.position - (Vector2)transform.position).normalized;
                RotateInstant(passDir);
                ball.Throw(passDir, passForce, false);
                requestedPassTarget = null;
                TriggerKickAnim();
                RpcPlayThrowSound();
                return;
            }
            else
            {
                MoveTowards(requestedPassTarget.position);
                return;
            }
        }
        else
        {
            requestedPassTarget = null;
        }

        // 3. PASES CONSTANTES ENTRE BOTS Y HACIA EL JUGADOR
        if (Time.time - ballGrabbedTimestamp >= minHoldTimeBeforePass && Time.time >= nextActionTime)
        {
            Transform bestTeammate = FindBestTeammateToPass(myTeam);
            if (bestTeammate != null)
            {
                nextActionTime = Time.time + actionCooldown;
                Vector2 passDir = ((Vector2)bestTeammate.position - (Vector2)transform.position).normalized;
                RotateInstant(passDir);
                ball.Throw(passDir, passForce, false);
                TriggerKickAnim();
                RpcPlayThrowSound();
                return;
            }
        }

        // 4. PINTAR CASILLAS LIBRES ALEJADAS DE LOS COMPAÑEROS
        if (FloorGridManager.Instance != null)
        {
            Vector2 nextTilePos = FloorGridManager.Instance.GetSpreadUnpaintedTile(myTeam, transform.position, teammatePositions);
            MoveTowards(nextTilePos);
        }
    }

    [Server]
    private Transform FindBestTeammateToPass(int myTeam)
    {
        TeamMember[] allMembers = FindObjectsByType<TeamMember>(FindObjectsSortMode.None);
        Transform bestTarget = null;
        float bestScore = -1f;

        foreach (var member in allMembers)
        {
            if (member == null || member.gameObject == gameObject) continue;
            if (member.teamId != myTeam) continue;

            if (member.TryGetComponent<PlayerMovement2D>(out var pm) && pm.isStunned) continue;
            if (member.TryGetComponent<BotAIController>(out var bm) && bm.isStunned) continue;

            float dist = Vector2.Distance(transform.position, member.transform.position);
            if (dist < 1.5f || dist > 14f) continue;

            if (!HasClearLineOfSight(transform.position, member.transform.position)) continue;

            float score = dist;
            if (member.GetComponent<PlayerMovement2D>() != null)
            {
                score += 3.0f; // Prioriza pasársela al jugador humano
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = member.transform;
            }
        }

        return bestTarget;
    }

    private List<Vector2> GetTeammatePositions(int myTeam)
    {
        List<Vector2> positions = new List<Vector2>();
        TeamMember[] allMembers = FindObjectsByType<TeamMember>(FindObjectsSortMode.None);
        foreach (var m in allMembers)
        {
            if (m != null && m.gameObject != gameObject && m.teamId == myTeam)
            {
                positions.Add(m.transform.position);
            }
        }
        return positions;
    }

    private bool HasClearLineOfSight(Vector2 from, Vector2 to)
    {
        Vector2 dir = to - from;
        float dist = dir.magnitude;
        RaycastHit2D[] hits = Physics2D.RaycastAll(from + dir.normalized * 0.7f, dir.normalized, dist - 0.7f, obstacleLayer);

        foreach (var hit in hits)
        {
            // Ignoramos el balón, otros jugadores/bots y triggers; solo bloquean paredes/obstáculos reales
            if (hit.collider == null || hit.collider.isTrigger) continue;
            if (hit.collider.GetComponent<TeamMember>() != null) continue;
            if (hit.collider.GetComponent<GrabbableItem>() != null) continue;

            return false; // Hay una pared u obstáculo real en medio
        }
        return true;
    }

    private void MoveTowards(Vector2 targetPos)
    {
        Vector2 diff = targetPos - (Vector2)transform.position;
        if (diff.sqrMagnitude > 0.15f)
        {
            currentMoveDir = diff.normalized;
            float targetAngle = Mathf.Atan2(currentMoveDir.y, currentMoveDir.x) * Mathf.Rad2Deg;
            float smoothAngle = Mathf.LerpAngle(transform.eulerAngles.z, targetAngle, rotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0, 0, smoothAngle);
            if (rb != null) rb.rotation = smoothAngle;
            UpdateAnimation(1f);
        }
        else
        {
            currentMoveDir = Vector2.zero;
            UpdateAnimation(0f);
        }
    }

    private void RotateInstant(Vector2 dir)
    {
        if (dir.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
            if (rb != null) rb.rotation = angle;
        }
    }

    [ServerCallback]
    private void FixedUpdate()
    {
        if (isStunned)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }
        rb.linearVelocity = currentMoveDir * moveSpeed;
    }

    private int GetHolderTeam(NetworkIdentity holder)
    {
        if (holder != null && holder.TryGetComponent<TeamMember>(out TeamMember tm))
        {
            return tm.teamId;
        }
        return 0;
    }

    [Server]
    public void ServerApplyStunAndDrop(Vector2 knockbackForce, float duration)
    {
        if (ball != null && ball.currentHolder == netIdentity)
        {
            ball.Drop();
            OnBallLostOrStolen();
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

    private void UpdateAnimation(float speedVal)
    {
        if (anim != null) anim.SetFloat("Movimiento", speedVal);
    }

    private void TriggerKickAnim()
    {
        if (networkAnimator != null) networkAnimator.SetTrigger("Patear");
        else if (anim != null) anim.SetTrigger("Patear");
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
    private void RpcPlayGrabSound()
    {
        if (grabSound != null && audioSource != null) audioSource.PlayOneShot(grabSound);
    }

    [ClientRpc]
    private void RpcPlayThrowSound()
    {
        if (throwSound != null && audioSource != null) audioSource.PlayOneShot(throwSound);
    }

    [Server]
    public void ReceivePassRequest(Transform requester)
    {
        requestedPassTarget = requester;
        passRequestExpireTime = Time.time + 2.0f;
    }
}