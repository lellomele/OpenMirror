# Componenti di terze parti

La licenza MIT nella radice si applica esclusivamente al codice originale OpenMirror, agli script e all'icona. Le dipendenze sono programmi separati o librerie distribuite con le proprie condizioni.

L'edizione **AirPlay** distribuisce UxPlay, GStreamer, FFmpeg e le librerie necessarie. I componenti Android, Cast e Miracast elencati sotto non sono inclusi in questa edizione; alcuni manifest e archivi di sorgenti coprono anche tali componenti facoltativi.

| Componente | Versione | Licenza / sorgenti |
|---|---|---|
| UxPlay | 1.74, commit `dbb8a8f451597aa5f2e56c830ce41c4a655a8f98` | GPL-3.0-or-later; albero completo in `third_party/UxPlay` |
| scrcpy | 5.0.1 | Apache-2.0; `third_party/scrcpy-v5.0.1.tar.gz` |
| FFmpeg MSYS2 | 9.0.2 | Build con componenti GPL; licenza, ricetta, patch e sorgenti nel pacchetto corrispondente |
| GStreamer | 1.28.8 | LGPL e licenze dei singoli plugin; sorgenti e ricette MSYS2 inclusi |
| Android platform-tools | build inclusa in scrcpy | `bin/android/NOTICE.txt` e avvisi nel pacchetto scrcpy |
| OpenSSL, libplist, GLib, SDL, libusb, codec e librerie di supporto | versioni incluse nei manifest | Licenze dei singoli progetti e sorgenti corrispondenti |
| Microsoft UWP / CoreRuntime | UWP 6.2.14, CoreRuntime 2.2.14 | Runtime ufficiali: licenze in `bin/miracast/Licenses`; provenienza e hash in `companion/MiracastReceiver/DEPENDENCIES.md` |
| Microsoft Windows SDK Contracts | 10.0.19041.1 | Metadati API Windows; condizioni Windows SDK indicate nella documentazione Miracast |
| Chromium Open Screen | commit `f001f3ffa6b62bdfcc3d3b0120c940dd495cdb46` | BSD-3-Clause; definizioni e adattamenti di protocollo Cast. Licenza e avvisi in `bin/cast` e `backend/ChromeCastReceiver` |

`third_party/runtime-licenses` conserva gli avvisi delle librerie MSYS2; `third_party/licenses-scrcpy` conserva i testi di licenza delle dipendenze di scrcpy. `third_party/UxPlay-LICENSE.txt` contiene la GPL del ricevitore e `third_party/platform-tools-NOTICE.txt` gli avvisi dei platform-tools Android. `third_party/scrcpy-dependencies` contiene i sorgenti delle dipendenze binarie di scrcpy.

`third_party/source-packages.json` elenca URL, dimensioni e SHA256 di 93 archivi di sorgenti MSYS2 presenti in `third_party/msys2-sources`. Contengono archivi upstream, ricette e patch. `runtime-source-map.json` associa i file distribuiti ai pacchetti di provenienza; `msys2-packages.json` conserva il manifesto dei pacchetti usati per la compilazione. Per scrcpy consulta anche `scrcpy-dependencies.json` e `scrcpy-SHA256SUMS.txt`.

Il pacchetto portabile include gli avvisi e i manifest. I sorgenti delle dipendenze open source sono distribuiti nel separato `OpenMirror-0.2-AirPlay-source.zip`: conserva e rendi disponibile questo archivio insieme ai binari quando li redistribuisci. Il solo archivio portabile non contiene i sorgenti delle dipendenze.

Siti upstream: [UxPlay](https://github.com/FDH2/UxPlay), [scrcpy](https://github.com/Genymobile/scrcpy), [FFmpeg](https://ffmpeg.org), [GStreamer](https://gstreamer.freedesktop.org), [MSYS2](https://www.msys2.org).
