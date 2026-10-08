using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;

public class PushAgentCollab : Agent
{
    private PushBlockSettings m_PushBlockSettings;
    private Rigidbody m_AgentRb;

    protected override void Awake()
    {
        base.Awake();
        m_PushBlockSettings = FindObjectOfType<PushBlockSettings>();
    }

    public override void Initialize()
    {
        m_AgentRb = GetComponent<Rigidbody>();
    }

    public void MoveAgent(ActionSegment<int> act)
    {
        var dirToGo = Vector3.zero;
        var rotateDir = Vector3.zero;

        switch (act[0])
        {
            case 1: dirToGo = transform.forward; break;
            case 2: dirToGo = -transform.forward; break;
            case 3: rotateDir = transform.up; break;
            case 4: rotateDir = -transform.up; break;
            case 5: dirToGo = -transform.right * 0.75f; break;
            case 6: dirToGo = transform.right * 0.75f; break;
        }

        transform.Rotate(rotateDir, Time.fixedDeltaTime * 200f);
        m_AgentRb.AddForce(dirToGo * m_PushBlockSettings.agentRunSpeed, ForceMode.VelocityChange);
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        MoveAgent(actionBuffers.DiscreteActions);
    }
}
