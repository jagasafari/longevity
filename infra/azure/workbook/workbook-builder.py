
import json, sys
from pathlib import Path

QUERIES_DIR = Path(__file__).parent / "queries"
DEFAULT_OUTPUT = Path(__file__).parent.parent / "modules" / "workbook.serialized.json"

QUERY_SPECS = [
    ("01-ingress-requests-all.kql", "All Incoming Requests (Ingress)",   "table"),
    ("02-route-split.kql",          "Traffic Split (Frontend vs Backend)", "timechart"),
    ("03-status-codes.kql",         "HTTP Status Code Distribution",      "barchart"),
    ("04-pod-bytes-in.kql",         "Pod Network Bytes In",               "timechart"),
    ("05-pod-health.kql",           "Pod Health Snapshot",                "table"),
    ("06-kube-events.kql",          "Kubernetes Events",                  "table"),
]

args          = sys.argv[1:]
output_path   = Path(args[0]) if args else DEFAULT_OUTPUT
workspace_id  = args[1] if len(args) > 1 else ""

def query_item(i, filename, title, visualization):
    query = (QUERIES_DIR / filename).read_text().strip()
    return {
        "type": 3,
        "content": {
            "version": "KqlItem/1.0",
            "query": query,
            "size": 0,
            "title": title,
            "timeContext": {"durationMs": 86400000},
            "queryType": 0,
            "resourceType": "microsoft.operationalinsights/workspaces",
            "visualization": visualization,
        },
        "name": f"query - {i} - {title}",
    }

workbook = {
    "version": "Notebook/1.0",
    "items": [query_item(i, f, t, v) for i, (f, t, v) in enumerate(QUERY_SPECS)],
    "isLocked": False,
    **({"fallbackResourceIds": [workspace_id]} if workspace_id else {}),
    "$schema": "https://github.com/Microsoft/Application-Insights-Workbooks/blob/master/schema/workbook.json",
}

output_path.parent.mkdir(parents=True, exist_ok=True)
output_path.write_text(json.dumps(workbook, separators=(",", ":")))
print(f"Workbook JSON generated: {output_path}")
