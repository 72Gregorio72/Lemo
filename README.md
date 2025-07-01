# Setup Progetto VR Lemo - Guida per il Team

## Panoramica
Questo progetto Unity VR utilizza un sistema di carosello 3D per visualizzare video 360°, immagini immersive e contenuti audio. Il progetto è ottimizzato per visori Meta Quest e utilizza una struttura di cartelle specifica per organizzare i contenuti multimediali.

## Struttura delle Cartelle

Il progetto utilizza la cartella `Assets/StreamingAssets/` come contenitore principale per tutti i file multimediali. La struttura deve essere così organizzata:

```
Assets/StreamingAssets/
├── Videos/
│   ├── Nature/
│   ├── Space/
│   └── Relaxation/
├── 360 Images/
│   ├── Nature/
│   ├── Space/
│   └── Relaxation/
├── Thumbnails/
│   ├── Nature/
│   ├── Space/
│   └── Relaxation/
├── Audio/
│   ├── Nature/
│   ├── Space/
│   └── Relaxation/
└── Data/
    ├── Nature/
    └── Space/
```

## Categorie Supportate

Il sistema riconosce **3 categorie principali**:
- **Nature** - Contenuti naturalistici e paesaggistici
- **Space** - Contenuti spaziali e astronomici  
- **Relaxation** - Contenuti per meditazione e rilassamento

## 1. Video 360°

### Formato e Posizionamento
- **Formato supportato**: `.mp4`
- **Posizione**: `Assets/StreamingAssets/Videos/[Categoria]/`
- **Naming convention**: `nomefile.mp4`

### Esempio:
```
Assets/StreamingAssets/Videos/Nature/bosco_autunnale.mp4
Assets/StreamingAssets/Videos/Space/viaggio_marte.mp4
```

### Raccomandazioni:
- Risoluzione minima: 4K (3840x1920)
- Codec video: H.264
- Frame rate: 30fps o 60fps
- Bitrate: 50-100 Mbps per la qualità ottimale

## 2. Immagini 360°

### Formato e Posizionamento
- **Formato supportato**: `.png`
- **Posizione**: `Assets/StreamingAssets/360 Images/[Categoria]/`
- **Naming convention**: `nomefile.png`

### Esempio:
```
Assets/StreamingAssets/360 Images/Nature/lago_montagna.png
Assets/StreamingAssets/360 Images/Space/nebulosa_orione.png
```

### Raccomandazioni:
- Risoluzione: 8K (8192x4096) o superiore
- Formato: Equirettangolare (2:1 aspect ratio)
- Compressione: PNG per qualità massima

## 3. Thumbnails (Anteprime)

### ⚠️ IMPORTANTE: Convenzione di Denominazione
Le thumbnails **DEVONO** avere lo **stesso nome identico** del file multimediale corrispondente.

### Formato e Posizionamento
- **Formato supportato**: `.png`
- **Posizione**: `Assets/StreamingAssets/Thumbnails/[Categoria]/`
- **Naming convention**: **Deve corrispondere esattamente al nome del file multimediale**

### Esempi Corretti:
```
Video: bosco_autunnale.mp4
Thumbnail: bosco_autunnale.png

Immagine 360°: lago_montagna.png  
Thumbnail: lago_montagna.png
```

### Raccomandazioni per Thumbnails:
- Risoluzione: 512x512px o 1024x1024px
- Aspect ratio: 1:1 (quadrato)
- Formato: PNG con trasparenza se necessaria
- Deve rappresentare chiaramente il contenuto del media

## 4. File Data (Configurazione Esperienza)

### Formato e Posizionamento
- **Formato supportato**: `.txt`
- **Posizione**: `Assets/StreamingAssets/Data/[Categoria]/`
- **Naming convention**: **Deve corrispondere al nome del file multimediale**

### Struttura del File Data:
```
Position X Y Z
Rotation X Y Z
Audio: Yes/No
Description: Descrizione del contenuto in italiano
```

### Esempio di file `bosco_autunnale.txt`:
```
Position 0 9 0
Rotation 0 270 0
Audio: Yes
Description: Un'esperienza immersiva attraverso un bosco autunnale con suoni naturali ambient
```

### Parametri Spiegati:
- **Position**: Posizione iniziale dell'utente nello spazio 3D (X Y Z)
- **Rotation**: Rotazione iniziale della vista (X Y Z in gradi)
- **Audio**: `Yes` se ha audio sincronizzato, `No` se silenzioso
- **Description**: Testo descrittivo mostrato nell'interfaccia (massimo 200 caratteri)

