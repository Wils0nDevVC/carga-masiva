# Quickstart: validación manual del correo

Base temporal `CargaMasivaApp_F016` con toda la pila; correos en Mailpit (`http://localhost:8025`). Al cerrar, borrar **solo** los correos de la prueba. **Ningún escenario se ha ejecutado todavía.**

| # | Escenario | Resultado esperado | Spec |
|---|---|---|---|
| 1 | Subir el Excel de ejemplo | El correo tiene archivo, carga y fecha; 3 periodos coloreados; «Total: 84» con el recuento por tipo, 20 filas y «y 64 más…»; «Ver detalle» → `http://localhost:3000/historial/{id}`; carga Notificado | US1-1, SC-001, SC-002 |
| 2 | Excel sin incidencias | «Sin incidencias» | US1-2 |
| 3 | Archivo inválido (texto renombrado a `.xlsx`) | «Sin periodos procesados» e incidencia `ArchivoInvalido` | US1-3 |
| 4 | Mailpit: pestaña de texto | Mismas secciones en texto plano | US1-4, SC-003 |
| 5 | Nombre de archivo con `<b>&` | Se muestra literal, sin formato inyectado | Edge Cases |
| 6 | `FRONTEND_URL` vacía | Correo sin enlace, enviado igual | FR-005 |
| 7 | Mailpit detenido | Reintentos, `ErrorNotificacion` y Finalizado (como en la 007) | US1-6, SC-004 |

## Resultados (implementación, 2026-11-03)

Base temporal `CargaMasivaApp_F016` con toda la pila. Tres subidas del Operador por el frontend (`:3000`), las tres con 202 y en Notificado. Correos leídos con la API de Mailpit (partes HTML y texto).

| # | Resultado |
|---|---|
| 1 | **Carga 1** (Excel de ejemplo): HTML y texto presentes; archivo, carga y fecha de fin; 3 periodos «Completado» en verde (`#166534` sobre `#dcfce7`); «Total: 84 (Existente: 84)», 20 incidencias y «y 64 más; consulte el detalle de la carga»; «Ver detalle» → `http://localhost:3000/historial/1` |
| 2 | **Carga 2** (`limpio.xlsx`, 5 filas válidas): «Sin incidencias»; periodo 2032-01 en verde |
| 3 | **Carga 3** (texto renombrado a `.xlsx`): «Sin periodos procesados»; «ArchivoInvalido: 1» con «No es un Excel legible.» |
| 4 | Las partes de texto de los 3 correos tienen las mismas secciones que el HTML |
| 5 | Archivo llamado `prueba <b>&.xlsx`: en el HTML aparece escapado (`&lt;b&gt;&amp;`), sin marcado inyectado; en el texto, literal |
| 6 | **No ejecutado** (`FRONTEND_URL` vacía): la rama sin enlace no se probó; con la URL por defecto, el enlace aparece |
| 7 | **No ejecutado** (Mailpit detenido): el flujo de reintentos y `ErrorNotificacion` no cambió (Feature 007) |

**No probados**: los colores rojo y ámbar (las pruebas no generaron periodos Rechazado, Bloqueado ni Fallido).

**Cierre**:
- Se borraron los 3 archivos de prueba de SeaweedFS (204) y **solo** los 3 correos de la prueba (por ID). Se conservan 2 correos anteriores ajenos («Carga 4» y «Carga 3»).
- `CargaMasivaApp_F016` está eliminada.
- Arranque normal sobre `CargaMasivaApp` con todo `healthy`.
