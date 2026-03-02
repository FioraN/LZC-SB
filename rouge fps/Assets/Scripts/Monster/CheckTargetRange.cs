using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 条件节点：检测是否有目标在范围内
public class CheckTargetRange : Node
{
    private readonly Transform _transform;
    private readonly Transform _target;
    private readonly float _sqrRange; // 存储距离的平方，用于比较

    public CheckTargetRange(Transform transform, Transform target, float range)
    {
        _transform = transform;
        _target = target;
        _sqrRange = range * range; // 预计算平方值
    }

    public override NodeState Evaluate()
    {
        if (_target == null) return NodeState.Failure;

        float sqrDistance = (_transform.position - _target.position).sqrMagnitude;
        return sqrDistance <= _sqrRange ? NodeState.Success : NodeState.Failure;
    }
}


// 条件节点：检测目标是否在扇形范围内（视野检测）
public class CheckTargetSector : Node
{
    private readonly Transform _transform;
    private readonly Transform _target;
    private readonly float _sqrRange; // 距离平方
    private readonly float _halfAngle; // 半角（比如视野90度，这里存45度）

    public CheckTargetSector(Transform transform, Transform target, float viewRange, float viewAngle)
    {
        _transform = transform;
        _target = target;
        _sqrRange = viewRange * viewRange;
        _halfAngle = viewAngle * 0.5f;
    }

    public override NodeState Evaluate()
    {
        if (_target == null) return NodeState.Failure;

        Vector3 dirToTarget = _target.position - _transform.position;

        // 1. 先检查距离 (SqrMagnitude 比 Distance 快)
        if (dirToTarget.sqrMagnitude > _sqrRange)
            return NodeState.Failure;

        // 2. 再检查角度
        // Vector3.Angle 返回两个向量的夹角(0~180)
        float angle = Vector3.Angle(_transform.forward, dirToTarget);

        if (angle <= _halfAngle)
        {
            return NodeState.Success;
        }

        return NodeState.Failure;
    }
}


// 动作节点：使用 NavMesh 移动
public class TaskNavMove : Node
{
    private readonly NavMeshAgent _agent;
    private readonly Transform _target;
    private readonly Animator _ani;
    private readonly int _isMovingHash;

    public TaskNavMove(NavMeshAgent agent, Transform target, Animator ani)
    {
        _agent = agent;
        _target = target;
        _ani = ani;
        // 缓存动画参数ID，提升性能
        _isMovingHash = Animator.StringToHash("IsMoving");
    }

    public override NodeState Evaluate()
    {
        if (_target == null) return NodeState.Failure;

        // 检查 NavMeshAgent 是否有效
        if (_agent == null || !_agent.isActiveAndEnabled || !_agent.isOnNavMesh)
        {
            return NodeState.Failure;
        }

        // 确保 NavMeshAgent 没有被手动停止
        if (_agent.isStopped)
        {
            _agent.isStopped = false;
        }

        // 持续更新目的地（追踪目标）
        _agent.SetDestination(_target.position);

        Debug.Log(_target.position);


        // 播放移动动画
        if (_ani != null) _ani.SetBool(_isMovingHash, true);

        // 始终返回 Running，保持持续追踪
        return NodeState.Running;
    }
}

// 动作节点：边移动边攻击（攻击范围内的行为）
public class TaskAttackWithMove : Node
{
    private readonly MonsterBase _monster;
    private readonly Animator _ani;
    private readonly NavMeshAgent _agent;
    private readonly Transform _target;

    private float _lastAttackTime;
    // 如果你的攻击动画很长，建议增加这个值，或者从外部传入
    private readonly float _attackAnimationDuration = 1.2f;

    // 缓存动画参数ID
    private readonly int _isMovingHash;

    public TaskAttackWithMove(MonsterBase monster, Animator ani, NavMeshAgent agent, Transform target)
    {
        _monster = monster;
        _ani = ani;
        _agent = agent;
        _target = target;
        _lastAttackTime = -9999f; // 初始化为一个很久以前的时间
        _isMovingHash = Animator.StringToHash("IsMoving");
    }

    public override NodeState Evaluate()
    {
        if (_target == null) return NodeState.Failure;
        if (_agent == null || !_agent.isActiveAndEnabled || !_agent.isOnNavMesh) return NodeState.Failure;

        // 计算当前是否处于攻击后的僵直期间
        bool inAttackAnimation = (Time.time - _lastAttackTime) < _attackAnimationDuration;

        if (inAttackAnimation)
        {
            // 处于攻击硬直中：强制停止移动
            SetStoppedState(true);
            return NodeState.Running;
        }

        // 尝试执行攻击逻辑（内部检查距离、冷却等条件）
        bool justAttacked = _monster.TryAttack();

        if (justAttacked)
        {
            _lastAttackTime = Time.time;
            // 攻击触发瞬间：立即停止
            SetStoppedState(true);
            // 这里可以加一个朝向修正，确保攻击时正对目标
            _monster.transform.LookAt(new Vector3(_target.position.x, _monster.transform.position.y, _target.position.z));

          


        }
        else
        {
            // 没攻击且不在硬直中 -> 恢复移动追踪
            SetStoppedState(false);
            _agent.SetDestination(_target.position);
        }

        return NodeState.Running;
    }

    private void SetStoppedState(bool isStopped)
    {
        if (_agent.isStopped != isStopped)
        {
            _agent.isStopped = isStopped;
            if (isStopped)
            {
                _agent.velocity = Vector3.zero; // 物理上清零速度，防止滑步
                _agent.ResetPath(); // 清除路径，确保彻底停下
            }
        }

        if (_ani != null)
        {
            _ani.SetBool(_isMovingHash, !isStopped);
        }
    }
}

