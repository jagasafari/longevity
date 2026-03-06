# Usage: python builder.py [workbook.yaml] [output_path] [workspace_resource_id]
import json, subprocess, sys
from pathlib import Path

args        = sys.argv[1:]
config_path = Path(args[0]) if args else Path(__file__).parent / "workbook.yaml"

cfg = json.loads(subprocess.run(
    ["yq", "-o=json", str(config_path)],
    capture_output=True, text=True, check=True
).stdout)

queries_dir  = config_path.parent / cfg.get("queries_dir", "queries")
default_out  = config_path.parent.parent / "modules" / "workbook.serialized.json"
output_path  = Path(args[1]) if len(args) > 1 else Path(cfg.get("output", default_out))
workspace_id = args[2] if len(args) > 2 else ""

def query_item(i, spec):
    query = (queries_dir / spec["file"]).read_text().strip()
    return {
        "type": 3,
        "content": {
            "version": cfg["kql_item_version"],
            "query": query,
            "size": 0,
            "showAnalytics": True,
            "title": spec["title"],
            "timeContext": {"durationMs": cfg["time_context_ms"]},
            "showRefreshButton": True,
            "showExportToExcel": True,
            "queryType": 0,
            "resourceType": cfg["resource_type"],
            "visualization": spec["visualization"],
        },
        "name": f"query - {i} - {spec['title']}",
    }

workbook = {
    "version": cfg["workbook_version"],
    "items": [query_item(i, s) for i, s in enumerate(cfg["queries"])],
    "isLocked": False,
    "autoRefresh": {"enabled": True, "interval": 5},
    **({"fallbackResourceIds": [workspace_id]} if workspace_id else {}),
    "$schema": cfg["schema"],
}

output_path.parent.mkdir(parents=True, exist_ok=True)
output_path.write_text(json.dumps(workbook, separators=(",", ":")))
print(f"Workbook JSON generated: {output_path}")
