// Configuración: vista previa del branding en vivo y formulario de avisos de recepción.
(function () {
  'use strict';

  // ---------- Color: misma regla que el servidor (Marca/Colores.cs) ----------
  const hexOk = (h) => /^#?([0-9a-f]{6}|[0-9a-f]{3})$/i.test((h || '').trim());
  function normalizar(h) {
    if (!hexOk(h)) return null;
    let x = h.trim().replace('#', '');
    if (x.length === 3) x = x.split('').map((c) => c + c).join('');
    return '#' + x.toUpperCase();
  }
  const rgb = (h) => [1, 3, 5].map((i) => parseInt(h.slice(i, i + 2), 16));
  const hex = (c) => '#' + c.map((v) => Math.round(v).toString(16).padStart(2, '0')).join('').toUpperCase();
  const lum = (c) => { const f = (v) => { v /= 255; return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); }; const [r, g, b] = c.map(f); return 0.2126 * r + 0.7152 * g + 0.0722 * b; };
  const contraste = (a, b) => { const [x, y] = [lum(a), lum(b)]; return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); };
  function legible(c, minimo) {
    for (let t = 0; t <= 1.0001; t += 0.04) { const x = c.map((v) => v * (1 - t)); if (contraste(x, [255, 255, 255]) >= minimo) return x; }
    return [15, 23, 42];
  }

  function initBranding() {
    const form = document.querySelector('[data-branding-form]');
    if (!form) return;
    const preview = form.querySelector('[data-marca-preview]');
    const textos = { principal: form.querySelector('[data-color-texto="principal"]'), secundario: form.querySelector('[data-color-texto="secundario"]') };

    function actualizarColores() {
      const p = normalizar(textos.principal.value), s = normalizar(textos.secundario.value);
      Object.values(textos).forEach((t) => t.classList.toggle('is-invalid', !normalizar(t.value)));
      if (!p || !s) return;
      const pc = legible(rgb(p), 3), sc = legible(rgb(s), 3);
      const ink = contraste(pc, [255, 255, 255]) >= contraste(pc, [15, 23, 42]) ? '#FFFFFF' : '#0F172A';
      preview.style.setProperty('--pv-a', hex(pc));
      preview.style.setProperty('--pv-b', hex(sc));
      preview.style.setProperty('--pv-ink', ink);
      const aviso = form.querySelector('[data-contraste]');
      const ajustado = hex(pc) !== p;
      aviso.hidden = !ajustado;
      if (ajustado) form.querySelector('[data-contraste-tono]').textContent = hex(pc);
      form.querySelectorAll('[data-color-picker]').forEach((pk) => {
        const t = form.querySelector(pk.dataset.colorPicker);
        const v = normalizar(t.value); if (v) pk.value = v.toLowerCase();
      });
    }
    form.querySelectorAll('[data-color-picker]').forEach((pk) => pk.addEventListener('input', () => {
      form.querySelector(pk.dataset.colorPicker).value = pk.value.toUpperCase(); actualizarColores();
    }));
    Object.values(textos).forEach((t) => t.addEventListener('input', actualizarColores));
    form.querySelectorAll('[data-paleta]').forEach((b) => b.addEventListener('click', () => {
      const [p, s] = b.dataset.paleta.split(',');
      textos.principal.value = p; textos.secundario.value = s; actualizarColores();
    }));

    // Textos de la vista previa
    form.querySelectorAll('[data-pv-marca]').forEach((inp) => inp.addEventListener('input', () => {
      const clave = inp.dataset.pvMarca;
      const v = inp.value.trim() || inp.placeholder || '';
      preview.querySelectorAll(`[data-pv="${clave}"]`).forEach((x) => { x.textContent = v || (clave === 'nombre' ? 'Mi gimnasio' : ''); });
    }));

    // Imágenes: vista previa inmediata y "quitar"
    form.querySelectorAll('[data-imagen-campo]').forEach((campo) => {
      const input = campo.querySelector('[data-imagen-input]');
      const caja = campo.querySelector('[data-imagen-preview]');
      const quitar = campo.querySelector('[data-imagen-quitar]');
      const original = caja.innerHTML;
      input.addEventListener('change', () => {
        const f = input.files && input.files[0];
        if (!f) { caja.innerHTML = original; return; }
        const img = document.createElement('img');
        img.alt = 'Vista previa'; img.src = URL.createObjectURL(f);
        caja.replaceChildren(img);
        if (quitar) quitar.checked = false;
        if (input.name === 'Input.LogoReducido' || (input.name === 'Input.Logo' && !preview.querySelector('[data-pv-iso] img'))) {
          const iso = preview.querySelector('[data-pv-iso]');
          const copia = img.cloneNode(); iso.replaceChildren(copia);
        }
      });
      quitar?.addEventListener('change', () => campo.classList.toggle('is-quitando', quitar.checked));
    });

    actualizarColores();
  }

  function initAvisos() {
    const form = document.querySelector('[data-aviso-form]');
    if (!form) return;
    const evento = form.querySelector('[data-solo-evento]');
    const sincronizarTipo = () => { evento.hidden = form.querySelector('[data-aviso-tipo]:checked')?.value !== 'Evento'; };
    form.addEventListener('change', (e) => { if (e.target.matches('[data-aviso-tipo]')) sincronizarTipo(); });
    document.addEventListener('click', (e) => {
      const b = e.target.closest('[data-aviso-editar]');
      if (!b) return;
      const id = b.dataset.avisoEditar || '';
      form.reset();
      form.querySelector('[data-aviso-id]').value = id;
      form.querySelector('[data-aviso-titulo-modal]').textContent = id ? 'Editar aviso' : 'Nuevo aviso';
      const tipo = id ? b.dataset.tipo : 'Aviso';
      form.querySelectorAll('[data-aviso-tipo]').forEach((r) => { r.checked = r.value === tipo; });
      form.querySelector('#avTitulo').value = id ? b.dataset.titulo || '' : '';
      form.querySelector('#avTexto').value = id ? b.dataset.texto || '' : '';
      form.querySelector('#avOrden').value = id ? b.dataset.orden || '0' : '0';
      form.querySelector('#avFecha').value = id ? b.dataset.fechaEvento || '' : '';
      form.querySelector('#avDesde').value = id ? b.dataset.desde || '' : '';
      form.querySelector('#avHasta').value = id ? b.dataset.hasta || '' : '';
      form.querySelector('[data-aviso-quitar-wrap]').hidden = !id;
      sincronizarTipo();
    });
  }

  const iniciar = () => { initBranding(); initAvisos(); };
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', iniciar); else iniciar();
})();
