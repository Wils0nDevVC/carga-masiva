# Quickstart: validación manual del Historial

En el navegador, `http://localhost:3000/historial`, con sesión. Para las subidas de prueba, usar preferiblemente una base temporal. **Ningún escenario se ha ejecutado todavía.**

| # | Escenario | Resultado esperado | Spec |
|---|---|---|---|
| 1 | Abrir `/historial` con cargas | Tabla con las 6 columnas y fechas locales | US1-1 |
| 2 | Pulsar el Id o el Archivo de una fila | Navega a `/historial/{idCarga}` | US1-2 |
| 3 | Usuario sin cargas | «No hay cargas registradas» | US1-3 |
| 4 | Subir un Excel desde `/subir` (otra pestaña) con el Historial abierto | La fila aparece y su estado avanza hasta Notificado sin recargar | US1-4, SC-001 |
| 5 | Pestaña de red: ocultar la pestaña 30 s y volver | 0 peticiones mientras está oculta; 1 inmediata al volver | US1-5, SC-002 |
| 6 | Detener `api-gateway` con la página abierta y luego arrancarlo | Mensaje de error con la tabla anterior; desaparece al recuperarse | US1-6 |
| 7 | Administrador frente a Operador | El Administrador ve todas las cargas; el Operador, solo las suyas | SC-003 |
| 8 | Página abierta 5 minutos | Ningún 429 ni error en la consola | SC-004 |
