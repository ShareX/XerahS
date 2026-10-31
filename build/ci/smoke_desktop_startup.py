#!/usr/bin/env python3
"""Launch the packaged desktop app twice with an isolated profile and real tray.

A valid archive is insufficient: #300 packaged successfully but crashed during
native tray initialization. Require both tray builds and loaded plugins, then
observe a live event loop before accepting the package.
"""
import argparse
import os
from pathlib import Path
import plistlib
import re
import subprocess
import tempfile
import time


def check_startup(executable, output_log, timeout=60, settle=3, launches=2):
    executable = Path(executable).resolve()
    output_log = Path(output_log)
    output_log.parent.mkdir(parents=True, exist_ok=True)
    output_log.write_text("")
    with tempfile.TemporaryDirectory(prefix="xerahs-startup-") as temporary:
        profile = Path(temporary) / "profile"
        for launch in range(1, launches + 1):
            logs = Path(temporary) / f"logs-{launch}"
            logs.mkdir()
            environment = dict(os.environ, XERAHS_LOGS_DIR=str(logs))
            with output_log.open("a", buffering=1) as capture:
                capture.write(f"\n=== Native startup {launch}/{launches} ===\n")
                process = subprocess.Popen(
                    [str(executable), "--settings-folder", str(profile)],
                    env=environment, stdout=capture, stderr=subprocess.STDOUT,
                )
                try:
                    deadline = time.monotonic() + timeout
                    ready_since = None
                    while time.monotonic() < deadline:
                        if process.poll() is not None:
                            raise RuntimeError(f"App exited during startup (code {process.returncode}).")
                        text = "\n".join(p.read_text(errors="replace") for p in logs.rglob("*.log"))
                        if "Critical application startup failure" in text or "The menu being updated does not match" in text:
                            raise RuntimeError("Native startup failure was logged.")
                        ready = text.count("Tray menu rebuilt with") >= 2 and re.search(r"Plugins: [1-9][0-9]* loaded", text)
                        if ready:
                            if ready_since is None:
                                ready_since = time.monotonic()
                            if time.monotonic() - ready_since >= settle:
                                capture.write("Native tray initialized, plugins loaded, and process stayed alive.\n")
                                break
                        time.sleep(0.1)
                    else:
                        raise RuntimeError("Startup timed out before two tray builds and plugin readiness were observed.")
                finally:
                    if process.poll() is None:
                        process.terminate()
                        try:
                            process.wait(timeout=10)
                        except subprocess.TimeoutExpired:
                            process.kill()
                            process.wait(timeout=10)
                    for path in sorted(logs.rglob("*.log")):
                        capture.write(f"\n--- {path.name} ---\n{path.read_text(errors='replace')}\n")
    print(f"Native startup smoke passed ({launches} launches). Log: {output_log}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--app-bundle", type=Path, required=True)
    parser.add_argument("--output-log", type=Path, required=True)
    args = parser.parse_args()
    with (args.app_bundle / "Contents/Info.plist").open("rb") as handle:
        executable = plistlib.load(handle)["CFBundleExecutable"]
    try:
        check_startup(args.app_bundle / "Contents/MacOS" / executable, args.output_log)
    except (OSError, RuntimeError) as error:
        if args.output_log.exists():
            print(args.output_log.read_text(errors="replace"))
        parser.exit(1, f"Native startup smoke failed: {error}\n")


if __name__ == "__main__":
    main()
