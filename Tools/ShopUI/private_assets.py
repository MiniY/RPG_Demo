"""Restore the exact locally licensed Shop UI payloads before opening Unity.

Only hashes, Unity identity metadata and this tool are public. The kit is not a
download source or a grant of a third-party license.
"""
from pathlib import Path, PurePosixPath
import argparse
import hashlib
import json
import os
import re
import stat
import subprocess
import sys
import tempfile


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def safe_file(root, name):
    relative = PurePosixPath(name)
    if (not name or '\\' in name or ':' in name or relative.is_absolute() or
            any(p in ('', '.', '..') for p in name.split('/'))):
        raise RuntimeError('UnsafePrivateAssetPath')
    root = Path(root).resolve()
    target = root.joinpath(*relative.parts)
    current = target
    while current != root:
        try:
            attributes = current.lstat()
        except FileNotFoundError:
            attributes = None
        if attributes is not None and (stat.S_ISLNK(attributes.st_mode) or
                getattr(attributes, 'st_file_attributes', 0) & getattr(stat, 'FILE_ATTRIBUTE_REPARSE_POINT', 0)):
            raise RuntimeError('PrivateAssetReparsePointRejected')
        current = current.parent
    if not target.resolve().is_relative_to(root):
        raise RuntimeError('PrivateAssetPathEscapesRoot')
    return target


def load_manifest(path):
    body = Path(path).read_bytes()
    manifest = json.loads(body.decode('utf-8'))
    if manifest.get('schema') != 1 or manifest.get('kind') != 'SHOP_PRIVATE_PAYLOADS_V1':
        raise RuntimeError('PrivateAssetManifestFormat')
    seen = set()
    for row in manifest['files']:
        name = row['path']
        safe_file(Path(path).parent, name)
        if (name in seen or not (name == 'Assets/Font/MyTMP.asset' or
                (name.startswith('Assets/ShopUiNightRun/') and name.endswith('.png'))) or
                not re.fullmatch('[0-9a-f]{64}', row['sha256']) or
                not re.fullmatch('[0-9a-f]{64}', row['metaSha256']) or
                not re.fullmatch('[0-9a-f]{32}', row['guid']) or type(row['bytes']) is not int):
            raise RuntimeError('PrivateAssetManifestIdentity')
        seen.add(name)
    if not seen:
        raise RuntimeError('EmptyPrivateAssetManifest')
    return manifest, body


def verify_metadata(project, manifest):
    """Verify public Unity identity without claiming to inspect licensed pixels."""
    for row in manifest['files']:
        meta = safe_file(project, row['path'] + '.meta')
        if not meta.is_file() or sha(meta) != row['metaSha256'] or not re.search(
                r'^guid: ' + row['guid'] + r'\r?$', meta.read_text('utf-8-sig'), re.M):
            raise RuntimeError('PrivateAssetMetadataMismatch: ' + row['path'])


def inspect(project, manifest):
    verify_metadata(project, manifest)
    result = []
    for row in manifest['files']:
        target = safe_file(project, row['path'])
        if target.exists():
            if not target.is_file() or sha(target) != row['sha256'] or target.stat().st_size != row['bytes']:
                raise RuntimeError('PrivateAssetExistingBytesConflict: ' + row['path'])
            state = 'EXACT'
        else:
            state = 'MISSING'
        result.append({'path': row['path'], 'state': state})
    return result


def idle(project):
    sys.path.insert(0, str(Path(project) / 'Tools/EngineeringAssurance'))
    import unity_guard
    state = unity_guard.status(Path(project))
    if state['state'] != 'FREE':
        raise RuntimeError('PrivateAssetRestoreRequiresIdleUnity: ' + state['state'])


def restore(project, manifest_path, kit, check_idle=idle):
    project, kit = Path(project).resolve(), Path(kit).resolve()
    manifest, body = load_manifest(manifest_path)
    if kit == project or kit.is_relative_to(project) or project.is_relative_to(kit):
        raise RuntimeError('PrivateKitMustBeOutsideProject')
    probe = subprocess.run(['git', 'rev-parse', '--show-toplevel'], cwd=kit, capture_output=True)
    if probe.returncode != 128 or b'not a git repository' not in probe.stderr:
        raise RuntimeError('PrivateKitMustBeOutsideAnyGitWorktree')
    if safe_file(kit, 'private-assets.json').read_bytes() != body:
        raise RuntimeError('PrivateKitManifestMismatch')
    states = inspect(project, manifest)
    for row in manifest['files']:
        source = safe_file(kit, row['path'])
        if not source.is_file() or sha(source) != row['sha256'] or source.stat().st_size != row['bytes']:
            raise RuntimeError('PrivateKitPayloadMismatch: ' + row['path'])
    check_idle(project)
    created = []
    try:
        for row, state in zip(manifest['files'], states):
            if state['state'] == 'EXACT':
                continue
            source, target = safe_file(kit, row['path']), safe_file(project, row['path'])
            target.parent.mkdir(parents=True, exist_ok=True)
            descriptor, temporary = tempfile.mkstemp(prefix='.shop-restore-', dir=target.parent)
            temporary = Path(temporary)
            try:
                with os.fdopen(descriptor, 'wb') as stream:
                    stream.write(source.read_bytes())
                    stream.flush()
                    os.fsync(stream.fileno())
                if sha(temporary) != row['sha256']:
                    raise RuntimeError('PrivateKitChangedDuringRestore')
                # Hard link creates the destination atomically and refuses an existing path.
                os.link(temporary, target)
                created.append((target, row['sha256']))
            finally:
                temporary.unlink(missing_ok=True)
        final = inspect(project, manifest)
        if any(row['state'] != 'EXACT' for row in final):
            raise RuntimeError('PrivateAssetRestoreIncomplete')
    except Exception:
        for target, expected in reversed(created):
            if target.is_file() and sha(target) == expected:
                target.unlink()
        raise
    return {'status': 'RESTORED' if created else 'EXACT_NO_OP',
            'manifestSha256': hashlib.sha256(body).hexdigest(), 'restored': len(created), 'files': final}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('operation', choices=['check', 'restore'])
    parser.add_argument('--project', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--kit', type=Path)
    parser.add_argument('--receipt', type=Path)
    args = parser.parse_args()
    manifest_path = args.project / 'Tools/ShopUI/private-assets.json'
    if args.operation == 'restore':
        if args.kit is None:
            parser.error('restore requires --kit <locally licensed exact kit>')
        result = restore(args.project, manifest_path, args.kit)
    else:
        manifest, body = load_manifest(manifest_path)
        rows = inspect(args.project, manifest)
        result = {'status': 'EXACT' if all(r['state'] == 'EXACT' for r in rows) else 'MISSING_PRIVATE_PAYLOADS',
                  'manifestSha256': hashlib.sha256(body).hexdigest(), 'files': rows}
    if args.receipt:
        receipt = args.receipt.resolve()
        if not receipt.is_relative_to((args.project / 'Artifacts').resolve()):
            parser.error('receipt must be in the ignored project Artifacts directory')
        receipt.parent.mkdir(parents=True, exist_ok=True)
        receipt.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result, indent=2))
    return 0 if result['status'] in ('EXACT', 'RESTORED', 'EXACT_NO_OP') else 2


if __name__ == '__main__':
    sys.exit(main())
