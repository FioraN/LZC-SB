using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

// Optimized melee monster AI with more resilient path planning and recovery.
public class Monster1Optimized : MonsterBase
{
    [Header("Optimized Pathing")]
    [Tooltip("Minimum chase repath interval.")]
    public float chaseRepathInterval = 0.2f;
    [Tooltip("Repath sooner if the target moved farther than this.")]
    public float chaseTargetMoveThreshold = 0.75f;
    [Tooltip("NavMesh sample radius around chase target.")]
    public float chaseTargetSampleRadius = 1.75f;
    [Tooltip("NavMesh sample radius around patrol points.")]
    public float patrolPointSampleRadius = 1.25f;
    [Tooltip("Side-step distance used during local recovery.")]
    public float recoveryProbeDistance = 1.2f;
    [Tooltip("How long to wait before treating no progress as stuck.")]
    public float noProgressRecoverDelay = 0.75f;

    private Animator ani;
    private List<Transform> patrolPoints;
    private TaskSmartPatrol patrolTask;

    protected override void Start()
    {
        ani = GetComponent<Animator>();
        type = MonsterType.Melee;

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = speed;
            agent.autoBraking = true;
            agent.autoRepath = true;
            agent.autoTraverseOffMeshLink = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            agent.acceleration = Mathf.Max(agent.acceleration, speed * 4f);
            agent.angularSpeed = Mathf.Max(agent.angularSpeed, 540f);
        }

        // The base fake-jump logic tends to misfire on uneven terrain and stairs.
        minJumpHeightDifference = 999f;
        maxJumpHeightDifference = 0f;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
        }

        if (PatrolPointManager.Instance != null)
        {
            patrolPoints = PatrolPointManager.Instance.GetAllPatrolPoints().ToList();
        }
        else
        {
            patrolPoints = new List<Transform>();
        }

        base.Start();
    }

    protected override void OnLostTarget()
    {
        base.OnLostTarget();

        if (patrolPoints == null || patrolPoints.Count == 0 || patrolTask == null)
        {
            return;
        }

        Transform nearest = GetNearestPatrolPoint();
        if (nearest != null)
        {
            patrolTask.SetNextPatrolPoint(nearest);
        }
    }

    private Transform GetNearestPatrolPoint()
    {
        Transform nearest = null;
        float minDistance = float.MaxValue;

        foreach (Transform point in patrolPoints)
        {
            if (point == null) continue;

            float distance = Vector3.Distance(transform.position, point.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = point;
            }
        }

        return nearest;
    }

    protected override void SetupBehaviorTree()
    {
        Node hurtNode = new TaskHurt(this, ani);

        Node checkAggro = new CheckAggro(this);
        Node checkViewSector = new CheckTargetSector(transform, playerTransform, viewRange, viewAngle);
        Node detectionCheck = new Selector(new List<Node> { checkAggro, checkViewSector });

        Node checkAttackRange = new CheckTargetRange(transform, playerTransform, attackRange);
        Node attackAction = new TaskAttackWithMove(this, ani, agent, playerTransform);
        Node chaseAction = new TaskSmartChase(
            transform,
            agent,
            playerTransform,
            ani,
            attackRange,
            chaseRepathInterval,
            chaseTargetMoveThreshold,
            chaseTargetSampleRadius,
            recoveryProbeDistance,
            noProgressRecoverDelay);

        Selector combatBehaviors = new Selector(new List<Node>
        {
            new Sequence(new List<Node> { checkAttackRange, attackAction }),
            chaseAction
        });

        Sequence combatSequence = new Sequence(new List<Node> { detectionCheck, combatBehaviors });

        patrolTask = new TaskSmartPatrol(
            transform,
            patrolPoints,
            agent,
            ani,
            patrolPointSampleRadius,
            recoveryProbeDistance,
            noProgressRecoverDelay);

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

    protected override void PerformAttack()
    {
        if (playerTransform == null) return;

        if (ani != null)
        {
            ani.SetTrigger("Attack");
        }
    }
}

