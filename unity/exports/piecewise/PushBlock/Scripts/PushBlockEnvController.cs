using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgentsExamples;

public class PushBlockEnvController : MonoBehaviour
{
    [Header("Agent List")]
    public List<PushAgentCollab> AgentsList;

    [Header("Block List")]
    public List<Transform> BlocksList;

    [Header("Max Steps")]
    public int MaxEnvironmentSteps = 5000;

    [Header("Debug Settings")]
    public bool enableDebugLogs = true;

    private SimpleMultiAgentGroup m_AgentGroup;
    private Dictionary<int, float> successMemory = new Dictionary<int, float>();

    // 用于记录剩余Block数，全部推进目标后结束回合
    private int m_RemainingBlocks;
    private int m_ResetTimer;

    // 地面与场景相关
    public GameObject ground;
    public GameObject area;
    private Renderer m_GroundRenderer;
    private Material m_GroundMaterial;
    private Bounds areaBounds;
    private PushBlockSettings m_PushBlockSettings;

    void Start()
    {
        // 1. 创建唯一的多智能体组
        m_AgentGroup = new SimpleMultiAgentGroup();

        // 2. 将所有场景Agent注册到同一个组
        foreach (var agent in AgentsList)
        {
            m_AgentGroup.RegisterAgent(agent);
        }

        // 基本初始化
        m_PushBlockSettings = FindObjectOfType<PushBlockSettings>();
        m_GroundRenderer = ground.GetComponent<Renderer>();
        m_GroundMaterial = m_GroundRenderer.material;
        areaBounds = ground.GetComponent<Collider>().bounds;

        ResetScene();
    }

    void FixedUpdate()
    {
        m_ResetTimer++;
        // 如果超时还没完成，就强制结束
        if (m_ResetTimer >= MaxEnvironmentSteps && MaxEnvironmentSteps > 0)
        {
            m_AgentGroup.GroupEpisodeInterrupted();
            ResetScene();
        }

        // 给所有Agent随时间流逝一点负奖励，鼓励快速完成
        m_AgentGroup.AddGroupReward(-0.05f / MaxEnvironmentSteps);
    }

    /// <summary>
    /// 当方块被推入目标区域时，由碰撞检测脚本或 Agent 调用本方法。
    /// 这里实现 Block3 需要3倍推力、Block2 需要2倍推力等判断与差异化奖励。
    /// </summary>
    public void OnBlockGoalScored(BlockTypeIdentifier.BlockType blockType)
    {
        // 找到对应类型的 block 对象
        GameObject targetBlock = null;
        foreach (var b in BlocksList)
        {
            if (b.GetComponent<BlockTypeIdentifier>().blockType == blockType)
            {
                targetBlock = b.gameObject;
                break;
            }
        }
        
        if (targetBlock == null) return;
        
        // 获取方块的贡献追踪器
        var contributionTracker = targetBlock.GetComponent<BlockContributionTracker>();
        if (contributionTracker == null) return;
        
        // 根据不同 Block 类型设定所需智能体数量和奖励
        int requiredAgents = 0;
        float rewardValue = 0f;
        string blockTypeName = "";
        
        switch (blockType)
        {
            case BlockTypeIdentifier.BlockType.Block3:
                requiredAgents = 3;
                rewardValue = 50f;
                blockTypeName = "Block3";
                break;
            case BlockTypeIdentifier.BlockType.Block2:
                requiredAgents = 2;
                rewardValue = 30f;
                blockTypeName = "Block2";
                break;
            default: // Block1
                requiredAgents = 1;
                rewardValue = 20f;
                blockTypeName = "Block1";
                break;
        }
        
        // 获取协作的智能体数量
        int activeAgents = contributionTracker.GetActiveAgentCount();
        
        // 记录所有贡献智能体的详情
        Dictionary<int, float> allContributions = contributionTracker.GetAllContributions();
        
        float totalAgentReward = 0f;
        bool validAgentCount = (activeAgents == requiredAgents);
        bool isCollaborationSuccess = false;
        
        if (activeAgents == 0)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"异常情况: {blockTypeName} 被推入目标，但没有检测到活跃智能体！");
                
