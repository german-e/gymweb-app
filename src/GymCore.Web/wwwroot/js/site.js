// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Botones de impresión (data-accion="imprimir"). Reemplaza los onclick="..." en línea, que la
// Content-Security-Policy bloquea.
document.addEventListener('click', (e) => {
    if (e.target.closest('[data-accion="imprimir"]')) window.print();
});
