# Restoring the Unity task

This archive contains partial exports, not an independently openable Unity project. No Editor compilation, inference, training or exact reproduction was performed during organization.

## Historical versions

Local metadata records Unity **2021.3.11f1**, ML-Agents **release_20**, Python packages **0.30.0**, Unity package **2.3.0-exp.3**, extensions **0.6.1-preview** and Python support **3.8.13–3.10.8**. These are reference versions, not a claim that arbitrary newer dependencies work. [Manifest copies](../environment/reference) assume the upstream relative package layout.

## Restore in a separate workspace

```bash
git clone --branch release_20 --depth 1 https://github.com/Unity-Technologies/ml-agents.git
```

Open upstream `Project` with the matching Editor and retain its packages/shared assets. Prepare a separate historical trainer environment using [release_20 installation instructions](https://github.com/Unity-Technologies/ml-agents/blob/release_20/docs/Installation.md).

Choose **one** `unity/exports/{piecewise,absolute_deviation}/PushBlock` directory. Back up upstream `Project/Assets/ML-Agents/Examples/PushBlock` outside `Assets`, then replace it with the chosen export, retaining the export's `.meta` files. The upstream folder's own `.meta` may remain; metadata for exported subfolders/assets is included. Do not combine exports or also import `src`: same-named classes conflict.

[External GUID references](../environment/external_guid_references.csv) lists references not defined within each export. They may resolve to retained upstream resources or require restoration. This inventory does not prove every external reference is broken or every dependency is present. Built-in zero GUIDs are omitted.

Open `PushBlock/Scenes/PushBlockCollab.unity` and resolve missing components, materials and model references. Inspect agent/block lists, colliders, goal callbacks, GridSensor tags, `PushBlockCollab` behavior name, observation/action compatibility, masses, forces, friction, time limits and tracking windows. Source defaults and slide tables do not establish active Inspector settings or which settings generated a specific plot.

## Inference and new training

Exports contain historical models; additional policies are in [models](../models) and metadata in [training status](../experiments/training_status). They are not guaranteed interchangeable across variants.

Only after the scene compiles and basic behavior is checked, a new run may start from a saved config:

```bash
mlagents-learn configs/training/optimize_5.yaml --run-id=restored_pushblock_new_run
```

Run from this archive's root in the prepared trainer environment, then start the restored Unity scene. Review historical runtime/checkpoint settings first. This command is guidance, not an executed reproduction. Use a fresh run ID and record actual environment settings/seeds.

CSV reanalysis is independent of Unity: `python analysis/recompute_efficiency.py` needs only Python 3.8+ and is the computational workflow verified during organization.