public class TaskSmartChase : Node
{
    private readonly Transform owner;
    private readonly NavMeshAgent agent;
    private readonly Transform target;
    private readonly Animator animator;
    private readonly float attackRange;
    private readonly float repathInterval;
    private readonly float targetMoveThreshold;
    private readonly float targetSampleRadius;
    private readonly float recoveryProbeDistance;
    private readonly float noProgressRecoverDelay;
    private readonly int isMovingHash;

    private Vector3 lastRequestedDestination;
    private Vector3 lastTargetPosition;
    private float nextRepathTime;
    private float noProgressTimer;
    private float lastRemainingDistance = float.PositiveInfinity;
    private bool hasDestination;

    public TaskSmartChase(
        Transform owner,
        NavMeshAgent agent,
        Transform target,
        Animator animator,
        float attackRange,
        float repathInterval,
        float targetMoveThreshold,
        float targetSampleRadius,
        float recoveryProbeDistance,
        float noProgressRecoverDelay)
    {
        this.owner = owner;
        this.agent = agent;
        this.target = target;
        this.animator = animator;
        this.attackRange = attackRange;
        this.repathInterval = Mathf.Max(0.05f, repathInterval);
        this.targetMoveThreshold = Mathf.Max(0.1f, targetMoveThreshold);
        this.targetSampleRadius = Mathf.Max(0.5f, targetSampleRadius);
        this.recoveryProbeDistance = Mathf.Max(0.3f, recoveryProbeDistance);
        this.noProgressRecoverDelay = Mathf.Max(0.25f, noProgressRecoverDelay);
        isMovingHash = Animator.StringToHash("IsMoving");
    }

    public override NodeState Evaluate()
    {
        if (target == null) return NodeState.Failure;
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return NodeState.Failure;

        if (agent.isStopped)
        {
            agent.isStopped = false;
        }

        bool targetMoved = (target.position - lastTargetPosition).sqrMagnitude >= targetMoveThreshold * targetMoveThreshold;
        bool shouldRepath = !hasDestination || Time.time >= nextRepathTime || targetMoved || NeedsPathRefresh();

        if (shouldRepath)
        {
            RefreshPath();
        }

        UpdateProgressMonitor();

        if (animator != null)
        {
            bool shouldMoveAnim = !agent.pathPending && agent.remainingDistance > Mathf.Max(agent.stoppingDistance, attackRange * 0.8f);
            animator.SetBool(isMovingHash, shouldMoveAnim);
        }

        return NodeState.Running;
    }

    private bool NeedsPathRefresh()
    {
        if (!agent.hasPath) return true;
        if (agent.pathStatus == NavMeshPathStatus.PathInvalid) return true;
        if (agent.pathStatus == NavMeshPathStatus.PathPartial && agent.remainingDistance > attackRange + 1f) return true;
        return false;
    }

    private void RefreshPath()
    {
        if (!TryBuildReachableDestination(target.position, out Vector3 destination))
        {
            return;
        }

        if (hasDestination && (destination - lastRequestedDestination).sqrMagnitude < 0.04f)
        {
            nextRepathTime = Time.time + repathInterval;
            lastTargetPosition = target.position;
            return;
        }

        NavMeshPath path = new NavMeshPath();
        if (!agent.CalculatePath(destination, path))
        {
            return;
        }

        if (path.status == NavMeshPathStatus.PathInvalid)
        {
            return;
        }

        agent.SetPath(path);
        agent.stoppingDistance = Mathf.Max(attackRange * 0.8f, 0.35f);

        lastRequestedDestination = destination;
        lastTargetPosition = target.position;
        nextRepathTime = Time.time + repathInterval;
        hasDestination = true;
        ResetProgressSnapshot();
    }

