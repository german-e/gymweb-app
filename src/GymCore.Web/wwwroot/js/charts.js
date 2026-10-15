// Gráficos de Inteligencia de negocio. SVG propio, sin dependencias.
// <div class="viz is-loading" data-chart="line|bars|diverging|donut" data-config='{...}'><svg></svg></div>
// Reglas: trazos de 2px, barras <= 24px con extremo redondeado de 4px, grilla hairline sólida, tooltip con
// todas las series (teclado incluido), etiquetas selectivas y textos siempre con textContent (son datos de usuario).
(function () {
  'use strict';
  const NS = 'http://www.w3.org/2000/svg';
  const fmtEntero = new Intl.NumberFormat('es-AR', { maximumFractionDigits: 0 });
  const fmtDecimal = new Intl.NumberFormat('es-AR', { maximumFractionDigits: 1 });
  const fmtCompacto = new Intl.NumberFormat('es-AR', { notation: 'compact', maximumFractionDigits: 1 });

  function formatear(v, formato, compacto) {
    if (v === null || v === undefined || Number.isNaN(v)) return '—';
    if (formato === 'dinero') return '$ ' + (compacto && Math.abs(v) >= 1000 ? fmtCompacto.format(v) : fmtEntero.format(v));
    if (formato === 'porcentaje') return fmtDecimal.format(v) + '%';
    if (formato === 'decimal') return fmtDecimal.format(v);
    return compacto && Math.abs(v) >= 1000 ? fmtCompacto.format(v) : fmtEntero.format(v);
  }

  function el(tag, attrs, parent) {
    const n = document.createElementNS(NS, tag);
    for (const k in attrs) n.setAttribute(k, attrs[k]);
    if (parent) parent.appendChild(n);
    return n;
  }

  function texto(parent, x, y, contenido, attrs) {
    const t = el('text', Object.assign({ x, y }, attrs || {}), parent);
    t.textContent = contenido;
    return t;
  }

  // Escala "linda": 0 .. max redondeado, ~4 divisiones
  function escala(min, max) {
    if (max === min) { max = max === 0 ? 1 : max * 1.2; }
    const rango = max - Math.min(0, min);
    const paso0 = rango / 4;
    const mag = Math.pow(10, Math.floor(Math.log10(paso0)));
    const paso = [1, 2, 2.5, 5, 10].map(m => m * mag).find(p => p >= paso0) || paso0;
    const lo = Math.min(0, Math.floor(min / paso) * paso);
    const hi = Math.ceil(max / paso) * paso;
    const ticks = [];
    for (let v = lo; v <= hi + paso / 2; v += paso) ticks.push(Math.round(v * 1e6) / 1e6);
    return { lo, hi, ticks };
  }

  // x, y en coordenadas del viewBox del SVG; se convierten a píxeles del contenedor
  function tooltip(cont) {
    let tip = cont.querySelector('.viz-tip');
    if (!tip) { tip = document.createElement('div'); tip.className = 'viz-tip'; tip.hidden = true; tip.setAttribute('role', 'status'); cont.appendChild(tip); }
    return {
      mostrar(titulo, filas, x, y) {
        tip.replaceChildren();
        const h = document.createElement('div'); h.className = 'viz-tip__titulo'; h.textContent = titulo; tip.appendChild(h);
        filas.forEach(f => {
          const r = document.createElement('div'); r.className = 'viz-tip__fila';
          const k = document.createElement('span'); k.className = 'viz-tip__key ' + (f.forma || 'linea'); k.style.background = f.color;
          const v = document.createElement('strong'); v.textContent = f.valor;
          const n = document.createElement('span'); n.className = 'viz-tip__nombre'; n.textContent = f.nombre;
          r.append(k, v, n); tip.appendChild(r);
        });
        tip.hidden = false;
        const svg = cont.querySelector('svg'), rs = svg.getBoundingClientRect(), rc = cont.getBoundingClientRect();
        const vb = svg.viewBox && svg.viewBox.baseVal, k = vb && vb.width && rs.width ? rs.width / vb.width : 1;
        const px = rs.left - rc.left + x * k, py = rs.top - rc.top + y * k;
        const w = tip.offsetWidth, cw = cont.clientWidth;
        tip.style.left = Math.max(4, Math.min(cw - w - 4, px - w / 2)) + 'px';
        tip.style.top = Math.max(0, py - tip.offsetHeight - 12) + 'px';
      },
      ocultar() { tip.hidden = true; }
    };
  }

  const color = (cont, slot) => getComputedStyle(cont).getPropertyValue('--series-' + slot).trim() || '#2a78d6';
  const varCss = (cont, nombre) => getComputedStyle(cont).getPropertyValue(nombre).trim();

  // ---------- Marco común: ejes, grilla y área de trazado ----------
  function marco(cont, svg, cfg, min, max) {
    const W = svg.clientWidth || cont.clientWidth || 600;
    const H = svg.clientHeight || 240;
    const sc = escala(min, max);
    const etiquetasY = sc.ticks.map(t => formatear(t, cfg.formato, true));
    const izq = Math.max(...etiquetasY.map(t => t.length)) * 6.6 + 14;
    const pad = { l: izq, r: cfg.margenDerecho ?? 16, t: 12, b: 26 };
    const pw = W - pad.l - pad.r, ph = H - pad.t - pad.b;
    const y = v => pad.t + (1 - (v - sc.lo) / (sc.hi - sc.lo)) * ph;
    svg.setAttribute('viewBox', `0 0 ${W} ${H}`);
    svg.replaceChildren();
    const g = el('g', { class: 'viz-grid' }, svg);
    sc.ticks.forEach((t, i) => {
      el('line', { x1: pad.l, x2: W - pad.r, y1: y(t), y2: y(t), class: t === 0 ? 'viz-base' : 'viz-gridline' }, g);
      texto(g, pad.l - 8, y(t) + 3.5, etiquetasY[i], { 'text-anchor': 'end', class: 'viz-axis' });
    });
    return { W, H, pad, pw, ph, y, sc };
  }

  function etiquetasX(svg, m, etiquetas, xDe) {
    const cada = Math.ceil(etiquetas.length / Math.max(1, Math.floor(m.pw / 46)));
    etiquetas.forEach((e, i) => {
      if (i % cada !== 0 && i !== etiquetas.length - 1) return;
      texto(svg, xDe(i), m.H - 8, e, { 'text-anchor': 'middle', class: 'viz-axis' });
    });
  }

  // ---------- Línea (1 o 2 series; área suave si es una sola) ----------
  function linea(cont, svg, cfg) {
    const todos = cfg.series.flatMap(s => s.valores).filter(v => v !== null);
    const m = marco(cont, svg, Object.assign({ margenDerecho: 56 }, cfg), Math.min(0, ...todos), Math.max(0, ...todos));
    const n = cfg.etiquetas.length;
    const x = i => m.pad.l + (n === 1 ? m.pw / 2 : i * m.pw / (n - 1));
    etiquetasX(svg, m, cfg.etiquetas, x);
    const finales = [];
    cfg.series.forEach((s, si) => {
      const c = color(cont, s.slot || si + 1);
      const tramos = [[]];
      s.valores.forEach((v, i) => { if (v === null) { if (tramos[tramos.length - 1].length) tramos.push([]); } else tramos[tramos.length - 1].push([x(i), m.y(v)]); });
      tramos.filter(t => t.length).forEach(t => {
        const d = t.map((p, i) => (i ? 'L' : 'M') + p[0].toFixed(1) + ',' + p[1].toFixed(1)).join(' ');
        if (cfg.series.length === 1 && cfg.area !== false && t.length > 1)
          el('path', { d: d + ` L${t[t.length - 1][0]},${m.y(Math.max(0, m.sc.lo))} L${t[0][0]},${m.y(Math.max(0, m.sc.lo))} Z`, fill: c, 'fill-opacity': 0.1 }, svg);
        el('path', { d, fill: 'none', stroke: c, 'stroke-width': 2, 'stroke-linejoin': 'round', 'stroke-linecap': 'round' }, svg);
      });
      const ult = s.valores.map((v, i) => [v, i]).filter(p => p[0] !== null).pop();
      if (ult) {
        el('circle', { cx: x(ult[1]), cy: m.y(ult[0]), r: 4, fill: c, stroke: varCss(cont, '--viz-surface'), 'stroke-width': 2 }, svg);
        finales.push({ x: x(ult[1]), y: m.y(ult[0]), v: ult[0] });
      }
    });
    // Valor al final de cada línea, solo si no chocan entre sí
    const chocan = finales.length === 2 && Math.abs(finales[0].y - finales[1].y) < 16;
    if (!chocan) finales.forEach(f => texto(svg, f.x + 8, f.y + 4, formatear(f.v, cfg.formato, true), { class: 'viz-label' }));

    // Crosshair + tooltip (mouse, touch y teclado)
    const tip = tooltip(cont);
    const cruz = el('line', { y1: m.pad.t, y2: m.pad.t + m.ph, class: 'viz-cross', visibility: 'hidden' }, svg);
    const puntos = cfg.series.map((s, si) => el('circle', { r: 4.5, fill: color(cont, s.slot || si + 1), stroke: varCss(cont, '--viz-surface'), 'stroke-width': 2, visibility: 'hidden' }, svg));
    const hit = el('rect', { x: m.pad.l - 10, y: 0, width: m.pw + 20, height: m.H, fill: 'transparent' }, svg);
    let actual = n - 1;
    const mostrar = (i) => {
      actual = i;
      const cx = x(i);
      cruz.setAttribute('x1', cx); cruz.setAttribute('x2', cx); cruz.setAttribute('visibility', 'visible');
      const filas = cfg.series.map((s, si) => {
        const v = s.valores[i];
        if (v !== null) { puntos[si].setAttribute('cx', cx); puntos[si].setAttribute('cy', m.y(v)); puntos[si].setAttribute('visibility', 'visible'); }
        else puntos[si].setAttribute('visibility', 'hidden');
        return { color: color(cont, s.slot || si + 1), valor: formatear(v, cfg.formato), nombre: s.nombre };
      });
      const ys = cfg.series.map(s => s.valores[i]).filter(v => v !== null).map(m.y);
      tip.mostrar(cfg.titulos ? cfg.titulos[i] : cfg.etiquetas[i], filas, cx, ys.length ? Math.min(...ys) : m.pad.t);
    };
    const ocultar = () => { cruz.setAttribute('visibility', 'hidden'); puntos.forEach(p => p.setAttribute('visibility', 'hidden')); tip.ocultar(); };
    const indice = (ev) => {
      const r = svg.getBoundingClientRect();
      const px = ((ev.touches ? ev.touches[0].clientX : ev.clientX) - r.left) * (m.W / r.width);
      return Math.max(0, Math.min(n - 1, Math.round((px - m.pad.l) / (m.pw / Math.max(1, n - 1)))));
    };
    hit.addEventListener('pointermove', ev => mostrar(indice(ev)));
    hit.addEventListener('pointerleave', ocultar);
    teclado(svg, () => actual, n, mostrar, ocultar);
  }

  // ---------- Columnas agrupadas (1-2 series) ----------
  function barras(cont, svg, cfg) {
    const todos = cfg.series.flatMap(s => s.valores).filter(v => v !== null);
    const m = marco(cont, svg, cfg, 0, Math.max(0, ...todos));
    const n = cfg.etiquetas.length, k = cfg.series.length;
    const banda = m.pw / n;
    const ancho = Math.max(4, Math.min(24, (banda * 0.7 - 2 * (k - 1)) / k));
    const grupo = ancho * k + 2 * (k - 1);
    const xc = i => m.pad.l + banda * i + banda / 2;
    etiquetasX(svg, m, cfg.etiquetas, xc);
    const marcas = [];
    cfg.series.forEach((s, si) => s.valores.forEach((v, i) => {
      if (v === null) return;
      const x0 = xc(i) - grupo / 2 + si * (ancho + 2);
      const p = el('path', { d: columna(x0, m.y(0), ancho, m.y(v)), fill: color(cont, s.slot || si + 1), class: 'viz-mark' }, svg);
      marcas.push({ p, i });
    }));
    interaccionPorBanda(cont, svg, m, n, banda, cfg, marcas, (i) => cfg.series.map((s, si) => ({ color: color(cont, s.slot || si + 1), valor: formatear(s.valores[i], cfg.formato), nombre: s.nombre, forma: 'caja' })), i => Math.min(...cfg.series.map(s => m.y(Math.max(0, s.valores[i] ?? 0)))));
  }

  // ---------- Columnas divergentes (crecimiento neto: positivo / negativo) ----------
  function divergente(cont, svg, cfg) {
    const v = cfg.valores;
    const m = marco(cont, svg, cfg, Math.min(0, ...v), Math.max(0, ...v));
    const n = v.length, banda = m.pw / n, ancho = Math.min(24, banda * 0.6);
    const xc = i => m.pad.l + banda * i + banda / 2;
    etiquetasX(svg, m, cfg.etiquetas, xc);
    const pos = varCss(cont, '--viz-pos'), neg = varCss(cont, '--viz-neg');
    const marcas = v.map((val, i) => ({ i, p: el('path', { d: columna(xc(i) - ancho / 2, m.y(0), ancho, m.y(val)), fill: val >= 0 ? pos : neg, class: 'viz-mark' }, svg) }));
    interaccionPorBanda(cont, svg, m, n, banda, cfg, marcas, (i) => [{ color: v[i] >= 0 ? pos : neg, valor: (v[i] > 0 ? '+' : '') + formatear(v[i], cfg.formato), nombre: cfg.nombre || '', forma: 'caja' }], i => m.y(Math.max(0, v[i])));
  }

  // Path de columna: extremo de datos redondeado (4px), base recta sobre la línea de cero
  function columna(x, base, w, tope) {
    const alto = Math.abs(base - tope);
    if (alto < 0.5) return `M${x},${base} h${w} v-0.5 h${-w} Z`;
    const r = Math.min(4, w / 2, alto);
    if (tope < base) return `M${x},${base} V${tope + r} Q${x},${tope} ${x + r},${tope} H${x + w - r} Q${x + w},${tope} ${x + w},${tope + r} V${base} Z`;
    return `M${x},${base} V${tope - r} Q${x},${tope} ${x + r},${tope} H${x + w - r} Q${x + w},${tope} ${x + w},${tope - r} V${base} Z`;
  }

  function interaccionPorBanda(cont, svg, m, n, banda, cfg, marcas, filasDe, yDe) {
    const tip = tooltip(cont);
    let actual = n - 1;
    const mostrar = (i) => {
      actual = i;
      marcas.forEach(mk => mk.p.classList.toggle('is-dim', mk.i !== i));
      tip.mostrar(cfg.titulos ? cfg.titulos[i] : cfg.etiquetas[i], filasDe(i), m.pad.l + banda * i + banda / 2, yDe(i));
    };
    const ocultar = () => { marcas.forEach(mk => mk.p.classList.remove('is-dim')); tip.ocultar(); };
    for (let i = 0; i < n; i++) {
      // Área sensible: toda la banda (más grande que la marca)
      const r = el('rect', { x: m.pad.l + banda * i, y: m.pad.t, width: banda, height: m.ph, fill: 'transparent' }, svg);
      r.addEventListener('pointerenter', () => mostrar(i));
      r.addEventListener('pointerleave', ocultar);
    }
    teclado(svg, () => actual, n, mostrar, ocultar);
  }

  // ---------- Donut (parte del todo, <= 6 segmentos) ----------
  function donut(cont, svg, cfg) {
    const S = Math.min(svg.clientWidth || 220, svg.clientHeight || 220);
    const R = S / 2 - 4, r = R * 0.62, c = S / 2;
    svg.setAttribute('viewBox', `0 0 ${S} ${S}`);
    svg.replaceChildren();
    const total = cfg.segmentos.reduce((a, s) => a + s.valor, 0);
    const tip = tooltip(cont);
    if (total <= 0) { el('circle', { cx: c, cy: c, r: (R + r) / 2, fill: 'none', stroke: varCss(cont, '--viz-grid'), 'stroke-width': R - r }, svg); }
    let ang = -Math.PI / 2;
    const segs = [];
    cfg.segmentos.forEach((s, i) => {
      if (s.valor <= 0) return;
      const a = (s.valor / total) * Math.PI * 2;
      const a1 = ang, a2 = ang + a; ang = a2;
      const p = el('path', { d: arco(c, R, r, a1, a2), fill: color(cont, s.slot || i + 1), stroke: varCss(cont, '--viz-surface'), 'stroke-width': 2, class: 'viz-mark', tabindex: 0 }, svg);
      const mostrar = () => {
        segs.forEach(o => o.classList.toggle('is-dim', o !== p));
        const medio = (a1 + a2) / 2;
        tip.mostrar(s.nombre, [{ color: color(cont, s.slot || i + 1), valor: formatear(s.valor, cfg.formato) + ' · ' + fmtDecimal.format(s.valor * 100 / total) + '%', nombre: '', forma: 'caja' }],
          c + Math.cos(medio) * R * 0.8, c + Math.sin(medio) * R * 0.8);
      };
      const ocultar = () => { segs.forEach(o => o.classList.remove('is-dim')); tip.ocultar(); };
      p.addEventListener('pointerenter', mostrar); p.addEventListener('pointerleave', ocultar);
      p.addEventListener('focus', mostrar); p.addEventListener('blur', ocultar);
      segs.push(p);
    });
    texto(svg, c, c - 2, cfg.centro ?? formatear(total, cfg.formato, true), { 'text-anchor': 'middle', class: 'viz-centro' });
    if (cfg.centroDetalle) texto(svg, c, c + 16, cfg.centroDetalle, { 'text-anchor': 'middle', class: 'viz-axis' });
  }

  function arco(c, R, r, a1, a2) {
    const grande = a2 - a1 > Math.PI ? 1 : 0;
    if (a2 - a1 >= Math.PI * 2 - 1e-6) a2 -= 1e-4;
    const p = (rad, a) => [c + rad * Math.cos(a), c + rad * Math.sin(a)];
    const [x1, y1] = p(R, a1), [x2, y2] = p(R, a2), [x3, y3] = p(r, a2), [x4, y4] = p(r, a1);
    return `M${x1},${y1} A${R},${R} 0 ${grande} 1 ${x2},${y2} L${x3},${y3} A${r},${r} 0 ${grande} 0 ${x4},${y4} Z`;
  }

  // Flechas izquierda/derecha recorren los puntos con el mismo tooltip que el mouse
  function teclado(svg, actual, n, mostrar, ocultar) {
    svg.setAttribute('tabindex', '0');
    svg.addEventListener('focus', () => mostrar(actual()));
    svg.addEventListener('blur', ocultar);
    svg.addEventListener('keydown', (e) => {
      if (e.key === 'ArrowRight') { e.preventDefault(); mostrar(Math.min(n - 1, actual() + 1)); }
      if (e.key === 'ArrowLeft') { e.preventDefault(); mostrar(Math.max(0, actual() - 1)); }
      if (e.key === 'Escape') ocultar();
    });
  }

  // ---------- Sparkline de tarjetas KPI (tendencia de 12 meses) ----------
  function sparkline(svg) {
    let v = [];
    try { v = JSON.parse(svg.dataset.spark || '[]'); } catch { }
    if (v.length < 2) return;
    const W = 120, H = 34, min = Math.min(...v), max = Math.max(...v), rango = max - min || 1;
    const x = i => 2 + i * (W - 6) / (v.length - 1), y = val => 3 + (1 - (val - min) / rango) * (H - 8);
    svg.setAttribute('viewBox', `0 0 ${W} ${H}`);
    el('path', { d: v.map((val, i) => (i ? 'L' : 'M') + x(i).toFixed(1) + ',' + y(val).toFixed(1)).join(' '), class: 'viz-spark' }, svg);
    el('circle', { cx: x(v.length - 1), cy: y(v[v.length - 1]), r: 3, class: 'viz-spark-dot' }, svg);
  }

  const tipos = { line: linea, bars: barras, diverging: divergente, donut };

  function dibujar(cont) {
    const svg = cont.querySelector('svg');
    let cfg;
    try { cfg = JSON.parse(cont.dataset.config); } catch { return; }
    const f = tipos[cont.dataset.chart];
    if (!f || !svg) return;
    f(cont, svg, cfg);
    cont.classList.remove('is-loading');
  }

  document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('svg[data-spark]').forEach(sparkline);
    document.querySelectorAll('[data-chart]').forEach((cont) => {
      dibujar(cont);
      let ancho = cont.clientWidth;
      new ResizeObserver(() => { if (cont.clientWidth !== ancho) { ancho = cont.clientWidth; dibujar(cont); } }).observe(cont);
    });
  });
})();
