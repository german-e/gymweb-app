// Kiosco de ingreso: captura del lector de códigos (teclado + Enter), teclado en pantalla, resultado a pantalla
// completa con cuenta regresiva, sonido, pantalla de espera con avisos y controles del personal.
(function () {
  'use strict';
  const root = document.querySelector('[data-kiosco]');
  if (!root) return;

  const $ = (s) => root.querySelector(s);
  const cfg = {
    url: root.dataset.url,
    segundos: parseInt(root.dataset.segundosResultado || '6', 10),
    sonido: root.dataset.sonido === '1',
    espera: root.dataset.espera === '1',
    inactividad: parseInt(root.dataset.segundosInactividad || '30', 10),
    rotacion: parseInt(root.dataset.segundosRotacion || '8', 10)
  };
  const token = root.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
  const input = $('[data-kiosco-input]');
  const pantallas = { listo: $('[data-pantalla="listo"]'), resultado: $('[data-pantalla="resultado"]'), espera: $('[data-pantalla="espera"]') };
  const teclado = $('[data-kiosco-teclado]');
  let timerResultado = null, timerInactividad = null, timerRotacion = null, enCurso = false;

  const el = (tag, attrs, ...hijos) => {
    const e = document.createElement(tag);
    Object.entries(attrs || {}).forEach(([k, v]) => { if (v == null) return; if (k === 'class') e.className = v; else if (k === 'text') e.textContent = v; else e.setAttribute(k, v); });
    hijos.forEach((h) => h != null && e.append(h));
    return e;
  };

  // ---------- Foco permanente en el campo del lector ----------
  const tecladoAbierto = () => !teclado.hidden;
  function enfocar() { if (!tecladoAbierto()) input.focus({ preventScroll: true }); }
  input.addEventListener('blur', () => setTimeout(enfocar, 60));
  document.addEventListener('pointerdown', (e) => { if (!e.target.closest('.kiosco-controles, [data-kiosco-teclado], [data-kiosco-teclado-abrir]')) setTimeout(enfocar, 0); });

  // ---------- Reloj ----------
  const fmtHora = new Intl.DateTimeFormat('es-AR', { hour: '2-digit', minute: '2-digit' });
  const fmtFecha = new Intl.DateTimeFormat('es-AR', { weekday: 'long', day: 'numeric', month: 'long' });
  function reloj() {
    const ahora = new Date();
    $('[data-kiosco-hora]').textContent = fmtHora.format(ahora);
    const f = fmtFecha.format(ahora);
    $('[data-kiosco-fecha]').textContent = f.charAt(0).toUpperCase() + f.slice(1);
  }
  reloj(); setInterval(reloj, 15000);

  // ---------- Sonido ----------
  let audio = null;
  function sonar(estado) {
    if (!cfg.sonido) return;
    try {
      audio = audio || new (window.AudioContext || window.webkitAudioContext)();
      const notas = estado === 'autorizado' ? [[660, 0], [880, 0.12]] : estado === 'restringido' ? [[520, 0], [520, 0.22]] : [[300, 0], [220, 0.2]];
      notas.forEach(([f, t]) => {
        const o = audio.createOscillator(), g = audio.createGain();
        o.type = 'sine'; o.frequency.value = f;
        g.gain.setValueAtTime(0.0001, audio.currentTime + t);
        g.gain.exponentialRampToValueAtTime(0.25, audio.currentTime + t + 0.02);
        g.gain.exponentialRampToValueAtTime(0.0001, audio.currentTime + t + 0.18);
        o.connect(g).connect(audio.destination);
        o.start(audio.currentTime + t); o.stop(audio.currentTime + t + 0.2);
      });
    } catch { /* sin audio */ }
  }

  // ---------- Pantallas ----------
  // "listo" nunca se oculta: resultado y espera se superponen, así el campo del lector conserva el foco
  // y un escaneo hecho sobre la pantalla de espera no se pierde.
  function mostrar(nombre) {
    Object.entries(pantallas).forEach(([k, p]) => { if (p) p.hidden = k !== nombre && k !== 'listo'; });
    root.dataset.pantallaActual = nombre;
  }

  function volverAListo() {
    clearTimeout(timerResultado);
    root.classList.remove('is-autorizado', 'is-restringido', 'is-denegado');
    mostrar('listo');
    reiniciarInactividad();
    enfocar();
  }
  pantallas.resultado.addEventListener('pointerdown', volverAListo);

  const iconos = { autorizado: 'bi-check-circle-fill', restringido: 'bi-exclamation-triangle-fill', denegado: 'bi-x-octagon-fill' };

  function pintarResultado(r) {
    const estado = r.estado in iconos ? r.estado : 'denegado';
    root.classList.remove('is-autorizado', 'is-restringido', 'is-denegado');
    root.classList.add('is-' + estado);
    $('[data-r-icono]').className = 'bi ' + iconos[estado];
    $('[data-r-titulo]').textContent = r.titulo;
    $('[data-r-mensaje]').textContent = r.mensaje || '';
    $('[data-r-nota]').textContent = r.nota || '';
    const s = r.socio;
    $('[data-r-nombre]').textContent = s ? s.nombre : '';
    const foto = $('[data-r-foto]');
    foto.replaceChildren(s?.foto ? el('img', { src: s.foto, alt: '' }) : el('span', { text: s ? s.iniciales : '' }, s ? null : el('i', { class: 'bi bi-person-x' })));
    foto.hidden = !s && estado !== 'denegado';
    const datos = s ? [['Plan', s.plan], ['Vencimiento', s.vence], ['Entrenador', estado === 'autorizado' ? s.entrenador : null], ['Hora de ingreso', estado === 'autorizado' ? r.hora : null]] : [];
    $('[data-r-dl]').replaceChildren(...datos.filter(([, v]) => v).map(([k, v]) => el('div', {}, el('dt', { text: k }), el('dd', { text: v }))));

    mostrar('resultado');
    sonar(estado);
    const barra = $('[data-r-progreso]');
    barra.style.transition = 'none'; barra.style.width = '100%';
    void barra.offsetWidth;   // reinicia la animación
    barra.style.transition = `width ${cfg.segundos}s linear`; barra.style.width = '0%';
    clearTimeout(timerResultado);
    timerResultado = setTimeout(volverAListo, cfg.segundos * 1000);
  }

  // ---------- Envío del código ----------
  async function procesar(codigo) {
    codigo = (codigo || '').trim();
    if (!codigo || enCurso) return;
    enCurso = true;
    ocultarEspera();
    const estado = $('[data-kiosco-estado]');
    estado.classList.add('is-busy'); estado.lastChild.textContent = ' Verificando…';
    try {
      const datos = new FormData(); datos.append('codigo', codigo);
      const resp = await fetch(cfg.url, { method: 'POST', body: datos, headers: { RequestVerificationToken: token, Accept: 'application/json' }, credentials: 'same-origin' });
      const tipo = resp.headers.get('content-type') || '';
      if (!resp.ok || !tipo.includes('application/json')) {
        // Sesión vencida o sin permiso: el kiosco se recarga (lleva al login del personal)
        pintarResultado({ estado: 'denegado', titulo: 'KIOSCO SIN SESIÓN', mensaje: 'Avisá en recepción para reactivar el kiosco.' });
        setTimeout(() => location.reload(), 4000);
        return;
      }
      pintarResultado(await resp.json());
    } catch {
      pintarResultado({ estado: 'denegado', titulo: 'SIN CONEXIÓN', mensaje: 'No se pudo verificar el ingreso.', nota: 'Diríjase a recepción' });
    } finally {
      enCurso = false;
      estado.classList.remove('is-busy'); estado.lastChild.textContent = ' Listo para escanear';
    }
  }

  $('[data-kiosco-form]').addEventListener('submit', (e) => { e.preventDefault(); const v = input.value; input.value = ''; procesar(v); });

  // ---------- Teclado en pantalla (DNI) ----------
  const display = $('[data-teclado-display]');
  let dni = '';
  const pintarDni = () => { display.textContent = dni ? dni.replace(/\B(?=(\d{3})+(?!\d))/g, '.') : ' '; };
  function abrirTeclado() { dni = ''; pintarDni(); teclado.hidden = false; ocultarEspera(); clearTimeout(timerInactividad); }
  function cerrarTeclado() { teclado.hidden = true; reiniciarInactividad(); enfocar(); }
  $('[data-kiosco-teclado-abrir]').addEventListener('click', abrirTeclado);
  $('[data-kiosco-teclado-cerrar]').addEventListener('click', cerrarTeclado);
  teclado.addEventListener('click', (e) => {
    const t = e.target.closest('[data-tecla]');
    if (!t) return;
    const k = t.dataset.tecla;
    if (k === 'borrar') dni = dni.slice(0, -1);
    else if (k === 'ok') { if (dni.length >= 6) { const v = dni; cerrarTeclado(); procesar(v); } return; }
    else if (dni.length < 10) dni += k;
    pintarDni();
  });

  // ---------- Pantalla de espera ----------
  let avisos = [];
  try { avisos = JSON.parse(document.getElementById('kioscoAvisos')?.textContent || '[]'); } catch { avisos = []; }
  let indice = 0;
  const slide = $('[data-espera-slide]'), puntos = $('[data-espera-puntos]');
  function pintarAviso() {
    const a = avisos[indice % avisos.length];
    slide.replaceChildren(...[   // replaceChildren convierte null en el texto "null": se filtra
      a.imagen ? el('img', { class: 'kiosco-espera__img', src: a.imagen, alt: '' }) : null,
      el('div', { class: 'kiosco-espera__texto' },
        el('span', { class: 'kiosco-espera__tipo', text: a.tipo }),
        el('h2', { text: a.titulo }),
        a.texto ? el('p', { text: a.texto }) : null,
        a.fecha ? el('span', { class: 'kiosco-espera__fecha' }, el('i', { class: 'bi bi-calendar-event' }), ' ' + a.fecha) : null)].filter(Boolean));
    slide.classList.toggle('sin-imagen', !a.imagen);
    slide.classList.remove('entra'); void slide.offsetWidth; slide.classList.add('entra');
    puntos.replaceChildren(...avisos.map((_, i) => el('span', { class: i === indice % avisos.length ? 'activo' : null })));
  }
  function mostrarEspera() {
    if (!cfg.espera || !avisos.length || root.dataset.pantallaActual !== 'listo' || tecladoAbierto()) return;
    indice = 0; pintarAviso(); mostrar('espera');
    clearInterval(timerRotacion);
    timerRotacion = setInterval(() => { indice++; pintarAviso(); }, cfg.rotacion * 1000);
  }
  function ocultarEspera() {
    clearInterval(timerRotacion);
    if (root.dataset.pantallaActual === 'espera') mostrar('listo');
  }
  function reiniciarInactividad() {
    clearTimeout(timerInactividad);
    root.classList.remove('sin-cursor');
    if (cfg.espera && avisos.length) timerInactividad = setTimeout(mostrarEspera, cfg.inactividad * 1000);
    setTimeout(() => root.classList.add('sin-cursor'), 8000);
  }
  ['pointerdown', 'keydown'].forEach((ev) => document.addEventListener(ev, () => {
    if (root.dataset.pantallaActual === 'espera') ocultarEspera();
    if (root.dataset.pantallaActual !== 'resultado') reiniciarInactividad();
  }, true));
  pantallas.espera?.addEventListener('pointerdown', () => { ocultarEspera(); reiniciarInactividad(); });

  // ---------- Controles del personal ----------
  const fs = $('[data-kiosco-fullscreen]');
  fs.addEventListener('click', () => {
    if (document.fullscreenElement) document.exitFullscreen?.(); else document.documentElement.requestFullscreen?.().catch(() => {});
  });
  document.addEventListener('fullscreenchange', () => {
    fs.querySelector('i').className = 'bi ' + (document.fullscreenElement ? 'bi-fullscreen-exit' : 'bi-arrows-fullscreen');
    enfocar();
  });
  // Salir requiere mantener presionado 1,5 s (evita que un socio cierre el kiosco por accidente)
  const salir = $('[data-kiosco-salir]');
  let timerSalir = null;
  salir.addEventListener('click', (e) => e.preventDefault());
  salir.addEventListener('pointerdown', () => { salir.classList.add('presionando'); timerSalir = setTimeout(() => { location.href = salir.href; }, 1500); });
  ['pointerup', 'pointerleave', 'pointercancel'].forEach((ev) => salir.addEventListener(ev, () => { salir.classList.remove('presionando'); clearTimeout(timerSalir); }));

  // Mantener la pantalla encendida (si el navegador lo permite)
  async function mantenerEncendida() { try { await navigator.wakeLock?.request('screen'); } catch { /* no disponible */ } }
  document.addEventListener('visibilitychange', () => { if (document.visibilityState === 'visible') mantenerEncendida(); });
  mantenerEncendida();

  mostrar('listo');
  reiniciarInactividad();
  enfocar();
  window.GymKiosco = { procesar, volverAListo, mostrarEspera };   // para pruebas
})();
