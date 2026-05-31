#!/usr/bin/env python3
"""
Reconstruct vocabulary subgroups for existing 107 photos by pairing
Netflix screenshot files with ChatGPT AI drawing files taken within
120 seconds of each other in the same vocabulary group.

Run with the port-forward active:
  kubectl port-forward -n longevity svc/postgres-svc 5434:5432

Usage:
  python3 reconstruct_subgroups.py [--dry-run]
"""

import re
import sys
import uuid
from collections import defaultdict
from datetime import datetime

import psycopg2

DB = dict(
    host=os.environ.get("PGHOST", "localhost"),
    port=int(os.environ.get("PGPORT", "5434")),
    dbname=os.environ.get("PGDATABASE", "longevity"),
    user=os.environ.get("PGUSER", "longevity"),
    password=os.environ["PGPASSWORD"],
)
PAIR_WINDOW_SECS = 120
DRY_RUN = "--dry-run" in sys.argv


def parse_screenshot_ts(name: str) -> datetime | None:
    m = re.match(r"Screenshot_(\d{8})_(\d{6})_", name)
    if m:
        try:
            return datetime.strptime(m.group(1) + m.group(2), "%Y%m%d%H%M%S")
        except ValueError:
            return None
    return None


def parse_chatgpt_ts(name: str) -> datetime | None:
    m = re.match(r"ChatGPT Image (.+?)\.png$", name)
    if not m:
        return None
    ts_str = m.group(1)
    # "Feb 24, 2026, 07_54_08 PM" -> "Feb 24, 2026, 07:54:08 PM"
    ts_str = re.sub(r"(\d+)_(\d+)_(\d+) (AM|PM)", r"\1:\2:\3 \4", ts_str)
    try:
        return datetime.strptime(ts_str, "%b %d, %Y, %I:%M:%S %p")
    except ValueError:
        return None


def get_ts(name: str) -> datetime | None:
    return parse_screenshot_ts(name) or parse_chatgpt_ts(name)


conn = psycopg2.connect(**DB)
cur = conn.cursor()

cur.execute(
    "SELECT photo_name, group_id FROM vocabulary.photos WHERE subgroup_id IS NULL ORDER BY group_id"
)
rows = cur.fetchall()
print(f"Photos without subgroup_id: {len(rows)}")

by_group: dict[str, list[str]] = defaultdict(list)
for photo_name, group_id in rows:
    by_group[group_id].append(photo_name)

updates: list[tuple[str, str]] = []
unpaired: list[str] = []
unparseable_names: list[str] = []

for group_id, photos in by_group.items():
    photos_with_ts = [(p, get_ts(p)) for p in photos]
    parseable = sorted(
        [(p, ts) for p, ts in photos_with_ts if ts is not None],
        key=lambda x: x[1],
    )
    for p, _ in photos_with_ts:
        if get_ts(p) is None:
            unparseable_names.append(p)

    used: set[str] = set()
    for i, (p1, ts1) in enumerate(parseable):
        if p1 in used:
            continue
        best_j: int | None = None
        best_diff: float | None = None
        for j in range(i + 1, min(i + 6, len(parseable))):
            p2, ts2 = parseable[j]
            if p2 in used:
                continue
            diff = abs((ts2 - ts1).total_seconds())
            if diff <= PAIR_WINDOW_SECS and (best_diff is None or diff < best_diff):
                best_j = j
                best_diff = diff
        if best_j is not None:
            p2 = parseable[best_j][0]
            sub_id = uuid.uuid4().hex
            updates.append((sub_id, p1))
            updates.append((sub_id, p2))
            used.add(p1)
            used.add(p2)
        else:
            unpaired.append(p1)

pairs = len(updates) // 2
print(f"Pairs found:   {pairs}")
print(f"Unpaired:      {len(unpaired)}")
print(f"Unparseable:   {len(unparseable_names)}")

if unparseable_names:
    print("  Unparseable filenames:")
    for n in unparseable_names:
        print(f"    {n}")

if unpaired:
    print("  Unpaired photos:")
    for n in unpaired:
        print(f"    {n}")

if DRY_RUN:
    print("\nDry-run — no changes written.")
    cur.close()
    conn.close()
    sys.exit(0)

if updates:
    cur.executemany(
        "UPDATE vocabulary.photos SET subgroup_id = %s WHERE photo_name = %s",
        updates,
    )
    conn.commit()
    print(f"\nUpdated {len(updates)} rows ({pairs} pairs).")
else:
    print("\nNothing to update.")

cur.close()
conn.close()