    private bool TryBuildReachableDestination(Vector3 desiredPosition, out Vector3 destination)
    {
        destination = desiredPosition;

        if (NavMesh.SamplePosition(desiredPosition, out NavMeshHit sampledTarget, targetSampleRadius, NavMesh.AllAreas))
        {
            destination = sampledTarget.position;
            return true;
        }

        Vector3 towardOwner = (owner.position - desiredPosition);
        towardOwner.y = 0f;
        if (towardOwner.sqrMagnitude > 0.001f)
        {
            towardOwner.Normalize();
            Vector3 fallback = desiredPosition + towardOwner * Mathf.Max(attackRange * 0.9f, 0.75f);
            if (NavMesh.SamplePosition(fallback, out sampledTarget, targetSampleRadius * 1.5f, NavMesh.AllAreas))
            {
                destination = sampledTarget.position;
                return true;
            }
        }

        if (NavMesh.SamplePosition(owner.position, out sampledTarget, targetSampleRadius, NavMesh.AllAreas))
        {
            destination = sampledTarget.position;
            return true;
        }

        return false;
    }

    private void UpdateProgressMonitor()
    {
        if (!agent.hasPath || agent.pathPending || agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            ResetProgressSnapshot();
            return;
        }

        bool tryingToMove = agent.desiredVelocity.sqrMagnitude > 0.04f;
        bool barelyMoving = agent.velocity.sqrMagnitude <= 0.03f;
        bool progressed = agent.remainingDistance < lastRemainingDistance - 0.08f;

        if (progressed)
        {
            noProgressTimer = 0f;
            lastRemainingDistance = agent.remainingDistance;
            return;
        }

        if (tryingToMove && barelyMoving)
        {
            noProgressTimer += Time.deltaTime;
            if (noProgressTimer >= noProgressRecoverDelay)
            {
                noProgressTimer = 0f;
                AttemptLocalRecovery();
            }
        }
        else
        {
            noProgressTimer = 0f;
        }

        lastRemainingDistance = Mathf.Min(lastRemainingDistance, agent.remainingDistance);
    }

    private void AttemptLocalRecovery()
    {
        if (TryRepathFromOffset(owner.right * recoveryProbeDistance)) return;
        if (TryRepathFromOffset(-owner.right * recoveryProbeDistance)) return;
        if (TryRepathFromOffset(-owner.forward * recoveryProbeDistance * 0.75f)) return;

        if (NavMesh.SamplePosition(owner.position, out NavMeshHit hit, recoveryProbeDistance, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            hasDestination = false;
            ResetProgressSnapshot();
        }
    }

    private bool TryRepathFromOffset(Vector3 offset)
    {
        Vector3 probe = owner.position + offset;
        if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, recoveryProbeDistance, NavMesh.AllAreas))
        {
            return false;
        }

        agent.Warp(hit.position);
        hasDestination = false;
        ResetProgressSnapshot();
        RefreshPath();
        return agent.hasPath;
    }

    private void ResetProgressSnapshot()
    {
        noProgressTimer = 0f;
        lastRemainingDistance = agent != null ? agent.remainingDistance : float.PositiveInfinity;
    }
}

public class TaskSmartPatrol : Node
{
    private readonly Transform owner;
    private readonly List<Transform> waypoints;
    private readonly NavMeshAgent agent;
    private readonly Animator animator;
    private readonly float waypointSampleRadius;
    private readonly float recoveryProbeDistance;
    private readonly float noProgressRecoverDelay;
    private readonly int isMovingHash;

    private int currentWaypointIndex;
    private float waitTimer;
    private float noProgressTimer;
    private float lastRemainingDistance = float.PositiveInfinity;
    private bool isWaiting;
    private bool destinationSet;
    private Transform forcedTarget;

    public TaskSmartPatrol(
        Transform owner,
        List<Transform> waypoints,
        NavMeshAgent agent,
        Animator animator,
        float waypointSampleRadius,
        float recoveryProbeDistance,
        float noProgressRecoverDelay)
    {
        this.owner = owner;
        this.waypoints = waypoints;
        this.agent = agent;
        this.animator = animator;
        this.waypointSampleRadius = Mathf.Max(0.4f, waypointSampleRadius);
        this.recoveryProbeDistance = Mathf.Max(0.3f, recoveryProbeDistance);
        this.noProgressRecoverDelay = Mathf.Max(0.25f, noProgressRecoverDelay);
        isMovingHash = Animator.StringToHash("IsMoving");
    }

