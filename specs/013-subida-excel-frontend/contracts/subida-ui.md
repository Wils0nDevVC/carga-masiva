# Contrato de la pantalla de Subida (`/subir`, protegida)

| Situación | Comportamiento |
|---|---|
| Inicial | Selector `type=file`, `accept=".xlsx"`, `required`; botón «Subir» |
| Envío | `POST /cargas` multipart (campo `archivo`); botón deshabilitado con «Subiendo…» |
| 202 | «Carga registrada», `idCarga` y `estado`; enlace «Ver detalle» a `/historial/{idCarga}`; selector limpio |
| 400 | `message` y la lista `errors` |
| 401 | Renovación transparente (F011); si falla, se va a `/login` |
| 403, 413, 429 o 500 | `message` del servidor |
| Sin conexión (incluido el corte por más de 11 MiB) | «No se pudo conectar con el servidor» |
