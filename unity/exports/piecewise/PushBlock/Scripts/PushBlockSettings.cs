using UnityEngine;

public class PushBlockSettings : MonoBehaviour
{
    /// <summary>
    /// The "walking speed" of the agents in the scene.
    /// </summary>
    public float agentRunSpeed;
    public float pushForce = 0.1f;

    /// <summary>
    /// The agent rotation speed.
    /// Every agent will use this setting.
    /// </summary>
    public float agentRotationSpeed;

    /// <summary>
    /// The spawn area margin multiplier.
    /// ex: .9 means 90% of spawn area will be used.
    /// .1 margin will be left (so players don't spawn off of the edge).
    /// The higher this value, the longer training time required.
    /// </summary>
    public float spawnAreaMarginMultiplier;

    /// 以下材质用于视觉反馈
    /// 优先使用合作相关的材质进行反馈
    /// 当多个智能体成功协作完成任务时显示

    public Material collaborationScoreMaterial;
    
    // 当协作推进失败时显示（合力不足或角色配置错误）
    public Material failedCollaborationMaterial;
    
    ///

    /// <summary>
    /// When a goal is scored the ground will switch to this
    /// material for a few seconds.
    /// </summary>
    public Material goalScoredMaterial;

    /// <summary>
    /// When an agent fails, the ground will turn this material for a few seconds.
    /// </summary>
    public Material failMaterial;

}
