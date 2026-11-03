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
