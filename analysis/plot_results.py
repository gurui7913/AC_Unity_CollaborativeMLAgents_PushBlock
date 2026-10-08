"""Render README figures from verified archive summaries (added in 2026).

Run recompute_efficiency.py first, then this file. Plotting requires matplotlib;
the underlying CSV reanalysis still uses only the standard library.
"""
import csv
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.ticker import PercentFormatter

ROOT = Path(__file__).resolve().parents[1]
ORDER = ["Initial", "Mass_Large_test", "Mass_Light_test", "Mass_Medium_test"]
LABELS = ["Initial", "Mass_Large", "Mass_Light", "Mass_Medium"]
COLORS = ["#586574", "#4477AA", "#228877", "#AA7733"]
HATCHES = ["", "//", "..", "xx"]


def load(name):
    with (ROOT / "analysis/output" / name).open(encoding="utf-8-sig", newline="") as stream:
        return list(csv.DictReader(stream))


def style(ax, horizontal=False):
    ax.set_axisbelow(True)
    for side in ("top", "right"):
        ax.spines[side].set_visible(False)
    ax.spines["left"].set_color("#CBD5E1")
    ax.spines["bottom"].set_color("#CBD5E1")
    ax.tick_params(length=0, pad=9, colors="#334155")
    if horizontal:
        ax.set_xlim(0, 100)
        ax.set_xticks(range(0, 101, 20))
        ax.xaxis.set_major_formatter(PercentFormatter(100))
        ax.grid(axis="x", color="#E2E8F0", linewidth=0.8)
    else:
        ax.set_ylim(0, 100)
        ax.set_yticks(range(0, 101, 20))
        ax.yaxis.set_major_formatter(PercentFormatter(100))
        ax.grid(axis="y", color="#E2E8F0", linewidth=0.8)


def save(fig, name):
    dest = ROOT / "docs/figures"
    dest.mkdir(parents=True, exist_ok=True)
    for suffix in ("png", "svg"):
        fig.savefig(dest / (name + "." + suffix), dpi=160, facecolor="white",
                    metadata={"Date": None} if suffix == "svg" else None)
    plt.close(fig)


def main():
    plt.rcParams.update({"font.family": "DejaVu Sans", "font.size": 12,
                         "axes.labelsize": 12, "svg.fonttype": "none",
                         "svg.hashsalt": "pushblock-archive-results"})
    summary = {r["label"]: r for r in load("summary.csv")}
    by_type = {(r["label"], int(r["target_count"])): r for r in load("by_block_type.csv")}
    # Ensure plotted values equal their stored numerator/denominator.
    for row in list(summary.values()) + list(by_type.values()):
        rate = int(row["matching_events"]) / int(row["valid_events"])
        if abs(rate - float(row["match_rate"])) > 1e-12:
            raise ValueError("Inconsistent chart input: " + row["label"])

    fig, ax = plt.subplots(figsize=(10, 5.8))
    fig.subplots_adjust(left=0.17, right=0.96, top=0.76, bottom=0.23)
    fig.text(0.055, 0.93, "Participation matching across archived configurations",
             fontsize=18, weight="bold", color="#172B4D")
    fig.text(0.055, 0.865, "Match = Used == Required, among logged goals with Used > 0",
             fontsize=12, color="#475569")
    values = [float(summary[key]["match_rate"]) * 100 for key in ORDER]
    ax.barh(range(4), values, height=0.5, color="#4477AA")
    ax.set_yticks(range(4), LABELS)
    ax.invert_yaxis()
    style(ax, horizontal=True)
    ax.set_xlabel("Participation-match rate")
    for i, (key, value) in enumerate(zip(ORDER, values)):
        row = summary[key]
        ax.text(value+1.4, i, f"{value:.2f}%", va="center", weight="bold", fontsize=13)
        ax.text(2, i+0.36, "{:,} / {:,} matching / valid goals".format(
            int(row["matching_events"]), int(row["valid_events"])),
            fontsize=10, color="#475569", va="center")
    ax.set_ylim(3.65, -0.55)
    fig.text(0.055, 0.10, "Team experiment archive | 2026 reanalysis | Source: analysis/output/summary.csv",
             fontsize=10, color="#475569")
    fig.text(0.055, 0.05, "Descriptive event rates; not completion speed or a controlled causal comparison.",
             fontsize=10, color="#475569")
    save(fig, "participation_match_overall")

    fig, ax = plt.subplots(figsize=(12, 7.2))
    fig.subplots_adjust(left=0.085, right=0.985, top=0.73, bottom=0.23)
    fig.text(0.055, 0.94, "Matching rates differ by target team size",
             fontsize=20, weight="bold", color="#172B4D")
    fig.text(0.055, 0.885, "The aggregate pattern does not hold for every block type; n = valid goal events",
             fontsize=12, color="#475569")
    width = 0.19
    for index, key in enumerate(ORDER):
        positions = [j + (index-1.5)*width for j in range(3)]
        rows = [by_type[(key, count)] for count in (1, 2, 3)]
        values = [float(row["match_rate"])*100 for row in rows]
        ax.bar(positions, values, width=width*0.92, label=LABELS[index],
               color=COLORS[index], hatch=HATCHES[index], edgecolor="white", linewidth=0.7)
        for x, y, row in zip(positions, values, rows):
            ax.text(x, y+1.3, "{:.1f}%\nn={:,}".format(y, int(row["valid_events"])),
                    ha="center", va="bottom", fontsize=10, linespacing=1.4)
    ax.set_xticks(range(3), ["Target: 1 agent", "Target: 2 agents", "Target: 3 agents"])
    ax.set_ylabel("Participation-match rate")
    style(ax)
    handles, labels = ax.get_legend_handles_labels()
    fig.legend(handles, labels, loc="upper left", bbox_to_anchor=(0.055, 0.85),
               ncol=4, frameon=False, fontsize=12)
    fig.text(0.055, 0.11, "Team experiment archive | 2026 reanalysis | Source: analysis/output/by_block_type.csv",
             fontsize=10, color="#475569")
    fig.text(0.055, 0.065, "Used > 0 filter; different mass/reward settings; no independent-run uncertainty estimate.",
             fontsize=10, color="#475569")
    save(fig, "participation_match_by_team_size")
    print("Rendered 2 figures (PNG + SVG) from verified matching-event counts.")


if __name__ == "__main__":
    main()
