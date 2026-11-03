# Data Model: Plantilla del correo

Sin cambios de esquema; todo es solo lectura.

| Entidad (ms-notificaciones) | Columnas | Cambio |
|---|---|---|
| `CargaArchivo` | `Id`, `Estado` + **`NombreArchivo`** nvarchar(260) | Se añade una propiedad al mapeo |
| `CargaPeriodo` | `Id` bigint, `CargaId` bigint, `Periodo` char(7), `Resultado` varchar(20) | **Entidad nueva** de solo lectura (sin `PeriodoReservado`) |
| `Incidencia` | ya mapeada | Sin cambios |

Records de Application:
- `ResumenCarga(string NombreArchivo, IReadOnlyList<PeriodoResumen> Periodos, int TotalIncidencias, IReadOnlyList<TipoRecuento> PorTipo, IReadOnlyList<IncidenciaResumen> Primeras)`
- `PeriodoResumen(string Periodo, string Resultado)`, `TipoRecuento(string Tipo, int Cantidad)`
- `IncidenciaResumen(string Tipo, int? NumeroFila, string? CodigoProducto, string? PeriodoTexto, string Motivo)`
- `CorreoNotificacion(string Asunto, string Html, string Texto)`
- `ConfiguracionCorreo(string? UrlFrontend)`
