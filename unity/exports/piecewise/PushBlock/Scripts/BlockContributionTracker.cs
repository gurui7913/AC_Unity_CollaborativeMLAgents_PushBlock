using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class BlockContributionTracker : MonoBehaviour
{
    // 记录每个智能体的贡献值
    private Dictionary<int, float> agentContributions = new Dictionary<int, float>();
    // 记录智能体最近一次贡献的时间
    private Dictionary<int, float> lastContributionTime = new Dictionary<int, float>();
    // 当前活跃的智能体集合
    public HashSet<int> contributingAgents = new HashSet<int>();

    [Header("Contribution Settings")]
    // 当贡献值低于此阈值时，认为智能体不再对方块产生有效贡献
    public float directContactThreshold = 0.0005f; // 降低阈值，更容易记录贡献
    // 每帧对贡献值乘以的衰减系数（越小衰减越快）
    public float indirectContactDecay = 0.98f; // 减缓衰减率，使贡献更持久
    // 最近一次贡献时间若在该秒数内，则判定为"活跃智能体"
    public float activeTimeWindow = 5.0f; // 增加活跃时间窗口

    [Header("Debug Settings")]
    public bool enableDebugLogs = true;
    private bool goalApproaching = false;
    private float lastLogTime = 0f;
    private float logInterval = 1.0f;

    // 添加智能体贡献
    public void AddAgentContribution(int agentId, float contributionValue)
    {
        if (!agentContributions.ContainsKey(agentId))
        {
            agentContributions[agentId] = 0f;
            lastContributionTime[agentId] = Time.time;
        }

        // 累加贡献
        agentContributions[agentId] += contributionValue;
        // 更新最近贡献时间
        lastContributionTime[agentId] = Time.time;
        // 加入活跃智能体集合
        contributingAgents.Add(agentId);

        // 如果方块速度较快且接近目标，开始记录日志
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && rb.velocity.magnitude > 0.2f) // 降低速度阈值
        {
            goalApproaching = true;
        }
    }
    
    // 获取当前协作的智能体数量
    public int GetActiveAgentCount()
    {
        float currentTime = Time.time;
        int count = 0;
        List<int> activeAgentIds = new List<int>();

        foreach (var agentId in contributingAgents)
        {
            // 检查是否在活跃时间窗口内有贡献
            if (currentTime - lastContributionTime[agentId] < activeTimeWindow)
            {
                count++;
                activeAgentIds.Add(agentId);
            }
        }

        if (enableDebugLogs && goalApproaching)
        {
            //Debug.Log($"方块 {gameObject.name} 活跃智能体: {count}，ID: {string.Join(",", activeAgentIds)}");
        }

        return count;
    }

    // 获取智能体的有效总力
    public float GetTotalEffectiveForce()
    {
        float totalForce = 0f;
        float currentTime = Time.time;

        foreach (var entry in agentContributions)
        {
            // 只计算活跃时间窗口内的贡献
            if (currentTime - lastContributionTime[entry.Key] < activeTimeWindow)
            {
                totalForce += entry.Value;
            }
        }

        return totalForce;
    }

    // 获取所有智能体的贡献详情（用于调试）
    public Dictionary<int, float> GetAllContributions()
    {
        return new Dictionary<int, float>(agentContributions);
    }

    // 每帧衰减贡献值，若贡献值低于阈值则移除
    void FixedUpdate()
    {
        // 记录方块状态
        if (enableDebugLogs && goalApproaching && Time.time - lastLogTime > logInterval)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                string contributionsLog = "";
                foreach (var entry in agentContributions)
                {
                    contributionsLog += $"Agent {entry.Key}: {entry.Value:F2} ({Time.time - lastContributionTime[entry.Key]:F1}s ago), ";
                }
                
                //Debug.Log($"方块 {gameObject.name} 速度: {rb.velocity.magnitude:F2}, 贡献: {contributionsLog}");
                lastLogTime = Time.time;
            }
        }

        // 衰减贡献值
        foreach (var agentId in agentContributions.Keys.ToList())
        {
            agentContributions[agentId] *= indirectContactDecay;
            
            // 只移除贡献字典中的项，但保留在活跃集合中
            if (agentContributions[agentId] < directContactThreshold)
            {
                agentContributions.Remove(agentId);
            }
        }
    }

    // 重置方块接近目标状态
    public void SetApproachingGoal(bool approaching)
    {
        goalApproaching = approaching;
    }
    
    // 重置所有贡献值
    public void ResetContributions()
    {
        agentContributions.Clear();
        contributingAgents.Clear();
        lastContributionTime.Clear();
        goalApproaching = false;
    }

    // 用于方块接近目标时记录当前状态
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("goal"))
        {
            SetApproachingGoal(true);
            if (enableDebugLogs)
            {
                //Debug.Log($"方块 {gameObject.name} 接近目标! 活跃智能体: {GetActiveAgentCount()}, 总力: {GetTotalEffectiveForce():F2}");
                
                // 打印所有智能体的贡献
                string agentLog = "";
                foreach (var agentId in contributingAgents)
                {
                    float timeSinceLastContribution = Time.time - lastContributionTime[agentId];
                    if (timeSinceLastContribution < activeTimeWindow) 
                    {
                        float contributionValue = agentContributions.ContainsKey(agentId) ? 
                            agentContributions[agentId] : 0f;
                        agentLog += $"Agent {agentId}: {contributionValue:F2} (贡献时间: {timeSinceLastContribution:F1}s前), ";
                    }
                }
                //Debug.Log($"智能体贡献详情: {agentLog}");
            }
        }
    }
}