// 动作节点：待机
public class TaskIdle : Node
{
    private readonly Animator _ani;
    private readonly int _isMovingHash;

    public TaskIdle(Animator ani)
    {
        _ani = ani;
        _isMovingHash = Animator.StringToHash("IsMoving");
    }

    public override NodeState Evaluate()
    {
        if (_ani != null) _ani.SetBool(_isMovingHash, false);
        return NodeState.Success;
    }
}

// 行为节点：巡逻
public class TaskPatrol : Node
{
    private Transform _transform;
    private List<Transform> _waypoints;
    private NavMeshAgent _agent;
    private Animator _ani;
    private int _currentWaypointIndex = 0;
    private float _waitTimer = 0f;
    private bool _isWaiting = false;

    // 添加一个标记，确保只在切换点时设置一次目的地
    private bool _destinationSet = false;

    private Transform _forcedTarget;

    public TaskPatrol(Transform transform, List<Transform> waypoints, NavMeshAgent agent, Animator ani)
    {
        _transform = transform;
        _waypoints = waypoints;
        _agent = agent;
        _ani = ani;
    }

    public void SetNextPatrolPoint(Transform point)
    {
        _forcedTarget = point;
        _isWaiting = false;
        _destinationSet = false; // 强制更新目标
    }

    public override NodeState Evaluate()
    {
        if (_waypoints == null || _waypoints.Count == 0) return NodeState.Failure;

        // 1. 处理强制目标点（脱战返回逻辑）
        if (_forcedTarget != null)
        {
            int index = _waypoints.IndexOf(_forcedTarget);
            if (index != -1) _currentWaypointIndex = index;

            _forcedTarget = null;
            _isWaiting = false;
            _waitTimer = 0f;
            _destinationSet = false; // 触发重新寻路
        }

        // 2. 等待逻辑
        if (_isWaiting)
        {
            if (_ani) _ani.SetBool("IsMoving", false);
            if (_agent.isActiveAndEnabled) _agent.isStopped = true; // 确保停止

            _waitTimer += Time.deltaTime;
            if (_waitTimer > 1.5f)
            {
                // 等待结束，切换到下一个点
                _isWaiting = false;
                _waitTimer = 0f;
                _currentWaypointIndex = (_currentWaypointIndex + 1) % _waypoints.Count;
                _destinationSet = false; // 标记需要设置新目的地
            }
            return NodeState.Running;
        }

        // 3. 设置移动目标 (仅在需要时调用一次)
        if (!_destinationSet)
        {
            Transform wp = _waypoints[_currentWaypointIndex];
            if (wp != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh)
            {
                _agent.SetDestination(wp.position);
                _agent.isStopped = false;
                if (_ani) _ani.SetBool("IsMoving", true);
                _destinationSet = true; // 标记已设置，避免Update中重复调用
            }
        }

        // 4. 检测是否到达
        // 增加 pathPending 检查，防止刚 SetDestination 还没算好路径就误判距离为 0
        if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
        {
            if (!_agent.hasPath || _agent.velocity.sqrMagnitude == 0f)
            {
                _isWaiting = true;
                _waitTimer = 0f;
                _destinationSet = false; // 准备下一次

                if (_ani) _ani.SetBool("IsMoving", false);
            }
        }

        return NodeState.Running;
    }
}
// 行为节点：受伤反应
public class TaskHurt : Node
{
    private MonsterBase _monster;
    private Animator _ani;
    private float _duration = 0.5f; // 受伤硬直时间
    private float _timer = 0f;
    private bool _started = false;

    public TaskHurt(MonsterBase monster, Animator ani)
    {
        _monster = monster;
        _ani = ani;
    }

    public override NodeState Evaluate()
    {
        // 如果没有处于受伤状态，返回失败，让后续节点运行
        if (!_monster.isHurt)
        {
            _started = false;
            return NodeState.Failure;
        }

        if (!_started)
        {
            _started = true;
            _timer = 0f;
            if (_ani) _ani.SetTrigger("Hit"); // 触发受伤动画
            // 停止移动
            if (_monster.agent != null && _monster.agent.isOnNavMesh)
                _monster.agent.isStopped = true;
        }

        _timer += Time.deltaTime;
        if (_timer >= _duration)
        {
            _monster.isHurt = false; // 恢复状态
            _monster.hasAggro = true; // 激怒：由于是受击，强制进入追踪模式
            _started = false;
            return NodeState.Success;
        }

        return NodeState.Running;
    }
}

// 行为节点：带时间的待机
public class TaskTimedIdle : Node
{
    private Animator _ani;
    private float _duration;
    private float _timer;

    public TaskTimedIdle(Animator ani, float duration)
    {
        _ani = ani;
        _duration = duration;
        _timer = 0f;
    }

    public override NodeState Evaluate()
    {
        if (_ani) _ani.SetBool("IsMoving", false);

        _timer += Time.deltaTime;
        if (_timer >= _duration)
        {
            _timer = 0f; // 重置计时器以便下次进入
            return NodeState.Success; // 待机结束
        }
        return NodeState.Running; // 正在待机
    }
}

// 检测是否有仇恨（受击后强制追踪）
public class CheckAggro : Node
{
    private MonsterBase _monster;
    public CheckAggro(MonsterBase monster) { _monster = monster; }
    public override NodeState Evaluate()
    {
        return _monster.hasAggro ? NodeState.Success : NodeState.Failure;
    }
}