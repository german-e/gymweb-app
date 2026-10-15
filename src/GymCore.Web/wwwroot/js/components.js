// Comportamientos reutilizables. Se activan por atributos data-*, sin dependencias salvo Bootstrap.
(function () {
  'use strict';

  const normalizar = (s) => (s || '').toString().normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().trim();

  // ---------- Modal de confirmación genérico (_ConfirmModal) ----------
  function initConfirm() {
    const modalEl = document.getElementById('confirmModal');
    if (!modalEl || !window.bootstrap) return;
    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    const form = modalEl.querySelector('[data-confirm-form]');
    const submit = modalEl.querySelector('[data-confirm-submit]');
    const icon = modalEl.querySelector('[data-confirm-icon]');

    document.addEventListener('click', (e) => {
      const btn = e.target.closest('[data-confirm-action]');
      if (!btn) return;
      e.preventDefault();
      const variant = btn.dataset.confirmVariant || 'danger';
      form.action = btn.dataset.confirmAction;
      modalEl.querySelector('[data-confirm-title-target]').textContent = btn.dataset.confirmTitle || '¿Confirmar acción?';
      modalEl.querySelector('[data-confirm-text-target]').textContent = btn.dataset.confirmText || '';
      submit.textContent = btn.dataset.confirmLabel || 'Confirmar';
      submit.className = 'btn px-3 btn-' + variant;
      icon.className = 'module-icon ' + variant;
      modal.show();
    });
  }

  // ---------- Filtro instantáneo de tablas ----------
  // <input data-table-filter="#tablaId">  filas con data-search="texto"
  // Filtros extra: <... data-table-facet="#tablaId" data-facet="estado"> con valor en data-<facet> de la fila
  //   data-facet-mode="tokens": la fila lista varias etiquetas separadas por espacio (ej. data-tags="activo moroso")
  function initTableFilter() {
    document.querySelectorAll('[data-table-filter]').forEach((input) => {
      const table = document.querySelector(input.dataset.tableFilter);
      if (!table) return;
      const rows = Array.from(table.querySelectorAll('tbody tr[data-search]'));
      const emptyRow = table.querySelector('[data-filter-empty]');
      const counter = document.querySelector('[data-filter-count="' + input.dataset.tableFilter + '"]');
      const facets = Array.from(document.querySelectorAll('[data-table-facet="' + input.dataset.tableFilter + '"]'));

      const facetValue = (el) => el.matches('select') ? el.value : (el.querySelector('.active')?.dataset.value ?? '');

      const apply = () => {
        const q = normalizar(input.value);
        let visibles = 0;
        rows.forEach((row) => {
          let ok = !q || normalizar(row.dataset.search).includes(q);
          facets.forEach((f) => {
            const v = facetValue(f);
            if (!ok || !v) return;
            const valorFila = normalizar(row.dataset[f.dataset.facet]);
            ok = f.dataset.facetMode === 'tokens'
              ? valorFila.split(/\s+/).includes(normalizar(v))
              : valorFila === normalizar(v);
          });
          row.hidden = !ok;
          if (ok) visibles++;
        });
        if (emptyRow) emptyRow.hidden = visibles !== 0 || rows.length === 0;
        if (counter) counter.textContent = visibles;
      };

      input.addEventListener('input', apply);
      facets.forEach((f) => {
        if (f.matches('select')) f.addEventListener('change', apply);
        else f.addEventListener('click', (e) => {
          const chip = e.target.closest('[data-value]');
          if (!chip) return;
          f.querySelectorAll('[data-value]').forEach((c) => c.classList.toggle('active', c === chip));
          apply();
        });
      });

      // Atajo "/" para enfocar el buscador
      document.addEventListener('keydown', (e) => {
        if (e.key === '/' && !/input|textarea|select/i.test(document.activeElement.tagName)) {
          e.preventDefault();
          input.focus();
        }
      });
      apply();
    });
  }

  // ---------- Navegación por secciones con scrollspy ----------
  function initSectionNav() {
    const nav = document.querySelector('[data-section-nav]');
    if (!nav) return;
    const links = Array.from(nav.querySelectorAll('a[href^="#"]'));
    const sections = links.map((a) => document.querySelector(a.getAttribute('href'))).filter(Boolean);
    const setActive = (id) => links.forEach((a) => a.classList.toggle('active', a.getAttribute('href') === '#' + id));

    const io = new IntersectionObserver((entries) => {
      entries.filter((en) => en.isIntersecting).forEach((en) => setActive(en.target.id));
    }, { rootMargin: '-30% 0px -60% 0px' });
    sections.forEach((s) => io.observe(s));

    // Marca secciones con errores de validación
    sections.forEach((s) => {
      if (s.querySelector('.field-validation-error, .input-validation-error')) {
        const link = links.find((a) => a.getAttribute('href') === '#' + s.id);
        if (link && !link.querySelector('.has-error')) link.insertAdjacentHTML('beforeend', '<i class="bi bi-exclamation-circle-fill has-error"></i>');
      }
    });
  }

  // ---------- Vista previa de foto ----------
  // <input type="file" data-photo-input="#previewId">
  function initPhotoPreview() {
    document.querySelectorAll('[data-photo-input]').forEach((input) => {
      const preview = document.querySelector(input.dataset.photoInput);
      if (!preview) return;
      input.addEventListener('change', () => {
        const file = input.files && input.files[0];
        if (!file) return;
        const url = URL.createObjectURL(file);
        preview.innerHTML = '';
        const img = document.createElement('img');
        img.src = url;
        img.alt = 'Vista previa';
        preview.appendChild(img);
        const remove = document.querySelector('[data-photo-remove]');
        if (remove) remove.checked = false;
      });
    });
  }

  // ---------- Pestañas persistentes por hash (#socios, #historial) ----------
  function initHashTabs() {
    // Accesos a pestañas desde fuera de la barra: <button data-open-tab="#tab-x">
    document.addEventListener('click', (e) => {
      const b = e.target.closest('[data-open-tab]');
      if (!b || !window.bootstrap) return;
      const t = document.querySelector('[data-bs-toggle="tab"][data-bs-target="' + b.dataset.openTab + '"]');
      if (!t) return;
      bootstrap.Tab.getOrCreateInstance(t).show();
      t.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
    const hash = window.location.hash;
    if (!hash || !window.bootstrap) return;
    const trigger = document.querySelector('[data-bs-toggle="tab"][data-bs-target="' + hash + '"]');
    if (trigger) {
      // Al entrar por enlace se muestra sin animación (evita un instante en blanco)
      const pane = document.querySelector(hash);
      const conFade = pane && pane.classList.contains('fade');
      if (conFade) pane.classList.remove('fade');
      bootstrap.Tab.getOrCreateInstance(trigger).show();
      if (conFade) requestAnimationFrame(() => pane.classList.add('fade'));
    }
    document.querySelectorAll('[data-bs-toggle="tab"]').forEach((t) => {
      t.addEventListener('shown.bs.tab', () => history.replaceState(null, '', t.dataset.bsTarget));
    });
  }

  // ---------- Códigos de barras de credenciales ----------
  // <svg data-barcode="CODIGO">. Carga JsBarcode bajo demanda (misma librería y formato que la tarjeta original).
  function cargarScript(src) {
    return new Promise((ok, err) => {
      if (document.querySelector('script[src="' + src + '"]')) return ok();
      const s = document.createElement('script');
      s.src = src; s.onload = ok; s.onerror = err;
      document.head.appendChild(s);
    });
  }
  async function initBarcodes() {
    const svgs = document.querySelectorAll('svg[data-barcode]');
    if (!svgs.length) return;
    try {
      if (!window.JsBarcode) await cargarScript('https://cdn.jsdelivr.net/npm/jsbarcode@3.11.6/dist/JsBarcode.all.min.js');
      svgs.forEach((svg) => {
        try {
          JsBarcode(svg, svg.dataset.barcode, {
            format: 'CODE128', lineColor: '#0f172a', background: 'transparent',
            width: 2, height: 60, displayValue: true, fontSize: 13, textMargin: 2, margin: 0
          });
          svg.removeAttribute('width'); svg.removeAttribute('height');
          svg.setAttribute('preserveAspectRatio', 'xMidYMid meet');
        } catch { /* código inválido para CODE128 */ }
      });
    } catch { /* sin conexión al CDN: se muestra el código en el aria-label */ }
  }

  // ---------- Calendario de asistencia ----------
  // <div data-att-calendar='{"2026-10-01":1,...}'></div>  (fechas locales → cantidad de ingresos)
  function initAttendanceCalendar() {
    document.querySelectorAll('[data-att-calendar]').forEach((root) => {
      let datos = {};
      try { datos = JSON.parse(root.dataset.attCalendar || '{}'); } catch { }
      const hoy = new Date();
      let mes = new Date(hoy.getFullYear(), hoy.getMonth(), 1);
      const limite = new Date(hoy.getFullYear() - 1, hoy.getMonth(), 1);
      const pad = (n) => String(n).padStart(2, '0');
      const fmtMes = new Intl.DateTimeFormat('es-AR', { month: 'long', year: 'numeric' });

      const render = () => {
        const y = mes.getFullYear(), m = mes.getMonth();
        const offset = (new Date(y, m, 1).getDay() + 6) % 7; // lunes primero
        const dias = new Date(y, m + 1, 0).getDate();
        let total = 0;
        let html = ['Lu', 'Ma', 'Mi', 'Ju', 'Vi', 'Sá', 'Do'].map((d) => '<span class="dow">' + d + '</span>').join('');
        for (let i = 0; i < offset; i++) html += '<span class="att-day empty"></span>';
        for (let d = 1; d <= dias; d++) {
          const clave = y + '-' + pad(m + 1) + '-' + pad(d);
          const n = datos[clave] || 0;
          total += n;
          const esHoy = y === hoy.getFullYear() && m === hoy.getMonth() && d === hoy.getDate();
          html += '<span class="att-day' + (n ? ' on' : '') + (n > 1 ? ' multi' : '') + (esHoy ? ' today' : '') + '"' +
            (n ? ' title="' + n + (n > 1 ? ' ingresos' : ' ingreso') + '"' : '') + '>' + d + '</span>';
        }
        const puedeAtras = mes > limite;
        const puedeAdelante = mes < new Date(hoy.getFullYear(), hoy.getMonth(), 1);
        root.innerHTML =
          '<div class="att-calendar__head">' +
          '<button type="button" class="icon-btn" data-nav="-1" aria-label="Mes anterior"' + (puedeAtras ? '' : ' disabled') + '><i class="bi bi-chevron-left"></i></button>' +
          '<div class="text-center"><strong>' + (() => { const t = fmtMes.format(mes); return t.charAt(0).toUpperCase() + t.slice(1); })() + '</strong><div class="small text-muted">' + total + (total === 1 ? ' asistencia' : ' asistencias') + '</div></div>' +
          '<button type="button" class="icon-btn" data-nav="1" aria-label="Mes siguiente"' + (puedeAdelante ? '' : ' disabled') + '><i class="bi bi-chevron-right"></i></button>' +
          '</div><div class="att-calendar__grid">' + html + '</div>' +
          '<div class="att-legend"><span><i class="on"></i> Asistió</span><span><i></i> Sin ingreso</span></div>';
      };
      root.addEventListener('click', (e) => {
        const b = e.target.closest('[data-nav]');
        if (!b || b.disabled) return;
        mes = new Date(mes.getFullYear(), mes.getMonth() + Number(b.dataset.nav), 1);
        render();
      });
      render();
    });
  }

  // ---------- Gráfico de línea de una sola serie (evolución) ----------
  // <div class="metric-chart" data-metric-chart='[{"d":"2026-01-10","v":82.5},...]' data-unit="kg" data-decimals="1">
  //   <svg></svg>
  // Una métrica por gráfico (small multiples): nunca doble eje.
  function initMetricCharts() {
    document.querySelectorAll('[data-metric-chart]').forEach((root) => {
      let pts = [];
      try { pts = JSON.parse(root.dataset.metricChart || '[]'); } catch { }
      const svg = root.querySelector('svg');
      if (!svg || pts.length === 0) return;
      const unidad = root.dataset.unit || '';
      const dec = Number(root.dataset.decimals || 1);
      const fmtV = (v) => v.toLocaleString('es-AR', { minimumFractionDigits: dec, maximumFractionDigits: dec }) + (unidad ? ' ' + unidad : '');
      const fmtD = (d) => new Date(d + 'T12:00:00').toLocaleDateString('es-AR', { day: '2-digit', month: 'short', year: '2-digit' });

      const draw = () => {
        const W = svg.clientWidth || 300, H = svg.clientHeight || 120;
        const pad = { l: 36, r: 8, t: 8, b: 18 };
        const vals = pts.map((p) => p.v);
        let min = Math.min(...vals), max = Math.max(...vals);
        if (min === max) { min -= 1; max += 1; }
        const margen = (max - min) * 0.15; min -= margen; max += margen;
        const t0 = new Date(pts[0].d).getTime(), t1 = new Date(pts[pts.length - 1].d).getTime();
        const x = (p, i) => pts.length === 1 ? (pad.l + W - pad.r) / 2 : (t1 === t0 ? pad.l + i * (W - pad.l - pad.r) / (pts.length - 1) : pad.l + (new Date(p.d).getTime() - t0) / (t1 - t0) * (W - pad.l - pad.r));
        const y = (v) => pad.t + (1 - (v - min) / (max - min)) * (H - pad.t - pad.b);
        const coords = pts.map((p, i) => [x(p, i), y(p.v)]);
        const linea = coords.map((c, i) => (i ? 'L' : 'M') + c[0].toFixed(1) + ',' + c[1].toFixed(1)).join(' ');
        const base = H - pad.b;
        const area = linea + ' L' + coords[coords.length - 1][0].toFixed(1) + ',' + base + ' L' + coords[0][0].toFixed(1) + ',' + base + ' Z';
        let g = '';
        [0, 0.5, 1].forEach((f) => {
          const v = min + (max - min) * (1 - f), yy = pad.t + f * (H - pad.t - pad.b);
          g += '<line class="grid-line" x1="' + pad.l + '" x2="' + (W - pad.r) + '" y1="' + yy + '" y2="' + yy + '"/>' +
               '<text class="axis-label" x="' + (pad.l - 6) + '" y="' + (yy + 3) + '" text-anchor="end">' + v.toFixed(dec === 0 ? 0 : 1) + '</text>';
        });
        g += '<text class="axis-label" x="' + pad.l + '" y="' + (H - 2) + '">' + fmtD(pts[0].d) + '</text>';
        if (pts.length > 1) g += '<text class="axis-label" x="' + (W - pad.r) + '" y="' + (H - 2) + '" text-anchor="end">' + fmtD(pts[pts.length - 1].d) + '</text>';
        g += '<path class="area" d="' + area + '"/><path class="series" d="' + linea + '"/>';
        // Marcadores solo en extremos (selectivos); el resto aparece al pasar el mouse
        [0, coords.length - 1].forEach((i) => { g += '<circle class="dot" r="4" cx="' + coords[i][0] + '" cy="' + coords[i][1] + '"/>'; });
        g += '<line class="crosshair" y1="' + pad.t + '" y2="' + base + '" visibility="hidden"/><circle class="dot hover-dot" r="5" visibility="hidden"/>';
        g += '<rect x="0" y="0" width="' + W + '" height="' + H + '" fill="transparent" class="hit"/>';
        svg.setAttribute('viewBox', '0 0 ' + W + ' ' + H);
        svg.innerHTML = g;

        let tip = root.querySelector('.chart-tip');
        if (!tip) { tip = document.createElement('div'); tip.className = 'chart-tip'; tip.hidden = true; root.appendChild(tip); }
        const cross = svg.querySelector('.crosshair'), dot = svg.querySelector('.hover-dot');
        const mostrar = (ev) => {
          const r = svg.getBoundingClientRect();
          const mx = (ev.touches ? ev.touches[0].clientX : ev.clientX) - r.left;
          let k = 0; coords.forEach((c, i) => { if (Math.abs(c[0] - mx) < Math.abs(coords[k][0] - mx)) k = i; });
          const [cx, cy] = coords[k];
          cross.setAttribute('x1', cx); cross.setAttribute('x2', cx); cross.setAttribute('visibility', 'visible');
          dot.setAttribute('cx', cx); dot.setAttribute('cy', cy); dot.setAttribute('visibility', 'visible');
          tip.innerHTML = fmtD(pts[k].d) + ' · <b>' + fmtV(pts[k].v) + '</b>';
          tip.style.left = (svg.offsetLeft + cx) + 'px'; tip.style.top = (svg.offsetTop + cy) + 'px';
          tip.hidden = false;
        };
        const ocultar = () => { cross.setAttribute('visibility', 'hidden'); dot.setAttribute('visibility', 'hidden'); tip.hidden = true; };
        const hit = svg.querySelector('.hit');
        hit.addEventListener('mousemove', mostrar); hit.addEventListener('touchstart', mostrar, { passive: true });
        hit.addEventListener('mouseleave', ocultar);
      };
      draw();
      // Redibuja al mostrarse (pestañas ocultas miden 0) y al redimensionar
      new ResizeObserver(() => draw()).observe(svg);
    });
  }

  // ---------- Ordenamiento de tablas ----------
  // <table data-sortable> con <th data-sort="text|num">. La celda puede fijar su clave con data-sort-value.
  // Respeta los filtros activos (solo reordena) y deja la fila [data-filter-empty] al final.
  function initTableSort() {
    document.querySelectorAll('table[data-sortable]').forEach((table) => {
      const tbody = table.tBodies[0];
      const headers = Array.from(table.querySelectorAll('thead th[data-sort]'));
      headers.forEach((th) => {
        const idx = Array.from(th.parentNode.children).indexOf(th);
        th.classList.add('sortable');
        th.tabIndex = 0;
        th.insertAdjacentHTML('beforeend', '<i class="bi bi-arrow-down-up sort-ic" aria-hidden="true"></i>');
        const ordenar = () => {
          const dir = th.dataset.dir === 'asc' ? 'desc' : 'asc';
          headers.forEach((o) => {
            delete o.dataset.dir;
            o.removeAttribute('aria-sort');
            o.querySelector('.sort-ic').className = 'bi bi-arrow-down-up sort-ic';
          });
          th.dataset.dir = dir;
          th.setAttribute('aria-sort', dir === 'asc' ? 'ascending' : 'descending');
          th.querySelector('.sort-ic').className = 'bi sort-ic ' + (dir === 'asc' ? 'bi-sort-up' : 'bi-sort-down');
          const num = th.dataset.sort === 'num';
          const clave = (tr) => {
            const td = tr.children[idx];
            const v = td ? (td.dataset.sortValue ?? td.textContent) : '';
            return num ? (Number(v) || 0) : normalizar(v);
          };
          const filas = Array.from(tbody.rows).filter((tr) => !tr.hasAttribute('data-filter-empty'));
          filas.sort((a, b) => {
            const x = clave(a), y = clave(b);
            const c = num ? x - y : x.localeCompare(y, 'es');
            return dir === 'asc' ? c : -c;
          });
          filas.forEach((tr) => tbody.appendChild(tr));
          const vacia = tbody.querySelector('[data-filter-empty]');
          if (vacia) tbody.appendChild(vacia);
        };
        th.addEventListener('click', ordenar);
        th.addEventListener('keydown', (e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); ordenar(); } });
      });
    });
  }

  // ---------- Tooltips (Bootstrap) ----------
  // <el data-bs-toggle="tooltip" title="...">
  function initTooltips() {
    if (!window.bootstrap) return;
    document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach((el) => bootstrap.Tooltip.getOrCreateInstance(el, { container: 'body' }));
  }

  // ---------- Estado de carga al enviar ----------
  // <form data-submit-loading> deshabilita el botón de envío y muestra un spinner (texto: data-loading-text).
  // Solo actúa si la validación del cliente dejó pasar el envío (jquery.validate corta la propagación si falla).
  function initSubmitLoading() {
    document.addEventListener('submit', (e) => {
      const form = e.target;
      if (e.defaultPrevented || !form.matches('[data-submit-loading]')) return;
      const btn = e.submitter || form.querySelector('[type="submit"]');
      if (!btn) return;
      btn.dataset.originalHtml = btn.innerHTML;
      btn.disabled = true;
      btn.innerHTML = '<span class="spinner-border spinner-border-sm" aria-hidden="true"></span> ' + (btn.dataset.loadingText || 'Procesando…');
    });
    // Volver con el botón "atrás" restaura los botones (bfcache)
    window.addEventListener('pageshow', () => {
      document.querySelectorAll('[data-original-html]').forEach((b) => { b.innerHTML = b.dataset.originalHtml; b.disabled = false; delete b.dataset.originalHtml; });
    });
  }

  // ---------- Toasts de confirmación ----------
  // GymUi.toast('Mensaje', 'success' | 'danger' | 'info')
  function toast(mensaje, variante = 'success') {
    let host = document.querySelector('.toast-host');
    if (!host) {
      host = document.createElement('div');
      host.className = 'toast-host';
      host.setAttribute('aria-live', 'polite');
      document.body.appendChild(host);
    }
    const iconos = { success: 'bi-check-circle-fill', danger: 'bi-exclamation-triangle-fill', info: 'bi-info-circle-fill' };
    const t = document.createElement('div');
    t.className = 'app-toast ' + variante;
    t.setAttribute('role', variante === 'danger' ? 'alert' : 'status');
    t.innerHTML = '<i class="bi ' + (iconos[variante] || iconos.info) + '"></i><span></span>';
    t.querySelector('span').textContent = mensaje;
    host.appendChild(t);
    requestAnimationFrame(() => t.classList.add('show'));
    setTimeout(() => { t.classList.remove('show'); setTimeout(() => t.remove(), 300); }, 3200);
  }

  // ---------- Copiar al portapapeles ----------
  // <button data-copy="texto">
  function initCopy() {
    document.addEventListener('click', async (e) => {
      const b = e.target.closest('[data-copy]');
      if (!b) return;
      try { await navigator.clipboard.writeText(b.dataset.copy); toast('Copiado al portapapeles.'); }
      catch { toast('No se pudo copiar el texto.', 'danger'); }
    });
  }

  // ---------- Política de contraseñas (checklist en vivo) ----------
  // <input data-password-policy="#lista" data-password-user="#campoUsuario">; la validación real es del servidor
  function initPasswordPolicy() {
    document.querySelectorAll('[data-password-policy]').forEach((input) => {
      const lista = document.querySelector(input.dataset.passwordPolicy);
      if (!lista) return;
      const usuario = () => (document.querySelector(input.dataset.passwordUser)?.value || '').trim().toLowerCase();
      const reglas = {
        largo: (v) => v.length >= 8,
        letra: (v) => /\p{L}/u.test(v),
        numero: (v) => /\d/.test(v),
        usuario: (v) => v.length > 0 && (!usuario() || !v.toLowerCase().includes(usuario()))
      };
      const evaluar = () => {
        const v = input.value;
        lista.querySelectorAll('[data-regla]').forEach((li) => {
          const ok = reglas[li.dataset.regla](v);
          li.classList.toggle('ok', ok);
          li.querySelector('i').className = 'bi ' + (ok ? 'bi-check-circle-fill' : 'bi-circle');
        });
      };
      input.addEventListener('input', evaluar);
      document.querySelector(input.dataset.passwordUser)?.addEventListener('input', evaluar);
      evaluar();
    });
  }

  // ---------- Filtro de período (reportes): "Personalizado" muestra el rango de fechas ----------
  function initFiltroPeriodo() {
    document.querySelectorAll('[data-filtro-periodo]').forEach((form) => {
      const btn = form.querySelector('[data-periodo-custom]');
      const rango = form.querySelector('[data-periodo-rango]');
      if (!btn || !rango) return;
      btn.addEventListener('click', () => {
        rango.hidden = !rango.hidden;
        btn.setAttribute('aria-expanded', String(!rango.hidden));
        if (!rango.hidden) rango.querySelector('input')?.focus();
      });
    });
  }

  document.addEventListener('DOMContentLoaded', () => {
    initFiltroPeriodo();
    initPasswordPolicy();
    initTableSort();
    initTooltips();
    initSubmitLoading();
    initCopy();
    initAttendanceCalendar();
    initMetricCharts();
    initBarcodes();
    initConfirm();
    initTableFilter();
    initSectionNav();
    initPhotoPreview();
    initHashTabs();
  });

  window.GymUi = { normalizar, cargarScript, toast };
})();
