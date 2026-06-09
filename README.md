# Cooperative Agent Simulation via Contribution-Aware Reward Engineering

**Designing Cooperation-Aware Reward Functions for Multi-Agent Reinforcement Learning in a Physics-Based Simulation**

> UCL Bartlett School of Architecture · Architectural Computation – Digital Studio 1: Simulated Realities  
> Team: Du Hao, **Gu Rui**, Lu Haiyu, Pan Lingfeng · March 2025

---

## Table of Contents

- [Research Question](#research-question)
- [Simulation Environment](#simulation-environment)
  - [Platform Selection](#platform-selection)
  - [Environment Architecture](#environment-architecture)
  - [Agent Design](#agent-design)
  - [Block & Task Design](#block--task-design)
- [Workflow](#workflow)
  - [Stage 1 — Environment Construction](#stage-1--environment-construction)
  - [Stage 2 — Reward Function Design (My Core Contribution)](#stage-2--reward-function-design-my-core-contribution)
  - [Stage 3 — Training & Iteration](#stage-3--training--iteration)
  - [Stage 4 — Analysis](#stage-4--analysis)
- [Results](#results)
  - [Quantitative Findings](#quantitative-findings)
  - [Emergent Behaviors](#emergent-behaviors)
- [Project Structure](#project-structure)
- [How to Run](#how-to-run)
- [Future Work](#future-work)
- [References](#references)

---

## Research Question

In multi-agent reinforcement learning (MARL), agents trained with standard reward functions tend to develop **individualistic strategies** — even when the task physically demands cooperation. This raises a central question:

> **Can a collaboration-aware reward function, combined with a real-time agent contribution tracking mechanism, induce emergent cooperative behavior among decentralized agents in a physics-based simulation?**

We operationalize this question through a **Collaborative Push Block** task in Unity ML-Agents, where three autonomous agents must push blocks of varying masses into a goal zone. Heavier blocks physically require multiple agents to move — but the default flat reward provides no signal distinguishing solo from cooperative actions. We hypothesize that a **piecewise reward function that validates agent-count matching per block type** will produce measurably higher collaboration rates and faster convergence compared to the baseline.

---

## Simulation Environment

### Platform Selection

We evaluated three platforms for multi-agent simulation research:

| Platform | Extensible Envs | Real-time Interaction | Multiplayer | Physiological Data | Difficulty |
|----------|:---:|:---:|:---:|:---:|:---:|
| Overcooked | ✗ | ✓ | 1+1 | ✗ | Medium |
| CREW | ✓ | ✓ | No Limit | ✓ | Difficult |
| **ML-Agents** | **✓** | **✓** | **No Limit** | **✓** | **Easy** |

We chose **Unity ML-Agents** for its tight integration with Unity's physics engine (essential for force-based block pushing), native support for **MA-POCA** (Multi-Agent POsthumous Credit Assignment), and extensible C# scripting for custom environment logic.

### Environment Architecture

The simulation is built on a **Centralized Training with Decentralized Execution (CTDE)** paradigm via MA-POCA:

```
┌─────────────────────────┐          ┌─────────────────────────────────┐
│  MA-POCA Training       │          │  Environment in Unity            │
│  (Python API)           │          │  (C# Scripts + Physics Engine)   │
│                         │          │                                  │
│  Policy Network         │◄─Train──►│  Agent1 ─► Observations          │
│   Input: grid obs,      │  /Learn  │            (GridSensor)          │
│   state, reward         │          │          ─► Actions              │
│   Output: action (ONNX) │          │            (discrete 7-dim)      │
│                         │          │          ─► Actor                │
│  Centralized Critic     │          │            (ONNX model)          │
│  Decentralized Actor    │          │                                  │
│  PPO with Clipping      │          │  Agent2, Agent3 ... AgentN       │
│  Attention Mechanism    │          │                                  │
│  Experience Replay      │          │  Env Parameters:                 │
│                         │          │   blocks, goal, walls, physics   │
└─────────────────────────┘          └─────────────────────────────────┘
```

### Agent Design

Each agent is an autonomous entity with **no inter-agent communication channel** — cooperation must emerge purely from shared reward signals and environmental observations.

- **Observation Space:** Grid-based CNN perception. Each agent observes a local 2D grid where every cell is encoded as a one-hot vector over 6 detectable tags (Nothing, Wall, Agent, Goal, BlockSmall, BlockLarge, BlockVeryLarge). The observation tensor has shape `GridSize.x × GridSize.z × NumDetectableTags`, processed by a convolutional encoder before feeding into the policy network.

- **Action Space:** 7 discrete actions — `0: idle`, `1: forward`, `2: backward`, `3: rotate CW`, `4: rotate CCW`, `5: strafe left`, `6: strafe right`. Movement is physics-based (`Rigidbody.AddForce` with `VelocityChange` mode), meaning agents interact with blocks through realistic contact forces.

- **Agent Constraints:** Equal strength, equal max speed, push-only (no lifting/grasping), facing-direction movement. These constraints ensure that moving a heavy block is physically impossible without multiple agents applying force from compatible directions.

### Block & Task Design

| Block Type | Mass (Optimized) | Required Agents | Tag |
|:---:|:---:|:---:|:---:|
| Small (1) | 10 | 1 | `BlockSmall` |
| Large (2) | 40–100 | 2 | `BlockLarge` |
| Very Large (3) | 90–150 | 3 | `BlockVeryLarge` |

**Episode Logic:**
- All agents and blocks spawn at random non-overlapping positions each episode
- Episode terminates when all blocks enter the goal zone, or `MaxEnvironmentSteps` is reached
- The platform rotates randomly at reset to prevent agents from memorizing spatial shortcuts

**Cooperation Challenges Observed in Simulation:**
- **Incorrect directions** — agents push blocks away from the goal when acting independently
- **Deadlock** — poor coordination causes blocks to jam against walls or each other
- **Redundant effort** — all three agents cluster on the small block while heavy blocks remain stationary

---

## Workflow

The project follows a four-stage pipeline from environment design through iterative reward engineering:

```
Stage 1              Stage 2                Stage 3              Stage 4
Environment    ──►   Reward Function   ──►  Training &     ──►  Analysis &
Construction         Design                 Iteration            Optimization

• Unity scene        • Identify baseline    • MA-POCA trainer    • Reward curves
• Agent scripts        failure modes        • Hyperparameter     • Entropy tracking
• Block physics      • Piecewise reward       tuning             • Cooperation
• GridSensor obs     • Contribution         • Mass ratio           efficiency metric
• Goal triggers        tracker                adjustment         • Behavior comparison
```

### Stage 1 — Environment Construction

Built the full simulation pipeline in Unity: scene geometry, physics materials, agent `Rigidbody` configuration, `GridSensor` setup for CNN-based observations, `GoalDetectTrigger` with `UnityEvent` callbacks, and the `PushBlockEnvController` that manages episode lifecycle (spawn, reset, scoring). Each block carries a `BlockTypeIdentifier` component that maps to the reward structure.

### Stage 2 — Reward Function Design (My Core Contribution)

#### The Baseline Problem

The default PushBlockCollab awards a flat `+1` group reward per block scored. Under this scheme, agents converge on a **selfish equilibrium**: each agent independently pushes the nearest small block, ignoring heavier blocks that require cooperation. The reward signal cannot distinguish "one agent pushed a small block" from "three agents cooperated on a heavy block."

#### Collaboration-Aware Reward: Piecewise Formulation

I designed a reward function with two components:

**Component 1 — Collaboration Reward** $R_{\text{collab}}$:

$$R_{\text{collab}} = \begin{cases} -2.0, & A_{\text{active}} = 0 \\ R_{\max}, & A_{\text{active}} = A_{\text{required}} \\ -1.0 \times (A_{\text{required}} - A_{\text{active}}), & A_{\text{active}} < A_{\text{required}} \end{cases}$$

| Symbol | Meaning |
|:---:|:---|
| $A_{\text{active}}$ | Number of agents actually contributing to pushing the block |
| $A_{\text{required}}$ | Minimum agents needed (1 / 2 / 3 by block type) |
| $R_{\max}$ | Maximum reward for successful collaboration |

**Component 2 — Time Penalty** $R_{\text{time}}$:

$$R_{\text{time}} = -\frac{0.05}{\text{MaxEnvironmentSteps}}$$

**Total Reward:**

$$R_{\text{total}} = R_{\text{collab}} + R_{\text{time}}$$

#### Agent Contribution Tracking System

The reward function depends on knowing **how many agents actually pushed a given block** at the moment it enters the goal — a non-trivial problem in a physics simulation where forces are continuous and indirect.

I implemented `BlockContributionTracker`, a per-block component that:

1. **Records collision-based contributions** — when an agent collides with a block, the impact force is logged with the agent's ID and a timestamp. Initial contact receives higher weight (1.2×) than sustained contact (0.4×).

2. **Maintains a time-windowed active set** — only agents who contributed within `activeTimeWindow` seconds are counted as active collaborators. This prevents agents who touched a block 30 seconds ago from receiving credit.

3. **Applies exponential decay** — contribution values decay each `FixedUpdate()` by a factor of `indirectContactDecay` (0.98), naturally aging out stale contributions.

4. **Handles indirect force propagation** — when Agent A pushes Agent B into a block, Agent A's force is propagated to the block's contribution tracker via `Physics.OverlapSphere`, with a distance-based attenuation.

```csharp
// Simplified core logic
public void AddAgentContribution(int agentId, float contributionValue)
{
    agentContributions[agentId] += contributionValue;
    lastContributionTime[agentId] = Time.time;
    contributingAgents.Add(agentId);
}

public int GetActiveAgentCount()
{
    return contributingAgents
        .Count(id => Time.time - lastContributionTime[id] < activeTimeWindow);
}
```

#### Refined Version: Continuous Reward Function

The piecewise formulation creates **hard thresholds** — a near-miss (2 of 3 agents) receives the same penalty as a complete miss (0 of 3). This produces noisy gradients during training. I refined this to a continuous formulation:

$$R_{\text{collab}} = \frac{R_{\max} + (1 - 0.5 \times |A_{\text{required}} - A_{\text{active}}|)}{2}$$

This provides proportional reward scaling, giving the policy smoother gradient signals for learning cooperative coordination.

### Stage 3 — Training & Iteration

**Algorithm:** MA-POCA with attention-based centralized critic and decentralized actors.

**Key iteration variable — block mass ratios.** The physical difficulty of pushing a block is governed by its `Rigidbody.mass`. If mass is too high, agents cannot discover cooperation within the training budget; too low, and single agents can move all blocks, removing the need for collaboration.

| Version | Block1 | Block2 | Block3 | Convergence Window |
|:---:|:---:|:---:|:---:|:---:|
| Original | 100 | 300 | 600 | Did not converge |
| Optimized 1 | 10 | 200 | 300 | ~6–10M steps |
| Optimized 2 | 10 | 100 | 150 | ~6–10M steps |
| **Optimized 3** | **10** | **40** | **90** | **~2–7M steps** |
| **Optimized 4** | **10** | **60** | **100** | **~2–7M steps** |

The final configurations (Optimized 3 & 4) also used the **continuous reward function** and a step-based contribution window (`contributionStepWindow = 20`) instead of the time-based tracker, with an angle threshold (`contributionAngleThreshold = 30°`) to filter agents pushing in non-goal-directed directions.

### Stage 4 — Analysis

We track three metrics across training:

- **Group Cumulative Reward** — measures overall task success and collaboration quality
- **Policy Entropy** — measures exploration vs. exploitation balance; decreasing entropy indicates strategy convergence
- **Dynamic Cooperation Efficiency** — the running ratio of scoring events where `Used == Required` agents, computed post-hoc:

```python
df['Efficient'] = df['Used'] == df['Required']
df['DynamicEfficiency'] = df['Efficient'].cumsum() / range(1, len(df) + 1)
```

---

## Results

### Quantitative Findings

| Metric | Default Reward | Customized (Opt 3&4) |
|--------|:---:|:---:|
| Peak Group Cumulative Reward | ~1–2 | **~11** |
| Convergence Steps | >15M (unstable) | **2–7M** |
| Policy Entropy (converged) | ~1.8 (high randomness) | **~0.4** (stable strategies) |
| Cooperation Efficiency | Not tracked (no mechanism) | Measurable, improving |

### Emergent Behaviors

Under the customized reward, agents developed qualitatively different strategies:

| Behavior | Default Reward | Customized Reward |
|----------|:-:|:-:|
| Agents push blocks independently | ✓ Common | Reduced |
| Multiple agents converge on heavy blocks | Rare | **Frequent** |
| Blocks deadlocked against walls | Common | Less common |
| All blocks cleared within episode | Inconsistent | **Consistent** |
| Agents "wait" for teammates near heavy blocks | Never | **Observed** |

The most notable emergent behavior: agents trained with the customized reward learned to **position themselves on the same side of a heavy block** and push in a coordinated direction — a strategy that was never explicitly programmed but emerged from the reward signal alone.

### Key Insight: Reward Function as Behavioral Shaping Tool

The experiment demonstrates that in physics-based multi-agent simulation, **the reward function acts as the primary lever for shaping emergent collective behavior**. The agents' neural network architecture (MA-POCA) and observation space remained constant across all experiments — only the reward structure and physical parameters changed, yet this produced fundamentally different collaborative dynamics.

---

## Project Structure

```
├── Code_Optimized 1 & 2/                # Piecewise reward + contribution tracker
│   ├── BlockContributionTracker.cs       #   Per-block agent contribution tracking
│   ├── BlockTypeIdentifier.cs            #   Block weight class enum
│   ├── GoalDetect.cs                     #   Collision-based goal detection
│   ├── GoalDetectTrigger.cs              #   Trigger-based detection with UnityEvents
│   ├── PushAgentBasic.cs                 #   Single-agent baseline script
│   ├── PushAgentCollab.cs                #   Collaborative agent with collision logging
│   ├── PushBlockEnvController.cs         #   Env controller with piecewise reward logic
│   └── PushBlockSettings.cs              #   Shared simulation parameters
│
├── Code_Optimized 3 & 4/                # Continuous reward + step-based tracking
│   ├── GoalDetectTrigger.cs              #   Simplified trigger detection
│   ├── PushAgentCollab.cs                #   Streamlined agent script
│   ├── PushBlockEnvController.cs         #   Env controller with flexible reward
│   └── PushBlockSettings.cs              #   Simplified settings
│
├── Code_ Cooperation Efficiency/
│   └── Calculate DynamicEfficiency.py    # Post-training cooperation efficiency analysis
│
├── ProjectSlides.pdf                     # Final presentation slides
└── README.md
```

---

## How to Run

### Prerequisites

- Unity 2021.3+ with [ML-Agents Toolkit](https://github.com/Unity-Technologies/ml-agents) (Release 20+)
- Python 3.8+ with `mlagents` package installed

### Setup

1. Clone this repository and open the Unity project
2. Configure your scene with the following component attachments:
   - `PushAgentCollab` → each agent GameObject
   - `BlockTypeIdentifier` → each block (set `.blockType` to Small/Large/VeryLarge)
   - `BlockContributionTracker` → each block (for Optimized 1&2 version)
   - `GoalDetectTrigger` → each block (tag to detect = `"goal"`)
   - `PushBlockEnvController` → environment root (assign agent & block lists in Inspector)
3. Configure your MA-POCA training YAML
4. Start training:
   ```bash
   mlagents-learn config/poca_pushblock.yaml --run-id=collab_v1
   ```

---

## Future Work

- **Human-in-the-loop training** — integrate real-time human feedback (audio, discrete/continuous scalar) into the reward signal, enabling Human-Guided ML as shown in our system diagram
- **Physiological sensing** — incorporate gaze & pupil tracking, EEG, and ECG data to study human cognitive load during human-agent teaming
- **Scalable environments** — extend to dynamic agent creation/termination scenarios, leveraging MA-POCA's native support for variable team sizes

---

## References

1. Juliani, A. et al. "Unity: A General Platform for Intelligent Agents." *arXiv:1809.02627*, 2020.
2. Cohen, A. et al. "On the Use and Misuse of Absorbing States in Multi-agent Reinforcement Learning." *arXiv:2111.05992*, 2022.
3. Zhang, L. et al. "CREW: Facilitating Human-AI Teaming Research." *arXiv:2408.00170*, 2024.
4. Carroll, M. et al. "On the Utility of Learning about Humans for Human-AI Coordination." *arXiv:1910.05789*, 2020.
5. Le Pelletier de Woillemont, P. et al. "Automated Play-Testing Through RL Based Human-Like Play-Styles Generation." *arXiv:2211.17188*, 2022.

---

## License

This project was developed as part of the UCL Bartlett Architectural Computation MSc program.
