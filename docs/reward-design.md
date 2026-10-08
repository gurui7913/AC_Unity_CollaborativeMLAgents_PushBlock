# Reward design and historical limitations

## Baseline and target

The official example already uses `SimpleMultiAgentGroup`, `AddGroupReward(score)` and a time penalty. The custom logic adds participation matching. Target count `k` is assigned by block type (1/2/3); it is not a calibrated physical minimum. Detected count `n` is an operational proxy for cooperation.

## Piecewise version

Slides give -2 for `n=0`, `Rmax` for `n=k`, and `-(k-n)` for `n<k`; they omit `n>k`. The [archived code listing](../src/piecewise/PushBlockEnvController.cs) uses -5 for zero, rewards 20/30/50 by type, and `-2*(k-n)` for every other mismatch. Thus `k=1,n=3` gives **+4**. A separate local revision uses coefficients closer to the slides and still gives positive rewards for excess participants. There is no single reconciled parameter set across historical revisions.

No behavioral correction was made in this cleanup. A corrected future variant must explicitly define both sides of the mismatch and be evaluated as new work.

## Absolute deviation

The [later controller](../src/absolute_deviation/PushBlockEnvController.cs) uses:

```text
correction = 1 - 0.5 * abs(k - n)
reward = (score + correction) / 2
```

For `score=3,k=2`, matching gives 2; one or three participants give 1.75; zero gives 1.5. A mismatch reduces reward without necessarily making it negative. The zero-participant rule therefore changes. The maximum is `(score+1)/2`, not generally the slide's symbol `Rmax`; “base goal score” is clearer.

The real-valued extension is continuous and piecewise linear with an absolute-value corner. Counts remain discrete and the reward remains goal-triggered: this is not dense feedback, continuous actions, or proof of smoother policy gradients.

## Tracking

The earlier [tracker](../src/piecewise/BlockContributionTracker.cs) stores IDs, heuristic contribution values and timestamps. [Agent collision logic](../src/piecewise/PushAgentCollab.cs) uses empirical weights/cooldowns and propagates a heuristic to nearby blocks after agent collisions. Active counts mainly use a five-second window; decayed contribution magnitudes do not directly determine membership. Values are not measured physical force or causal contribution.

The later controller uses speed >0.15, radius 3.8 and angle <30 degrees **toward the block**, not the goal. It normally checks 20 steps, sometimes extending to 100 for larger block types, and ignores initial frames. Proximity is not proof of contact; thresholds and windows require validation against labeled rollouts.

Rewards use `AddGroupReward`; participant tracking does not imply contributor-only individual rewards. `successMemory` bookkeeping is not evidence of proportional individual policy updates. The environment proxy and MA-POCA's learned credit assignment are separate mechanisms.

## Other version differences

The earlier listing uses time penalty `-0.05/MaxEnvironmentSteps`; the later uses `-0.5/MaxEnvironmentSteps`. Inspector settings can override field defaults. Whole-episode return sums event rewards and all step penalties.

The exported grid prefab specifies 20×20 cells, six tags and a seven-choice discrete action branch. The later script implements all seven choices; the earlier standalone listing implements only idle, forward/backward and rotations. Do not assume every revision has seven implemented movement choices. Reading copies in `src` and scene exports remain separate and may differ.

Neither reward variant has a controlled causal performance estimate in this archive. Future evaluation should fix environment settings, validate tracking, use multiple training seeds and common external task metrics.
