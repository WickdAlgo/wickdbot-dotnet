#!/usr/bin/env python3
"""Summarize WickdBot structure journals for review questions."""

from __future__ import annotations

import argparse
import json
import sys
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any


CURRENT_EVENT_TYPES = {
    "swingHighCandidate",
    "swingLowCandidate",
    "swingHighCandidateUpdated",
    "swingLowCandidateUpdated",
    "swingHighFinalized",
    "swingLowFinalized",
    "equalHighLiquidity",
    "equalLowLiquidity",
    "buySideLiquidityBreached",
    "sellSideLiquidityBreached",
    "buySideSweepCandidate",
    "sellSideSweepCandidate",
    "buySideRejectionConfirmed",
    "sellSideRejectionConfirmed",
    "buySideSweepConfirmed",
    "sellSideSweepConfirmed",
    "buySideBreakoutConfirmed",
    "sellSideBreakoutConfirmed",
    "bullishOrderBlockDiscovered",
    "bearishOrderBlockDiscovered",
    "bullishExpansion",
    "bearishExpansion",
    "bullishFvgDiscovered",
    "bearishFvgDiscovered",
    "fvgFillUpdated",
    "orderBlockMitigated",
    "orderBlockConsumed",
}

BREACH_TYPES = {
    "buySideLiquidityBreached": "buySide",
    "sellSideLiquidityBreached": "sellSide",
}

CANDIDATE_TYPES = {
    "buySideSweepCandidate": "buySide",
    "sellSideSweepCandidate": "sellSide",
}

REJECTION_TYPES = {
    "buySideRejectionConfirmed": "buySide",
    "sellSideRejectionConfirmed": "sellSide",
}

SWEEP_CONFIRMED_TYPES = {
    "buySideSweepConfirmed": "buySide",
    "sellSideSweepConfirmed": "sellSide",
}

BREAKOUT_CONFIRMED_TYPES = {
    "buySideBreakoutConfirmed": "buySide",
    "sellSideBreakoutConfirmed": "sellSide",
}

LIFECYCLE_TYPES = {
    **BREACH_TYPES,
    **CANDIDATE_TYPES,
    **REJECTION_TYPES,
    **SWEEP_CONFIRMED_TYPES,
    **BREAKOUT_CONFIRMED_TYPES,
}

STAGE_BY_TYPE = {
    **{event_type: "breached" for event_type in BREACH_TYPES},
    **{event_type: "sweepCandidate" for event_type in CANDIDATE_TYPES},
    **{event_type: "rejectionConfirmed" for event_type in REJECTION_TYPES},
    **{event_type: "sweepConfirmed" for event_type in SWEEP_CONFIRMED_TYPES},
    **{event_type: "breakoutConfirmed" for event_type in BREAKOUT_CONFIRMED_TYPES},
}

LIFECYCLE_STAGES = {
    "breached",
    "sweepCandidate",
    "rejectionConfirmed",
    "sweepConfirmed",
    "breakoutConfirmed",
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Summarize WickdBot structures.jsonl event counts and staged liquidity transitions."
    )
    parser.add_argument(
        "path",
        help="Path to a run directory or a structures.jsonl file.",
    )
    parser.add_argument(
        "--side",
        choices=("all", "buySide", "sellSide"),
        default="all",
        help="Limit lifecycle transition counts to one liquidity side.",
    )
    parser.add_argument(
        "--json",
        action="store_true",
        help="Write machine-readable JSON instead of a text summary.",
    )
    return parser.parse_args()


def resolve_structures_path(raw_path: str) -> Path:
    path = Path(raw_path)
    if path.is_dir():
        path = path / "structures.jsonl"

    if not path.exists():
        raise SystemExit(f"Could not find structures journal: {path}")

    if path.is_dir():
        raise SystemExit(f"Expected a structures.jsonl file, got directory: {path}")

    return path


def read_jsonl(path: Path) -> list[dict[str, Any]]:
    records: list[dict[str, Any]] = []
    with path.open("r", encoding="utf-8-sig") as handle:
        for line_number, line in enumerate(handle, start=1):
            line = line.strip()
            if not line:
                continue
            try:
                record = json.loads(line)
            except json.JSONDecodeError as exc:
                raise SystemExit(f"{path}:{line_number}: invalid JSON: {exc}") from exc
            record["_lineNumber"] = line_number
            records.append(record)
    return records


def lifecycle_stage(record: dict[str, Any]) -> str | None:
    event_type = as_text(record.get("eventType"))
    if event_type in STAGE_BY_TYPE:
        return STAGE_BY_TYPE[event_type]

    stage = as_text(record.get("classificationStage"))
    entity_id = as_text(record.get("entityId"))
    if stage in LIFECYCLE_STAGES and entity_id.startswith("breach-"):
        return stage

    return None


