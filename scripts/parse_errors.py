import re
from pathlib import Path
log = Path('build.log')
text = log.read_text(encoding='utf-8')
lines = text.splitlines()
entries = []
ansi = re.compile(r'\x1b\[[0-9;]*m')
pattern = re.compile(r'^(?P<path>.+?)\((?P<line>\d+),(?P<col>\d+)\): error (?P<code>\w+): (?P<message>.+?) \[(?P<proj>.+?)\]$')
for raw in lines:
    line = ansi.sub('', raw).strip()
    if not line:
        continue
    m = pattern.match(line)
    if not m:
        continue
    entries.append({
        'path': m.group('path'),
        'line': int(m.group('line')),
        'col': int(m.group('col')),
        'code': m.group('code'),
        'message': m.group('message'),
        'project': Path(m.group('proj')).name,
    })
summary = {}
files_per_code = {}
projects_per_code = {}
for e in entries:
    code = e['code']
    summary[code] = summary.get(code, 0) + 1
    files_per_code.setdefault(code, set()).add(e['path'])
    projects_per_code.setdefault(code, set()).add(e['project'])
md = ['# Build Errors Report', '', '## Error Summary', '', '| Code | Count | Projects | Files |', '| --- | --- | --- | --- |']
for code, count in sorted(summary.items(), key=lambda item: -item[1]):
    projects = ', '.join(sorted(projects_per_code[code]))
    files = ', '.join(sorted({Path(f).name for f in files_per_code[code]}))
    md.append(f'| {code} | {count} | {projects} | {files} |')
md.extend(['', '## Error Details', '', '| File | Line | Project | Code | Message |', '| --- | --- | --- | --- | --- |'])
for e in entries:
    md.append(f"| {e['path']} | {e['line']} | {e['project']} | {e['code']} | {e['message']} |")
Path('docs/build').mkdir(parents=True, exist_ok=True)
Path('docs/build/errors.md').write_text('\n'.join(md) + '\n', encoding='utf-8')