    public void SetNextPatrolPoint(Transform point)
    {
        forcedTarget = point;
        isWaiting = false;
        destinationSet = false;
        waitTimer = 0f;
    }

    public override NodeState Evaluate()
    {
        if (waypoints == null || waypoints.Count == 0) return NodeState.Failure;
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return NodeState.Failure;

        if (forcedTarget != null)
        {
            int index = waypoints.IndexOf(forcedTarget);
            if (index >= 0)
            {
                currentWaypointIndex = index;
            }

            forcedTarget = null;
            destinationSet = false;
        }

        if (isWaiting)
        {
            SetMoveAnimation(false);
            agent.isStopped = true;
            waitTimer += Time.deltaTime;

            if (waitTimer >= 1.5f)
            {
                isWaiting = false;
                waitTimer = 0f;
                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Count;
                destinationSet = false;
            }

            return NodeState.Running;
        }

        if (!destinationSet)
        {
            TrySetWaypointDestination();
        }

        MonitorPatrolProgress();

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude <= 0.01f)
            {
                isWaiting = true;
                destinationSet = false;
                waitTimer = 0f;
                SetMoveAnimation(false);
            }
        }

        return NodeState.Running;
    }

    private void TrySetWaypointDestination()
    {
        Transform waypoint = waypoints[currentWaypointIndex];
        if (waypoint == null)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Count;
            return;
        }

        if (!NavMesh.SamplePosition(waypoint.position, out NavMeshHit hit, waypointSampleRadius, NavMesh.AllAreas))
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Count;
            return;
        }

        NavMeshPath path = new NavMeshPath();
        if (!agent.CalculatePath(hit.position, path) || path.status == NavMeshPathStatus.PathInvalid)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Count;
            return;
        }

        agent.isStopped = false;
        agent.SetPath(path);
        destinationSet = true;
        lastRemainingDistance = agent.remainingDistance;
        noProgressTimer = 0f;
        SetMoveAnimation(true);
    }

    private void MonitorPatrolProgress()
    {
        if (!destinationSet || !agent.hasPath || agent.pathPending)
        {
            noProgressTimer = 0f;
            return;
        }

        bool barelyMoving = agent.velocity.sqrMagnitude <= 0.03f;
        bool tryingToMove = agent.desiredVelocity.sqrMagnitude > 0.04f;
        bool progressed = agent.remainingDistance < lastRemainingDistance - 0.08f;

        if (progressed)
        {
            noProgressTimer = 0f;
            lastRemainingDistance = agent.remainingDistance;
            return;
        }

        if (tryingToMove && barelyMoving)
        {
            noProgressTimer += Time.deltaTime;
            if (noProgressTimer >= noProgressRecoverDelay)
            {
                noProgressTimer = 0f;
                TryPatrolRecovery();
            }
        }
        else
        {
            noProgressTimer = 0f;
        }

        lastRemainingDistance = Mathf.Min(lastRemainingDistance, agent.remainingDistance);
    }

    private void TryPatrolRecovery()
    {
        Vector3[] probes =
        {
            owner.position + owner.right * recoveryProbeDistance,
            owner.position - owner.right * recoveryProbeDistance,
            owner.position - owner.forward * recoveryProbeDistance * 0.75f
        };

        foreach (Vector3 probe in probes)
        {
            if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, recoveryProbeDistance, NavMesh.AllAreas))
            {
                continue;
            }

            agent.Warp(hit.position);
            destinationSet = false;
            TrySetWaypointDestination();
            if (destinationSet)
            {
                return;
            }
        }

        currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Count;
        destinationSet = false;
        TrySetWaypointDestination();
    }

    private void SetMoveAnimation(bool moving)
    {
        if (animator != null)
        {
            animator.SetBool(isMovingHash, moving);
        }
    }
}
