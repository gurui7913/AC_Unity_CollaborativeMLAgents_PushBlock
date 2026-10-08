# Evaluation notes

The [legacy script](../analysis/legacy/Calculate%20DynamicEfficiency.py) filters `Used=0`, labels `Used==Required` as efficient and computes a cumulative fraction. It has hard-coded desktop paths and is preserved for provenance; use `python analysis/recompute_efficiency.py` for portable reanalysis.

| Label | Raw events | Excluded zero | Valid events | Matches | Match rate |
|---|---:|---:|---:|---:|---:|
| Initial | 5,243 | 13 | 5,230 | 3,114 | 59.54% |
| Mass_Large | 4,909 | 29 | 4,880 | 3,044 | 62.38% |
| Mass_Light | 5,052 | 76 | 4,976 | 3,120 | 62.70% |
| Mass_Medium | 4,438 | 87 | 4,351 | 2,867 | 65.89% |

Initial to Mass_Medium is 6.35 percentage points, approximately 10.67% relative. **Neither should replace the unsupported 40% task-efficiency claim.** For target count three, match rate falls from 83.81% to 64.58%; for target one, it rises from 50.76% to 77.08%. See [block-type results](../analysis/output/by_block_type.csv).

Interpretation limits:

- Rows are successful goal events, not independent episodes or training repetitions. Episode IDs, completion times and a full failure/timeout denominator are absent.
- Zero-participant filtering can remove inertia-driven goals or tracking misses; the script also reports all-event rates.
- Labels do not establish evaluation-to-model or matched-environment mappings. Nearby saved models do not establish that mapping either.
- Masses, rewards and some trainer hyperparameters vary. This is not an isolated reward ablation.
- Seed=-1 and thousands of events do not establish independent multi-seed evaluation.
- Raw reward magnitudes across different reward definitions and policy entropy are not common cooperation-quality metrics.
- Repeated field combinations may represent legitimate separate events; they were not deduplicated without event identifiers.

Original [figures](figures) and [training images](media) are unchanged historical artifacts, not revalidated causal comparisons. The current standard-library calculations are 2026 retrospective checks, not Gu Rui's personal 2025 analysis.
