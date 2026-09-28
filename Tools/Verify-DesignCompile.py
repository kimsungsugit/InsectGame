"""Compile current C# sources against cached Unity references without launching the editor.

This checks C# compilation only, not Unity imports, tests, visuals, or Android builds.
Requires an existing Unity Bee editor response file from this checkout.
"""
import argparse
from pathlib import Path
import subprocess


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--unity-data', default='D:/Unity/Unity Hub/6000.3.10f1/Editor/Data')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    data = Path(args.unity_data)
    output = root / '.claude/cache/design-validation'
    output.mkdir(parents=True, exist_ok=True)
    candidates = list((root / 'Library/Bee/artifacts').glob('*E.dag/Assembly-CSharp.rsp'))
    if not candidates:
        raise SystemExit('No cached editor references. Open the project in a licensed Unity editor first.')
    source = max(candidates, key=lambda p: p.stat().st_mtime).parent
    for name, directories in [('Assembly-CSharp', ('Assets/Scripts', 'Assets/Tests')),
                              ('Assembly-CSharp-Editor', ('Assets/Editor',))]:
        response = source / (name + '.rsp')
        lines = response.read_text(encoding='utf-8-sig').splitlines()
        lines = [line for line in lines if not line.startswith(('-out:', '-refout:'))
                 and not (line.startswith('"Assets/') and line.endswith('.cs"'))]
        if name.endswith('-Editor'):
            lines = [('-r:"' + (output / 'Assembly-CSharp.dll').as_posix() + '"')
                     if line.startswith('-r:') and ('Assembly-CSharp.dll' in line or 'Assembly-CSharp.ref.dll' in line)
                     else line for line in lines]
        for directory in directories:
            lines.extend('"' + p.relative_to(root).as_posix() + '"' for p in sorted((root / directory).rglob('*.cs')))
        lines.append('-out:"' + (output / (name + '.dll')).as_posix() + '"')
        generated = output / (name + '.rsp')
        generated.write_text('\n'.join(lines), encoding='utf-8')
        result = subprocess.run([str(data / 'NetCoreRuntime/dotnet.exe'), str(data / 'DotNetSdkRoslyn/csc.dll'),
                                 '@' + str(generated)], cwd=root, capture_output=True, text=True, encoding='utf-8', errors='replace')
        (output / (name + '.compile.log')).write_text(result.stdout + result.stderr, encoding='utf-8')
        print(result.stdout + result.stderr)
        if result.returncode:
            raise SystemExit(result.returncode)
        print(name + ': C# compilation PASS (runtime not executed)')


if __name__ == '__main__':
    main()
