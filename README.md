# OpenMirror · AirPlay

Ricevitore AirPlay open source per **un dispositivo alla volta**, per Windows 10/11 x64. Interfaccia italiana e inglese, screenshot PNG, registrazione MP4 e schermo intero con il solo comando per uscire. [English](README.en.md)

## Installazione e requisiti

[Scarica la versione Alpha](https://github.com/lellomele/OpenMirror/releases/tag/v0.2.0-alpha).

Estrai completamente `OpenMirror-0.2-AirPlay-windows-x64.zip` e avvia `OpenMirror.exe`. Mantieni le cartelle incluse accanto all'eseguibile. Occorre .NET Framework 4.8. Non sono richiesti certificati di sviluppo o l'installazione di componenti Miracast.

Questa edizione include soltanto AirPlay. PC e iPad, iPhone o Mac devono essere sulla stessa rete locale, con comunicazione tra dispositivi consentita. Sono usate le porte TCP 7100–7102 e mDNS UDP 5353: consenti la comunicazione sulla rete privata quando Windows lo richiede.

## Uso

1. Premi **AirPlay** in OpenMirror.
2. Sul dispositivo apri **Duplica schermo** e scegli il nome mostrato nell'app.
3. Inserisci il PIN mostrato sul PC, se richiesto. Per terminare premi **Disconnetti**.

**F11** attiva lo schermo intero; **Esc** e il piccolo comando **Esci** ripristinano la finestra. Nelle impostazioni scegli se mantenere le proporzioni oppure riempire l'area deformando l'immagine. È disponibile **2560 × 1440 QHD**; Full HD a 30 fps privilegia la fluidità sui PC meno potenti. Risoluzione e frequenza effettive dipendono dal dispositivo.

**Screenshot** salva un PNG; **Registra / Ferma registrazione** crea un MP4. **Registrazioni** apre la cartella dei file. Scorciatoie: **Ctrl+Maiusc+S**, **Ctrl+Maiusc+R**. Impostazioni, lingua e Informazioni sono nel menu superiore.

## Limiti e risoluzione dei problemi

- Le acquisizioni registrano l'area visibile: una finestra sovrapposta può comparire nel file. La registrazione si ferma quando cambiano posizione o dimensioni dell'area.
- L'audio del dispositivo viene riprodotto. Nel MP4 viene incluso soltanto il microfono facoltativo; senza microfono il file è silenzioso.
- Contenuti protetti, isolamento Wi-Fi, VPN e firewall possono impedire la ricezione. Chiudi eventuali altri ricevitori AirPlay che occupano le stesse porte.

Se il ricevitore non viene trovato, controlla rete locale, multicast, VPN e firewall. Dopo un errore OpenMirror tenta due riavvii, usando la decodifica software al secondo tentativo. **Log** mostra i dettagli; i log sono in `%LOCALAPPDATA%\OpenMirror`.

## Compilazione

Da Windows PowerShell, nella cartella dei sorgenti:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\build.ps1 -Test
```

Chiudi l'eseguibile da ricompilare. La UI usa il compilatore .NET Framework e le librerie Windows incluse, senza NuGet. `scripts\generate-icon.ps1` rigenera PNG e ICO; il sorgente SVG è in `assets`. `scripts\test-ui.ps1` verifica il layout senza aprire finestre.

Per ricompilare i backend consulta `scripts\build-backends.ps1`: occorrono MSYS2 MINGW64 con Python, GCC/G++, CMake, Ninja, pkgconf e binutils, accesso a Internet e diversi GB liberi. Sorgenti, ricette, patch e manifest delle dipendenze sono nell'archivio separato `OpenMirror-0.2-AirPlay-source.zip`.

## Licenza e copyright

Codice, script e icona OpenMirror: MIT. UxPlay: GPL-3.0-or-later. Gli altri componenti mantengono le proprie licenze; consulta [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) e conserva i sorgenti corrispondenti quando redistribuisci i binari. Progetto indipendente da Reflector e dagli altri prodotti citati.

(C) 2026 prof. ing. Raffaele Mele - https://github.com/lellomele
