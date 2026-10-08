using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using UnityEngine;

public class PushBlockEnvController : MonoBehaviour
{
    public enum BlockType { Small = 1, Large = 2, VeryLarge = 3 }

    [System.Serializable]
    public class PlayerInfo
    {
        public PushAgentCollab Agent;
        [HideInInspector] public Vector3 StartingPos;
        [HideInInspector] public Quaternion StartingRot;
        [HideInInspector] public Rigidbody Rb;
    }

    [System.Serializable]
    public class BlockInfo
    {
        public Transform T;
        public BlockType Type;
        [HideInInspector] public Vector3 StartingPos;
        [HideInInspector] public Quaternion StartingRot;
        [HideInInspector] public Rigidbody Rb;

        public Dictionary<PushAgentCollab, int> recentContributors = new Dictionary<PushAgentCollab, int>();
    }

    [Header("Max Environment Steps")]
    public int MaxEnvironmentSteps = 25000;

    [HideInInspector] public Bounds areaBounds;
    public GameObject ground;
    public GameObject area;

    private Material m_GroundMaterial;
    private Renderer m_GroundRenderer;
    private PushBlockSettings m_PushBlockSettings;

    public List<PlayerInfo> AgentsList = new List<PlayerInfo>();
    public List<BlockInfo> BlocksList = new List<BlockInfo>();

    public bool UseRandomAgentRotation = true;
    public bool UseRandomAgentPosition = true;
    public bool UseRandomBlockRotation = true;
    public bool UseRandomBlockPosition = true;

    private int m_NumberOfRemainingBlocks;
    private SimpleMultiAgentGroup m_AgentGroup;
    private int m_ResetTimer;

    private int currentStepCount = 0;

    [Header("Contribution Parameters")]
    public int contributionStepWindow = 20; // 此处修改检测窗口期，当前为20帧
    private float contributionAngleThreshold = 30f; // 贡献角度阈值。越小越严格

    [Header("Initialization Ignore Settings")]
    public int contributionIgnoreFrames = 22; // 初始化跳过的帧数，避免误测

    void Start()
    {
        foreach (var item in BlocksList)
        {
            string name = item.T.name.ToLower();
            if (name.Contains("small")) item.Type = BlockType.Small;
            else if (name.Contains("large") && !name.Contains("very")) item.Type = BlockType.Large;
            else if (name.Contains("verylarge")) item.Type = BlockType.VeryLarge;
        }

        areaBounds = ground.GetComponent<Collider>().bounds;
        m_GroundRenderer = ground.GetComponent<Renderer>();
        m_GroundMaterial = m_GroundRenderer.material;
        m_PushBlockSettings = FindObjectOfType<PushBlockSettings>();

        foreach (var item in BlocksList)
        {
            item.StartingPos = item.T.transform.position;
            item.StartingRot = item.T.transform.rotation;
            item.Rb = item.T.GetComponent<Rigidbody>();
        }

        m_AgentGroup = new SimpleMultiAgentGroup();
        foreach (var item in AgentsList)
        {
            item.StartingPos = item.Agent.transform.position;
            item.StartingRot = item.Agent.transform.rotation;
            item.Rb = item.Agent.GetComponent<Rigidbody>();
            m_AgentGroup.RegisterAgent(item.Agent);
        }

        ResetScene();
    }

    void FixedUpdate()
    {
        m_ResetTimer++;

        if (currentStepCount >= contributionIgnoreFrames)
        {
            foreach (var block in BlocksList)
            {
                foreach (var agent in AgentsList)
                {
                    float speed = agent.Rb.velocity.magnitude;
                    if (speed > 0.15f)
                    {
                        float contactRadius = 3.8f;
                        Collider[] colliders = Physics.OverlapSphere(agent.Agent.transform.position, contactRadius);
                        bool isTouchingBlock = colliders.Any(c => c.attachedRigidbody == block.Rb);

                        if (isTouchingBlock)
                        {
                            Vector3 toBlock = (block.T.position - agent.Agent.transform.position).normalized;
                            float angle = Vector3.Angle(agent.Rb.velocity, toBlock);

                            if (angle < contributionAngleThreshold)
                            {
                                block.recentContributors[agent.Agent] = currentStepCount;
                            }
                        }
                    }
                }
            }
        }

        m_AgentGroup.AddGroupReward(-0.5f / MaxEnvironmentSteps);

        if (m_ResetTimer >= MaxEnvironmentSteps && MaxEnvironmentSteps > 0)
        {
            m_AgentGroup.GroupEpisodeInterrupted();
            ResetScene();
        }

        // 最后再增加 step 避免早期带来的误判
        currentStepCount++;
    }

    public Vector3 GetRandomSpawnPos()
    {
        var foundNewSpawnLocation = false;
        var randomSpawnPos = Vector3.zero;

        while (!foundNewSpawnLocation)
        {
            var randomPosX = Random.Range(-areaBounds.extents.x * m_PushBlockSettings.spawnAreaMarginMultiplier,
                                           areaBounds.extents.x * m_PushBlockSettings.spawnAreaMarginMultiplier);

            var randomPosZ = Random.Range(-areaBounds.extents.z * m_PushBlockSettings.spawnAreaMarginMultiplier,
                                           areaBounds.extents.z * m_PushBlockSettings.spawnAreaMarginMultiplier);

            randomSpawnPos = ground.transform.position + new Vector3(randomPosX, 1f, randomPosZ);

            if (!Physics.CheckBox(randomSpawnPos, new Vector3(1.5f, 0.01f, 1.5f)))
            {
                foundNewSpawnLocation = true;
            }
        }

        return randomSpawnPos;
    }

