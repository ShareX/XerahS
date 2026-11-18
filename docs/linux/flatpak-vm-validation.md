# Flatpak VM Validation Runbook

This runbook is for validating the XerahS Flatpak from a Linux desktop VM.

Use it when developing from Windows or macOS and you need a real Linux desktop session for Flatpak, XDG portals, Wayland, and sandbox behavior.

## Recommended VM

- Hypervisor on Windows ARM64: Hyper-V
- Guest OS: Fedora Workstation ARM64/aarch64
- RAM: 8 GB minimum, 12 GB preferred
- CPU: 6 virtual processors
- Disk: 100 GB
- Secure Boot: disable if the Fedora ISO is blocked by Hyper-V UEFI
- Session: GNOME Wayland

Do not use WSL, Docker, SSH-only Linux, or a headless VM for final Flatpak validation. They do not represent the desktop portal environment used by Flathub users.

## 1. Install VM Packages

Inside Fedora:

```bash
sudo dnf update -y
sudo reboot
```

After reboot:

```bash
sudo dnf install -y git flatpak flatpak-builder dotnet-sdk-10.0 nodejs npm rpm-build
flatpak remote-add --if-not-exists flathub https://flathub.org/repo/flathub.flatpakrepo
flatpak install -y flathub org.flatpak.Builder
```

If `dotnet-sdk-10.0` is not available from Fedora packages, install the current .NET 10 SDK using the Microsoft Linux package instructions for Fedora, then rerun:

```bash
dotnet --info
```

## 2. Get the Repository

```bash
mkdir -p ~/src
cd ~/src
git clone https://github.com/ShareX/XerahS.git
cd XerahS
git checkout develop
git submodule update --init --recursive
```

If the repo already exists:

```bash
cd ~/src/XerahS
git checkout develop
git pull origin develop
git submodule update --init --recursive
```

## 3. Baseline Build

```bash
dotnet build -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false
```

This must pass before treating a Flatpak failure as a Flatpak-specific issue.

## 4. Build Linux ARM64 Publish Output

On a Windows ARM64 host running an ARM64 Fedora VM, build only `linux-arm64`:

```bash
XERAHS_ARCHITECTURES=linux-arm64 XERAHS_PLUGIN_JOBS=2 ./build/linux/package-linux.sh
```

The publish output should be under:

```text
src/desktop/app/XerahS.App/bin/Release/net11.0/linux-arm64/publish
```

## 5. Prepare Current Local Flatpak Staging

The current repository Flatpak manifest uses a local staging directory for validation:

```text
dist/xerahs-flatpak-staging
```

Prepare it from the ARM64 publish output:

```bash
rm -rf dist/xerahs-flatpak-staging
mkdir -p dist/xerahs-flatpak-staging
cp -a src/desktop/app/XerahS.App/bin/Release/net11.0/linux-arm64/publish/. dist/xerahs-flatpak-staging/
```

Check required files:

```bash
test -f dist/xerahs-flatpak-staging/XerahS
test -f dist/xerahs-flatpak-staging/xerahs-watchfolder-daemon
test -d dist/xerahs-flatpak-staging/frontend/dist
```

## 6. Manifest Lint

```bash
flatpak run --command=flatpak-builder-lint org.flatpak.Builder manifest flatpak/com.xerahs.XerahS.yml
```

Fix every error. Record warnings in:

```text
docs/linux/flathub-submission-checklist.md
```

## 7. Local Flatpak Build And Install

```bash
flatpak-builder --force-clean --user --install-deps-from=flathub --install build-dir flatpak/com.xerahs.XerahS.yml
```

Run it:

```bash
flatpak run com.xerahs.XerahS
```

## 8. Smoke Tests

Minimum checks:

- App starts.
- Screenshot works.
- Region capture works.
- Screen recording either works through the ScreenCast portal or fails with a clear portal diagnostic.
- Notification works.
- OpenURI/browser open works.
- Startup/background permission uses the portal flow.
- No broad host filesystem access is needed.

Home-litter check:

```bash
find "$HOME" -maxdepth 1 -type d \( -name "XerahS" -o -name ".XerahS" -o -name "ShareX" -o -name "Screenshots" \) -print
```

Expected output: nothing.

## 9. Repo Export And Repo Lint

```bash
flatpak-builder --force-clean --repo=repo build-dir flatpak/com.xerahs.XerahS.yml
flatpak run --command=flatpak-builder-lint org.flatpak.Builder repo repo
```

## 10. Record Results

Update:

```text
docs/linux/flathub-submission-checklist.md
```

Record:

- Linux distro and version
- VM or hardware
- CPU architecture
- `dotnet build` result
- manifest linter result
- Flatpak build result
- Flatpak repo linter result
- GNOME Wayland smoke result
- KDE Wayland smoke result, if tested
- known warnings or limitations

## 11. Flathub Submission Note

The current local manifest is useful for VM validation. For Flathub submission, the manifest may need to be converted from local `type: dir` staging to reproducible fetchable sources, plus any generated offline dependency sources required for .NET/NuGet.

Open and manage the Flathub PR manually as a human maintainer.

