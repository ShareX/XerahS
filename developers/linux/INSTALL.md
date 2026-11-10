# Installing XerahS on Linux

This guide covers building and installing XerahS on **Ubuntu**, **Fedora**, and **Arch** from the repository. For Flatpak, see [docs/linux/](../../docs/linux/). First-party PPA / COPR / OBS update channels are documented in [docs/linux/distro-repos.md](../../docs/linux/distro-repos.md).

---

## Distro repositories (auto-update)

After a maintainer creates the projects (see [docs/linux/distro-repos.md](../../docs/linux/distro-repos.md)):

```bash
# Ubuntu / Debian
sudo add-apt-repository ppa:sharex/xerahs
sudo apt update
sudo apt install xerahs

# Fedora
sudo dnf copr enable sharex/xerahs
sudo dnf install xerahs

# openSUSE Tumbleweed
sudo zypper ar -f https://download.opensuse.org/repositories/home:/ShareX:/XerahS/openSUSE_Tumbleweed/ xerahs
sudo zypper refresh
sudo zypper in xerahs
```

Until those projects exist, install the GitHub `.deb` / `.rpm` / AppImage from a release, or the community AUR package below.

---

## Prerequisites

| Tool | Ubuntu 24.04+ | Fedora (current) | Arch / EndeavourOS |
|---|---|---|---|
| .NET SDK 10 | `sudo apt install dotnet-sdk-10.0` | `sudo dnf install dotnet-sdk-10.0` | `sudo pacman -S dotnet-sdk` |
| Node.js 18+ | `sudo apt install nodejs npm` (or [NodeSource](https://github.com/nodesource/distributions) if the archive lags) | `sudo dnf install nodejs npm` | `sudo pacman -S nodejs npm` |
| Packaging tools | `sudo apt install dpkg-dev` (for `.deb`) | `sudo dnf install rpm-build desktop-file-utils` (for `.rpm`) | `sudo pacman -S base-devel` (for AUR) |

Verify:

```bash
dotnet --version   # expect 10.x
node --version     # expect 18+
npm --version
```

---

## Build packages (Ubuntu / Fedora / generic)

From the repository root:

```bash
cd /path/to/xerahs
./build/linux/package-linux.sh
```

This produces `.tar.gz`, `.deb`, `.rpm`, and `.AppImage` artifacts under `dist/` (linux-x64 and linux-arm64). Flatpak is built separately and is unchanged.

Install:

```bash
# Ubuntu / Debian
sudo apt install ./dist/XerahS-*-linux-x64.deb

# Fedora
sudo dnf install ./dist/XerahS-*-linux-x64.rpm

# AppImage (no install)
chmod +x ./dist/XerahS-*-linux-x64.AppImage
./dist/XerahS-*-linux-x64.AppImage
```

The `.deb` and `.rpm` packages **recommend** `wl-clipboard` and `xclip` for CLI/background clipboard workflows. `apt` installs Recommends by default; on Fedora use `sudo dnf install wl-clipboard xclip` if they were not pulled in.

---

## Run from source (development)

```bash
dotnet run --project src/desktop/app/XerahS.App
```

### Debug-build hotkey caveat (Wayland)

The XDG GlobalShortcuts portal matches the running binary against a `.desktop` file `Exec=` line. A raw `dotnet run` build often fails portal binding even though hotkeys work when packaged.

Workaround for local dev on Wayland:

```bash
mkdir -p ~/.local/share/applications
cat > ~/.local/share/applications/xerahs.desktop <<'EOF'
[Desktop Entry]
Type=Application
Name=XerahS
Exec=/path/to/xerahs/src/desktop/app/XerahS.App/bin/Debug/net10.0/XerahS.App
Icon=xerahs
Terminal=false
Categories=Utility;
EOF
update-desktop-database ~/.local/share/applications
```

Replace `Exec=` with your actual published or debug binary path.

Check **Settings → Hotkeys** for the delivery-state banner (`PortalBound` vs `X11FallbackFocusOnly`). See [XIP0044](../../docs/proposals/xip/XIP0044-linux-global-hotkeys-not-firing-when-app-is-backgrounded.md).

---

## Arch / EndeavourOS (AUR-style local package)

```bash
./build/linux/package-aur.sh
sudo pacman -U dist/aur/xerahs-git-*.pkg.tar.zst
```

To limit parallelism on low-RAM machines:

```bash
XERAHS_PLUGIN_JOBS=1 ./build/linux/package-aur.sh
```

---

## Run

```bash
xerahs
```

Or launch **XerahS** from your application menu.

---

## Optional runtime dependencies

Install packages for your session type and desktop environment:

| Purpose | Ubuntu / Debian | Fedora | Arch |
|---|---|---|---|
| Wayland clipboard (CLI) | `wl-clipboard` | `wl-clipboard` | `wl-clipboard` |
| X11 clipboard (CLI) | `xclip` | `xclip` | `xclip` |
| wlroots region capture | `grim`, `slurp` | `grim`, `slurp` | `grim`, `slurp` |
| wlroots recording | `wf-recorder` | `wf-recorder` | `wf-recorder` |
| GNOME system tray | `gnome-shell-extension-appindicator` | `gnome-shell-extension-appindicator` | `gnome-shell-extension-appindicator` |
| Video editor webview | `libwebkit2gtk-4.1-0` | `webkit2gtk4.1` | `webkit2gtk-4.1` |
| X11 automation fallback | `xdotool` | `xdotool` | `xdotool` |

Example (Ubuntu Wayland GNOME):

```bash
sudo apt install wl-clipboard gnome-shell-extension-appindicator
```

---

## Installed layout (deb/rpm)

| Path | Contents |
|---|---|
| `/usr/lib/xerahs/` | Application binary, `omaxerahs`, plugins, assets |
| `/usr/bin/xerahs` | Symlink → `/usr/lib/xerahs/XerahS` |
| `/usr/bin/omaxerahs` | Symlink → `/usr/lib/xerahs/omaxerahs` (Omarchy image upload host) |
| `/usr/share/applications/xerahs.desktop` | Desktop entry |
| `/usr/share/pixmaps/xerahs.png` | Application icon |

---

## Distro smoke checklist (release gate)

Run on each target VM before claiming Linux support:

1. Region capture → image on clipboard → paste in Firefox
2. Quit XerahS → paste again (Wayland: needs `wl-clipboard` or **Persist clipboard after exit** enabled)
3. Upload task → notification **Open URL** button works
4. Tray icon visible (GNOME: extension installed)
5. Global hotkey fires while another app is focused (portal-capable DE)
6. Record 10 seconds, file saved

| Target | Session | Status |
|---|---|---|
| Ubuntu 24.04 GNOME | Wayland | Manual matrix pending |
| Ubuntu 24.04 GNOME | X11 | Manual matrix pending |
| Fedora GNOME | Wayland | Manual matrix pending |
| Arch KDE Plasma 6 | Wayland | Manual matrix pending |
| Arch Hyprland | Wayland | Manual matrix pending |

---

## Uninstall

```bash
# Ubuntu / Debian
sudo apt remove xerahs

# Fedora
sudo dnf remove xerahs

# Arch
sudo pacman -R xerahs-git
```

---

## Related files

- **Packaging**: `build/linux/package-linux.sh`, `build/linux/XerahS.Packaging/`
- **AUR PKGBUILD**: `build/linux/aur/xerahs-git/PKGBUILD`
- **PPA / COPR / OBS templates**: `build/linux/repo-staging/` and [docs/linux/distro-repos.md](../../docs/linux/distro-repos.md)
- **Improvement plan**: [docs/proposals/xip/XIP0079-linux-improvement-plan.md](../../docs/proposals/xip/XIP0079-linux-improvement-plan.md)
- **Flatpak**: [docs/linux/](../../docs/linux/)
