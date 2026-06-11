# Cooperative Agent Simulation

**Designing Cooperation-Aware Reward Functions for Multi-Agent Reinforcement Learning in a Physics-Based Simulation**

> UCL Bartlett School of Architecture · Architectural Computation – Digital Studio 1: Simulated Realities  
> Team: Du Hao, **Gu Rui**, Lu Haiyu, Pan Lingfeng · March 2025

> **My contribution:** Reward function design · BlockContributionTracker system · Training iteration & analysis

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
- [Project Structure](#project-structure)
- [How to Run](#how-to-run)
- [Future Work](#future-work)
- [References](#references)

---

## Research Question

In multi-agent reinforcement learning (MARL), agents trained with standard reward functions tend to develop **individualistic strategies** — even when the task physically demands cooperation. This raises a central question:

> **Can a collaboration-aware reward function, combined with a real-time agent contribution tracking mechanism, induce emergent cooperative behavior among decentralized agents in a physics-based simulation?**

We operationalize this through a **Collaborative Push Block** task in Unity ML-Agents, where three agents must push blocks of varying masses into a goal zone. Heavier blocks physically require multiple agents to move — but the default flat reward provides no signal distinguishing solo from cooperative actions. We hypothesize that a **piecewise reward function validating agent-count matching per block type** will produce measurably higher collaboration rates and faster convergence compared to the baseline.

---

## Simulation Environment

### Platform Selection

We evaluated three platforms for multi-agent simulation research:

| Platform | Extensible Envs | Real-time Interaction | Multiplayer | Open-sourced | Difficulty |
|----------|:---:|:---:|:---:|:---:|:---:|
| Overcooked | ✗ | ✓ | 1+1 | ✓ | Medium |
| CREW | ✓ | ✓ | No Limit | ✓ | Difficult |
| **ML-Agents** | **✓** | **✓** | **No Limit** | **✓** | **Easy** |

We chose **Unity ML-Agents** for its tight integration with Unity's physics engine (essential for force-based block pushing), native support for **MA-POCA** (Multi-Agent POsthumous Credit Assignment), and extensible C# scripting for custom environment logic.

### Environment Architecture

The simulation uses a **Centralized Training with Decentralized Execution (CTDE)** paradigm via MA-POCA:

```
┌─────────────────────────┐          ┌─────────────────────────────────┐
│  MA-POCA Training       │          │  Environment in Unity           │
│  (Python API)           │          │  (C# Scripts + Physics Engine)  │
│                         │          │                                 │
│  Policy Network         │◄─Train──►│  Agent1 ─► Observations         │
│   Input: grid obs,      │  /Learn  │            (GridSensor)         │
│   state, reward         │          │          ─► Actions             │
│   Output: action (ONNX) │          │            (discrete 7-dim)     │
│                         │          │          ─► Actor               │
│  Centralized Critic     │          │            (ONNX model)         │
│  Decentralized Actor    │          │                                 │
│  PPO with Clipping      │          │  Agent2, Agent3 ... AgentN      │
│  Attention Mechanism    │          │                                 │
│  Experience Replay      │          │  Env Parameters:                │
│                         │          │   blocks, goal, walls, physics  │
└─────────────────────────┘          └─────────────────────────────────┘
```

**Why MA-POCA over plain PPO or COMA:**

The task has three structural properties that make standard decentralized approaches insufficient:

- **Group-level reward** — agents share a single reward signal; individual critics cannot correctly attribute which agent caused the reward
- **Physically enforced cooperation** — Block3 cannot move unless all three agents push simultaneously; this requires the critic to evaluate joint states, not individual states
- **Dynamic contribution** — an agent may contribute to Block3 early in an episode then move away; its contribution must be credited posthumously

MA-POCA's **Centralized Critic** observes all agents' states simultaneously, solving the credit assignment problem that causes non-stationarity in fully decentralized critics. Its **Posthumous Credit Assignment** mechanism specifically handles the case where an agent's contribution to a group outcome precedes the reward signal — directly matching our task structure.

### Agent Design

Each agent has **no inter-agent communication channel** — cooperation must emerge purely from shared reward signals and environmental observations.

- **Observation Space:** Grid-based CNN perception. Each agent observes a local 2D grid encoded as a one-hot tensor over 6 detectable tags. Shape: `GridSize.x × GridSize.z × NumDetectableTags`.

  | Tag | One-Hot | Object |
  |:---:|:---:|:---|
  | N | `[0,0,0,0,0,0]` | Nothing |
  | 0 | `[1,0,0,0,0,0]` | Wall |
  | 1 | `[0,1,0,0,0,0]` | Agent |
  | 2 | `[0,0,1,0,0,0]` | Goal |
  | 3 | `[0,0,0,1,0,0]` | BlockSmall |
  | 4 | `[0,0,0,0,1,0]` | BlockLarge |
  | 5 | `[0,0,0,0,0,1]` | BlockVeryLarge |

- **Action Space:** 7 discrete actions — `0: idle`, `1: forward`, `2: backward`, `3: rotate CW`, `4: rotate CCW`, `5: strafe left`, `6: strafe right`. Movement is physics-based (`Rigidbody.AddForce`, `VelocityChange` mode).

- **Constraints:** Equal strength, equal max speed, push-only, facing-direction movement. These constraints make moving a heavy block physically impossible without multiple agents applying force from compatible angles.

### Block & Task Design

| Block Type | Mass (Optimized) | Required Agents | Tag |
|:---:|:---:|:---:|:---:|
| Small (1) | 10 | 1 | `BlockSmall` |
| Large (2) | 40–100 | 2 | `BlockLarge` |
| Very Large (3) | 90–150 | 3 | `BlockVeryLarge` |

**Episode Logic:**
- All agents and blocks spawn at random non-overlapping positions outside the goal zone
- Episode terminates when all blocks enter the goal, or `MaxEnvironmentSteps` is reached
- Platform rotates randomly at reset to prevent agents memorizing spatial shortcuts

---

## Workflow

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

Built the full simulation pipeline in Unity: scene geometry, physics materials, agent `Rigidbody` configuration, `GridSensor` setup, `GoalDetectTrigger` with `UnityEvent` callbacks, and `PushBlockEnvController` managing episode lifecycle. Each block carries a `BlockTypeIdentifier` component mapping to the reward structure.

### Stage 2 — Reward Function Design (My Core Contribution)

#### The Baseline Problem

The default PushBlockCollab awards a flat `+1` group reward per block scored. Under this scheme, agents converge on a **selfish equilibrium**: each independently pushes the nearest small block, ignoring heavier blocks requiring cooperation. The reward signal cannot distinguish "one agent pushed a small block" from "three agents cooperated on a heavy block."

#### Collaboration-Aware Reward: Piecewise Formulation

**Component 1 — Collaboration Reward** $R_{\text{collab}}$:

$$R_{\text{collab}} = \begin{cases} -2.0, & A_{\text{active}} = 0 \\ R_{\max}, & A_{\text{active}} = A_{\text{required}} \\ -1.0 \times (A_{\text{required}} - A_{\text{active}}), & A_{\text{active}} < A_{\text{required}} \end{cases}$$

| Symbol | Meaning |
|:---:|:---|
| $A_{\text{active}}$ | Agents actually contributing to pushing the block |
| $A_{\text{required}}$ | Minimum agents needed (1 / 2 / 3 by block type) |
| $R_{\max}$ | Maximum reward for exact-match collaboration |

**Component 2 — Time Penalty** $R_{\text{time}}$:

$$R_{\text{time}} = -\frac{0.05}{\text{MaxEnvironmentSteps}}$$

**Total:** $R_{\text{total}} = R_{\text{collab}} + R_{\text{time}}$

#### Agent Contribution Tracking System

The reward function requires knowing **which agents actually pushed a block** at scoring — non-trivial in a physics simulation where forces are continuous and indirect.

`BlockContributionTracker` (per-block C# component):

1. **Collision-based logging** — records impact force per agent ID with timestamp; initial contact weighted 1.2×, sustained contact 0.4×
2. **Time-windowed active set** — only agents contributing within `activeTimeWindow` seconds count as active collaborators
3. **Exponential decay** — contribution values decay by `indirectContactDecay` (0.98) each `FixedUpdate()`, naturally aging out stale contributions
4. **Indirect force propagation** — when Agent A pushes Agent B into a block, A's force propagates via `Physics.OverlapSphere` with distance attenuation

```csharp
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

The piecewise formulation creates **hard thresholds** — a near-miss (2 of 3 agents) receives the same penalty as a complete miss (0 of 3), producing noisy gradients. Refined to a continuous formulation:

$$R_{\text{collab}} = \frac{R_{\max} + (1 - 0.5 \times |A_{\text{required}} - A_{\text{active}}|)}{2}$$

Rewards now scale linearly with distance from the target agent count, providing smoother gradient signals for cooperative coordination.

### Stage 3 — Training & Iteration

**Algorithm:** MA-POCA with attention-based centralized critic and decentralized actors.

**Key iteration variable — block mass ratios.** If mass is too high, agents cannot discover cooperation within the training budget; too low, single agents can move all blocks, eliminating the need for collaboration.

| Version | Block1 | Block2 | Block3 | Reward Formula | Convergence |
|:---:|:---:|:---:|:---:|:---:|:---:|
| Original | 100 | 300 | 600 | Flat +1 | Did not converge |
| Optimized 1 | 10 | 200 | 300 | Piecewise | ~6–10M steps |
| Optimized 2 | 10 | 100 | 150 | Piecewise | ~6–10M steps |
| **Optimized 3** | **10** | **40** | **90** | **Continuous** | **~2–7M steps** |
| **Optimized 4** | **10** | **60** | **100** | **Continuous** | **~2–7M steps** |

Optimized 3 & 4 additionally use a **step-based contribution window** (`contributionStepWindow = 20`) with an **angle threshold** (`contributionAngleThreshold = 30°`) to filter agents pushing in non-goal-directed directions.

### Stage 4 — Analysis

Three metrics tracked across training:

- **Group Cumulative Reward** — overall task success and collaboration quality
- **Policy Entropy** — exploration/exploitation balance; decreasing entropy signals strategy convergence
- **Dynamic Cooperation Efficiency** — running ratio of scoring events where `Used == Required` agents:

```python
df['Efficient'] = df['Used'] == df['Required']
df['DynamicEfficiency'] = df['Efficient'].cumsum() / range(1, len(df) + 1)
```

---

## Results

### Quantitative

| Metric | Default Reward | Customized (Opt 3&4) |
|--------|:---:|:---:|
| Peak Group Cumulative Reward | ~1–2 | **~11** |
| Convergence Steps | >15M (unstable) | **2–7M** |
| Policy Entropy (converged) | ~1.8 | **~0.4** |
| Cooperation Efficiency | — | Measurable, improving |

### Emergent Behaviors

| Behavior | Default | Customized |
|----------|:---:|:---:|
| Agents push independently | Common | Reduced |
| Multiple agents converge on heavy blocks | Rare | **Frequent** |
| Blocks deadlocked against walls | Common | Less common |
| All blocks cleared within episode | Inconsistent | **Consistent** |
| Agents position on same side of heavy block | Never | **Observed** |

The most notable emergent behavior: agents trained with the customized reward learned to **position on the same side of a heavy block and push in a coordinated direction** — never explicitly programmed, emerging from reward structure alone.

---

## Project Structure

```
├── Code_Optimized 1 & 2/                # Piecewise reward + time-based contribution tracker
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
│   ├── PushBlockEnvController.cs         #   Env controller with continuous reward
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
- Python 3.8+ with `mlagents` package

### Setup

1. Clone this repository and open the Unity project
2. Attach components in your scene:
   - `PushAgentCollab` → each agent GameObject
   - `BlockTypeIdentifier` → each block (set `.blockType` to Small / Large / VeryLarge)
   - `BlockContributionTracker` → each block (Optimized 1&2 only)
   - `GoalDetectTrigger` → each block (tag = `"goal"`)
   - `PushBlockEnvController` → environment root (assign agent & block lists in Inspector)

3. Create a training config `config/poca_pushblock.yaml`:

```yaml
behaviors:
  PushAgentCollab:
    trainer_type: poca
    hyperparameters:
      batch_size: 1024
      buffer_size: 10240
      learning_rate: 3.0e-4
      beta: 5.0e-3
      epsilon: 0.2
      lambd: 0.99
      num_epoch: 3
    network_settings:
      normalize: false
      hidden_units: 256
      num_layers: 2
      memory:
        memory_size: 256
        sequence_length: 64
    reward_signals:
      extrinsic:
        gamma: 0.99
        strength: 1.0
    max_steps: 15000000
    time_horizon: 128
    summary_freq: 10000
    team_change: 100000
```

4. Start training:

```bash
mlagents-learn config/poca_pushblock.yaml --run-id=collab_v1
```

5. Monitor training in TensorBoard:

```bash
tensorboard --logdir results/collab_v1
```

---

## Future Work

- **Human-in-the-loop training** — integrate real-time human feedback (audio, discrete/continuous scalar signals) into the reward pipeline, enabling Human-Guided ML
- **Physiological sensing integration** — connect gaze tracking, EEG, and ECG data streams via ML-Agents Side Channel to study human cognitive load during human-agent teaming
- **Variable team sizes** — extend to dynamic agent creation/termination scenarios, leveraging MA-POCA's native support for variable-size groups

---

## References

1. Juliani, A. et al. "Unity: A General Platform for Intelligent Agents." *arXiv:1809.02627*, 2020.
2. Cohen, A. et al. "On the Use and Misuse of Absorbing States in Multi-agent Reinforcement Learning." *arXiv:2111.05992*, 2022.
3. Zhang, L. et al. "CREW: Facilitating Human-AI Teaming Research." *arXiv:2408.00170*, 2024.
4. Carroll, M. et al. "On the Utility of Learning about Humans for Human-AI Coordination." *arXiv:1910.05789*, 2020.
5. Le Pelletier de Woillemont, P. et al. "Automated Play-Testing Through RL Based Human-Like Play-Styles Generation." *arXiv:2211.17188*, 2022.

---

## License

Developed as part of the UCL Bartlett Architectural Computation MSc program.
