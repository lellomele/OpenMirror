# OpenMirror · AirPlay

Open source AirPlay receiver for **one device at a time**, for Windows 10/11 x64. Italian and English UI, PNG screenshots, MP4 recording and fullscreen with a small exit control.

## Installation and requirements

[Download the Alpha release](https://github.com/lellomele/OpenMirror/releases/tag/v0.2.0-alpha).

**To use OpenMirror, download only [the Windows package](https://github.com/lellomele/OpenMirror/releases/download/v0.2.0-alpha/OpenMirror-0.2-AirPlay-windows-x64.zip). It already includes all required dependencies; the source archive is not needed to run the application.** `OpenMirror-0.2-AirPlay-source.zip` is provided to inspect or rebuild the complete dependency sources and to accompany redistribution of the binaries under their respective licenses.

Extract all of `OpenMirror-0.2-AirPlay-windows-x64.zip` and run `OpenMirror.exe`, keeping the supplied folders beside it. .NET Framework 4.8 is required. Development certificates and Miracast components are not required.

This edition includes AirPlay only. Connect the PC and your iPad, iPhone or Mac to the same LAN with communication between devices enabled. TCP ports 7100–7102 and mDNS UDP 5353 are used; allow private-network access when Windows asks.

## Use

1. Click **AirPlay** in OpenMirror.
2. Open **Screen Mirroring** on the device and select the displayed receiver name.
3. Enter the PIN displayed on the PC if requested. Click **Disconnect** to finish.

Use **F11** for fullscreen; **Esc** and the small Exit control restore the window. Settings offer aspect-ratio preservation or stretching to fill the area, including **2560 × 1440 QHD**. Full HD at 30 fps favors smooth playback on less powerful PCs. Actual resolution and frame rate depend on the source.

**Screenshot** saves PNG; **Record / Stop recording** creates MP4. **Recordings** opens the output folder. Shortcuts: **Ctrl+Shift+S**, **Ctrl+Shift+R**. Settings, language and About are in the top menu.

## Limits and troubleshooting

- Captures record the visible area, including overlapping windows. Recording stops when the area moves or changes size.
- Device audio is played; MP4 audio includes only the optional microphone. Without a microphone, recordings are silent.
- Protected content, Wi-Fi isolation, VPNs and firewalls may prevent reception. Close other AirPlay receivers using the same ports.

If the receiver is not found, check LAN access, multicast, VPN and firewall settings. After a failure OpenMirror attempts two restarts, enabling software decoding on the second attempt. **Log** displays details; logs are in `%LOCALAPPDATA%\OpenMirror`.

## Build

From Windows PowerShell in the source folder:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\build.ps1 -Test
```

Close the executable before rebuilding it. The UI uses the system .NET Framework compiler and Windows libraries, without NuGet. `scripts\generate-icon.ps1` regenerates PNG/ICO files from the SVG in `assets`; `scripts\test-ui.ps1` checks layout without opening windows.

See `scripts\build-backends.ps1` for backend builds, requiring MSYS2 MINGW64 with Python, GCC/G++, CMake, Ninja, pkgconf and binutils, Internet access and several GB of free space. Corresponding sources, recipes, patches and manifests are in `OpenMirror-0.2-AirPlay-source.zip`.

## License and copyright

OpenMirror code, scripts and icon: MIT. UxPlay: GPL-3.0-or-later. Other components retain their licenses; read [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) and preserve corresponding sources when redistributing binaries. Independent of Reflector and the other products mentioned.

(C) 2026 prof. ing. Raffaele Mele - https://github.com/lellomele
