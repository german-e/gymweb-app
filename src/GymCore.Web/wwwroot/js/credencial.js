// Acciones sobre la credencial del socio. Botones con data-cred-action:
//   pdf | png | share | whatsapp | email | print
// Atributos: data-cred-target="#idTarjeta" data-cred-file="credencial-123"
//   whatsapp: data-wa-phone, data-wa-text · email: data-mail-to, data-mail-subject, data-mail-body · print: data-print-url
(function () {
  'use strict';
  const CDN_CANVAS = 'https://cdnjs.cloudflare.com/ajax/libs/html2canvas/1.4.1/html2canvas.min.js';
  const CDN_PDF = 'https://cdnjs.cloudflare.com/ajax/libs/jspdf/2.5.1/jspdf.umd.min.js';
  const cargar = (src) => window.GymUi.cargarScript(src);

  async function capturar(el) {
    await cargar(CDN_CANVAS);
    return html2canvas(el, { scale: 3, backgroundColor: null, useCORS: true, logging: false });
  }

  function descargar(url, nombre) {
    const a = document.createElement('a');
    a.href = url; a.download = nombre;
    document.body.appendChild(a); a.click(); a.remove();
  }

  async function pdf(el, nombre) {
    const canvas = await capturar(el);
    await cargar(CDN_PDF);
    // Tamaño tarjeta CR80: 85.6 × 54 mm
    const doc = new window.jspdf.jsPDF({ orientation: 'landscape', unit: 'mm', format: [85.6, 54] });
    doc.addImage(canvas.toDataURL('image/png'), 'PNG', 0, 0, 85.6, 54);
    doc.save(nombre + '.pdf');
  }

  async function png(el, nombre) {
    const canvas = await capturar(el);
    descargar(canvas.toDataURL('image/png'), nombre + '.png');
    return canvas;
  }

  // Comparte la imagen con la hoja nativa (móvil) o cae a WhatsApp Web con el texto
  async function compartir(el, nombre, btn) {
    try {
      const canvas = await capturar(el);
      const blob = await new Promise((ok) => canvas.toBlob(ok, 'image/png'));
      const file = new File([blob], nombre + '.png', { type: 'image/png' });
      if (navigator.canShare && navigator.canShare({ files: [file] })) {
        await navigator.share({ files: [file], title: 'Credencial', text: btn.dataset.waText || '' });
        return true;
      }
    } catch { /* cancelado o no soportado */ }
    return false;
  }

  function whatsapp(btn) {
    const tel = btn.dataset.waPhone || '';
    const texto = encodeURIComponent(btn.dataset.waText || '');
    window.open('https://wa.me/' + tel + '?text=' + texto, '_blank', 'noopener');
  }

  document.addEventListener('click', async (e) => {
    const btn = e.target.closest('[data-cred-action]');
    if (!btn) return;
    e.preventDefault();
    const accion = btn.dataset.credAction;
    const el = document.querySelector(btn.dataset.credTarget || '.credencial');
    const nombre = btn.dataset.credFile || 'credencial';
    const original = btn.innerHTML;
    const ocupado = (on) => { btn.disabled = on; if (on) btn.style.opacity = '.6'; else btn.style.opacity = ''; };

    try {
      ocupado(true);
      switch (accion) {
        case 'pdf': await pdf(el, nombre); break;
        case 'png': await png(el, nombre); break;
        case 'whatsapp':
          // En móviles intenta enviar la imagen; si no, abre el chat con el texto
          if (!(await compartir(el, nombre, btn))) whatsapp(btn);
          break;
        case 'email':
          await pdf(el, nombre); // se descarga para adjuntarla
          window.location.href = 'mailto:' + encodeURIComponent(btn.dataset.mailTo || '') +
            '?subject=' + encodeURIComponent(btn.dataset.mailSubject || 'Tu credencial') +
            '&body=' + encodeURIComponent(btn.dataset.mailBody || '');
          break;
        case 'print':
          if (btn.dataset.printUrl) window.open(btn.dataset.printUrl, '_blank');
          else window.print();
          break;
      }
    } catch (err) {
      console.error(err);
      alert('No se pudo completar la acción. Verificá la conexión e intentá nuevamente.');
    } finally {
      ocupado(false);
      btn.innerHTML = original;
    }
  });
})();
