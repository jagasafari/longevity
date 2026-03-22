#!/usr/bin/env python3
# Usage: python report-results.py
# Reads the first TRX file under tests/, then posts:
#   - Availability telemetry  → App Insights  (APPINSIGHTS_CONNECTION_STRING secret)
#   - Custom log row          → Log Analytics  (LA_WORKSPACE_ID + LA_WORKSPACE_KEY secrets)
# Missing secrets are skipped gracefully; the script never fails the pipeline step.

import base64
import glob
import hashlib
import hmac
import json
import os
import sys
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from urllib.error import URLError
from urllib.request import Request, urlopen

_TRX_NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def parse_trx(path: str) -> dict:
    root = ET.parse(path).getroot()
    summary = root.find("t:ResultSummary", _TRX_NS)
    counters = summary.find("t:Counters", _TRX_NS)
    return {
        "outcome": summary.get("outcome", "Unknown"),
        "total": int(counters.get("total", 0)),
        "passed": int(counters.get("passed", 0)),
        "failed": int(counters.get("failed", 0)),
    }


def _post(url: str, headers: dict, body: bytes) -> None:
    urlopen(Request(url, data=body, headers=headers), timeout=10)


def post_appinsights_availability(
    conn_str: str, result: dict, run_id: str, branch: str, sha: str
) -> None:
    parts = dict(kv.split("=", 1) for kv in conn_str.split(";") if "=" in kv)
    ikey = parts["InstrumentationKey"]
    endpoint = parts["IngestionEndpoint"].rstrip("/")

    success = result["outcome"] == "Passed"
    message = f"{result['passed']}/{result['total']} tests passed"
    now = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.000Z")

    payload = {
        "name": f"Microsoft.ApplicationInsights.{ikey}.Availability",
        "time": now,
        "iKey": ikey,
        "tags": {"ai.cloud.role": "github-actions"},
        "data": {
            "baseType": "AvailabilityData",
            "baseData": {
                "ver": 2,
                "id": run_id,
                "name": "E2E Smoke",
                "duration": "00:00:30.000",
                "success": success,
                "runLocation": "GitHub Actions",
                "message": message,
                "properties": {"branch": branch, "commit": sha},
            },
        },
    }

    _post(
        f"{endpoint}/v2/track",
        {"Content-Type": "application/json"},
        json.dumps(payload).encode(),
    )
    print(f"App Insights: posted availability — {message}, success={success}")


def _la_auth_header(workspace_id: str, workspace_key: str, date: str, body: bytes) -> str:
    string_to_hash = f"POST\n{len(body)}\napplication/json\nx-ms-date:{date}\n/api/logs"
    decoded_key = base64.b64decode(workspace_key)
    sig = hmac.new(decoded_key, string_to_hash.encode("utf-8"), hashlib.sha256).digest()
    return f"SharedKey {workspace_id}:{base64.b64encode(sig).decode()}"


def post_log_analytics(
    workspace_id: str,
    workspace_key: str,
    result: dict,
    run_id: str,
    branch: str,
    sha: str,
) -> None:
    now_utc = datetime.now(timezone.utc)
    rfc1123date = now_utc.strftime("%a, %d %b %Y %H:%M:%S GMT")

    row = [
        {
            "TimeGenerated": now_utc.strftime("%Y-%m-%dT%H:%M:%SZ"),
            "TestSuite": "E2E Smoke",
            "Outcome": result["outcome"],
            "Passed": result["passed"],
            "Failed": result["failed"],
            "Total": result["total"],
            "Branch": branch,
            "CommitSha": sha,
            "RunId": run_id,
        }
    ]
    body = json.dumps(row).encode("utf-8")

    _post(
        f"https://{workspace_id}.ods.opinsights.azure.com/api/logs?api-version=2016-04-01",
        {
            "Content-Type": "application/json",
            "Authorization": _la_auth_header(workspace_id, workspace_key, rfc1123date, body),
            "Log-Type": "GitHubE2EResults",
            "x-ms-date": rfc1123date,
        },
        body,
    )
    print(f"Log Analytics: posted GitHubE2EResults — outcome={result['outcome']}")


def main() -> None:
    trx_files = glob.glob("tests/**/*.trx", recursive=True)
    if not trx_files:
        print("No TRX file found — skipping telemetry", file=sys.stderr)
        return

    result = parse_trx(trx_files[0])
    run_id = os.environ.get("GITHUB_RUN_ID", "local")
    branch = os.environ.get("GITHUB_REF_NAME", "unknown")
    sha = os.environ.get("GITHUB_SHA", "unknown")

    conn_str = os.environ.get("APPINSIGHTS_CONNECTION_STRING")
    if conn_str:
        try:
            post_appinsights_availability(conn_str, result, run_id, branch, sha)
        except (URLError, KeyError) as e:
            print(f"App Insights: failed — {e}", file=sys.stderr)
    else:
        print("APPINSIGHTS_CONNECTION_STRING not set — skipping App Insights", file=sys.stderr)

    la_id = os.environ.get("LA_WORKSPACE_ID")
    la_key = os.environ.get("LA_WORKSPACE_KEY")
    if la_id and la_key:
        try:
            post_log_analytics(la_id, la_key, result, run_id, branch, sha)
        except (URLError, ValueError) as e:
            print(f"Log Analytics: failed — {e}", file=sys.stderr)
    else:
        print("LA_WORKSPACE_ID / LA_WORKSPACE_KEY not set — skipping Log Analytics", file=sys.stderr)

    print(f"Result: {result['outcome']} ({result['passed']}/{result['total']} passed)")


if __name__ == "__main__":
    main()
