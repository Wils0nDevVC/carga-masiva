# Contratos: HTTP común y operación de la fundación

Descripción de contratos, no código. Aplica a los cuatro servicios salvo indicación. Procedencia: Spec 002 y `research.md`.

## Endpoint de salud
| | |
|---|---|
| Ruta | `GET /health` (sin prefijo; el API Gateway no existe todavía) |
| Autenticación | Ninguna |
| Respuesta 200 | `Response<string>` con `Data` = "Healthy", `IsSuccess` verdadero, `Code` 200 |
| Significado | El host arrancó. No consulta la base ni dependencias externas. |
| Control | Solo responde después de completar la migración (el servidor no escucha antes); si falla la inicialización, nunca responde. |

Es el único endpoint de la fundación (FR-009).

## Contrato `Response<T>`
Campos (JSON en camelCase, `System.Text.Json`): `data` (puede ser null), `isSuccess`, `message`, `code`, `errors` (lista de textos o null). Los cinco campos aparecen siempre. `code` es igual al estado HTTP de la respuesta.

| Situación | Estado HTTP | `isSuccess` | `message` | `errors` |
|---|---|---|---|---|
| Éxito (salud) | 200 | true | "OK" | null |
| Validación fallida (FluentValidation) | 400 | false | mensaje genérico de validación | mensajes de cada fallo |
| Ruta inexistente | 404 | false | "No encontrado" | null |
| Método no permitido | 405 | false | "Método no permitido" | null |
| Excepción no controlada | 500 | false | mensaje genérico, sin detalles | null |

Nunca se incluyen trazas, cadenas de conexión ni secretos en el cuerpo. Los textos exactos de los mensajes son provisionales y no son requisito de negocio.

## Variables de entorno
| Variable | Servicios | Uso |
|---|---|---|
| `MSSQL_SA_PASSWORD` | Compose | Contraseña de SA (sin valor en el repositorio; `.env` no versionado) |
| `DB_NAME` | Compose | Base de la fundación; valor por defecto `CargaMasivaDb` |
| `SQLSERVER_HOST_PORT` | Compose | Puerto del host para SQL Server (14333 por defecto) |
| `ConnectionStrings__Default` | los 4 (obligatoria en Control) | Cadena de conexión ensamblada por Compose con las dos primeras |
| `ASPNETCORE_ENVIRONMENT` | los 4 | Entorno (`Production` por defecto en Compose) |
| `Startup__SqlWaitAttempts`, `Startup__SqlWaitSeconds` | solo Control | Espera acotada de SQL Server (30 y 2 por defecto) |

## Logs
- Archivos `logs/<servicio>-AAAAMMDD.log` en `/app/logs` de cada contenedor; rotación diaria, 14 archivos; un volumen por servicio. Formato estructurado con la propiedad `Application`.
- Sin contraseñas, tokens ni cadenas de conexión; sin cuerpos de solicitudes.

## Docker Compose
| Servicio | Depende de | Puerto del host | Volumen |
|---|---|---|---|
| sqlserver (existente) | — | `SQLSERVER_HOST_PORT` (14333) | `sqlserver-data` (existente) |
| ms-control | sqlserver `service_healthy` | 8082 | `logs-control` |
| ms-autenticacion | ms-control `service_healthy` | 8081 | `logs-autenticacion` |
| ms-carga-masiva | ms-control `service_healthy` | 8083 | `logs-carga-masiva` |
| ms-notificaciones | ms-control `service_healthy` | 8084 | `logs-notificaciones` |

Contenedor: escucha en 8080; healthcheck con `bash` y `/dev/tcp` sobre `/health`; sin reinicio automático. Si la migración de Control falla, Control termina con error, no queda healthy y los tres dependientes no arrancan. No se tocan volúmenes ni contenedores ajenos.

## Arranque de Control (secuencia)
1. Configuración y Serilog. 2. Construir el host. 3. Esperar SQL Server (acotado). 4. Guarda de base heredada. 5. `MigrateAsync`. 6. Empezar a escuchar y responder `/health`. Cualquier fallo en 3–5: registrar el error sin secretos, código de salida distinto de cero, sin escuchar.
