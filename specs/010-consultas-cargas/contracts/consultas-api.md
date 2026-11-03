# Contrato HTTP: consultas de cargas

Se exponen en Control (`:8082`) y en el Gateway (`:8080`, que reenvía sin cambios).

- **Autenticación**: `Authorization: Bearer <accessToken>` obligatorio; rol Administrador u Operador (Control).
- **Formato**: `Response<T>` en camelCase, con `code` igual al estado HTTP. Fechas en UTC (ISO 8601 con `Z`).
- **Rate Limiting**: estas rutas no están sujetas al Rate Limiting de `POST /cargas`.

## GET /cargas: historial

200 `data`: lista de cargas, la más reciente primero. Si no hay cargas, `[]`.
```json
[{ "idCarga": 3, "nombreArchivo": "productos.xlsx", "usuario": "operador@carga-masiva.test",
   "fechaRecepcion": "2026-11-03T18:00:00Z", "estado": "Notificado", "fechaFin": "2026-11-03T18:00:05Z" }]
```
El Administrador ve todas las cargas; el Operador, solo las suyas.

## GET /cargas/{id}: detalle

200 `data`: los campos del historial más:
```json
{ "historial":   [{ "estado": "Pendiente", "fecha": "…Z" }],
  "periodos":    [{ "periodo": "2024-01", "resultado": "Completado", "fecha": "…Z" }],
  "incidencias": [{ "tipo": "FilaOmitida", "numeroFila": 12, "codigoProducto": "P-1", "periodoTexto": "2024-01",
                    "motivo": "…", "fecha": "…Z" }] }
```
Las secciones sin datos se devuelven como `[]`.

## GET /cargas/{id}/contenido: filas del Excel original

200 `data`:
```json
{ "idCarga": 3, "nombreArchivo": "productos.xlsx", "hoja": "Productos",
  "encabezados": ["Periodo", "CodigoProducto", "NombreProducto", "Precio"],
  "filas": [{ "numeroFila": 2, "celdas": ["2024-01", "P-1", "Producto A", "12.5"] },
            { "numeroFila": 3, "celdas": [null, "P-2", null, null] }] }
```
Valores sin normalizar (research D-06). Filas desde la 2 hasta la última usada de la primera hoja.

## Errores

| Caso | Rutas | Estado | `message` |
|---|---|---|---|
| Sin token o con token inválido | todas | 401 | Gateway: «No autorizado»; Control: «No autenticado» |
| Rol no permitido | todas | 403 | (manejador existente de Control) |
| `id ≤ 0` | detalle y contenido | 400 | «Error de validación» (con `errors`) |
| `id` no numérico | detalle y contenido | 404 | «No encontrado» (ruta inexistente) |
| Carga inexistente, o ajena para un Operador | detalle y contenido | 404 | «La carga no existe». |
| Archivo ilegible (no es Excel o no tiene hojas) | contenido | 422 | «El archivo no se puede leer como Excel». |
| Archivo ausente en la ruta registrada | contenido | 500 | «No se pudo recuperar el archivo asociado a la carga». |
| SeaweedFS caído o inaccesible | contenido | 503 | «El archivo no está disponible temporalmente. Intente nuevamente más tarde». |
