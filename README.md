# 🎵 FluentLyrics

<p align="center">
  <img src="Assets/logo.png" width="128" height="128" alt="FluentLyrics Logo" />
</p>

<p align="center">
  <strong>Widget de letras sincronizadas (karaoke) ultra-ligero para Windows 11 y 10.</strong><br>
  Diseñado para posicionarse al lado o encima de tu barra de tareas, acompañando widgets como FluentFlyouts.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-9.0-purple.svg" alt=".NET 9" />
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-blue.svg" alt="Windows" />
  <img src="https://img.shields.io/badge/RAM-~28%20MB-brightgreen.svg" alt="RAM" />
  <img src="https://img.shields.io/badge/CPU-0.0%25-brightgreen.svg" alt="CPU" />
  <img src="https://img.shields.io/badge/License-MIT-green.svg" alt="License" />
</p>

---

## ✨ Características Principales

- 📌 **Modo Integrado a la Barra de Tareas (Windows 11):** Se ancla automáticamente al tamaño y altura de la barra de tareas de Windows. Permanece estático, sin riesgo de ser arrastrado por error, y adapta su ancho inteligentemente si la barra de tareas se llena de aplicaciones abiertas para nunca tapar tus programas.
- ⚡ **Ultra-ligero y Eficiente:** Desarrollado en C# .NET nativo con aceleración por hardware DWM. Nada de Electron ni navegadores embebidos pesados. Consume menos de 30 MB de memoria RAM y 0.0% de CPU en reposo.
- 🎶 **Cero configuración de cuentas:** No necesitas crear API Keys de Spotify ni iniciar sesión en la web. Utiliza la API nativa de Windows **GSMTC** (`Windows.Media.Control`), compatible con Spotify Desktop, Spotify Web, YouTube Music y cualquier reproductor de Windows.
- 🎤 **Letras Sincronizadas (Karaoke):** Conexión con **LRCLIB** para obtener letras sincronizadas línea a línea con interpolación de milisegundos en tiempo real.
- 💾 **Caché Inteligente sin conexión:** Guarda las letras reproducidas en disco para cargarlas al instante sin volver a consultar internet.
- 🪟 **Diseño Fluent Windows 11:** Efectos de desenfoque **Acrylic Blur**, **Mica**, o modo **Completamente Transparente** (solo las letras flotando sobre la barra).
- 👻 **Modo Fantasma (Click-Through):** Al activarlo, el widget se vuelve traspasable al ratón para que puedas hacer clic en la barra de tareas o ventanas que estén detrás sin que el widget interfiera.
- 🔒 **Bloqueo y Posicionamiento Libre:** Si prefieres usarlo como widget flotante, arrástralo donde quieras y bloquéalo con un clic.
- ⚙️ **Panel de Personalización en vivo:** Cambia colores, tamaño de letra, opacidad de fondo, alineación y desfase temporal (offset ± ms).
- 🕒 **Auto-ocultar:** Se puede configurar para ocultarse automáticamente cuando la música se detiene.

---

## 🚀 Instalación y Uso

### Opción 1: Instalador Automático (Recomendado)
1. Descarga `FluentLyrics-Setup-v1.1.0.exe` desde la sección de [Releases](https://github.com/xJhony911/FluentLyrics/releases).
2. Ejecuta el instalador. Puedes elegir que inicie automáticamente al encender Windows.

### Opción 2: Versión Portable
1. Descarga el ejecutable autónomo `FluentLyrics.exe`.
2. Guárdalo en la carpeta que prefieras y ejecútalo directamente. No requiere instalar nada.

---

## 🎛️ Controles y Atajos

| Acción | Cómo realizarlo |
| :--- | :--- |
| **Mover el widget** | Haz clic izquierdo sobre el widget y arrástralo donde quieras. |
| **Bloquear posición** | Pasa el ratón sobre el widget y presiona el candado `🔓` o desde el icono en la bandeja. |
| **Abrir Ajustes** | Haz clic en el engranaje `⚙` o clic derecho en el icono de la bandeja del sistema. |
| **Modo Fantasma** | Clic derecho en el icono de la bandeja del sistema -> `Modo Fantasma (Click-Through)`. |
| **Ocultar / Mostrar** | Doble clic en el icono de la bandeja del sistema. |

---

## 🛠️ Tecnologías Utilizadas

- **C# .NET 9.0 (WPF)**
- **Windows Runtime (WinRT) GSMTC API** (`Windows.Media.Control`)
- **Desktop Window Manager (DWM) API** para Mica y Acrílico nativo
- **LRCLIB REST API**
- **Inno Setup 6** para el instalador

---

## 📦 Compilación desde el Código Fuente

Si deseas compilar el proyecto manualmente:

```powershell
# Clonar el repositorio
git clone https://github.com/xJhony911/FluentLyrics.git
cd FluentLyrics

# Compilar ejecutable único para Windows x64
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish

# Generar el instalador (requiere Inno Setup 6)
iscc installer\setup.iss
```

---

## 📄 Licencia

Este proyecto está distribuido bajo la Licencia **MIT**. Consulta el archivo `LICENSE` para más detalles.
