// Comportamiento de la interfaz. Los importes NUNCA se calculan aquí: el servidor es la única fuente.
(function () {
    "use strict";

    // Menú en pantallas pequeñas.
    document.querySelectorAll("[data-alternar-menu]").forEach(function (boton) {
        boton.addEventListener("click", function () {
            var menu = document.getElementById("menu-principal");
            var abierto = menu.classList.toggle("abierto");
            boton.setAttribute("aria-expanded", abierto ? "true" : "false");
        });
    });

    // Confirmación de acciones sensibles: <form data-confirmar="¿Seguro?">.
    document.addEventListener("submit", function (e) {
        var form = e.target;
        var mensaje = form.getAttribute("data-confirmar");
        var boton = e.submitter;
        if (boton && boton.hasAttribute("data-confirmar")) mensaje = boton.getAttribute("data-confirmar");
        if (mensaje && !window.confirm(mensaje)) {
            e.preventDefault();
            return;
        }
        // Indicador de carga y bloqueo del doble clic.
        if (!e.defaultPrevented && boton && !boton.hasAttribute("data-sin-bloqueo")) {
            boton.classList.add("cargando");
            boton.setAttribute("aria-busy", "true");
            setTimeout(function () { boton.disabled = true; }, 0);
            // Conserva el nombre/valor del botón pulsado (formaction ya lo resuelve el navegador).
        }
    }, true);

    // Cambio de tipo de documento en la emisión: recarga la pantalla con los clientes aptos.
    var selectorTipo = document.querySelector("[data-cambiar-tipo]");
    if (selectorTipo) {
        selectorTipo.addEventListener("change", function () {
            window.location.href = selectorTipo.getAttribute("data-cambiar-tipo") + "?tipo=" + encodeURIComponent(selectorTipo.value);
        });
    }

    // Líneas de productos: agregar y quitar filas, renumerando los nombres Solicitud.Lineas[i].*
    var tablaLineas = document.querySelector("[data-lineas]");
    if (tablaLineas) {
        var cuerpo = tablaLineas.querySelector("tbody");
        var plantilla = document.getElementById("plantilla-linea");

        function renumerar() {
            Array.prototype.forEach.call(cuerpo.querySelectorAll("tr"), function (fila, i) {
                fila.querySelectorAll("[data-nombre]").forEach(function (campo) {
                    campo.name = "Solicitud.Lineas[" + i + "]." + campo.getAttribute("data-nombre");
                    campo.id = "linea-" + i + "-" + campo.getAttribute("data-nombre");
                });
                var etiqueta = fila.querySelector("[data-numero]");
                if (etiqueta) etiqueta.textContent = String(i + 1);
            });
        }

        function mostrarPrecio(select) {
            var opcion = select.options[select.selectedIndex];
            var celda = select.closest("tr").querySelector("[data-precio]");
            if (celda) celda.textContent = opcion && opcion.getAttribute("data-precio-documento") ? "$" + opcion.getAttribute("data-precio-documento") : "—";
        }

        document.querySelector("[data-agregar-linea]").addEventListener("click", function () {
            cuerpo.appendChild(plantilla.content.firstElementChild.cloneNode(true));
            renumerar();
        });
        cuerpo.addEventListener("click", function (e) {
            var quitar = e.target.closest("[data-quitar-linea]");
            if (!quitar) return;
            if (cuerpo.querySelectorAll("tr").length > 1) quitar.closest("tr").remove();
            else quitar.closest("tr").querySelectorAll("select, input").forEach(function (c) { c.value = c.type === "number" ? (c.getAttribute("data-nombre") === "Cantidad" ? "1" : "0") : ""; });
            renumerar();
        });
        cuerpo.addEventListener("change", function (e) {
            if (e.target.matches("select[data-nombre=ProductoId]")) mostrarPrecio(e.target);
        });
        cuerpo.querySelectorAll("select[data-nombre=ProductoId]").forEach(mostrarPrecio);
        renumerar();
    }

    // Detalle de documento: mientras haya envíos pendientes, consulta el estado y recarga al terminar.
    var monitor = document.querySelector("[data-monitor-envios]");
    if (monitor) {
        var url = monitor.getAttribute("data-monitor-envios");
        var intentos = 0;
        var temporizador = setInterval(function () {
            intentos++;
            fetch(url, { credentials: "same-origin", headers: { "Accept": "application/json" } })
                .then(function (r) { return r.ok ? r.json() : null; })
                .then(function (datos) {
                    if (datos && datos.pendientes === 0) { clearInterval(temporizador); window.location.reload(); }
                })
                .catch(function () { /* se reintenta en el siguiente ciclo */ });
            if (intentos >= 40) clearInterval(temporizador);
        }, 3000);
    }
})();
