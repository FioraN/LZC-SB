using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
<<<<<<< Updated upstream
=======
[RequireComponent(typeof(MonsterHealth))]
>>>>>>> Stashed changes
public class MonsterBase : MonoBehaviour
{
    [Header("Base Stats")]
    public MonsterType type;
<<<<<<< Updated upstream
    public float hp = 100f;
    
=======
    [Header("移动速度")]
    public float speed = 5;
    [Header("攻击力")]
    public float attack = 15;
>>>>>>> Stashed changes

    [Header("感知设置")]
    [Tooltip("索敌范围：怪物未发现玩家时，能看到玩家的距离")]
    public float viewRange = 8f;

<<<<<<< Updated upstream
    [Tooltip("׷����Χ�����﷢����Һ��ܳ���׷�ٵ�������")]
=======
    [Header("感知角度")]
    [Range(0, 360)]
    public float viewAngle = 120f;

    [Tooltip("追击范围：怪物发现玩家后，能持续追踪的最大距离")]
>>>>>>> Stashed changes
    public float chaseRange = 15f;

    [Header("攻击范围")]
    public float attackRange = 2f;
    [Header("攻击冷却时间")]
    public float attackCooldown = 1.5f;

<<<<<<< Updated upstream
=======
    [Header("寻路脱困")]
    [Tooltip("怪物有路径但速度长期接近 0 时，判定为卡住。")]
    public float stuckVelocityThreshold = 0.08f;
    [Tooltip("持续多久几乎不动，开始执行脱困。")]
    public float stuckRecoverDelay = 0.75f;
    [Tooltip("脱困时在当前位置附近采样 NavMesh 的半径。")]
    public float navMeshRecoverRadius = 1.5f;

    [Header("假跳跃")]
    [Tooltip("前方高低差不超过这个值时，怪物会尝试假跳跃。")]
    public float maxJumpHeightDifference = 1.2f;
    [Tooltip("高低差至少超过这个值才触发跳跃，避免平地乱跳。")]
    public float minJumpHeightDifference = 0.25f;
    [Tooltip("沿移动方向向前检测的距离。")]
    public float jumpForwardCheckDistance = 1.2f;
    [Tooltip("假跳跃的抛物线高度。")]
    public float jumpArcHeight = 0.8f;
    [Tooltip("假跳跃持续时间。")]
    public float jumpDuration = 0.35f;
    [Tooltip("两次跳跃之间的冷却时间。")]
    public float jumpCooldown = 0.75f;

>>>>>>> Stashed changes
    [HideInInspector] public bool isHurt = false;
    [HideInInspector] public bool hasAggro = false;
    [HideInInspector] public bool isJumping = false;
    protected bool isDead = false;
    [HideInInspector] public NavMeshAgent agent;

    protected MonsterHealth monsterHealth;
    protected float lastAttackTime;
    [HideInInspector] public Transform playerTransform;
    protected Node rootNode;
    private float stuckTimer;
    private float lastJumpTime = -999f;

    protected virtual void Awake()
    {
        monsterHealth = GetComponent<MonsterHealth>();
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.updateRotation = true;
            agent.updatePosition = true;
        }

