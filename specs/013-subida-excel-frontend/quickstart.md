# Quickstart: validación manual de la Subida

En el navegador, `http://localhost:3000/subir`, con sesión iniciada. **Ningún escenario se ha ejecutado todavía.**

Para no generar datos en la base principal, usar una base temporal (`DB_NAME=CargaMasivaApp_F013 docker compose up -d --build`) o aceptar explícitamente las cargas de prueba.

| # | Escenario | Resultado esperado | Spec |
|---|---|---|---|
| 1 | Subir `referencias/carga_masiva_productos (1).xlsx` | «Carga registrada», `idCarga` y Pendiente; el enlace abre `/historial/{idCarga}` | US1-1, SC-001 |
| 2 | Pulsar «Subir» sin archivo | El formulario lo impide | US1-3 |
| 3 | Elegir un `.txt` (selector en «Todos los archivos») | 400 con el motivo de extensión | US1-4, SC-002 |
| 4 | 11 subidas en menos de 1 minuto | La subida 11 muestra «Demasiadas solicitudes» | US1-5, SC-002 |
| 5 | Pestaña de red con conexión lenta (*throttling*) | Botón con «Subiendo…»; 1 sola petición | US1-2, SC-003 |
| 6 | Subir otro archivo tras un éxito | Se acepta; el resultado se reemplaza | US1-7 |
| 7 | Consola del navegador | Sin tokens | SC-004 |

## Resultados (implementación, 2026-11-03)

Llamada verificada con el módulo real `src/api/cargas.ts` (ejecutado con `tsx` desde el scratchpad, con el cliente de la 011 y la sesión de un login real) contra nginx (`:3000`), sobre la base temporal `CargaMasivaApp_F013`:

| # | Resultado |
|---|---|
| 1 (llamada) | Excel de ejemplo → `{ idCarga: 1, estado: "Pendiente" }` (202). La carga llegó a Notificado |
| 3 (llamada) | `notas.txt` → `ApiError` 400 «Error de validación», `errors` ["Solo se admiten archivos .xlsx."]; archivo vacío → 400 ["El archivo está vacío."] |
| 4 (llamada) | Ráfaga: 7 rechazos 400 y luego 429 en las peticiones 11 y 12 del minuto (contando las 3 anteriores) |
| 2, 5, 6, 7 e interfaz de 1, 3 y 4 | **No ejecutados**: requieren un navegador (ninguno disponible en la sesión) |

**Cierre**: se borraron el archivo de prueba de SeaweedFS (204) y **solo** su correo en Mailpit (filtrado por asunto y fecha; no había otros mensajes); `CargaMasivaApp_F013` está eliminada; arranque normal sobre `CargaMasivaApp` con todo `healthy`.
