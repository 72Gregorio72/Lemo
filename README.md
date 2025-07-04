# 🎮 Lemovie VR - Sistema di Caricamento Esperienze Immersive

Un sistema VR avanzato per esperienze immersive 360° basato su metadata, ottimizzato per Quest 3S.

## 📋 Indice

- [👨‍💻 Istruzioni per Sviluppatori](#-istruzioni-per-sviluppatori)
- [🎯 Istruzioni per Utenti VR](#-istruzioni-per-utenti-vr)
- [📁 Struttura delle Cartelle](#-struttura-delle-cartelle)
- [📝 Formato File Metadata](#-formato-file-metadata)
- [🎨 Esempi Pratici](#-esempi-pratici)
- [⚡ Caratteristiche Tecniche](#-caratteristiche-tecniche)

---

## 👨‍💻 Istruzioni per Sviluppatori

### 🔧 Setup Iniziale

1. **Clonare il repository**
2. **Aprire in Unity 2022.3 LTS o superiore**
3. **Configurare per Android/Quest** nelle Build Settings
4. **Il sistema è già configurato** - nessuna configurazione aggiuntiva richiesta

### 🏗️ Architettura del Sistema

Il sistema utilizza un **approccio basato su metadata** con caricamento on-demand:

- **📂 Metadata**: File `.txt` in `Data/Categoria/` definiscono le esperienze
- **🎬 Media**: Caricamento dinamico da `Videos/` e `360 Images/` solo quando necessario
- **🖼️ Thumbnails**: Caricamento progressivo dal centro verso l'esterno
- **💾 Persistenza**: Thumbnails rimangono in memoria una volta caricati

### 🔄 Flusso di Sviluppo

1. **Aggiungere nuove esperienze**: Creare file metadata `.txt`
2. **Posizionare media**: Organizzare in cartelle per categoria
3. **Testare**: Il sistema rileva automaticamente i nuovi contenuti
4. **Build**: Deploy diretto su Quest senza configurazione aggiuntiva

---

## 🎯 Istruzioni per Utenti VR

### 📱 Come Aggiungere Contenuti al Tuo Visore

#### 1️⃣ Collega il Quest al PC
```
Collega → Attiva Developer Mode → Consenti Debug USB
```

#### 2️⃣ Naviga alla Cartella dell'App
```
Quest 3S/Android/data/com.unity.Lemovie.vr/files/
```

#### 3️⃣ Organizza i Tuoi File

**🎬 Per i Video:**
```
📁 Videos/
  └── 📁 Nature/          ← Tua categoria
      └── Video Rilassante.mp4
  └── 📁 Space/
      └── Viaggio Galattico.mp4
  └── 📁 Relaxation/
      └── Meditazione Guidata.mp4
```

**🖼️ Per le Immagini 360°:**
```
📁 360 Images/
  └── 📁 Nature/          ← Stessa categoria
      └── Foresta Incantata.jpg
  └── 📁 Space/
      └── Nebulosa Colorata.png
```

**🖼️ Per le Thumbnails:**
```
📁 Thumbnails/
  └── 📁 Nature/          ← Stessa categoria
      └── Video Rilassante.jpg      ← Stesso nome del video
      └── Foresta Incantata.jpg     ← Stesso nome dell'immagine
```

**📝 Per i Metadata (IMPORTANTE!):**
```
📁 Data/
  └── 📁 Nature/          ← Stessa categoria
      └── Video Rilassante.txt      ← Stesso nome del video
      └── Foresta Incantata.txt     ← Stesso nome dell'immagine
```

---

## 📁 Struttura delle Cartelle

```
Quest/Android/data/com.unity.Lemovie.vr/files/
├── 📁 Data/                    ← FILE METADATA (OBBLIGATORI)
│   ├── 📁 Nature/
│   │   ├── Video Rilassante.txt
│   │   └── Foresta Incantata.txt
│   ├── 📁 Space/
│   └── 📁 Relaxation/
│
├── 📁 Videos/                  ← VIDEO (.mp4)
│   ├── 📁 Nature/
│   │   └── Video Rilassante.mp4
│   ├── 📁 Space/
│   └── 📁 Relaxation/
│
├── 📁 360 Images/              ← IMMAGINI 360° (.jpg, .png)
│   ├── 📁 Nature/
│   │   └── Foresta Incantata.jpg
│   ├── 📁 Space/
│   └── 📁 Relaxation/
│
├── 📁 Thumbnails/              ← ANTEPRIME (.jpg, .png)
│   ├── 📁 Nature/
│   │   ├── Video Rilassante.jpg
│   │   └── Foresta Incantata.jpg
│   ├── 📁 Space/
│   └── 📁 Relaxation/
│
└── 📁 Audio/                   ← AUDIO OPZIONALE (.wav)
    ├── Suoni Natura.wav
    └── Musica Rilassante.wav
```

---

## 📝 Formato File Metadata

### 🎬 Per i Video (`Esempio Video.txt`)

```
Position 0 0 0
Rotation 0 0 0
Type: Video
Audio: Musica Rilassante.wav
Description: Un'esperienza immersiva nella natura selvaggia con suoni di sottofondo rilassanti.
```

### 🖼️ Per le Immagini 360° (`Esempio Immagine.txt`)

```
Position 0 0 0
Rotation 0 0 0
Type: Image
Audio: Suoni Natura.wav
Description: Una vista mozzafiato di una foresta incantata al tramonto.
```

### 📋 Campi Disponibili

| Campo | Obbligatorio | Descrizione | Esempio |
|-------|--------------|-------------|---------|
| `Position` | ✅ Sì | Posizione 3D della camera | `0 0 0` |
| `Rotation` | ✅ Sì | Rotazione 3D della camera | `0 90 0` |
| `Type` | ✅ Sì | Tipo di media | `Video` o `Image` |
| `Audio` | ❌ No | File audio di sottofondo | `Musica.wav` o `No` |
| `Description` | ❌ No | Descrizione dell'esperienza | `Testo libero` |

---

## ⚡ Caratteristiche Tecniche


### 🎯 Sistema di Caricamento

1. **🏁 Avvio**: Carica thumbnail centrale immediatamente
2. **📍 Espansione**: Carica vicini (-1, +1) entro 300ms
3. **🌊 Progressivo**: Carica resto in anelli con delay 400ms
4. **🎬 On-Demand**: Media caricati solo al trigger utente

### 🔧 Supporto Formati

| Tipo | Formati Supportati | Note |
|------|-------------------|------|
| **Video** | `.mp4` | H.264 raccomandato |
| **Immagini 360°** | `.jpg`, `.png` | Risoluzione 4K-8K |
| **Thumbnails** | `.jpg`, `.png` | Auto-ridimensionate a 256x256 |
| **Audio** | `.wav` | Mono/Stereo, 44.1kHz |

### 🛠️ Risoluzione Problemi

**❌ Problema**: Esperienza non appare
- ✅ **Soluzione**: Verifica che esista il file `.txt` in `Data/Categoria/`

**❌ Problema**: Video non si riproduce
- ✅ **Soluzione**: Controlla che `Type: Video` sia esatto nel `.txt`

**❌ Problema**: Audio non funziona  
- ✅ **Soluzione**: Verifica che il file `.wav` esista in `Audio/`

**❌ Problema**: Thumbnail non carica
- ✅ **Soluzione**: Assicurati che il nome del thumbnail corrisponda esattamente

---

## 🎮 Come Usare nel VR

1. **👓 Indossa il Visore**: Avvia l'app Lemovie
2. **🎯 Naviga**: Usa joystick per scorrere il carousel
3. **🔘 Seleziona**: Premi trigger per avviare l'esperienza
4. **🔄 Torna**: Premi trigger di nuovo per tornare al carousel
5. **🎨 Esplora**: Ruota la testa per esplorare l'ambiente 360°

---

## 📞 Supporto

Per problemi tecnici o domande:
- 📧 Email: support@lemovie.vr
- 📱 Discord: LemovieVR Community
- 📚 Wiki: github.com/lemovie/docs

---

**🚀 Buona Immersione nel Mondo VR! 🌟** 