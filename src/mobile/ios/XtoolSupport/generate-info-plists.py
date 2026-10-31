"""Generate xtool Info.plists from the Xcode project's plists.

xtool merges infoPath plists verbatim, so Xcode build-setting references such as
$(MARKETING_VERSION) must be resolved first. Keys xtool computes itself (bundle ID,
executable, name, package type) are dropped so xtool's values win.
"""

import plistlib
import re
import sys
from pathlib import Path

IOS_DIR = Path(__file__).resolve().parent.parent
PBXPROJ = IOS_DIR / "XerahSMobile.xcodeproj" / "project.pbxproj"
OUT_DIR = IOS_DIR / "XtoolSupport" / ".generated"

TARGETS = {
    "XerahSMobile": IOS_DIR / "XerahSMobile" / "Info.plist",
    "ShareExtension": IOS_DIR / "ShareExtension" / "Info.plist",
}

XTOOL_OWNED_KEYS = {"CFBundleIdentifier", "CFBundleExecutable", "CFBundleName", "CFBundlePackageType"}


def single_setting(pbxproj: str, name: str) -> str:
    values = set(re.findall(rf"^\s*{name} = \"?([^\";]+)\"?;", pbxproj, re.M))
    if len(values) != 1:
        sys.exit(f"Expected one {name} across build configurations, found {sorted(values)}")
    return values.pop()


def resolve(value, settings: dict):
    if isinstance(value, str):
        def sub(match):
            key = match.group(1)
            if key not in settings:
                sys.exit(f"Unresolved build setting $({key}) in Info.plist")
            return settings[key]
        return re.sub(r"\$\((\w+)\)", sub, value)
    if isinstance(value, list):
        return [resolve(item, settings) for item in value]
    if isinstance(value, dict):
        return {key: resolve(item, settings) for key, item in value.items()}
    return value


def main() -> None:
    pbxproj = PBXPROJ.read_text(encoding="utf-8")
    common = {
        "MARKETING_VERSION": single_setting(pbxproj, "MARKETING_VERSION"),
        "CURRENT_PROJECT_VERSION": single_setting(pbxproj, "CURRENT_PROJECT_VERSION"),
        "DEVELOPMENT_LANGUAGE": "en",
    }

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for target, source in TARGETS.items():
        with source.open("rb") as handle:
            info = plistlib.load(handle)
        for key in XTOOL_OWNED_KEYS:
            info.pop(key, None)
        info = resolve(info, {**common, "PRODUCT_MODULE_NAME": target})
        with (OUT_DIR / f"{target}-Info.plist").open("wb") as handle:
            plistlib.dump(info, handle)

    print(f"Generated xtool Info.plists (version {common['MARKETING_VERSION']} build {common['CURRENT_PROJECT_VERSION']})")


if __name__ == "__main__":
    main()
