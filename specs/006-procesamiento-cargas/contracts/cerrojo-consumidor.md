# Contrato: cerrojo de exclusividad del consumidor

Procedencia: constitución §9; F001 FR-042, R-02 y contrato "Cerrojo de exclusividad del consumidor de carga masiva"; spec 006 FR-003; `research.md` D-03.

| Aspecto | Valor |
|---|---|
| Recurso | `carga-masiva:consumidor-cargas` (distingue mayúsculas; calificado por la base `CargaMasivaApp`) |
| Modo / propietario / espera | `Exclusive` / `Session` / `@LockTimeout = 0` |
| Conexión | dedicada, misma cadena con `Pooling=false`, abierta durante la vida del consumidor |
| Obtención | `sp_getapplock` → `>= 0` obtenido; `-1` ocupado (otra instancia): no consume ni recupera; reintenta cada 15 s |
| Vigencia | antes de cada grupo: conexión abierta y `APPLOCK_MODE('public', recurso, 'Session') = 'Exclusive'`; si no, se detiene sin confirmar |
| Liberación | al detenerse el host (`sp_releaseapplock` y cierre); si el proceso muere, SQL Server la libera al cerrar la sesión |

Límites (F001 R-02): un proceso "zombi" puede seguir un instante tras perder el cerrojo; la base lo contiene (`UQ_CargaPeriodo_Carga_Periodo`, índice filtrado de reserva, `UX_Producto_CodigoProducto`). Una réplica configurada no es garantía; el cerrojo sí.
