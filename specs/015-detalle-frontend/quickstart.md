# Quickstart: validación manual del Detalle

En el navegador, `http://localhost:3000/historial/{id}`, con sesión. Para subir cargas o forzar errores, usar una base temporal. **Ningún escenario se ha ejecutado todavía.**

| # | Escenario | Resultado esperado | Spec |
|---|---|---|---|
| 1 | Detalle de una carga procesada | Datos, historial completo, periodos e incidencias (recuentos iguales a los del servidor) | US1-1, SC-001 |
| 2 | Carga Pendiente sin procesar (insertada por SQL en la base temporal) | «Sin periodos» y «Sin incidencias» | US1-2 |
| 3 | Subir un Excel y abrir su detalle enseguida | El estado avanza hasta Notificado; después, 0 peticiones en la pestaña de red | US1-3, US1-4, SC-002 |
| 4 | Operador que abre el detalle de una carga ajena; `/historial/999999`; `/historial/abc` | «La carga no existe.» con un enlace; sin más peticiones | US1-5, SC-003 |
| 5 | «Ver contenido» en la carga del Excel de ejemplo | 200 filas tal cual; 1 sola petición a `/contenido` | US2-1, US2-5, SC-004 |
| 6 | Contenido con archivo ilegible, ausente o con SeaweedFS detenido (preparado como en la Feature 010) | Mensajes de 422, 500 y 503 como error; se puede reintentar | US2-3, SC-005 |
| 7 | Ocultar la pestaña con una carga no terminal | Sin peticiones mientras está oculta; 1 al volver | US1-7 |

## Resultados (implementación, 2026-11-03)

`obtenerDetalleCarga` y `obtenerContenidoCarga` se ejecutaron con los módulos reales (`tsx` desde el scratchpad y el cliente de la 011) contra nginx (`:3000`), sobre `CargaMasivaApp`. Fueron solo lecturas de las 3 cargas existentes.

| # | Resultado |
|---|---|
| 1 (llamada) | Cargas 1, 2 y 3 (Notificado): historial 5; periodos 3; incidencias 84, 3 y 3 |
| 4 (llamada) | El Operador pide la carga 3 del Administrador: detalle y contenido → 404 «La carga no existe.». La carga 999999 → 404 |
| 5 (llamada) | Contenido de las 3 cargas: hoja «Productos», 4 encabezados y 200 filas |
| 2, 3, 6, 7 e interfaz | **No ejecutados**: requieren un navegador (y, para el 6, una base temporal con errores preparados) |
