// FluentLyrics Interactive Simulator
document.addEventListener('DOMContentLoaded', () => {
  const words = ["And", "you", "will", "FIND", "a", "way", "through", "the", "neon", "rain..."];
  let currentWordIdx = 0;
  let currentMode = 'highlight'; // 'highlight', 'oneword', 'desktop', 'rgb'
  let rgbHue = 180;

  const lyricActive = document.getElementById('lyricActive');
  const widgetDot = document.getElementById('widgetDot');
  const virtualTaskbar = document.getElementById('virtualTaskbar');
  const modeButtons = document.querySelectorAll('.demo-mode-btn');

  // Mode button click handling
  modeButtons.forEach(btn => {
    btn.addEventListener('click', () => {
      modeButtons.forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      currentMode = btn.dataset.mode;

      if (currentMode === 'desktop') {
        virtualTaskbar.classList.add('desktop-mode');
      } else {
        virtualTaskbar.classList.remove('desktop-mode');
      }

      renderLyric();
    });
  });

  // Render lyric depending on current mode
  function renderLyric() {
    if (!lyricActive) return;

    if (currentMode === 'oneword') {
      // TikTok One Word pop mode
      const activeWord = words[currentWordIdx];
      lyricActive.innerHTML = `<span class="word-active" style="animation: pop 0.15s ease;">${activeWord}</span>`;
      return;
    }

    // Full line rendering (Word Highlight, Desktop, RGB)
    let html = '';
    words.forEach((word, idx) => {
      let className = 'word-future';
      let extraStyle = '';

      if (idx === currentWordIdx) {
        className = 'word-active';
        if (currentMode === 'rgb') {
          extraStyle = `color: hsl(${rgbHue}, 100%, 65%); text-shadow: 0 0 15px hsl(${rgbHue}, 100%, 60%);`;
        }
      } else if (idx < currentWordIdx) {
        className = 'word-past';
      }

      html += `<span class="${className}" style="${extraStyle}">${word}</span> `;
    });

    lyricActive.innerHTML = html.trim();
  }

  // Timer loop for word advancement & RGB cycle
  setInterval(() => {
    // RGB Hue cycling
    if (currentMode === 'rgb') {
      rgbHue = (rgbHue + 3) % 360;
      if (widgetDot) {
        widgetDot.style.background = `hsl(${rgbHue}, 100%, 55%)`;
        widgetDot.style.boxShadow = `0 0 10px hsl(${rgbHue}, 100%, 55%)`;
      }
    } else {
      if (widgetDot) {
        widgetDot.style.background = '#00f0ff';
        widgetDot.style.boxShadow = '0 0 10px #00f0ff';
      }
    }

    renderLyric();
  }, 50);

  // Advance to next word every 500ms
  setInterval(() => {
    currentWordIdx = (currentWordIdx + 1) % words.length;
    renderLyric();
  }, 480);

  // Initial render
  renderLyric();
});
