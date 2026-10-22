# OpenMirror · AirPlay

[Italiano](#italiano) | [English](#english)

## Italiano

Ricevitore AirPlay open source per **un dispositivo alla volta**, per Windows 10/11 x64. Interfaccia italiana e inglese, screenshot PNG, registrazione MP4 e schermo intero con il solo comando per uscire.

### Installazione e requisiti

[Scarica la versione Alpha](https://github.com/lellomele/OpenMirror/releases/tag/v0.2.0-alpha).

**Per usare OpenMirror basta scaricare [il pacchetto Windows](https://github.com/lellomele/OpenMirror/releases/download/v0.2.0-alpha/OpenMirror-0.2-AirPlay-windows-x64.zip). Include già tutte le dipendenze necessarie: non occorre scaricare l'archivio dei sorgenti.** `OpenMirror-0.2-AirPlay-source.zip` serve per consultare o ricompilare i sorgenti completi delle dipendenze e per accompagnare la redistribuzione dei binari secondo le rispettive licenze.

Estrai completamente `OpenMirror-0.2-AirPlay-windows-x64.zip` e avvia `OpenMirror.exe`. Mantieni le cartelle incluse accanto all'eseguibile. Occorre .NET Framework 4.8. Non sono richiesti certificati di sviluppo o l'installazione di componenti Miracast.

Questa edizione include soltanto AirPlay. PC e iPad, iPhone o Mac devono essere sulla stessa rete locale, con comunicazione tra dispositivi consentita. Sono usate le porte TCP 7100–7102 e mDNS UDP 5353: consenti la comunicazione sulla rete privata quando Windows lo richiede.

### Uso

1. Premi **AirPlay** in OpenMirror.
2. Sul dispositivo apri **Duplica schermo** e scegli il nome mostrato nell'app.
3. Inserisci il PIN mostrato sul PC, se richiesto. Per terminare premi **Disconnetti**.

**F11** attiva lo schermo intero; **Esc** e il piccolo comando **Esci** ripristinano la finestra. Nelle impostazioni scegli se mantenere le proporzioni oppure riempire l'area deformando l'immagine. È disponibile **2560 × 1440 QHD**; Full HD a 30 fps privilegia la fluidità sui PC meno potenti. Risoluzione e frequenza effettive dipendono dal dispositivo.

**Screenshot** salva un PNG; **Registra / Ferma registrazione** crea un MP4. **Registrazioni** apre la cartella dei file. Scorciatoie: **Ctrl+Maiusc+S**, **Ctrl+Maiusc+R**. Impostazioni, lingua e Informazioni sono nel menu superiore.

### Limiti e risoluzione dei problemi

- Le acquisizioni registrano l'area visibile: una finestra sovrapposta può comparire nel file. La registrazione si ferma quando cambiano posizione o dimensioni dell'area.
- L'audio del dispositivo viene riprodotto. Nel MP4 viene incluso soltanto il microfono facoltativo; senza microfono il file è silenzioso.
- Contenuti protetti, isolamento Wi-Fi, VPN e firewall possono impedire la ricezione. Chiudi eventuali altri ricevitori AirPlay che occupano le stesse porte.

Se il ricevitore non viene trovato, controlla rete locale, multicast, VPN e firewall. Dopo un errore OpenMirror tenta due riavvii, usando la decodifica software al secondo tentativo. **Log** mostra i dettagli; i log sono in `%LOCALAPPDATA%\OpenMirror`.

### Compilazione

Da Windows PowerShell, nella cartella dei sorgenti:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\build.ps1 -Test
```

Chiudi l'eseguibile da ricompilare. La UI usa il compilatore .NET Framework e le librerie Windows incluse, senza NuGet. `scripts\generate-icon.ps1` rigenera PNG e ICO; il sorgente SVG è in `assets`. `scripts\test-ui.ps1` verifica il layout senza aprire finestre.

Per ricompilare i backend consulta `scripts\build-backends.ps1`: occorrono MSYS2 MINGW64 con Python, GCC/G++, CMake, Ninja, pkgconf e binutils, accesso a Internet e diversi GB liberi. Sorgenti, ricette, patch e manifest delle dipendenze sono nell'archivio separato `OpenMirror-0.2-AirPlay-source.zip`.

### Licenza e copyright

Codice, script e icona OpenMirror: MIT. UxPlay: GPL-3.0-or-later. Gli altri componenti mantengono le proprie licenze; consulta [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) e conserva i sorgenti corrispondenti quando redistribuisci i binari. Progetto indipendente da Reflector e dagli altri prodotti citati.

(C) 2026 prof. ing. Raffaele Mele - https://github.com/lellomele

---

## English

Open source AirPlay receiver for **one device at a time**, for Windows 10/11 x64. Italian and English UI, PNG screenshots, MP4 recording and fullscreen with a small exit control.

### Installation and requirements

[Download the Alpha release](https://github.com/lellomele/OpenMirror/releases/tag/v0.2.0-alpha).

**To use OpenMirror, download only [the Windows package](https://github.com/lellomele/OpenMirror/releases/download/v0.2.0-alpha/OpenMirror-0.2-AirPlay-windows-x64.zip). It already includes all required dependencies; the source archive is not needed to run the application.** `OpenMirror-0.2-AirPlay-source.zip` is provided to inspect or rebuild the complete dependency sources and to accompany redistribution of the binaries under their respective licenses.

Extract all of `OpenMirror-0.2-AirPlay-windows-x64.zip` and run `OpenMirror.exe`, keeping the supplied folders beside it. .NET Framework 4.8 is required. Development certificates and Miracast components are not required.

This edition includes AirPlay only. Connect the PC and your iPad, iPhone or Mac to the same LAN with communication between devices enabled. TCP ports 7100–7102 and mDNS UDP 5353 are used; allow private-network access when Windows asks.

### Use

1. Click **AirPlay** in OpenMirror.
2. Open **Screen Mirroring** on the device and select the displayed receiver name.
3. Enter the PIN displayed on the PC if requested. Click **Disconnect** to finish.

Use **F11** for fullscreen; **Esc** and the small Exit control restore the window. Settings offer aspect-ratio preservation or stretching to fill the area, including **2560 × 1440 QHD**. Full HD at 30 fps favors smooth playback on less powerful PCs. Actual resolution and frame rate depend on the source.

**Screenshot** saves PNG; **Record / Stop recording** creates MP4. **Recordings** opens the output folder. Shortcuts: **Ctrl+Shift+S**, **Ctrl+Shift+R**. Settings, language and About are in the top menu.

### Limits and troubleshooting

- Captures record the visible area, including overlapping windows. Recording stops when the area moves or changes size.
- Device audio is played; MP4 audio includes only the optional microphone. Without a microphone, recordings are silent.
- Protected content, Wi-Fi isolation, VPNs and firewalls may prevent reception. Close other AirPlay receivers using the same ports.

If the receiver is not found, check LAN access, multicast, VPN and firewall settings. After a failure OpenMirror attempts two restarts, enabling software decoding on the second attempt. **Log** displays details; logs are in `%LOCALAPPDATA%\OpenMirror`.

### Build

From Windows PowerShell in the source folder:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\build.ps1 -Test
```

Close the executable before rebuilding it. The UI uses the system .NET Framework compiler and Windows libraries, without NuGet. `scripts\generate-icon.ps1` regenerates PNG/ICO files from the SVG in `assets`; `scripts\test-ui.ps1` checks layout without opening windows.

See `scripts\build-backends.ps1` for backend builds, requiring MSYS2 MINGW64 with Python, GCC/G++, CMake, Ninja, pkgconf and binutils, Internet access and several GB of free space. Corresponding sources, recipes, patches and manifests are in `OpenMirror-0.2-AirPlay-source.zip`.

### License and copyright

OpenMirror code, scripts and icon: MIT. UxPlay: GPL-3.0-or-later. Other components retain their licenses; read [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) and preserve corresponding sources when redistributing binaries. Independent of Reflector and the other products mentioned.

(C) 2026 prof. ing. Raffaele Mele - https://github.com/lellomele
