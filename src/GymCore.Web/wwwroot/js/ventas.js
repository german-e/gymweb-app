// Ventas e inventario: Punto de Venta, modal de movimientos de stock, categorías y formulario de producto.
// Todo el contenido proveniente de datos se inserta con textContent (nunca innerHTML).
(function () {
  'use strict';

  const fmt = new Intl.NumberFormat('es-AR', { minimumFractionDigits: 0, maximumFractionDigits: 2 });
  const dinero = (v) => '$ ' + fmt.format(Math.round((v || 0) * 100) / 100);
  const normalizar = (s) => (window.GymUi?.normalizar ? window.GymUi.normalizar(s || '') : (s || '').toLowerCase());
  const toast = (m, v) => window.GymUi?.toast ? window.GymUi.toast(m, v) : alert(m);
  const num = (v) => { const n = parseFloat(String(v ?? '').replace(',', '.')); return Number.isFinite(n) ? n : 0; };

  function el(tag, attrs, ...hijos) {
    const e = document.createElement(tag);
    Object.entries(attrs || {}).forEach(([k, v]) => {
      if (v === null || v === undefined || v === false) return;
      if (k === 'class') e.className = v;
      else if (k === 'text') e.textContent = v;
      else if (k.startsWith('on')) e.addEventListener(k.slice(2), v);
      else e.setAttribute(k, v === true ? '' : v);
    });
    hijos.flat().forEach((h) => h !== null && h !== undefined && e.append(h));
    return e;
  }
  const icono = (clase) => el('i', { class: 'bi ' + clase, 'aria-hidden': 'true' });

  // ========== Campos de código de barras: el escáner manda Enter, no debe enviar el formulario ==========
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' && e.target.matches?.('[data-enter-no-submit]')) e.preventDefault();
  });

  // ========== Modal de movimiento de stock ==========
  function initAjusteStock() {
    const modalEl = document.getElementById('ajusteStockModal');
    if (!modalEl || !window.bootstrap) return;
    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    const $ = (s) => modalEl.querySelector(s);
    const cantidad = $('[data-ajuste-cantidad]');
    const motivo = $('[data-ajuste-motivo]');
    let stock = 0;
    const ayudas = {
      Compra: ['Ingreso de mercadería del proveedor.', 'Unidades que entran', 'Ej.: factura 0001-00012345', false],
      Ajuste: ['Suma o resta unidades por un motivo puntual. Usá un número negativo para descontar (ej. −2 por rotura).', 'Unidades (+ / −)', 'Rotura, vencimiento, consumo interno…', true],
      Correccion: ['Conteo físico: ingresá cuántas unidades hay realmente. El sistema registra la diferencia.', 'Stock contado', 'Ej.: inventario mensual', true]
    };
    const tipo = () => modalEl.querySelector('[data-ajuste-tipo]:checked')?.value || 'Compra';

    function actualizar() {
      const t = tipo();
      const [ayuda, label, ph, requiereMotivo] = ayudas[t];
      $('[data-ajuste-ayuda]').textContent = ayuda;
      $('[data-ajuste-label-cantidad]').textContent = label;
      motivo.placeholder = ph;
      motivo.required = requiereMotivo;
      $('[data-ajuste-motivo-req]').hidden = !requiereMotivo;
      modalEl.querySelectorAll('[data-solo-compra]').forEach((x) => { x.hidden = t !== 'Compra'; });
      cantidad.min = t === 'Ajuste' ? '' : (t === 'Compra' ? '1' : '0');
      const c = Math.trunc(num(cantidad.value));
      const res = cantidad.value === '' ? null : t === 'Correccion' ? c : stock + c;
      const out = $('[data-ajuste-resultante]');
      out.textContent = res === null ? '—' : String(res) + (t === 'Correccion' && res !== null ? ` (${res - stock >= 0 ? '+' : ''}${res - stock})` : '');
      out.classList.toggle('text-danger', res !== null && res < 0);
    }

    document.addEventListener('click', (e) => {
      const b = e.target.closest('[data-ajuste-stock]');
      if (!b) return;
      stock = parseInt(b.dataset.stock || '0', 10);
      $('[data-ajuste-id]').value = b.dataset.ajusteStock;
      $('[data-ajuste-nombre]').textContent = b.dataset.nombre || '';
      $('[data-ajuste-stock-actual]').textContent = String(stock);
      const t = b.dataset.tipo || 'Compra';
      modalEl.querySelectorAll('[data-ajuste-tipo]').forEach((r) => { r.checked = r.value === t; });
      cantidad.value = t === 'Compra' && parseInt(b.dataset.sugerido || '0', 10) > 0 ? b.dataset.sugerido : (t === 'Correccion' ? String(stock) : '');
      $('[data-ajuste-costo]').value = b.dataset.costo || '';
      motivo.value = '';
      actualizar();
      modal.show();
      setTimeout(() => { cantidad.focus(); cantidad.select(); }, 250);
    });
    modalEl.addEventListener('change', actualizar);
    modalEl.addEventListener('input', actualizar);
    modalEl.querySelector('[data-ajuste-form]').addEventListener('submit', (e) => {
      const t = tipo();
      const c = Math.trunc(num(cantidad.value));
      let error = null;
      if (cantidad.value === '') error = 'Ingresá la cantidad.';
      else if (t === 'Compra' && c <= 0) error = 'Ingresá cuántas unidades entraron.';
      else if (t === 'Ajuste' && c === 0) error = 'El ajuste no puede ser cero.';
      else if (t !== 'Correccion' && stock + c < 0) error = `No podés dejar el stock en negativo: hay ${stock}.`;
      else if (t === 'Correccion' && c === stock) error = 'El stock contado coincide con el del sistema.';
      else if (t !== 'Compra' && !motivo.value.trim()) error = 'Indicá el motivo.';
      if (error) { e.preventDefault(); e.stopImmediatePropagation(); toast(error, 'danger'); }
    }, true);
  }

  // ========== Modal de categoría ==========
  function initCategorias() {
    const form = document.querySelector('[data-categoria-form]');
    if (!form) return;
    document.addEventListener('click', (e) => {
      const b = e.target.closest('[data-categoria-editar]');
      if (!b) return;
      const id = b.dataset.categoriaEditar || '';
      form.querySelector('[data-categoria-id]').value = id;
      form.querySelector('[data-categoria-titulo]').textContent = id ? 'Editar categoría' : 'Nueva categoría';
      form.querySelector('[data-categoria-nombre]').value = id ? (b.dataset.nombre || '') : '';
      form.querySelector('[data-categoria-descripcion]').value = id ? (b.dataset.descripcion || '') : '';
      const ic = id ? (b.dataset.icono || 'bi-box') : 'bi-box';
      form.querySelectorAll('input[name="icono"]').forEach((r) => { r.checked = r.value === ic; });
      setTimeout(() => form.querySelector('[data-categoria-nombre]').focus(), 250);
    });
  }

  // ========== Formulario de producto: vista previa y margen ==========
  function initProductoForm() {
    const venta = document.querySelector('[data-pv="venta"]');
    if (!venta) return;
    const compra = document.querySelector('[data-pv="compra"]');
    const q = (s) => document.querySelector(s);
    const cat = q('[data-pv="categoria"]');
    let iconos = {};
    try { iconos = JSON.parse(cat?.dataset.iconos || '{}'); } catch { /* sin íconos */ }

    function actualizar() {
      const pv = num(venta.value), pc = num(compra.value);
      const nombre = q('[data-pv="nombre"]')?.value.trim();
      q('[data-pv-nombre]').textContent = nombre || 'Nuevo producto';
      q('[data-pv-marca]').textContent = q('[data-pv="marca"]')?.value.trim() || 'Sin marca';
      q('[data-pv-precio]').textContent = dinero(pv);
      const margen = pv - pc;
      const pct = pv > 0 ? (margen * 100 / pv) : null;
      q('[data-pv-margen]').textContent = pv > 0 ? dinero(margen) : '—';
      q('[data-pv-margen-pct]').textContent = pct === null ? '—' : fmt.format(Math.round(pct * 10) / 10) + '%';
      const badge = q('[data-pv-margen-badge]');
      badge.textContent = pv > 0 ? `Margen ${dinero(margen)} · ${fmt.format(Math.round(pct * 10) / 10)}%` : 'Margen —';
      badge.className = 'margin-badge ' + (pv <= 0 ? '' : margen < 0 ? 'is-bad' : pct < 15 ? 'is-low' : 'is-good');
      const i = q('[data-pv-icono] i');
      if (i && cat) i.className = 'bi ' + (iconos[cat.value] || 'bi-box');
    }
    document.addEventListener('input', (e) => { if (e.target.closest('[data-pv]')) actualizar(); });
    document.addEventListener('change', (e) => { if (e.target.closest('[data-pv]')) actualizar(); });
    actualizar();
  }

  // ========== Punto de Venta ==========
  function initPos() {
    const root = document.querySelector('[data-pos]');
    if (!root) return;
    const $ = (s) => root.querySelector(s);
    let productos = [];
    try { productos = JSON.parse(document.getElementById('posProductos')?.textContent || '[]'); } catch { productos = []; }
    const porId = new Map(productos.map((p) => [p.id, p]));

    const estado = { lineas: [], socio: null, cuota: null, categoria: '' };   // lineas: [{ id, cantidad }]
    let enviando = false;   // tras Cobrar el botón queda bloqueado (sin doble cobro)

    // ----- Catálogo -----
    const grid = $('[data-pos-grid]');
    const buscar = $('[data-pos-buscar]');
    const tiles = new Map();

    function claseStock(p) {
      if (p.stock <= 0) return 'sin';
      if (p.stockMinimo > 0 && p.stock <= p.stockMinimo) return 'critico';
      if (p.stockMinimo > 0 && p.stock <= Math.ceil(p.stockMinimo * 1.5)) return 'proximo';
      return 'ok';
    }

    function enCarrito(id) { return estado.lineas.find((l) => l.id === id)?.cantidad || 0; }

    function pintarTile(p) {
      const t = tiles.get(p.id);
      if (!t) return;
      const disponible = p.stock - enCarrito(p.id);
      t.disabled = disponible <= 0;
      t.querySelector('.pos-tile__stock').textContent = p.stock <= 0 ? 'Sin stock' : disponible <= 0 ? 'Todo en el ticket' : `${disponible} disp.`;
      const cant = enCarrito(p.id);
      const badge = t.querySelector('.pos-tile__badge');
      badge.hidden = cant === 0;
      badge.textContent = String(cant);
    }

    if (grid) {
      productos.forEach((p) => {
        const t = el('button', {
          type: 'button', class: 'pos-tile', role: 'listitem', 'data-id': p.id,
          'aria-label': `${p.nombre}, ${dinero(p.precio)}, stock ${p.stock}`,
          onclick: () => agregar(p.id)
        },
          el('span', { class: 'pos-tile__icon' }, icono(p.icono)),
          p.destacado ? el('span', { class: 'pos-tile__star', title: 'Acceso rápido' }, icono('bi-star-fill')) : null,
          el('span', { class: 'pos-tile__badge', hidden: true }),
          el('strong', { class: 'pos-tile__nombre', text: p.nombre }),
          el('small', { class: 'pos-tile__meta', text: p.marca || p.codigo }),
          el('span', { class: 'pos-tile__pie' },
            el('span', { class: 'pos-tile__precio num', text: dinero(p.precio) }),
            el('span', { class: 'pos-tile__stock stock-' + claseStock(p) })));
        tiles.set(p.id, t);
        grid.append(t);
        pintarTile(p);
      });
    }

    function visibles() {
      const q = normalizar(buscar.value.trim());
      return productos.filter((p) => {
        if (estado.categoria === 'destacados' && !p.destacado) return false;
        if (estado.categoria && estado.categoria !== 'destacados' && String(p.categoriaId) !== estado.categoria) return false;
        return !q || normalizar(`${p.nombre} ${p.codigo} ${p.barras || ''} ${p.marca || ''}`).includes(q);
      });
    }

    function filtrar() {
      const v = new Set(visibles().map((p) => p.id));
      tiles.forEach((t, id) => { t.hidden = !v.has(id); });
      const vacio = $('[data-pos-sin-resultados]');
      if (vacio) vacio.hidden = v.size > 0;
    }

    buscar?.addEventListener('input', filtrar);
    buscar?.addEventListener('keydown', (e) => {
      if (e.key === 'Escape') { buscar.value = ''; filtrar(); return; }
      if (e.key !== 'Enter') return;
      e.preventDefault();
      const q = buscar.value.trim();
      if (!q) return;
      // Lector de códigos: coincidencia exacta de código de barras o código interno
      const exacto = productos.find((p) => (p.barras && p.barras === q) || p.codigo.toLowerCase() === q.toLowerCase());
      const candidatos = exacto ? [exacto] : visibles();
      if (candidatos.length === 1) {
        agregar(candidatos[0].id);
        buscar.value = '';
        filtrar();
      } else if (candidatos.length === 0) {
        toast(`No hay ningún producto activo con “${q}”.`, 'danger');
        buscar.select();
      }
    });

    $('[data-pos-categorias]')?.addEventListener('click', (e) => {
      const chip = e.target.closest('[data-cat]');
      if (!chip) return;
      estado.categoria = chip.dataset.cat;
      root.querySelectorAll('[data-pos-categorias] [data-cat]').forEach((c) => c.classList.toggle('active', c === chip));
      filtrar();
    });

    // ----- Carrito -----
    function agregar(id, cantidad = 1) {
      const p = porId.get(id);
      if (!p) return;
      const linea = estado.lineas.find((l) => l.id === id);
      const actual = linea?.cantidad || 0;
      if (actual + cantidad > p.stock) {
        toast(p.stock <= 0 ? `${p.nombre} no tiene stock.` : `Solo hay ${p.stock} de ${p.nombre}.`, 'danger');
        return;
      }
      if (linea) linea.cantidad += cantidad; else estado.lineas.push({ id, cantidad });
      render(id);
    }

    function fijar(id, cantidad) {
      const p = porId.get(id);
      const linea = estado.lineas.find((l) => l.id === id);
      if (!p || !linea) return;
      cantidad = Math.trunc(cantidad);
      if (cantidad <= 0) { quitar(id); return; }
      if (cantidad > p.stock) { toast(`Solo hay ${p.stock} de ${p.nombre}.`, 'danger'); cantidad = p.stock; }
      linea.cantidad = cantidad;
      render();
    }

    function quitar(id) {
      estado.lineas = estado.lineas.filter((l) => l.id !== id);
      render();
    }

    const carrito = $('[data-pos-carrito]');

    function lineaProducto(l) {
      const p = porId.get(l.id);
      const input = el('input', {
        type: 'number', class: 'form-control num', min: '1', max: String(p.stock), step: '1', value: String(l.cantidad),
        'aria-label': `Cantidad de ${p.nombre}`,
        onchange: (e) => fijar(l.id, num(e.target.value))
      });
      return el('li', { class: 'pos-linea', 'data-id': l.id },
        el('div', { class: 'pos-linea__copy' },
          el('strong', { text: p.nombre }),
          el('small', { class: 'num', text: `${dinero(p.precio)} c/u` })),
        el('div', { class: 'pos-stepper' },
          el('button', { type: 'button', class: 'icon-btn', 'aria-label': `Restar ${p.nombre}`, onclick: () => fijar(l.id, l.cantidad - 1) }, icono('bi-dash')),
          input,
          el('button', { type: 'button', class: 'icon-btn', 'aria-label': `Sumar ${p.nombre}`, onclick: () => agregar(l.id) }, icono('bi-plus'))),
        el('span', { class: 'pos-linea__subtotal num', text: dinero(p.precio * l.cantidad) }),
        el('button', { type: 'button', class: 'icon-btn pos-linea__quitar', 'aria-label': `Quitar ${p.nombre}`, title: 'Quitar', onclick: () => quitar(l.id) }, icono('bi-x-lg')));
    }

    function lineaCuota() {
      const c = estado.cuota;
      const input = el('input', {
        type: 'number', class: 'form-control num', min: '0', step: '0.01', value: String(c.importe), 'aria-label': 'Importe de la cuota',
        oninput: (e) => { c.importe = Math.max(0, num(e.target.value)); totales(); }
      });
      return el('li', { class: 'pos-linea pos-linea--cuota' },
        el('div', { class: 'pos-linea__copy' },
          el('strong', {}, icono('bi-card-checklist'), ' Cuota'),
          el('small', { text: c.detalle })),
        el('div', { class: 'input-group input-group-sm pos-cuota-importe' }, el('span', { class: 'input-group-text', text: '$' }), input),
        el('button', { type: 'button', class: 'icon-btn pos-linea__quitar', 'aria-label': 'Quitar cuota', title: 'Quitar cuota', onclick: () => { estado.cuota = null; render(); } }, icono('bi-x-lg')));
    }

    function totales() {
      const productosTotal = estado.lineas.reduce((a, l) => a + porId.get(l.id).precio * l.cantidad, 0);
      const cuota = estado.cuota ? estado.cuota.importe : 0;
      const total = productosTotal + cuota;
      $('[data-pos-total-productos]').textContent = dinero(productosTotal);
      $('[data-pos-total-cuota]').textContent = dinero(cuota);
      $('[data-pos-fila-productos]').hidden = !estado.cuota;
      $('[data-pos-fila-cuota]').hidden = !estado.cuota;
      $('[data-pos-total]').textContent = dinero(total);
      const etiqueta = $('[data-pos-cobrar-total]');
      if (etiqueta) etiqueta.textContent = total > 0 ? dinero(total) : '';
      $('[data-pos-cobrar]').disabled = enviando || (estado.lineas.length === 0 && !(estado.cuota && estado.cuota.importe > 0));
      return total;
    }

    function render(resaltar) {
      carrito.replaceChildren(...estado.lineas.map(lineaProducto), ...(estado.cuota ? [lineaCuota()] : []));
      const vacio = estado.lineas.length === 0 && !estado.cuota;
      $('[data-pos-vacio]').hidden = !vacio;
      $('[data-pos-vaciar]').hidden = vacio;
      productos.forEach(pintarTile);
      actualizarCuotaCta();
      totales();
      if (resaltar) {
        const li = carrito.querySelector(`[data-id="${resaltar}"]`);
        li?.classList.add('is-flash');
        li?.scrollIntoView({ block: 'nearest' });
      }
    }

    $('[data-pos-vaciar]')?.addEventListener('click', () => { estado.lineas = []; estado.cuota = null; render(); buscar?.focus(); });

    // ----- Cliente: socio u ocasional -----
    const bloqueSocio = $('[data-pos-bloque-socio]');
    const bloqueOcasional = $('[data-pos-bloque-ocasional]');
    root.querySelectorAll('[data-pos-tipo-cliente]').forEach((r) => r.addEventListener('change', () => {
      const ocasional = r.value === 'ocasional' && r.checked;
      if (!r.checked) return;
      bloqueSocio.hidden = ocasional;
      bloqueOcasional.hidden = !ocasional;
      if (ocasional) { quitarSocio(); $('[data-pos-ocasional]')?.focus(); }
    }));

    const q = $('[data-pos-socio-q]');
    const resultados = $('[data-pos-socio-resultados]');
    let timer = null, ultimos = [], activo = -1;

    function cerrarResultados() { resultados.hidden = true; q.setAttribute('aria-expanded', 'false'); activo = -1; }

    function pintarResultados() {
      resultados.replaceChildren(...(ultimos.length === 0
        ? [el('li', { class: 'pos-socio-resultados__vacio', text: 'Sin coincidencias' })]
        : ultimos.map((s, i) => el('li', {
            role: 'option', id: 'posSocio' + s.id, class: i === activo ? 'active' : '', 'aria-selected': i === activo ? 'true' : 'false',
            onmousedown: (e) => { e.preventDefault(); elegirSocio(s.id); }
          }, el('strong', { text: s.nombre }), el('small', { class: 'num', text: `DNI ${s.dni} · N° ${s.numero}` })))));
      resultados.hidden = false;
      q.setAttribute('aria-expanded', 'true');
      if (activo >= 0) q.setAttribute('aria-activedescendant', 'posSocio' + ultimos[activo].id);
    }

    q?.addEventListener('input', () => {
      clearTimeout(timer);
      const term = q.value.trim();
      if (term.length < 2) { cerrarResultados(); return; }
      timer = setTimeout(async () => {
        try {
          const r = await fetch(root.dataset.sociosUrl + '&term=' + encodeURIComponent(term), { headers: { Accept: 'application/json' } });
          ultimos = r.ok ? await r.json() : [];
        } catch { ultimos = []; }
        activo = ultimos.length ? 0 : -1;
        pintarResultados();
      }, 220);
    });
    q?.addEventListener('keydown', (e) => {
      if (resultados.hidden) return;
      if (e.key === 'ArrowDown') { e.preventDefault(); activo = Math.min(ultimos.length - 1, activo + 1); pintarResultados(); }
      else if (e.key === 'ArrowUp') { e.preventDefault(); activo = Math.max(0, activo - 1); pintarResultados(); }
      else if (e.key === 'Enter') { e.preventDefault(); if (ultimos[activo]) elegirSocio(ultimos[activo].id); }
      else if (e.key === 'Escape') cerrarResultados();
    });
    q?.addEventListener('blur', () => setTimeout(cerrarResultados, 120));

    async function elegirSocio(id) {
      cerrarResultados();
      let s = null;
      try {
        const r = await fetch(root.dataset.socioUrl + '&id=' + encodeURIComponent(id), { headers: { Accept: 'application/json' } });
        s = r.ok ? await r.json() : null;
      } catch { s = null; }
      if (!s || !s.ok) { toast('No se pudo cargar el socio.', 'danger'); return; }
      estado.socio = s;
      const card = $('[data-pos-socio-card]');
      const avatar = $('[data-pos-socio-avatar]');
      avatar.replaceChildren(s.foto ? el('img', { src: s.foto, alt: '' }) : document.createTextNode(s.iniciales || '?'));
      $('[data-pos-socio-nombre]').textContent = s.nombre;
      $('[data-pos-socio-meta]').textContent = `N° ${s.numero} · DNI ${s.dni}` + (s.baja ? ' · dado de baja' : '');
      const plan = $('[data-pos-socio-plan]');
      plan.textContent = s.plan ? `${s.plan}${s.vence ? (s.vigente ? ' · vence ' : ' · venció ') + s.vence : ''}` : 'Sin plan';
      plan.classList.toggle('text-danger', !!s.plan && !s.vigente);
      $('[data-pos-socio-asistencia]').textContent = s.ultimaAsistencia || 'Sin registros';
      card.hidden = false;
      $('[data-pos-socio-buscador]').hidden = true;
      $('[data-pos-socio-id]').value = s.id;
      q.value = '';
      actualizarCuotaCta();
      buscar?.focus();
    }

    function quitarSocio() {
      estado.socio = null;
      estado.cuota = null;
      $('[data-pos-socio-id]').value = '';
      $('[data-pos-socio-card]').hidden = true;
      $('[data-pos-socio-buscador]').hidden = false;
      render();
    }
    $('[data-pos-socio-quitar]')?.addEventListener('click', () => { quitarSocio(); q?.focus(); });

    const cuotaCta = $('[data-pos-cuota-agregar]');
    function actualizarCuotaCta() {
      const s = estado.socio;
      if (!cuotaCta) return;
      cuotaCta.hidden = !s || !s.puedeCobrarCuota || !!estado.cuota;
      if (!s) return;
      $('[data-pos-cuota-texto]').textContent = s.faltante > 0 ? `Saldo pendiente ${dinero(s.faltante)} · ${s.plan}`
        : s.precio ? `${s.plan} · ${dinero(s.precio)} (al día)` : 'Sin plan vigente: queda como pago a cuenta';
      cuotaCta.classList.toggle('is-pending', s.faltante > 0);
    }
    cuotaCta?.addEventListener('click', () => {
      const s = estado.socio;
      if (!s) return;
      const importe = s.faltante > 0 ? s.faltante : (s.precio || 0);
      estado.cuota = { importe, detalle: s.plan ? `${s.plan} · ${s.nombre}` : `Pago a cuenta · ${s.nombre}` };
      render();
      carrito.querySelector('.pos-linea--cuota input')?.focus();
    });

    // ----- Cobrar -----
    const form = $('[data-pos-form]');
    form.addEventListener('submit', (e) => {
      const total = totales();
      if (enviando || $('[data-pos-cobrar]').disabled) { e.preventDefault(); return; }
      if (estado.cuota && !(estado.cuota.importe > 0)) { e.preventDefault(); toast('Ingresá el importe de la cuota o quitala del ticket.', 'danger'); return; }
      $('[data-pos-lineas]').value = JSON.stringify(estado.lineas);
      $('[data-pos-cuota-importe]').value = estado.cuota ? String(estado.cuota.importe) : '';
      enviando = true;
      const btn = $('[data-pos-cobrar]');
      btn.disabled = true;
      btn.replaceChildren(el('span', { class: 'spinner-border spinner-border-sm', 'aria-hidden': 'true' }), ` Cobrando ${dinero(total)}…`);
    });

    // Atajos: F2 buscar · F9 cobrar
    document.addEventListener('keydown', (e) => {
      if (e.key === 'F2') { e.preventDefault(); buscar?.focus(); buscar?.select(); }
      else if (e.key === 'F9') { e.preventDefault(); form.requestSubmit(); }
    });

    // ----- Estado inicial (socio preseleccionado o reintento tras un error) -----
    try {
      const previas = JSON.parse(root.dataset.lineasInicial || '[]');
      previas.forEach((l) => { const p = porId.get(l.id); if (p && l.cantidad > 0) estado.lineas.push({ id: l.id, cantidad: Math.min(l.cantidad, Math.max(p.stock, 0)) }); });
      estado.lineas = estado.lineas.filter((l) => l.cantidad > 0);
    } catch { /* carrito vacío */ }
    render();
    filtrar();
    if (root.dataset.socioInicial) {
      elegirSocio(root.dataset.socioInicial).then(() => {
        const cuota = num(root.dataset.cuotaInicial);
        if (cuota > 0 && estado.socio?.puedeCobrarCuota) {
          estado.cuota = { importe: cuota, detalle: estado.socio.plan ? `${estado.socio.plan} · ${estado.socio.nombre}` : `Pago a cuenta · ${estado.socio.nombre}` };
          render();
        }
      });
    }
  }

  // ========== Ticket: impresión automática tras cobrar (?imprimir=1) ==========
  function initTicket() {
    document.querySelectorAll('[data-imprimir]').forEach((b) => b.addEventListener('click', () => window.print()));
  }

  let iniciado = false;   // idempotente: si el script se incluye o se inicializa dos veces, no duplica listeners
  const iniciar = () => { if (iniciado) return; iniciado = true; initAjusteStock(); initCategorias(); initProductoForm(); initPos(); initTicket(); };
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', iniciar); else iniciar();
})();
