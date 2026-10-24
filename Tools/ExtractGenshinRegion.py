"""Extract a declared region, audit its assets, and prepare a Unity import plan."""

import argparse
from datetime import datetime, timezone
import hashlib
import io
import json
from pathlib import Path
import re
import shutil
import struct
import subprocess
import sys


TYPE_IDS = {"Mesh": 43, "Texture2D": 28}
EXTENSIONS = {"Mesh": ".obj", "Texture2D": ".png"}


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_new_json(path, value):
    content = json.dumps(value, ensure_ascii=False, indent=2) + "\n"
    if path.exists():
        if path.read_text(encoding="utf-8") != content:
            raise RuntimeError(f"Refusing to replace existing output: {path}")
        return
    with path.open("x", encoding="utf-8", newline="\n") as output:
        output.write(content)


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def project_path(root, relative):
    path = (root / relative).resolve()
    if not path.is_relative_to(root):
        raise ValueError(f"Path leaves the project: {relative}")
    return path


def index_rows(path):
    try:
        import lz4.block
        import msgpack
    except ImportError as error:
        raise RuntimeError("Install msgpack and lz4, or pass --python-deps with their existing directory.") from error
    outer = msgpack.unpackb(path.read_bytes(), raw=False, strict_map_key=False)
    extension = outer[0]
    if not isinstance(extension, msgpack.ExtType) or extension.code != 98:
        raise ValueError("Expected an AnimeStudio MessagePack LZ4 block-array index.")
    sizes = list(msgpack.Unpacker(io.BytesIO(extension.data), raw=False))
    if len(sizes) != len(outer) - 1:
        raise ValueError("Invalid compressed block table.")
    unpacker = msgpack.Unpacker(raw=False, strict_map_key=False, max_buffer_size=16 * 1024 * 1024)
    seen = 0
    for number, (part, size) in enumerate(zip(outer[1:], sizes)):
        unpacker.feed(lz4.block.decompress(part, uncompressed_size=size))
        if number == 0:
            if unpacker.read_array_header() != 2:
                raise ValueError("Invalid index root.")
            unpacker.unpack()
            expected = unpacker.read_array_header()
        for row in unpacker:
            seen += 1
            yield row
    if seen != expected:
        raise ValueError(f"Incomplete index: {seen} of {expected} records.")


def create_plan(root, config, profile_id):
    profile = config["profiles"][profile_id]
    if not re.fullmatch(r"[a-zA-Z0-9_-]+", profile_id):
        raise ValueError("Invalid profile ID.")
    requested = {(a["name"], TYPE_IDS[a["kind"]]): a for a in profile["assets"]}
    matches = {key: [] for key in requested}
    for row in index_rows(Path(config["assetMap"])):
        key = (row[0], row[4])
        if key in matches:
            matches[key].append(row)
    records = []
    for key, asset in requested.items():
        unique = {(r[2], r[3], r[5], r[6]): r for r in matches[key]}
        if len(unique) != 1:
            raise ValueError(f"Expected one source for {asset['name']}; found {len(unique)}.")
        row = next(iter(unique.values()))
        if not Path(row[2]).is_file():
            raise FileNotFoundError(row[2])
        records.append({**asset, "source": row[2], "pathId": row[3], "bundle": row[5], "offset": row[6]})
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    run = project_path(root, config["stagingRoot"]) / profile_id / stamp
    run.mkdir(parents=True)
    version = subprocess.run([config["cliPath"], "--version"], capture_output=True, text=True, check=True).stdout.strip()
    plan = {"schemaVersion": 1, "profile": profile, "assets": records,
            "assetRoot": config["assetRoot"], "materialRoot": config["materialRoot"], "sceneRoot": config["sceneRoot"],
            "cliPath": config["cliPath"], "cliVersion": version, "assetMap": config["assetMap"]}
    write_new_json(run / "extraction_plan.json", plan)
    return run


def extract(run, plan):
    output = run / "extracted"
    if output.exists():
        # Resume only complete exports. Partial runs get a fresh plan instead of
        # allowing the extractor to overwrite an uncertain set of files.
        for asset in plan["assets"]:
            expected = output / asset["kind"] / (asset["name"] + EXTENSIONS[asset["kind"]])
            if not expected.is_file():
                raise RuntimeError("Partial export exists; create a fresh plan.")
        return
    pattern = "^(?:" + "|".join(re.escape(a["name"]) for a in plan["assets"]) + ")$"
    args = [plan["cliPath"], plan["assetMap"], str(output), "--game", "GI",
            "--map_op", "AssetMap, Load", "--map_type", "MessagePack", "--map_name", plan["assetMap"],
            "--types", *sorted({a["kind"] for a in plan["assets"]}),
            "--names", pattern, "--group_assets", "ByType", "--export_type", "Convert"]
    log_path = run / "extract.log"
    with log_path.open("x", encoding="utf-8") as log:
        process = subprocess.run(args, stdout=log, stderr=subprocess.STDOUT, timeout=600)
    log = log_path.read_text(encoding="utf-8", errors="replace")
    # AnimeStudio can return exit code zero after individual export failures.
    if process.returncode or re.search(r"\[Error\]|Error while|Exception", log, re.IGNORECASE):
        raise RuntimeError(f"Extraction failed; inspect {log_path}")


