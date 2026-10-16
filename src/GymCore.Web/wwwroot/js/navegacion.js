// Menú lateral: grupos colapsables (estado recordado por usuario en este navegador) y apertura en móvil.
(function () {
  'use strict';
  const CLAVE = 'gymcore.nav.abiertos';
  const leer = () => { try { return JSON.parse(localStorage.getItem(CLAVE) || 'null'); } catch { return null; } };
  const guardar = (v) => { try { localStorage.setItem(CLAVE, JSON.stringify(v)); } catch { /* sin almacenamiento */ } };

  function init() {
    const nav = document.querySelector('[data-sidebar-nav]');
    if (nav) {
      const grupos = Array.from(nav.querySelectorAll('[data-nav-group]'));
      const fijar = (g, abierto) => {
        g.classList.toggle('is-open', abierto);
        g.querySelector('.nav-group__toggle')?.setAttribute('aria-expanded', abierto ? 'true' : 'false');
      };
      // Restaura lo que el usuario dejó abierto (el grupo de la pantalla actual siempre queda abierto)
      const guardados = leer();
      if (Array.isArray(guardados) && nav.dataset.navTodoAbierto !== '1') {
        grupos.forEach((g) => fijar(g, g.classList.contains('has-active') || guardados.includes(g.dataset.navGroup)));
      }
      nav.addEventListener('click', (e) => {
        const t = e.target.closest('.nav-group__toggle');
        if (!t) return;
        const g = t.closest('[data-nav-group]');
        fijar(g, !g.classList.contains('is-open'));
        guardar(grupos.filter((x) => x.classList.contains('is-open')).map((x) => x.dataset.navGroup));
      });
      // El ítem activo queda a la vista aunque el menú tenga scroll
      nav.querySelector('.nav-item.active')?.scrollIntoView({ block: 'nearest' });
    }

    // Móvil / tablet: menú deslizable con fondo para cerrar y Escape
    document.querySelectorAll('[data-sidebar-toggle]').forEach((b) => b.addEventListener('click', () => {
      const abierto = document.body.classList.toggle('sidebar-open');
      document.querySelector('.sidebar-toggle')?.setAttribute('aria-expanded', abierto ? 'true' : 'false');
    }));
    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape' && document.body.classList.contains('sidebar-open')) document.body.classList.remove('sidebar-open');
    });
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init); else init();
})();
