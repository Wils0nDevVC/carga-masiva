# Quickstart: validación manual del API Gateway

Verificación manual (constitución §11), con curl o Postman. Contrato: `contracts/gateway-api.md`. **Ningún escenario se ha ejecutado todavía.**

## Reglas

- Los datos de prueba van en una base temporal `CargaMasivaApp_F009` creada por el runner. `CargaMasiva`, `CargaMasivaDb` y los volúmenes ajenos no se tocan, y nunca se usa `docker compose down -v`.
- Toda la pila apunta a la misma base (`DB_NAME=CargaMasivaApp_F009 docker compose up -d --build`).
- Los secretos solo se pasan por entorno. No se imprimen tokens ni contraseñas. Las credenciales salen de `ADMIN_*` y `OPERADOR_*` de `.env`, leyendo solo esas variables.

## Escenarios

| # | Escenario | Pasos | Resultado esperado | Spec |
|---|---|---|---|---|
| 1 | Arranque | `docker compose ps` | `api-gateway` `healthy`; `GET :8080/health` → 200 | SC-005 |
| 2 | Login y refresh por el Gateway | `POST :8080/auth/login` (correcto y erróneo) y `POST :8080/auth/refresh` | Mismos estados y cuerpos que contra :8081 (200, 401 genérico, 200 con el par nuevo) | US2, SC-003 |
| 3 | Subida por el Gateway | `POST :8080/cargas` con el Excel y el token de Operador | 202 con `idCarga`; la carga llega a Notificado (correo en Mailpit) | US1-1, SC-001 |
| 4 | Rechazos de token | Sin token; token alterado; token vencido (generador de la F005, sin imprimirlo); otro emisor; otra audiencia; refresh token como acceso | 401 `Response<T>` "No autorizado" del Gateway; en los logs de Control no aparece la solicitud | US1-2, US1-3, SC-002 |
| 5 | Errores de Control reenviados | Sin archivo (400); archivo > 10 MiB (413); ráfaga hasta 429 | Mismo estado y cuerpo que contra :8082, sin volver a envolverlos | US1-4, SC-003 |
| 6 | Rutas no expuestas | `GET :8080/cargas`; `GET :8080/auth/login`; `GET :8080/otra` | 404 `Response<T>` "No encontrado" | FR-003, Edge Cases |
| 7 | Servicio caído | Detener `ms-control` y hacer `POST :8080/cargas` con un token válido; después arrancarlo | 502 `Response<T>` "Servicio no disponible", sin detalles | US1-5 |
| 8 | Configuración inválida | Arrancar el Gateway con una clave JWT corta (solo en el entorno del comando) | No arranca; el log no muestra el valor | FR-012 |
| 9 | Logs | Buscar con `grep -F` los tokens emitidos, `JWT_CLAVE` y `Bearer ` en los logs del Gateway | 0 coincidencias | SC-004 |

## Cierre

Borrar los archivos y correos de prueba (solo los de esta prueba), eliminar `CargaMasivaApp_F009`, volver al arranque normal sobre `CargaMasivaApp` y comprobar que todo está `healthy`. Registrar en "Resultados" lo ejecutado y lo no ejecutado.

## Resultados (implementación, 2026-11-03)

**Estado: implementada y verificada en ejecución** sobre `CargaMasivaApp_F009`, con toda la pila apuntando a esa base. Validación en el equipo de evaluación: no realizada.

| # | Resultado |
|---|---|
| 1 | `api-gateway` `healthy`; `GET :8080/health` → 200 `Response<string>` "Healthy". Abierto 3 confirmado: `/health` no pasa por Ocelot |
| 2 | Login 200 con los 4 campos; login erróneo: 401 idéntico (estado y cuerpo) a través del Gateway y directo; refresh 200; reusar el refresh da 401 "Refresh token inválido" de Autenticación, sin volver a envolverlo |
| 3 | `POST :8080/cargas` con el Excel de ejemplo → 202 `idCarga` 1. La carga llegó a Notificado y su correo quedó en Mailpit. Abierto 1 confirmado para el archivo de ejemplo |
| 4 | Sin token, alterado, vencido, otro emisor, otra audiencia, refresh token como acceso y `sub` no numérico: 401 `Response<T>` "No autorizado" del Gateway. Control responde "No autenticado" a su propio 401, así que estas solicitudes no le llegaron. Abierto 2 confirmado |
| 5 | Sin archivo y 10 MiB + 1 byte: el mismo 400 de Control a través del Gateway y directo. Ráfaga: 429 "Demasiadas solicitudes" de Control, reenviado sin cambios (límite por usuario, F005) |
| 5 (observación) | Un cuerpo de 11 MiB supera el límite más el margen de Control, y Kestrel de Control corta la conexión. Directo a :8082 el cliente recibe un error de conexión, no un 413; a través del Gateway llega como 502 `Response<T>` "Servicio no disponible". Es el comportamiento previo de Control, no del Gateway; no se cambia (fuera de alcance) |
| 6 | `GET /cargas`, `GET /auth/login`, `/otra` y `/ms-control/health`: 404 `Response<T>` "No encontrado" |
| 7 | Con `ms-control` detenido: 502 `Response<T>` "Servicio no disponible", sin detalles internos |
| 8 | Con `JWT_CLAVE=corta` (solo en el entorno del comando): el servicio termina con código 1 y "Configuración Jwt inválida…", sin el valor |
| 9 | 298 líneas de log (contenedor y archivo): 0 coincidencias de `JWT_CLAVE`, contraseñas, 10 tokens emitidos ni `Bearer ` |

**Notas**:
- Una prueba mal planteada ("sin archivo" enviando un archivo con otro nombre de campo) y la ráfaga crearon 10 cargas en la base temporal; todas llegaron a Notificado.
- Los mensajes de 401 son distintos: el Gateway dice "No autorizado" (contrato de la 009) y Control "No autenticado". No se unificaron porque cambiar Control queda fuera de alcance.

### Cierre (T014)

- Se borraron los 10 archivos de prueba de SeaweedFS (204) y los correos de Mailpit. La base principal tenía 0 cargas, así que no se borró nada ajeno.
- Colas con 0 mensajes.
- Arranque normal sobre `CargaMasivaApp`, con todos los servicios `healthy`, incluido `api-gateway`.
- `CargaMasivaApp_F009` eliminada; quedan `CargaMasiva`, `CargaMasivaApp` y `CargaMasivaDb`.
