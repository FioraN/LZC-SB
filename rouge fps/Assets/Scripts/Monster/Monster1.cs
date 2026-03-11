// Keep the existing class name so current prefabs and scenes continue to work.
public class Monster1 : Monster1Optimized
{
<<<<<<< Updated upstream
    private Animator ani;
    public float speed = 3;
    public float attack = 10;// 近战攻击力

    private List<Transform> patrolPoints;

    // 我们需要引用这个Task，以便在脱战时重置它的状态或目标
    private TaskPatrol patrolTask;

    protected override void Start()
    {
        ani = GetComponent<Animator>();
        type = MonsterType.Melee;

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.speed = speed;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            transform.position = hit.position;

        if (PatrolPointManager.Instance != null)
            patrolPoints = PatrolPointManager.Instance.GetAllPatrolPoints().ToList();
        else
            patrolPoints = new List<Transform>();

        base.Start();
    }


    //脱战
    protected override void OnLostTarget()
    {
        base.OnLostTarget();

        // 核心逻辑：脱战后，找到最近的巡逻点
        if (patrolPoints != null && patrolPoints.Count > 0)
        {
            Transform nearest = GetNearestPatrolPoint();
            if (nearest != null && patrolTask != null)
            {
                // 告诉巡逻任务：下次开始巡逻时，先去这个最近的点
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

        // 2. 战斗检测 (被激怒 OR 看见人)
        Node checkAggro = new CheckAggro(this);
        // 如果距离 <= viewRange，视为发现敌人
        Node checkViewRange = new CheckTargetRange(transform, playerTransform, viewRange);
        Node detectionCheck = new Selector(new List<Node> { checkAggro, checkViewRange });

        // 战斗行为
        Node checkAttackRange = new CheckTargetRange(transform, playerTransform, attackRange);
        Node attackAction = new TaskAttackWithMove(this, ani, agent, playerTransform);
        Node chaseAction = new TaskNavMove(agent, playerTransform, ani);

        Selector combatBehaviors = new Selector(new List<Node>
        {
            new Sequence(new List<Node> { checkAttackRange, attackAction }),
            chaseAction
        });

        Sequence combatSequence = new Sequence(new List<Node> { detectionCheck, combatBehaviors });

        // 3. 巡逻 (创建实例并保存引用)
        patrolTask = new TaskPatrol(transform, patrolPoints, agent, ani);
        Node idle5s = new TaskTimedIdle(ani, 5.0f);

        // 巡逻逻辑：先巡逻 -> 到了休息 -> 重复
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



    protected override void PerformAttack()
    {
        // 1. 检查玩家是否存在且存活
        if (playerTransform == null) return;


        if (ani != null) ani.SetTrigger("Attack");

       


    }



    
}
=======
}
>>>>>>> Stashed changes
