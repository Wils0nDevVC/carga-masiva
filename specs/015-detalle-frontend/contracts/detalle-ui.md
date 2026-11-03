# Contrato de la pantalla de Detalle (`/historial/:idCarga`, protegida)

| Situación | Comportamiento |
|---|---|
| `idCarga` no es un entero > 0 | «La carga no existe.» y un enlace «Volver al historial»; sin llamadas |
| Primera consulta en curso | «Cargando…» |
| 200 | Datos generales (id, archivo, usuario, recepción, estado, fin), historial de estados, tabla de periodos («Sin periodos» si está vacía) y tabla de incidencias («Sin incidencias» si está vacía) |
| No terminal y pestaña visible | Nueva consulta 5 s después de terminar la anterior |
| Notificado o Fallido | Sin más consultas |
| 404 | «La carga no existe.» y un enlace al historial; sin más consultas |
| Otro error | `message`, conservando los datos; se sigue consultando si no es terminal |
| «Ver contenido» | `GET /cargas/{id}/contenido`; botón deshabilitado con «Cargando contenido…» |
| Contenido 200 | Hoja, encabezados y filas (número de fila y celdas; `null` en blanco). Sin filas: «El archivo no tiene filas de datos» |
| Contenido 422, 500 o 503 | Mensaje del servidor como error; botón disponible para reintentar |