    void ResetBlock(BlockInfo block)
    {
        block.T.position = GetRandomSpawnPos();
        block.Rb.velocity = Vector3.zero;
        block.Rb.angularVelocity = Vector3.zero;
        block.recentContributors.Clear();
    }

    IEnumerator GoalScoredSwapGroundMaterial(Material mat, float time)
    {
        m_GroundRenderer.material = mat;
        yield return new WaitForSeconds(time);
        m_GroundRenderer.material = m_GroundMaterial;
    }

    public void ScoredAGoal(Collider col, float score)
    {
        bool blockMatched = false;
        m_NumberOfRemainingBlocks--;
        bool done = m_NumberOfRemainingBlocks == 0;
        col.gameObject.SetActive(false);

        foreach (var block in BlocksList)
        {
            if (block.T.GetComponent<Collider>() == col)
            {
                int usedCount = 0;
                List<PushAgentCollab> contributorsInWindow = new List<PushAgentCollab>();

                foreach (var kvp in block.recentContributors)
                {
                    int stepDiff = currentStepCount - kvp.Value;
                    if (stepDiff <= contributionStepWindow)
                    {
                        usedCount++;
                        contributorsInWindow.Add(kvp.Key);
                    }
                }

                int requiredCount = (int)block.Type;

                bool usedExtendedWindow = false;

                // 如果是大块或者超大块，则考虑更长窗口期下的贡献情况
                if ((block.Type == BlockType.VeryLarge && usedCount <= 2 && block.recentContributors.Count >= 3) ||
                    (block.Type == BlockType.Large && usedCount <= 1 && block.recentContributors.Count >= 2))
                {
                    usedExtendedWindow = true;

                    foreach (var kvp in block.recentContributors)
                    {
                        if (!contributorsInWindow.Contains(kvp.Key))
                        {
                            int stepDiff = currentStepCount - kvp.Value;
                            if (stepDiff <= 100)
                            {
                                usedCount++;
                                contributorsInWindow.Add(kvp.Key);
                            }
                        }
                    }
                }
                
                // 修正奖励值，使其始终为正数
                float correction = 1f - 0.5f * Mathf.Abs(requiredCount - usedCount);
                // 最终奖励值为分数加入修正值，并映射到0-12之间
                float finalReward = (score + correction) / 2f;
                m_AgentGroup.AddGroupReward(finalReward);

                blockMatched = true;

                int stepWindowUsedForLog = usedExtendedWindow ? 100 : contributionStepWindow;

                string contributorsLog = string.Join(", ", block.recentContributors.Select(kvp =>
                {
                    int stepDiff = currentStepCount - kvp.Value;
                    string status = contributorsInWindow.Contains(kvp.Key) ? "✔" : "✖";
                    return $"{kvp.Key.name}:{stepDiff}({status})";
                }));

                Debug.Log($"[SCORE LOG] Block: {block.T.name}, Type: {block.Type}, Required: {requiredCount}, " +
                          $"Used: {usedCount}, Correction: {correction:0.00}, StepWindow: {stepWindowUsedForLog}, " +
                          $"Contributors: [{contributorsLog}]");
                break;
            }
        }

        if (!blockMatched)
        {
            Debug.Log("[SCORE LOG] No matching block found for goal collider. Possibly no block was pushed into goal.");
        }

        StartCoroutine(GoalScoredSwapGroundMaterial(m_PushBlockSettings.goalScoredMaterial, 0.5f));

        if (done)
        {
            m_AgentGroup.EndGroupEpisode();
            ResetScene();
        }
    }

    Quaternion GetRandomRot()
    {
        return Quaternion.Euler(0, Random.Range(0f, 360f), 0);
    }

    public void ResetScene()
    {
        m_ResetTimer = 0;
        currentStepCount = 0;

        var rotation = Random.Range(0, 4);
        area.transform.Rotate(new Vector3(0f, rotation * 90f, 0f));

        foreach (var item in AgentsList)
        {
            var pos = UseRandomAgentPosition ? GetRandomSpawnPos() : item.StartingPos;
            var rot = UseRandomAgentRotation ? GetRandomRot() : item.StartingRot;

            item.Agent.transform.SetPositionAndRotation(pos, rot);
            item.Rb.velocity = Vector3.zero;
            item.Rb.angularVelocity = Vector3.zero;
        }

        foreach (var item in BlocksList)
        {
            var pos = UseRandomBlockPosition ? GetRandomSpawnPos() : item.StartingPos;
            var rot = UseRandomBlockRotation ? GetRandomRot() : item.StartingRot;

            item.T.transform.SetPositionAndRotation(pos, rot);
            item.Rb.velocity = Vector3.zero;
            item.Rb.angularVelocity = Vector3.zero;
            item.T.gameObject.SetActive(true);
            item.recentContributors.Clear();
        }

        m_NumberOfRemainingBlocks = BlocksList.Count;
    }
}