                // 获取方块速度
                Rigidbody blockRb = targetBlock.GetComponent<Rigidbody>();
                if (blockRb != null)
                {
                    Debug.LogWarning($"方块 {blockTypeName} 速度: {blockRb.velocity.magnitude}，可能是被惯性或碰撞带入目标");
                }
            }
            
            // 对无智能体推动的情况给予负奖励
            totalAgentReward = -5f;
        }
        else if (validAgentCount)
        {
            // 正确数量的智能体协作，给予满额奖励
            totalAgentReward = rewardValue;
            isCollaborationSuccess = true;
            
            if (enableDebugLogs)
            {
                Debug.Log($"成功协作! {blockTypeName} 被 {activeAgents} 个智能体成功推入目标，奖励: {rewardValue}");
                
                // 打印贡献智能体详情
                string contributionDetails = "";
                foreach (var entry in allContributions)
                {
                    contributionDetails += $"智能体 {entry.Key}: 贡献值 {entry.Value:F2}, ";
                }
                
                Debug.Log($"贡献详情: {contributionDetails}");
            }
        }
        else
        {
            // 智能体数量不匹配，给予部分惩罚
            totalAgentReward = -2f * (requiredAgents - activeAgents);
            
            if (enableDebugLogs)
            {
                Debug.Log($"协作不完全! {blockTypeName} 被 {activeAgents}/{requiredAgents} 个智能体推入目标，惩罚: {totalAgentReward}");
            }
        }
        
        // 应用奖励
        m_AgentGroup.AddGroupReward(totalAgentReward);

        // 记录成功合作的智能体贡献
        if (totalAgentReward > 0)
        {
            // 记录每个参与智能体的成功贡献
            foreach (var agentId in contributionTracker.contributingAgents)
            {
                if (!successMemory.ContainsKey(agentId))
                    successMemory[agentId] = 0;
                successMemory[agentId] += totalAgentReward/activeAgents;
            }
        }
        
        // 重置贡献追踪器
        contributionTracker.ResetContributions();
        
        // 更新剩余 Block 数量
        m_RemainingBlocks--;
        if (m_RemainingBlocks <= 0)
        {
            // 所有方块都已完成，结束回合
            m_AgentGroup.EndGroupEpisode();
            ResetScene();
        }
        
        // 根据协作情况切换地面材质
        if (isCollaborationSuccess)
        {
            // 协作成功，优先使用协作成功材质，若未设置则使用得分材质
            if (m_PushBlockSettings.collaborationScoreMaterial != null)
            {
                StartCoroutine(GoalScoredSwapGroundMaterial(m_PushBlockSettings.collaborationScoreMaterial, 0.5f));
            }
            else
            {
                StartCoroutine(GoalScoredSwapGroundMaterial(m_PushBlockSettings.goalScoredMaterial, 0.5f));
            }
        }
        else
        {
            // 协作失败，优先使用协作失败材质，若未设置则使用失败材质
            if (m_PushBlockSettings.failedCollaborationMaterial != null)
            {
                StartCoroutine(GoalScoredSwapGroundMaterial(m_PushBlockSettings.failedCollaborationMaterial, 0.5f));
            }
            else if (m_PushBlockSettings.failMaterial != null)
            {
                StartCoroutine(GoalScoredSwapGroundMaterial(m_PushBlockSettings.failMaterial, 0.5f));
            }
            else
            {
                // 如果没有设置任何失败材质，则默认使用得分材质
                StartCoroutine(GoalScoredSwapGroundMaterial(m_PushBlockSettings.goalScoredMaterial, 0.5f));
            }
        }
    }

    /// <summary>
    /// 重置场景
    /// </summary>
    public void ResetScene()
    {
        m_ResetTimer = 0;
        // 随机旋转平台
        int rotationIndex = Random.Range(0, 4);
        area.transform.rotation = Quaternion.Euler(0f, 90f * rotationIndex, 0f);

        // 重置Agent位置
        foreach (var agent in AgentsList)
        {
            agent.transform.position = GetRandomSpawnPos();
            agent.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            var rb = agent.GetComponent<Rigidbody>();
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 重置Block位置
        m_RemainingBlocks = BlocksList.Count;
        foreach (var block in BlocksList)
        {
            block.gameObject.SetActive(true);
            block.transform.position = GetRandomSpawnPos();
            block.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);

            var rb = block.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        
            // 重置每个方块的贡献记录
            var contributionTracker = block.GetComponent<BlockContributionTracker>();
            if (contributionTracker != null)
            {
                contributionTracker.ResetContributions();
            }
        }
    }
    
    /// <summary>
    /// 这个方法与 GoalDetectTrigger 的 UnityEvent 形参匹配，
    /// 用于处理方块进入目标时的奖励逻辑。
    /// </summary>
    public void OnBlockGoalTriggerEnter(Collider col, float score)
    {
        // 禁用方块 - 使方块在得分后消失
        col.gameObject.SetActive(false);
        
        // 拿到方块的脚本，比如 BlockTypeIdentifier
        var blockId = col.GetComponent<BlockTypeIdentifier>();
        if (blockId != null)
        {
            // 根据 blockId.blockType 区分 Block1/Block2/Block3
            OnBlockGoalScored(blockId.blockType);
        }
    }

    /// <summary>
    /// 获取地面范围内的随机位置
    /// </summary>
    public Vector3 GetRandomSpawnPos()
    {
        var foundNewSpawnLocation = false;
        var randomSpawnPos = Vector3.zero;
        while (foundNewSpawnLocation == false)
        {
            var randomPosX = Random.Range(-areaBounds.extents.x * m_PushBlockSettings.spawnAreaMarginMultiplier,
                areaBounds.extents.x * m_PushBlockSettings.spawnAreaMarginMultiplier);

            var randomPosZ = Random.Range(-areaBounds.extents.z * m_PushBlockSettings.spawnAreaMarginMultiplier,
                areaBounds.extents.z * m_PushBlockSettings.spawnAreaMarginMultiplier);
            randomSpawnPos = ground.transform.position + new Vector3(randomPosX, 1f, randomPosZ);
            if (Physics.CheckBox(randomSpawnPos, new Vector3(1.5f, 0.01f, 1.5f)) == false)
            {
                foundNewSpawnLocation = true;
            }
        }
        return randomSpawnPos;
    }

    /// <summary>
    /// 地面材质切换协程 - 用于可视化得分
    /// </summary>
    IEnumerator GoalScoredSwapGroundMaterial(Material mat, float time)
    {
        // 保存原始材质
        Material originalMaterial = m_GroundRenderer.material;
        
        // 更换为得分材质
        m_GroundRenderer.material = mat;
        
        // 等待指定的时间
        yield return new WaitForSeconds(time);
        
        // 恢复原始材质
        m_GroundRenderer.material = m_GroundMaterial;
    }
}