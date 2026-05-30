# Usage: python builder.py [workbook.yaml] [output_path] [workspace_resource_id]
import json, subprocess, sys, uuid
from pathlib import Path

PARAMETER_ID_NAMESPACE = uuid.UUID("9c7d99ff-bdcf-4f07-a4f5-0019e3034f64")

def stable_parameter_id(name):
    return str(uuid.uuid5(PARAMETER_ID_NAMESPACE, name))

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

def build_param(p):
    kind = p.get("kind", "timerange")
    if kind == "dropdown":
        multi = p.get("multi_select", False)
        return {
            "id": stable_parameter_id(p["name"]),
            "version": cfg["kql_item_version"],
            "name": p["name"],
            "type": 2,
            "isRequired": p.get("required", True),
            "multiSelect": multi,
            **({"quote": "'"} if multi else {}),
            **({"delimiter": ","} if multi else {}),
            "typeSettings": {
                "additionalResourceOptions": [],
                "showDefault": False,
            },
            "jsonData": json.dumps([{"value": v, "label": v} for v in p["values"]]),
            "value": p.get("default", p["values"][0]),
        }
    return {
        "id": stable_parameter_id(p["name"]),
        "version": cfg["kql_item_version"],
        "name": p["name"],
        "type": p["type"],
        "isRequired": p.get("required", True),
        "typeSettings": {
            "selectableValues": [{"durationMs": ms} for ms in p.get("selectable_values_ms", [])],
            "allowCustom": p.get("allow_custom", True),
        },
        "timeContext": {"durationMs": p.get("default_duration_ms", 86400000)},
        "value": {"durationMs": p.get("default_duration_ms", 86400000)},
    }

def param_item(params):
    parameters = [build_param(p) for p in params]
    return {
        "type": 9,
        "content": {
            "version": cfg["kql_item_version"],
            "parameters": parameters,
            "style": params[0].get("style", "pills"),
            "queryType": 0,
            "resourceType": cfg["resource_type"],
        },
        "name": "parameters - 0",
    }

def query_item(i, spec, time_param_name):
    query = (queries_dir / spec["file"]).read_text().strip()
    column_formatters = spec.get("column_formatters") or {}
    sort_by_col = spec.get("sort_by")
    grid_settings = {}
    if column_formatters or sort_by_col:
        grid_settings["gridSettings"] = {
            **({
                "formatters": [
                    {"columnMatch": col, "formatter": 0, "formatOptions": {"customColumnWidthSetting": width}}
                    for col, width in column_formatters.items()
                ]
            } if column_formatters else {}),
            **({
                "sortBy": [{"itemKey": sort_by_col, "sortOrder": 2}]
            } if sort_by_col else {}),
        }
    sort_by = ({"sortBy": [{"itemKey": sort_by_col, "sortOrder": 2}]} if sort_by_col else {})
    return {
        "type": 3,
        "content": {
            "version": cfg["kql_item_version"],
            "query": query,
            "size": 0,
            "showAnalytics": True,
            "title": spec["title"],
            "timeContext": {"durationMs": 0},
            **({"timeContextFromParameter": time_param_name}
               if time_param_name else {}),
            "showRefreshButton": True,
            "showExportToExcel": True,
            "queryType": 0,
            "resourceType": cfg["resource_type"],
            "visualization": spec["visualization"],
            **grid_settings,
            **sort_by,
        },
        "name": f"query - {i} - {spec['title']}",
    }

def group_item(i, spec, query):
    return {
        "type": 12,
        "content": {
            "version": "NotebookGroup/1.0",
            "groupType": "editable",
            "title": spec["title"],
            "expandable": True,
            "expanded": spec.get("expanded", False),
            "items": [query],
        },
        "name": f"group - {i} - {spec['title']}",
    }

def clock_item():
    return {
        "type": 1,
        "content": {
            "json": "Local time: **{now:hh:mm tt}** &nbsp;&nbsp; {now:dddd, d MMMM yyyy}",
        },
        "name": "text - local time",
    }

params = cfg.get("parameters", [])
time_param_name = next(
    (p["name"] for p in params if p.get("kind", "timerange") == "timerange"),
    None
)
foldable_sections = cfg.get("foldable_sections", False)
show_clock = cfg.get("show_local_time", False)
items = []
if params:
    items.append(param_item(params))
if show_clock:
    items.append(clock_item())
for i, spec in enumerate(cfg["queries"]):
    query = query_item(i, spec, time_param_name)
    items.append(group_item(i, spec, query) if foldable_sections else query)

workbook = {
    "version": cfg["workbook_version"],
    "items": items,
    "isLocked": False,
    "autoRefresh": {"enabled": True, "interval": 5},
    **({"fallbackResourceIds": [workspace_id]} if workspace_id else {}),
    "$schema": cfg["schema"],
}

output_path.parent.mkdir(parents=True, exist_ok=True)
output_path.write_text(json.dumps(workbook, separators=(",", ":")))
print(f"Workbook JSON generated: {output_path}")
