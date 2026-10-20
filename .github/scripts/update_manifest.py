"""Add (or replace) a version entry in the Jellyfin plugin repository manifest."""

import argparse
import datetime
import json
from pathlib import Path

TARGET_ABI = "12.1.0.0"

parser = argparse.ArgumentParser()
parser.add_argument("--version", required=True)
parser.add_argument("--checksum", required=True)
parser.add_argument("--url", required=True)
parser.add_argument("--changelog", default="")
args = parser.parse_args()

path = Path("manifest.json")
manifest = json.loads(path.read_text(encoding="utf-8"))
plugin = manifest[0]

entry = {
    "version": args.version,
    "changelog": args.changelog,
    "targetAbi": TARGET_ABI,
    "sourceUrl": args.url,
    "checksum": args.checksum,
    "timestamp": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
}

plugin["versions"] = [entry] + [v for v in plugin.get("versions", []) if v["version"] != args.version]
path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
print(f"manifest: {args.version} -> {args.url}")
