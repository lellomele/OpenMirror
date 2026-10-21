# OpenMirror AirPlay sul PC ricevente

Estrai l'intero archivio portabile e avvia `OpenMirror.exe`. Non occorre installare il componente Miracast. PC e dispositivo devono trovarsi sulla stessa rete locale.

Premi **AirPlay**, quindi seleziona il nome del ricevitore in **Duplica schermo** sull'iPad. Inserisci il PIN mostrato sul PC. Muovi o ruota lo schermo del dispositivo e prova un breve video con audio.

**F11** attiva lo schermo intero; **Esci / Esc** ripristina la finestra senza interrompere il mirroring. Le impostazioni includono **2560 × 1440 QHD**; la risoluzione effettiva è decisa dal dispositivo. Disconnetti prima di cambiare impostazioni e poi riconnetti.

Screenshot salva PNG; Registra crea MP4. I file acquisiscono l'area visibile. Il MP4 contiene solo l'eventuale microfono: l'audio del dispositivo viene riprodotto ma non registrato.

In caso di errore, apri **Log** e annota il messaggio. I log sono in `%LOCALAPPDATA%\OpenMirror`. Se le porte sono già occupate, chiudi l'altro ricevitore AirPlay. Controlla anche rete privata, multicast, VPN e firewall.

(C) 2026 prof. ing. Raffaele Mele - https://github.com/lellomele