def lifecycle_side(record: dict[str, Any]) -> str | None:
    event_type = as_text(record.get("eventType"))
    if event_type in LIFECYCLE_TYPES:
        return LIFECYCLE_TYPES[event_type]

    direction = as_text(record.get("direction"))
    stage = lifecycle_stage(record)
    if stage in {"sweepCandidate", "rejectionConfirmed", "sweepConfirmed"}:
        if direction == "bearish":
            return "buySide"
        if direction == "bullish":
            return "sellSide"
    if stage == "breakoutConfirmed":
        if direction == "bullish":
            return "buySide"
        if direction == "bearish":
            return "sellSide"

    return None


def as_text(value: Any) -> str:
    return value if isinstance(value, str) else ""


def sequence_key(record: dict[str, Any]) -> tuple[int, int]:
    sequence = record.get("sequence")
    if not isinstance(sequence, int):
        sequence = 0
    line_number = record.get("_lineNumber")
    if not isinstance(line_number, int):
        line_number = 0
    return sequence, line_number


def build_lifecycles(records: list[dict[str, Any]]) -> dict[str, dict[str, Any]]:
    groups: dict[str, dict[str, Any]] = {}
    for record in records:
        stage = lifecycle_stage(record)
        if stage is None:
            continue

        entity_id = as_text(record.get("entityId"))
        if not entity_id:
            entity_id = as_text(record.get("originalBreachEventId"))
        if not entity_id:
            entity_id = as_text(record.get("eventId"))
        if not entity_id:
            continue

        group = groups.setdefault(
            entity_id,
            {
                "entityId": entity_id,
                "side": None,
                "stages": set(),
                "events": [],
                "breachedLiquidityId": None,
                "protectedSwingId": None,
            },
        )
        group["stages"].add(stage)
        group["events"].append(record)

        side = lifecycle_side(record)
        if side:
            group["side"] = side

        if record.get("breachedLiquidityId"):
            group["breachedLiquidityId"] = record["breachedLiquidityId"]
        if record.get("protectedSwingId"):
            group["protectedSwingId"] = record["protectedSwingId"]

    for group in groups.values():
        group["events"].sort(key=sequence_key)
    return groups


def final_stage(group: dict[str, Any]) -> str:
    stages: set[str] = group["stages"]
    if "sweepConfirmed" in stages:
        return "sweepConfirmed"
    if "breakoutConfirmed" in stages:
        return "breakoutConfirmed"
    if "rejectionConfirmed" in stages:
        return "rejectionConfirmed"
    if "sweepCandidate" in stages:
        return "sweepCandidate"
    return "breached"


def summarize(path: Path, records: list[dict[str, Any]], side_filter: str) -> dict[str, Any]:
    event_counts = Counter(as_text(record.get("eventType")) for record in records)
    event_counts.pop("", None)
    unknown_event_types = sorted(
        event_type for event_type in event_counts if event_type not in CURRENT_EVENT_TYPES
    )

    lifecycles = build_lifecycles(records)
    filtered_lifecycles = {
        entity_id: group
        for entity_id, group in lifecycles.items()
        if side_filter == "all" or group.get("side") == side_filter
    }

    stage_counts = Counter()
    final_counts = Counter()
    by_side: dict[str, Counter[str]] = defaultdict(Counter)
    candidate_outcomes = Counter()

    for group in filtered_lifecycles.values():
        stages: set[str] = group["stages"]
        side = group.get("side") or "unknown"
        for stage in stages:
            stage_counts[stage] += 1
            by_side[side][stage] += 1

        final = final_stage(group)
        final_counts[final] += 1
        by_side[side][f"final:{final}"] += 1

        if "sweepCandidate" in stages:
            if final == "sweepConfirmed":
                candidate_outcomes["candidateToSweepConfirmed"] += 1
                if "rejectionConfirmed" in stages:
                    candidate_outcomes["candidateToRejectionThenSweep"] += 1
            elif final == "breakoutConfirmed":
                candidate_outcomes["candidateToBreakoutConfirmed"] += 1
            elif final == "rejectionConfirmed":
                candidate_outcomes["candidateToRejectionOnly"] += 1
            else:
                candidate_outcomes["candidateStillOpen"] += 1

    candidate_total = stage_counts["sweepCandidate"]
    breakout_total = stage_counts["breakoutConfirmed"]
    sweep_confirmed_total = stage_counts["sweepConfirmed"]
    breakout_without_candidate = sum(
        1
        for group in filtered_lifecycles.values()
        if "breakoutConfirmed" in group["stages"] and "sweepCandidate" not in group["stages"]
    )

    return {
        "path": str(path),
        "recordCount": len(records),
        "runIds": sorted({as_text(record.get("runId")) for record in records if record.get("runId")}),
        "markets": sorted({as_text(record.get("marketId")) for record in records if record.get("marketId")}),
        "timeframes": sorted({as_text(record.get("timeframe")) for record in records if record.get("timeframe")}),
        "eventTypeCounts": dict(sorted(event_counts.items())),
        "unknownEventTypes": unknown_event_types,
        "sideFilter": side_filter,
        "liquidityLifecycle": {
            "uniqueBreaches": len(filtered_lifecycles),
            "stageCounts": dict(sorted(stage_counts.items())),
            "finalStageCounts": dict(sorted(final_counts.items())),
            "bySide": {
                side: dict(sorted(counter.items()))
                for side, counter in sorted(by_side.items())
            },
            "sweepCandidatesFormed": candidate_total,
            "sweepCandidatesToSweepConfirmed": candidate_outcomes["candidateToSweepConfirmed"],
            "sweepCandidatesToBreakoutConfirmed": candidate_outcomes["candidateToBreakoutConfirmed"],
            "sweepCandidatesToRejectionOnly": candidate_outcomes["candidateToRejectionOnly"],
            "sweepCandidatesStillOpen": candidate_outcomes["candidateStillOpen"],
            "sweepCandidatesToRejectionThenSweep": candidate_outcomes["candidateToRejectionThenSweep"],
            "breakoutsTotal": breakout_total,
            "sweepConfirmedTotal": sweep_confirmed_total,
            "breakoutsWithoutPriorCandidate": breakout_without_candidate,
            "rates": {
                "candidateToSweepConfirmedPercent": percent(
                    candidate_outcomes["candidateToSweepConfirmed"],
                    candidate_total,
                ),
                "candidateToBreakoutConfirmedPercent": percent(
                    candidate_outcomes["candidateToBreakoutConfirmed"],
                    candidate_total,
                ),
                "breachToBreakoutConfirmedPercent": percent(
                    breakout_total,
                    len(filtered_lifecycles),
                ),
                "breachToSweepConfirmedPercent": percent(
                    sweep_confirmed_total,
                    len(filtered_lifecycles),
                ),
            },
        },
    }