## 5. File Audio

### Formato e Posizionamento
- **Formati supportati**: `.wav`, `.mp3`
- **Posizione**: `Assets/StreamingAssets/Audio/[Categoria]/`
- **Naming convention**: **Deve corrispondere al nome del file multimediale**

### Esempi:
```
Video: bosco_autunnale.mp4
Audio: bosco_autunnale.wav

Immagine: lago_montagna.png
Audio: lago_montagna.mp3
```

### Raccomandazioni Audio:
- **Formato preferito**: `.wav` per qualità massima
- **Sample rate**: 48kHz o 44.1kHz
- **Bit depth**: 16-bit o 24-bit
- **Canali**: Stereo o Audio Spaziale
- **Compressione MP3**: 320kbps se necessario per dimensioni file

## Processo di Setup Completo

### 1. Preparazione dei File
Per ogni esperienza VR, preparare:
- 1 video `.mp4` O 1 immagine 360° `.png`
- 1 thumbnail `.png` con nome identico
- 1 file data `.txt` con nome identico
- 1 file audio `.wav/.mp3` con nome identico (opzionale)

### 2. Organizzazione per Categoria
Decidere la categoria appropriata (Nature/Space/Relaxation) e posizionare tutti i file nelle rispettive cartelle.

### 3. Esempio Setup Completo:
```
# Per un'esperienza chiamata "aurora_boreale" nella categoria Space:

Assets/StreamingAssets/Videos/Space/aurora_boreale.mp4
Assets/StreamingAssets/Thumbnails/Space/aurora_boreale.png  
Assets/StreamingAssets/Data/Space/aurora_boreale.txt
Assets/StreamingAssets/Audio/Space/aurora_boreale.wav
```

### 4. Generazione Manifesti
Dopo aver aggiunto i file, utilizzare il menu Unity:
**Tools → Generate Streaming Assets Manifests**

Questo creerà automaticamente i file `files.txt` necessari per il deployment su Android.

## Strumenti di Gestione Unity

### Streaming Assets Manager
Accedibile da **Tools → Streaming Assets Manager**

Permette di:
- Visualizzare tutti i file organizzati per categoria
- Aggiungere nuovi file con selezione automatica della categoria
- Rimuovere file esistenti
- Generare manifesti automaticamente

### Validazione Automatica
Il sistema include validazione automatica che:
- Verifica la presenza delle cartelle richieste
- Controlla la corrispondenza dei nomi file
- Avvisa in caso di file mancanti o mal denominati

## Deployment su VR Headset

### Android (Meta Quest)
1. I file vengono automaticamente copiati da `StreamingAssets` a `PersistentDataPath` al primo avvio
2. Il sistema utilizzerà una barra di progresso per mostrare l'avanzamento della copia
3. Una volta completata la copia, l'esperienza sarà pronta per l'uso

### Editor Unity
In modalità editor, i file vengono letti direttamente da `StreamingAssets`.

## Risoluzione Problemi

### File Non Visualizzati nel Carosello
- Verificare che il nome del thumbnail corrisponda esattamente al file multimediale
- Controllare che i file siano nelle cartelle di categoria corrette
- Rigenerare i manifesti con **Tools → Generate Streaming Assets Manifests**

### Audio Non Funzionante
- Verificare che il file data contenga `Audio: Yes`
- Controllare che il nome del file audio corrisponda al media
- Verificare che il formato audio sia `.wav` o `.mp3`

### Problemi di Performance
- Ridurre la risoluzione dei video se necessario
- Utilizzare compressione appropriata per le immagini
- Limitare il numero di file per categoria (massimo 20-30 per performance ottimali)

## Note Tecniche

### Limitazioni File
- **Dimensione massima video**: 2GB per file
- **Dimensione totale StreamingAssets**: Massimo 4GB per build ottimali
- **Nomi file**: Evitare caratteri speciali, utilizzare solo lettere, numeri e underscore

### Backup e Versioning
Si raccomanda di mantenere backup dei file sorgente ad alta qualità, poiché i file in StreamingAssets potrebbero essere compressi per il deployment.

---

**Versione Documento**: 1.0  
**Ultimo Aggiornamento**: Dicembre 2024  
**Compatibilità**: Unity 2022.3 LTS, Meta Quest 2/3/Pro 