        HookHealthEvents(true);
    }

    protected virtual void OnDestroy()
    {
        HookHealthEvents(false);
    }

    protected virtual void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }

        if (agent != null)
        {
            agent.stoppingDistance = attackRange * 0.8f;
            if (!agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
                {
                    transform.position = hit.position;
                }
            }
        }

        SetupBehaviorTree();
    }

    protected virtual void Update()
    {
        if (isDead) return;
        if (isJumping) return;

        CheckAggroState();

        if (rootNode != null)
        {
            rootNode.Evaluate();
        }

        TryStartFakeJump();
        MonitorAgentStuckState();
    }

    protected virtual void HandleDamaged(DamageInfo info)
    {
        if (isDead) return;

        isHurt = true;
        hasAggro = true;
    }

    protected virtual void HandleHealthDied()
    {
        Die();
    }

    private void HookHealthEvents(bool subscribe)
    {
        if (monsterHealth == null) return;

        if (subscribe)
        {
            monsterHealth.Damaged += HandleDamaged;
            monsterHealth.Died += HandleHealthDied;
        }
        else
        {
            monsterHealth.Damaged -= HandleDamaged;
            monsterHealth.Died -= HandleHealthDied;
        }
    }

    protected virtual void CheckAggroState()
    {
        if (playerTransform == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (hasAggro)
        {
            if (distanceToPlayer > chaseRange)
            {
                hasAggro = false;
                Debug.Log($"{name} lost target. Returning to patrol.");
                OnLostTarget();
            }
        }
        else
        {
            if (distanceToPlayer <= viewRange)
            {
<<<<<<< Updated upstream
                if (!hasAggro)
=======
                Vector3 dirToPlayer = (playerTransform.position - transform.position).normalized;
                float angle = Vector3.Angle(transform.forward, dirToPlayer);

                if (angle <= viewAngle * 0.5f)
>>>>>>> Stashed changes
                {
                    hasAggro = true;
                    Debug.Log($"{name} spotted player! Engaging.");
                }
            }
        }
    }

    protected virtual void OnLostTarget()
    {
        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.ResetPath();
        }
    }

    protected virtual void SetupBehaviorTree() { }

    private void TryStartFakeJump()
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
        if (Time.time - lastJumpTime < jumpCooldown) return;
        if (agent.isStopped || agent.pathPending || !agent.hasPath) return;
        if (agent.remainingDistance <= agent.stoppingDistance + 0.15f) return;
        if (agent.desiredVelocity.sqrMagnitude <= 0.04f) return;

        Vector3 moveDir = agent.desiredVelocity.normalized;
        Vector3 sampleOrigin = transform.position + moveDir * jumpForwardCheckDistance;

        if (!NavMesh.SamplePosition(sampleOrigin, out NavMeshHit hit, agent.radius + 0.8f, NavMesh.AllAreas))
        {
            return;
        }

        float heightDelta = hit.position.y - transform.position.y;
        float absHeightDelta = Mathf.Abs(heightDelta);

        if (absHeightDelta < minJumpHeightDifference || absHeightDelta > maxJumpHeightDifference)
        {
            return;
        }

        Vector3 horizontalOffset = new Vector3(
            hit.position.x - transform.position.x,
            0f,
            hit.position.z - transform.position.z
        );

        if (horizontalOffset.sqrMagnitude < 0.09f || horizontalOffset.sqrMagnitude > (jumpForwardCheckDistance + 0.8f) * (jumpForwardCheckDistance + 0.8f))
        {
            return;
        }

        if (!CanJumpToward(hit.position))
        {
            return;
        }

        StartCoroutine(FakeJumpTo(hit.position));
    }

    private bool CanJumpToward(Vector3 targetPosition)
    {
        NavMeshPath path = new NavMeshPath();
        if (!agent.CalculatePath(targetPosition, path))
        {
            return false;
        }

        return path.status != NavMeshPathStatus.PathComplete;
    }

    private IEnumerator FakeJumpTo(Vector3 targetPosition)
    {
        isJumping = true;
        lastJumpTime = Time.time;

        Vector3 startPosition = transform.position;
        Vector3 flatTarget = targetPosition;

        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.updatePosition = false;
            agent.updateRotation = false;
        }

        float elapsed = 0f;
        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / jumpDuration);
            Vector3 basePosition = Vector3.Lerp(startPosition, flatTarget, t);
            float arcOffset = Mathf.Sin(t * Mathf.PI) * jumpArcHeight;
            transform.position = basePosition + Vector3.up * arcOffset;

            Vector3 lookDir = flatTarget - startPosition;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(lookDir);
            }

            yield return null;
        }

        transform.position = flatTarget;

        if (agent != null)
        {
            agent.enabled = true;
            agent.Warp(flatTarget);
            agent.updatePosition = true;
            agent.updateRotation = true;
            agent.isStopped = false;
        }

        isJumping = false;
    }

    private void MonitorAgentStuckState()
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
        {
            stuckTimer = 0f;
            return;
        }

        if (agent.isStopped || agent.pathPending)
        {
            stuckTimer = 0f;
            return;
        }

        if (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.2f)
        {
            stuckTimer = 0f;
            return;
        }

        bool wantsToMove = agent.desiredVelocity.sqrMagnitude > 0.01f;
        bool barelyMoving = agent.velocity.sqrMagnitude <= stuckVelocityThreshold * stuckVelocityThreshold;

        if (!wantsToMove || !barelyMoving)
        {
            stuckTimer = 0f;
            return;
        }

        stuckTimer += Time.deltaTime;
        if (stuckTimer < stuckRecoverDelay) return;

        stuckTimer = 0f;
        RecoverToNearestNavMeshPoint();
    }

    private void RecoverToNearestNavMeshPoint()
    {
        float sampleRadius = Mathf.Max(navMeshRecoverRadius, agent.radius + 0.2f);
        Vector3 sampleCenter = transform.position;

        if (!NavMesh.SamplePosition(sampleCenter, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
        {
            if (!NavMesh.SamplePosition(sampleCenter, out hit, sampleRadius * 2f, NavMesh.AllAreas))
            {
                return;
            }
        }

        agent.Warp(hit.position);
        agent.ResetPath();
    }

    public bool TryAttack()
    {
        if (Time.time - lastAttackTime >= attackCooldown)
        {
            if (playerTransform != null)
            {
                Vector3 lookPos = playerTransform.position;
                lookPos.y = transform.position.y;
                transform.LookAt(lookPos);
            }

            PerformAttack();
            lastAttackTime = Time.time;
            return true;
        }
        return false;
    }

    protected virtual void PerformAttack() { }

    protected virtual void Die()
    {
        if (isDead) return;
        isDead = true;
        isJumping = false;

        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false; 
        }

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Animator ani = GetComponent<Animator>();
        if (ani != null)
        {
            ani.SetTrigger("Die");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public virtual void OnDeathAnimationEnd()
    {
        Destroy(gameObject);
    }

    protected virtual void OnDrawGizmosSelected()
    {
<<<<<<< Updated upstream
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewRange);
=======
        Gizmos.color = new Color(1, 1, 0, 0.2f);
        Gizmos.DrawWireSphere(transform.position, viewRange);

        Vector3 leftDir = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * transform.forward;
        Vector3 rightDir = Quaternion.Euler(0, viewAngle * 0.5f, 0) * transform.forward;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + leftDir * viewRange);
        Gizmos.DrawLine(transform.position, transform.position + rightDir * viewRange);

>>>>>>> Stashed changes
        Gizmos.color = new Color(1, 0, 0, 0.5f);
        Gizmos.DrawWireSphere(transform.position, chaseRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}

public enum MonsterType { Melee, Ranged }