def percent(numerator: int, denominator: int) -> float | None:
    if denominator == 0:
        return None
    return round((numerator / denominator) * 100, 2)


def print_text(summary: dict[str, Any]) -> None:
    lifecycle = summary["liquidityLifecycle"]
    print("WickdBot backtest summary")
    print(f"Path: {summary['path']}")
    print(f"Records: {summary['recordCount']}")
    if summary["runIds"]:
        print(f"Run IDs: {', '.join(summary['runIds'])}")
    if summary["markets"]:
        print(f"Markets: {', '.join(summary['markets'])}")
    if summary["timeframes"]:
        print(f"Timeframes: {', '.join(summary['timeframes'])}")
    if summary["unknownEventTypes"]:
        print(
            "Schema warning: event types not in the current StructureEventType enum: "
            + ", ".join(summary["unknownEventTypes"])
        )

    print()
    print("Top event types:")
    event_counts = Counter(summary["eventTypeCounts"])
    for event_type, count in event_counts.most_common(12):
        print(f"  {event_type}: {count}")

    print()
    print("Liquidity lifecycle, counted by unique breach entityId:")
    print(f"  unique breaches: {lifecycle['uniqueBreaches']}")
    print(f"  sweep candidates formed: {lifecycle['sweepCandidatesFormed']}")
    print(f"  candidate -> sweepConfirmed: {lifecycle['sweepCandidatesToSweepConfirmed']}")
    print(f"  candidate -> breakoutConfirmed: {lifecycle['sweepCandidatesToBreakoutConfirmed']}")
    print(f"  candidate -> rejection only: {lifecycle['sweepCandidatesToRejectionOnly']}")
    print(f"  candidate still open: {lifecycle['sweepCandidatesStillOpen']}")
    print(f"  all sweepConfirmed: {lifecycle['sweepConfirmedTotal']}")
    print(f"  all breakoutConfirmed: {lifecycle['breakoutsTotal']}")
    print(f"  breakout without prior candidate: {lifecycle['breakoutsWithoutPriorCandidate']}")

    rates = lifecycle["rates"]
    print()
    print("Rates:")
    for name, value in rates.items():
        rendered = "n/a" if value is None else f"{value}%"
        print(f"  {name}: {rendered}")

    if lifecycle["bySide"]:
        print()
        print("By liquidity side:")
        for side, counts in lifecycle["bySide"].items():
            print(f"  {side}:")
            for key, value in counts.items():
                print(f"    {key}: {value}")


def main() -> int:
    args = parse_args()
    path = resolve_structures_path(args.path)
    records = read_jsonl(path)
    summary = summarize(path, records, args.side)
    if args.json:
        json.dump(summary, sys.stdout, indent=2, sort_keys=True)
        print()
    else:
        print_text(summary)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
