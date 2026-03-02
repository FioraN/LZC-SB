using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]


public class MonsterBase : MonoBehaviour
{
    [Header("Base Stats")]
    public MonsterType type;
    public float hp = 100f;
    [Header("移动速度")]
    public float speed = 5;// 移动速度
    [Header("攻击力")]
    public float attack = 15;// 远程攻击力

    [Header("感知设置")]
    [Tooltip("索敌范围：怪物未发现玩家时，能看到玩家的距离")]
    public float viewRange = 8f;

    [Header("感知角度")]
    [Range(0, 360)]
    public float viewAngle = 120f;

    [Tooltip("追击范围：怪物发现玩家后，能持续追踪的最大距离")]
    public float chaseRange = 15f;

    [Header("攻击范围")]
    public float attackRange = 2f;
    [Header("攻击冷却时间")]
    public float attackCooldown = 1.5f;



    [HideInInspector] public bool isHurt = false;
    [HideInInspector] public bool hasAggro = false; // 是否已发现敌人
    protected bool isDead = false;//是否死亡
    [HideInInspector] public NavMeshAgent agent;

    protected float lastAttackTime;
    [HideInInspector] public Transform playerTransform;
    protected Node rootNode;




    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.updateRotation = true;
            agent.updatePosition = true;
        }
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

        if (isDead) return;//检测死亡

        CheckAggroState();

        if (rootNode != null)
        {
            rootNode.Evaluate();
        }
    }

    protected virtual void CheckAggroState()
    {
        if (playerTransform == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (hasAggro)
        {
            // 如果玩家跑出了追击范围
            if (distanceToPlayer > chaseRange)
            {
                hasAggro = false;
                Debug.Log($"{name} lost target. Returning to patrol.");
                OnLostTarget(); // 触发脱战逻辑
            }
        }
        else
        {
            // 只有当玩家进入视线范围(viewRange)才会被重新激怒
            // 之前的 Detection Check 节点也会做这个检查，这里是双重保险
            if (distanceToPlayer <= viewRange)
            {

                Vector3 dirToPlayer = (playerTransform.position - transform.position).normalized;
                // 计算前方与玩家方向的夹角
                float angle = Vector3.Angle(transform.forward, dirToPlayer);

                // 如果在扇形范围内（角度的一半，因为 Angle 返回的是两者夹角）
                if (angle <= viewAngle * 0.5f)
                {
                    if (!hasAggro)
                    {
                        hasAggro = true;
                    }
                }
            }
        }
    }

    // 新增：当丢失目标时触发，子类可以重写此方法来重置巡逻点
    protected virtual void OnLostTarget()
    {
        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.ResetPath(); // 立即停止追击移动
        }
    }

    protected virtual void SetupBehaviorTree() { }


    // 尝试执行攻击，内部会检查冷却时间和朝向调整
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


    // 受到伤害
    public virtual void TakeDamage(float amount)
    {
        if (isDead) return; // 死了就不再受伤

        hp -= amount;
        if (!hasAggro) hasAggro = true; // 被打立马反击

        if (hp <= 0)
        {
            Die();//死亡
        }
        else
        {
            isHurt = true;
        }
    }


    protected virtual void Die()
    {
        if (isDead) return;
        isDead = true;

        // 1. 禁用寻路，防止尸体滑行
        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        // 2. 禁用碰撞体（可选，防止玩家被尸体挡住路）
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // 3. 播放动画
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


    //死亡动画结束后调用
    public virtual void OnDeathAnimationEnd()
    {
        Destroy(gameObject);
    }




    protected virtual void OnDrawGizmosSelected()
    {
        // 1. 画距离圆
        Gizmos.color = new Color(1, 1, 0, 0.2f); // 黄色半透明
        Gizmos.DrawWireSphere(transform.position, viewRange);

        // 2. 画扇形角度 (左右两条线)
        Vector3 leftDir = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * transform.forward;
        Vector3 rightDir = Quaternion.Euler(0, viewAngle * 0.5f, 0) * transform.forward;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + leftDir * viewRange);
        Gizmos.DrawLine(transform.position, transform.position + rightDir * viewRange);

        // 3. 追击和攻击范围
        Gizmos.color = new Color(1, 0, 0, 0.5f);
        Gizmos.DrawWireSphere(transform.position, chaseRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}

public enum MonsterType { Melee, Ranged }