def geometry(path):
    minimum = [float("inf")] * 3
    maximum = [float("-inf")] * 3
    vertices = triangles = uv = 0
    largest_index = 0
    for line in path.read_text(encoding="utf-8-sig").splitlines():
        parts = line.split()
        if not parts:
            continue
        if parts[0] == "v":
            vertices += 1
            for axis, value in enumerate(map(float, parts[1:4])):
                if not float("-inf") < value < float("inf"):
                    raise ValueError(f"Nonfinite vertex in {path}")
                minimum[axis] = min(minimum[axis], value)
                maximum[axis] = max(maximum[axis], value)
        elif parts[0] == "vt":
            uv += 1
        elif parts[0] == "f":
            triangles += len(parts) - 3
            indices = [int(value.split("/")[0]) for value in parts[1:]]
            if min(indices) < 1:
                raise ValueError(f"Expected absolute OBJ indices: {path}")
            largest_index = max(largest_index, max(indices))
    if not vertices or not triangles or largest_index > vertices or not uv:
        raise ValueError(f"Invalid or incomplete OBJ: {path}")
    return {"vertices": vertices, "triangles": triangles, "min": minimum, "max": maximum}


def audit(run, plan):
    files = []
    for asset in plan["assets"]:
        path = run / "extracted" / asset["kind"] / (asset["name"] + EXTENSIONS[asset["kind"]])
        if not path.is_file():
            raise FileNotFoundError(path)
        details = geometry(path) if asset["kind"] == "Mesh" else {}
        if asset["kind"] == "Texture2D":
            header = path.read_bytes()[:24]
            if len(header) < 24 or header[:8] != b"\x89PNG\r\n\x1a\n":
                raise ValueError(f"Invalid PNG: {path}")
            details["width"], details["height"] = struct.unpack(">II", header[16:24])
        files.append({**asset, "stagedPath": str(path.resolve()), "sha256": digest(path), "bytes": path.stat().st_size, **details})
    result = {"schemaVersion": 1, "complete": True, "files": files}
    write_new_json(run / "audit.json", result)
    return result


def sync(root, run, plan, result):
    if not plan["assetRoot"].startswith("Assets/Art/Map/"):
        raise ValueError("Legacy flat-directory plan; create a fresh plan for the map folders.")
    destination = project_path(root, plan["assetRoot"])
    if not destination.is_dir():
        raise FileNotFoundError(destination)
    terrain_assets = {name for mesh in plan["profile"]["meshes"] if mesh["role"] == "Terrain"
                      for name in (mesh["name"], mesh["texture"])}
    prepared = []
    for asset in result["files"]:
        source = Path(asset["stagedPath"])
        category = "Models" if asset["kind"] == "Mesh" else "Textures"
        if asset["name"] in terrain_assets:
            category = "Terrain/Meshes" if asset["kind"] == "Mesh" else "Terrain/Textures"
        target = destination / category / source.name
        if not target.parent.is_dir():
            raise FileNotFoundError(target.parent)
        if digest(source) != asset["sha256"]:
            raise ValueError(f"Staged file changed after audit: {source}")
        if target.exists() and digest(target) != asset["sha256"]:
            raise RuntimeError(f"Refusing to replace different project asset: {target}")
        prepared.append((source, target, asset))
    files = []
    for source, target, asset in prepared:
        if not target.exists():
            shutil.copyfile(source, target)
        files.append({**asset, "assetPath": target.relative_to(root).as_posix()})
    imported = {"schemaVersion": 1, "profile": plan["profile"], "files": files,
                "materialRoot": plan["materialRoot"], "assetRoot": plan["assetRoot"], "sceneRoot": plan["sceneRoot"]}
    write_new_json(run / "import_plan.json", imported)
    return imported


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mode", choices=["plan", "extract", "audit", "sync", "all"], default="plan")
    parser.add_argument("--manifest", type=Path, default=Path(__file__).with_name("GenshinRegionManifest.json"))
    parser.add_argument("--profile")
    parser.add_argument("--run", type=Path)
    parser.add_argument("--python-deps", type=Path)
    args = parser.parse_args()
    if args.python_deps:
        sys.path.insert(0, str(args.python_deps.resolve()))
    root = Path(__file__).resolve().parents[1]
    config = read_json(args.manifest)
    profile = args.profile or config["activeProfile"]
    run = args.run.resolve() if args.run else None
    if args.mode in ("plan", "all"):
        run = create_plan(root, config, profile)
    if run is None or not run.is_relative_to(project_path(root, config["stagingRoot"])):
        raise ValueError("--run must point to a run beneath the configured staging root.")
    plan = read_json(run / "extraction_plan.json")
    if args.mode in ("extract", "all"):
        extract(run, plan)
    result = audit(run, plan) if args.mode in ("audit", "sync", "all") else None
    if args.mode in ("sync", "all"):
        sync(root, run, plan, result)
    print(json.dumps({"mode": args.mode, "run": str(run), "profile": plan["profile"]["id"],
                      "assetCount": len(plan["assets"]), "nextUnityMenu": "Tools/Genshin Region/Build Latest Extraction"}, ensure_ascii=False))


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(f"ERROR: {error}", file=sys.stderr)
        sys.exit(1)
