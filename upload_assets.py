import argparse
import hashlib
import json
import os
import subprocess
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path


REPOSITORY = "veasnag8/Management-Tools"
API_ROOT = f"https://api.github.com/repos/{REPOSITORY}"


def get_git_token() -> str:
    process = subprocess.Popen(
        ["git", "credential", "fill"],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,
        text=True,
    )
    output, _ = process.communicate("protocol=https\nhost=github.com\n\n")
    if process.returncode != 0:
        raise RuntimeError("Unable to query the GitHub credential manager")
    for line in output.splitlines():
        if line.startswith("password="):
            return line.split("=", 1)[1]
    raise RuntimeError("GitHub credentials were not found in the credential manager")


def request_json(url: str, headers: dict, data: bytes = None) -> dict:
    request_headers = dict(headers)
    if data is not None:
        request_headers["Content-Type"] = "application/json"
    request = urllib.request.Request(url, data=data, headers=request_headers)
    with urllib.request.urlopen(request) as response:
        return json.loads(response.read().decode("utf-8"))


def get_or_create_release(tag: str, version: str, headers: dict) -> dict:
    tag_url = f"{API_ROOT}/releases/tags/{urllib.parse.quote(tag, safe='')}"
    try:
        return request_json(tag_url, headers)
    except urllib.error.HTTPError as error:
        if error.code != 404:
            raise

    payload = json.dumps(
        {
            "tag_name": tag,
            "target_commitish": "main",
            "name": f"Tool Dubber v{version}",
            "body": (
                "Cancelling video export with X now stops its FFmpeg process, "
                "waits for it to exit, and deletes the incomplete output so a "
                "later export starts cleanly. Also fixes text-layer layout stack "
                "overflow that could close the app during translation and fixes "
                "no-overlap status button styling, "
                "Srey Mom voice changes by "
                "regenerating cached audio with the selected voice; highlights "
                "and navigates overlapping dialogue; allows start/end time "
                "editing; and removes Gemini Keys buttons. Includes the full "
                "installer and fast patch for existing installations."
            ),
            "draft": False,
            "prerelease": False,
        }
    ).encode("utf-8")
    return request_json(f"{API_ROOT}/releases", headers, payload)


def upload_asset(release: dict, asset_path: Path, headers: dict) -> None:
    asset_name = asset_path.name
    content = asset_path.read_bytes()
    digest = hashlib.sha256(content).hexdigest()

    existing = next(
        (asset for asset in release.get("assets", []) if asset["name"] == asset_name),
        None,
    )
    if existing:
        if existing.get("digest") == f"sha256:{digest}" and existing.get("size") == len(content):
            print(f"Already uploaded and verified: {asset_name}")
            return
        raise RuntimeError(
            f"Release already has a different {asset_name}; remove or inspect it manually."
        )

    upload_url = (
        f"https://uploads.github.com/repos/{REPOSITORY}/releases/"
        f"{release['id']}/assets?name={urllib.parse.quote(asset_name)}"
    )
    upload_headers = dict(headers)
    upload_headers["Content-Type"] = (
        "application/zip" if asset_path.suffix.lower() == ".zip" else "application/octet-stream"
    )
    upload_headers["Content-Length"] = str(len(content))
    print(f"Uploading {asset_name} ({len(content)} bytes)...", flush=True)
    request = urllib.request.Request(
        upload_url,
        data=content,
        headers=upload_headers,
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=3600) as response:
        uploaded = json.loads(response.read().decode("utf-8"))
    if uploaded.get("digest") != f"sha256:{digest}" or uploaded.get("size") != len(content):
        raise RuntimeError(f"GitHub asset verification failed for {asset_name}")
    print(f"Uploaded and verified: {asset_name}")


def main() -> None:
    parser = argparse.ArgumentParser(description="Publish Tool Dubber release assets.")
    parser.add_argument(
        "--release-dir",
        default=r"C:\VEASNA\Release",
        help="Directory containing the generated setup EXE and patch ZIP.",
    )
    args = parser.parse_args()

    manifest_path = Path(__file__).with_name("version.json")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    version = manifest["latest_version"]
    tag = f"v{version}"
    release_dir = Path(args.release_dir)
    assets = [
        release_dir / f"ToolDubber-Patch-v{version}.zip",
        release_dir / f"ToolDubber-Setup-v{version}.exe",
    ]
    for asset in assets:
        if not asset.is_file():
            raise FileNotFoundError(f"Release asset was not found: {asset}")

    token = get_git_token()
    headers = {
        "Authorization": f"Bearer {token}",
        "Accept": "application/vnd.github+json",
        "User-Agent": "ToolDubber-Release",
        "X-GitHub-Api-Version": "2022-11-28",
    }
    release = get_or_create_release(tag, version, headers)
    if release.get("tag_name") != tag:
        raise RuntimeError(f"Expected release tag {tag}, got {release.get('tag_name')}")

    for asset in assets:
        upload_asset(release, asset, headers)

    verified = request_json(f"{API_ROOT}/releases/{release['id']}", headers)
    for asset in assets:
        uploaded = next(
            (item for item in verified.get("assets", []) if item["name"] == asset.name),
            None,
        )
        if uploaded is None or uploaded.get("state") != "uploaded":
            raise RuntimeError(f"GitHub release is missing uploaded asset {asset.name}")
    print(f"Release assets are live: {verified['html_url']}")


if __name__ == "__main__":
    main()
