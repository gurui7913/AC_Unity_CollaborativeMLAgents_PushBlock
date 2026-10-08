using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using System.Collections;
using System.Collections.Generic;

public class PushAgentCollab : Agent
{
    private Rigidbody m_AgentRb;
    private PushBlockEnvController m_EnvController;
    private PushBlockSettings m_PushBlockSettings;

    // 闲置检测
    private float idleTime = 0f;
    private float idleThreshold = 2.0f;
    //private float extraIdlePenaltyPerStep = -0.05f;
    private float minEffectiveSpeed = 0.1f;

    // 调试帮助
    public bool enableDebugLogs = true;
    private float lastPushTime = 0f;
    private float debugLogInterval = 1.0f;

    // 用于连续接触的贡献计算
    private float contactCooldown = 0.1f;  // 减少连续接触的冷却时间，更频繁记录贡献
    private Dictionary<int, float> lastContactTime = new Dictionary<int, float>();

    public override void Initialize()
    {
        m_AgentRb = GetComponent<Rigidbody>();
        m_PushBlockSettings = FindObjectOfType<PushBlockSettings>();
        // 获取场景里的 EnvController 引用
        m_EnvController = FindObjectOfType<PushBlockEnvController>();
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        // 代理移动
        MoveAgent(actionBuffers.DiscreteActions);

        // 闲置惩罚示例
        float velocityMagnitude = m_AgentRb.velocity.magnitude;
        if (velocityMagnitude < minEffectiveSpeed)
        {
            idleTime += Time.fixedDeltaTime;
            if (idleTime >= idleThreshold)
            {
                //AddReward(extraIdlePenaltyPerStep);
            }
        }
        else
        {
            idleTime = 0f;
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = 0;
        if (Input.GetKey(KeyCode.W)) discreteActions[0] = 1;
        else if (Input.GetKey(KeyCode.S)) discreteActions[0] = 2;
        else if (Input.GetKey(KeyCode.D)) discreteActions[0] = 3;
        else if (Input.GetKey(KeyCode.A)) discreteActions[0] = 4;
    }

    private void MoveAgent(ActionSegment<int> act)
    {
        Vector3 dirToGo = Vector3.zero;
        Vector3 rotateDir = Vector3.zero;
        int action = act[0];

        switch (action)
        {
            case 1: dirToGo = transform.forward; break;
            case 2: dirToGo = -transform.forward; break;
            case 3: rotateDir = transform.up; break;
            case 4: rotateDir = -transform.up; break;
        }
        transform.Rotate(rotateDir, Time.fixedDeltaTime * 200f);
        m_AgentRb.AddForce(dirToGo * m_PushBlockSettings.pushForce, ForceMode.VelocityChange); 
    }

    // 改进碰撞检测 - 当发生碰撞时
    private void OnCollisionEnter(Collision collision)
    {
        RecordCollision(collision, 1.2f);  // 增加初始碰撞权重
    }

    // 添加持续碰撞检测 - 当持续碰撞时
    private void OnCollisionStay(Collision collision)
    {
        RecordCollision(collision, 0.4f);  // 增加持续碰撞权重
    }

    // 统一处理碰撞逻辑
    private void RecordCollision(Collision collision, float weightMultiplier)
    {
        // 检查是否与方块碰撞
        var blockId = collision.gameObject.GetComponent<BlockTypeIdentifier>();
        if (blockId != null)
        {
            int blockInstanceId = collision.gameObject.GetInstanceID();
            
            // 检查冷却时间 (减少冷却时间以更频繁记录贡献)
            float currentTime = Time.time;
            if (!lastContactTime.ContainsKey(blockInstanceId) || 
                currentTime - lastContactTime[blockInstanceId] >= contactCooldown)
            {
                // 计算碰撞力 - 基于相对速度和角度
                float relativeSpeed = collision.relativeVelocity.magnitude;
                float baseImpactForce = Mathf.Max(0.5f, relativeSpeed * 0.6f);
                float impactForce = baseImpactForce * weightMultiplier;
                
                // 更高的最小力值，确保即使慢速碰撞也会被记录
                if (impactForce < 0.3f)
                    impactForce = 0.3f;
                
                // 获取方块的贡献追踪器
                var contributionTracker = collision.gameObject.GetComponent<BlockContributionTracker>();
                if (contributionTracker != null)
                {
                    // 添加此智能体的贡献
                    contributionTracker.AddAgentContribution(GetInstanceID(), impactForce);
                    
                    // 更新最后接触时间
                    lastContactTime[blockInstanceId] = currentTime;
                    
                    // 调试日志
                    if (enableDebugLogs && (currentTime - lastPushTime > debugLogInterval || impactForce > 1.0f))
                    {
                        Debug.Log($"智能体 {gameObject.name} (ID:{GetInstanceID()}) 推动方块 {blockId.blockType}，力: {impactForce:F2}");
                        lastPushTime = currentTime;
                    }
                }
            }
        }
        // 检查是否与其他智能体碰撞
        else if (collision.gameObject.GetComponent<PushAgentCollab>() != null)
        {
            // 如果与另一个智能体碰撞，传递力量到附近的方块
            PropagateForceToNearbyBlocks(transform.position, m_PushBlockSettings.pushForce * 0.6f);
        }
    }

    // 改进力的传递到附近方块
    private void PropagateForceToNearbyBlocks(Vector3 position, float forceMagnitude)
    {
        // 检测附近的方块 (增加检测半径)
        Collider[] hitColliders = Physics.OverlapSphere(position, 2.5f);
        foreach (var hitCollider in hitColliders)
        {
            var blockId = hitCollider.GetComponent<BlockTypeIdentifier>();
            if (blockId != null)
            {
                // 计算距离因子
                float distance = Vector3.Distance(position, hitCollider.transform.position);
                float distanceFactor = Mathf.Max(0, 1 - (distance / 2.5f));
                
                // 基于距离计算传递的力
                float transferredForce = forceMagnitude * distanceFactor * 0.8f;
                
                // 添加到方块的贡献追踪器 (降低最小力阈值)
                var contributionTracker = hitCollider.GetComponent<BlockContributionTracker>();
                if (contributionTracker != null && transferredForce > 0.01f)
                {
                    contributionTracker.AddAgentContribution(GetInstanceID(), transferredForce);
                    
                    if (enableDebugLogs && transferredForce > 0.2f)
                    {
                        Debug.Log($"智能体 {gameObject.name} 间接影响方块 {blockId.blockType}，距离: {distance:F2}，传递力: {transferredForce:F2}");
                    }
                }
            }
        }
    }

    // 当碰到目标时的触发事件处理
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("goal"))
        {
            var blockId = GetComponent<BlockTypeIdentifier>();
            if (blockId != null && m_EnvController != null)
            {
                m_EnvController.OnBlockGoalScored(blockId.blockType);
            }
        }
    }

    // 重写AgentReset，确保重置所有状态
    public override void OnEpisodeBegin()
    {
        base.OnEpisodeBegin();
        // 重置接触时间和闲置时间
        lastContactTime.Clear();
        idleTime = 0f;
        lastPushTime = 0f;
    }
}