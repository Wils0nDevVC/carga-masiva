# Contrato del correo de notificación (sustituye el contenido de F007 D-04)

- **De**: `Smtp:Remitente`. **Para**: el `usuario` del mensaje. **Asunto**: `Carga {idCarga} finalizada`.
- **Cuerpo**: `multipart/alternative`, con una parte `text/plain` y otra `text/html` (UTF-8).

## Secciones (iguales en HTML y en texto)

1. «La carga {idCarga} finalizó.», con archivo «{NombreArchivo}» y fecha de fin (UTC) `yyyy-MM-dd HH:mm:ss`.
2. **Resultados por periodo**: tabla con Periodo y Resultado, coloreada en HTML; si no hay, «Sin periodos procesados».
3. **Incidencias**:
   - «Total: N» y el recuento por tipo;
   - las primeras 20 con Tipo, Fila, Código, Periodo y Motivo;
   - si N > 20, «y {N−20} más; consulte el detalle de la carga»;
   - si N = 0, «Sin incidencias».
4. **Ver detalle** (botón en HTML y URL en texto): `{UrlFrontend}/historial/{idCarga}`, solo si está configurada.

## Colores (HTML)

| Resultado | Color |
|---|---|
| Completado | verde (`#166534` sobre `#dcfce7`) |
| Rechazado, Fallido | rojo (`#991b1b` sobre `#fee2e2`) |
| Bloqueado, NoProcesado | ámbar (`#92400e` sobre `#fef3c7`) |
| otro | gris (`#374151` sobre `#f3f4f6`) |

Sin imágenes, hojas externas ni adjuntos; todos los valores van escapados.
