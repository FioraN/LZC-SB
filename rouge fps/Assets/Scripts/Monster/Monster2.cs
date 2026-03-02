using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

//远程攻击怪物：会在一定范围内使用投射物攻击玩家，脱战后回最近的巡逻点
public class Monster2 : MonsterBase
{
    private Animator ani;

    // 投射物预制体 (必须在 Inspector 赋值)
    [Header("Ranged Settings")]
    public GameObject projectilePrefab;
    public Transform firePoint; // 发射点的位置

    private TaskPatrol patrolTask;
    private List<Transform> patrolPoints;

    protected override void Start()
    {
        ani = GetComponent<Animator>();
        type = MonsterType.Ranged;

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.speed = speed;

        // 纠正坐标到 NavMesh
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            transform.position = hit.position;

        if (PatrolPointManager.Instance != null)
            patrolPoints = PatrolPointManager.Instance.GetAllPatrolPoints().ToList();
        else
            patrolPoints = new List<Transform>();

        base.Start();
    }

    // 脱战回最近点的逻辑
    protected override void OnLostTarget()
    {
        base.OnLostTarget();

        if (patrolPoints != null && patrolPoints.Count > 0)
        {
            Transform nearest = GetNearestPatrolPoint();
            if (nearest != null && patrolTask != null)
            {
                patrolTask.SetNextPatrolPoint(nearest);
            }
        }
    }

    private Transform GetNearestPatrolPoint()
    {
        Transform nearest = null;
        float minDst = float.MaxValue;
        foreach (var p in patrolPoints)
        {
            if (p == null) continue;
            float d = Vector3.Distance(transform.position, p.position);
            if (d < minDst)
            {
                minDst = d;
                nearest = p;
            }
        }
        return nearest;
    }

    protected override void SetupBehaviorTree()
    {
        // 1. 受伤
        Node hurtNode = new TaskHurt(this, ani);

        // 2. 战斗检测
        Node checkAggro = new CheckAggro(this);

        // 如果距离 <= viewRange，而且在角度范围内 视为发现敌人
        Node checkViewSector = new CheckTargetSector(transform, playerTransform, viewRange, viewAngle);

        Node detectionCheck = new Selector(new List<Node> { checkAggro, checkViewSector });

        // 3. 战斗行为

        Node checkAttackRange = new CheckTargetRange(transform, playerTransform, attackRange);

        // TaskAttackWithMove 负责：停止 -> 转身 -> 前摇 -> 攻击(此时调用PerformAttack) -> 后摇
        Node attackAction = new TaskAttackWithMove(this, ani, agent, playerTransform);

        Node chaseAction = new TaskNavMove(agent, playerTransform, ani);

        Selector combatBehaviors = new Selector(new List<Node>
        {
            new Sequence(new List<Node> { checkAttackRange, attackAction }),
            chaseAction
        });

        Sequence combatSequence = new Sequence(new List<Node> { detectionCheck, combatBehaviors });

        // 4. 巡逻
        patrolTask = new TaskPatrol(transform, patrolPoints, agent, ani);
        Node idle5s = new TaskTimedIdle(ani, 5.0f);

        Sequence patrolIdleSeq = new Sequence(new List<Node>
        {
            patrolTask,
            idle5s
        });

        rootNode = new Selector(new List<Node>
        {
            hurtNode,
            combatSequence,
            patrolIdleSeq
        });
    }

    //远程攻击逻辑
    protected override void PerformAttack()
    {

        // 这里可以添加一些攻击前的动画或效果
        ani.SetTrigger("Attack");



        // 生成投射物
        if (projectilePrefab != null && firePoint != null)
        {
            GameObject proj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
            // 这里你可以初始化子弹，给它方向等
            proj.SetActive(true);

            // 简单朝向玩家
            if (playerTransform != null)
            {
                proj.transform.LookAt(playerTransform.position + Vector3.up * 1.2f); // 稍微抬高一点瞄准胸口
            }

            //子弹往玩家方向飞行
            Rigidbody rb = proj.GetComponent<Rigidbody>();
            rb.velocity = Vector3.zero;
            if (playerTransform != null)
            {
                Vector3 direction = (playerTransform.position + Vector3.up * 1.2f - firePoint.position).normalized;
                float projectileSpeed = 10f; // 你可以根据需要调整这个速度
                rb.velocity = direction * projectileSpeed;
            }



        }
        else
        {
            Debug.LogWarning("Monster2 missing projectilePrefab or firePoint!");
        }
    }
}