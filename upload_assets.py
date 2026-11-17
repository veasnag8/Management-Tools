import os
import sys
import json
import urllib.request
import subprocess

def get_git_token():
    p = subprocess.Popen(['git', 'credential', 'fill'], stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True)
    out, _ = p.communicate('protocol=https\nhost=github.com\n\n')
    for line in out.splitlines():
        if line.startswith('password='):
            return line.split('=', 1)[1]
    raise RuntimeError("GitHub token not found in git credential manager")

def main():
    token = get_git_token()
    release_id = 444342267
    base_headers = {
        'Authorization': f'token {token}',
        'User-Agent': 'ToolDubber-Release'
    }

    print(f"Checking existing assets for release {release_id}...")
    req = urllib.request.Request(f'https://api.github.com/repos/veasnag8/Management-Tools/releases/{release_id}', headers=base_headers)
    with urllib.request.urlopen(req) as resp:
        data = json.loads(resp.read().decode('utf-8'))
        for a in data.get('assets', []):
            print(f"Deleting previous asset: {a['name']} (ID: {a['id']})...")
            del_req = urllib.request.Request(
                f"https://api.github.com/repos/veasnag8/Management-Tools/releases/assets/{a['id']}",
                headers=base_headers,
                method='DELETE'
            )
            urllib.request.urlopen(del_req)
            print("Deleted.")

    # 1. Upload Patch ZIP
    patch_path = r'D:\Project\Release_v1.2.0\Output\ToolDubber-Patch-v2.1.2.zip'
    patch_size = os.path.getsize(patch_path)
    print(f"\nUploading Patch: {patch_path} ({patch_size} bytes)...")
    with open(patch_path, 'rb') as f:
        patch_data = f.read()

    patch_url = f"https://uploads.github.com/repos/veasnag8/Management-Tools/releases/{release_id}/assets?name=ToolDubber-Patch-v2.1.2.zip"
    patch_headers = dict(base_headers)
    patch_headers['Content-Type'] = 'application/zip'
    patch_headers['Content-Length'] = str(patch_size)

    req_patch = urllib.request.Request(patch_url, data=patch_data, headers=patch_headers, method='POST')
    with urllib.request.urlopen(req_patch) as resp:
        res = json.loads(resp.read().decode('utf-8'))
        print(f"✓ Patch uploaded successfully: {res.get('browser_download_url')} (size: {res.get('size')})")

    # 2. Upload Setup EXE
    setup_path = r'D:\Project\Release_v1.2.0\Output\ToolDubber-Setup-v2.1.2.exe'
    setup_size = os.path.getsize(setup_path)
    print(f"\nUploading Setup EXE: {setup_path} ({setup_size} bytes)... This may take 1-2 minutes.")
    with open(setup_path, 'rb') as f:
        setup_data = f.read()

    setup_url = f"https://uploads.github.com/repos/veasnag8/Management-Tools/releases/{release_id}/assets?name=ToolDubber-Setup-v2.1.2.exe"
    setup_headers = dict(base_headers)
    setup_headers['Content-Type'] = 'application/octet-stream'
    setup_headers['Content-Length'] = str(setup_size)

    req_setup = urllib.request.Request(setup_url, data=setup_data, headers=setup_headers, method='POST')
    with urllib.request.urlopen(req_setup, timeout=300) as resp:
        res = json.loads(resp.read().decode('utf-8'))
        print(f"✓ Setup uploaded successfully: {res.get('browser_download_url')} (size: {res.get('size')})")

    print("\nAll assets for v2.1.2 successfully uploaded and verified on GitHub!")

if __name__ == '__main__':
    main()
