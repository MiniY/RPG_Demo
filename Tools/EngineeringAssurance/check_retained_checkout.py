"""Check the exact retained-content contract on a checkout, without Unity or mutations."""
from pathlib import Path
import argparse, hashlib, json, subprocess, sys

parser = argparse.ArgumentParser()
parser.add_argument('project', type=Path)
parser.add_argument('output', type=Path)
parser.add_argument('--probe-filtered', action='store_true')
args = parser.parse_args()
project = args.project.resolve()
manifest = json.loads((project / 'Assets/Resources/Ecology/SurfaceV4MainV1/RetainedContent_MainV1.json').read_text(encoding='utf-8'))
pins = manifest.get('files', manifest.get('sourceFiles', []))
assert pins, 'Manifest source-file collection is required'
digest = lambda value: hashlib.sha256(value).hexdigest()
rows = []
for pin in pins:
    name = pin.get('path', pin.get('assetPath'))
    expected = pin.get('sha256', pin.get('sha256Hex'))
    assert name and expected, pin
    path = (project / name).resolve()
    assert path.is_relative_to(project), name
    content = path.read_bytes()
    actual = digest(content)
    if actual == expected:
        continue
    row = {'path': name, 'expected': expected, 'actual': actual,
           'lf': content.count(b'\n'), 'crlf': content.count(b'\r\n')}
    if args.probe_filtered:
        filtered = subprocess.check_output(['git', '-c', 'safe.directory=' + str(project), '-C', str(project), 'cat-file', '--filters', 'HEAD:' + name])
        row.update(filtered=digest(filtered), normalized=digest(content.replace(b'\r\n', b'\n')))
    rows.append(row)
result = {'project': str(project), 'total': len(pins), 'mismatches': rows,
          'state': 'FAIL' if rows else 'PASS'}
args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps({'state': result['state'], 'total': len(pins), 'mismatches': len(rows),
                  'allFilteredMatch': all(r.get('filtered') == r['expected'] for r in rows) if args.probe_filtered else None,
                  'allCrLfOnly': all(r.get('normalized') == r['expected'] for r in rows) if args.probe_filtered else None}), flush=True)
sys.exit(1 if rows else 0)
