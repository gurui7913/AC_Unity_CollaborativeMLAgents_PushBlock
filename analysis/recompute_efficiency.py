"""2026 reanalysis of archived goal events; no Unity/trainer dependencies.

Run from any directory: python path/to/analysis/recompute_efficiency.py
This is a participation proxy, not completion time or episode success.
"""
import argparse
import csv
import hashlib
import json
from pathlib import Path


def read_rows(path):
    with path.open(encoding="utf-8-sig", newline="") as stream:
        return list(csv.DictReader(stream))


def write_table(path, rows):
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)


def main():
    root = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--data-dir", type=Path, default=root / "data")
    parser.add_argument("--output-dir", type=Path, default=root / "analysis/output")
    args = parser.parse_args()
    summaries, types, checks = [], [], []
    paths = sorted((args.data_dir / "raw").glob("*.csv"))
    if not paths:
        raise ValueError("No raw CSVs found in data directory")
    for path in paths:
        rows = read_rows(path)
        if not rows or not {"Required", "Used"}.issubset(rows[0]):
            raise ValueError("Missing rows or required columns: " + str(path))
        for row in rows:
            if int(row["Required"]) not in (1, 2, 3) or int(row["Used"]) not in (0, 1, 2, 3):
                raise ValueError("Out-of-range participant count: " + str(path))
        valid = [r for r in rows if int(r["Used"]) != 0]
        if not valid:
            raise ValueError("No eligible events: " + str(path))
        matches = sum(int(r["Used"]) == int(r["Required"]) for r in valid)
        summaries.append(dict(label=path.stem, raw_events=len(rows), excluded_zero=len(rows)-len(valid),
                              valid_events=len(valid), matching_events=matches,
                              match_rate=matches/len(valid), all_event_match_rate=matches/len(rows)))
        for required in sorted({int(r["Required"]) for r in valid}):
            group = [r for r in valid if int(r["Required"]) == required]
            count = sum(int(r["Used"]) == required for r in group)
            types.append(dict(label=path.stem, target_count=required, valid_events=len(group),
                              matching_events=count, match_rate=count/len(group)))
        derived_name = "Initial_test_Efficiency.csv" if path.stem == "Initial" else path.stem+"_Efficiency.csv"
        derived_path = args.data_dir / "derived" / derived_name
        derived = read_rows(derived_path)
        if len(derived) != len(valid):
            raise ValueError("Derived row-count mismatch: " + str(derived_path))
        running, max_error = 0, 0.0
        for index, (raw, processed) in enumerate(zip(valid, derived), 1):
            # pandas rewrites numeric formatting (e.g. 1.00 to 1.0).
            if any((raw[key] != processed[key] if key == "BlockType"
                    else float(raw[key]) != float(processed[key])) for key in raw):
                raise ValueError("Derived order/content mismatch: " + str(derived_path))
            matched = int(raw["Used"]) == int(raw["Required"])
            running += matched
            max_error = max(max_error, abs(float(processed["DynamicEfficiency"]) - running/index))
            if int(processed["ValidCount"]) != index or int(processed["EfficientCount"]) != running:
                raise ValueError("Derived cumulative-count mismatch: " + str(derived_path))
            if processed["Efficient"].lower() != str(matched).lower():
                raise ValueError("Derived efficient-label mismatch: " + str(derived_path))
        if max_error > 1e-12:
            raise ValueError("Derived rate mismatch: " + str(derived_path))
        checks.append(dict(raw_file=path.name, derived_file=derived_name,
                           raw_sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                           derived_sha256=hashlib.sha256(derived_path.read_bytes()).hexdigest(),
                           derived_matches_filtered_raw=True, max_cumulative_rate_error=max_error))
    args.output_dir.mkdir(parents=True, exist_ok=True)
    write_table(args.output_dir / "summary.csv", summaries)
    write_table(args.output_dir / "by_block_type.csv", types)
    receipt = dict(scope="Goal-event participation proxy; no Unity or causal validation", inputs=checks)
    (args.output_dir / "verification.json").write_text(json.dumps(receipt, indent=2)+"\n", encoding="utf-8")
    for row in summaries:
        print("{}: {}/{} = {:.4%}".format(row["label"], row["matching_events"], row["valid_events"], row["match_rate"]))
    print("Verified {} raw/derived file pairs; results: {}".format(len(checks), args.output_dir))


if __name__ == "__main__":
    main()
