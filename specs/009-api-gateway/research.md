# Research: API Gateway mínimo con Ocelot

**Procedencia**: `Dec` = decisión del usuario o constitución v3.5.0; `Spec` = spec 009; `F005/F008` = features anteriores; `Plan` = decisión técnica de este plan (alternativa más sencilla); `Verif` = comprobado en esta etapa; `Sin verif` = se comprueba en la implementación.

## D-01 Paquetes

| Paquete | Versión | Licencia | Comprobación |
|---|---|---|---|
| `Ocelot` | 25.0.2 | MIT | Verif: incluye `net10.0`; publicado el 2026-10-15; licencia MIT del repositorio ThreeMammals/Ocelot. Arrastra FluentValidation 12.2.0 o superior y `Mvc.NewtonsoftJson` 10.0.13 o superior |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.14 | MIT | La misma versión que usa Control (F005) |
| `Serilog.AspNetCore` | 10.0.0 | Apache-2.0 | La misma que los demás servicios; incluye el sink de archivo |

Nada más. Quedan fuera los paquetes de caché, QoS (Polly de Ocelot), descubrimiento de servicios y Swagger. [Dec §9, §12]

## D-02 Estructura

`api-gateway/Dockerfile` y `api-gateway/src/Acity.Gateway.slnx`, con un solo proyecto `Acity.Api`. [Spec Assumptions] [Dec §4: no se crean capas vacías]

- `Response<T>`: copia del `record` de los servicios en `Acity.Api/Helpers/Response.cs`. El Gateway no tiene `Acity.Common` y no se comparte ninguna biblioteca. La firma y la serialización son idénticas. [Dec §5, Q3]
- `Options/JwtOptions.cs`: copia de la de Control (`Clave`, `Emisor`, `Audiencia`, `EsValida`), validada al arrancar. [Spec FR-012]
- `Controllers/HealthController.cs`: `GET /health`, igual que en los servicios, para el healthcheck de Compose. Es la salud del propio Gateway, no un `/health` interno. [Spec Edge Cases] [Dec §3, controladores]

## D-03 Rutas (`ocelot.json`) [Dec Q1]

| Upstream | Método | Downstream | Autenticación |
|---|---|---|---|
| `/auth/login` | POST | `http://ms-autenticacion:8080/auth/login` | no |
| `/auth/refresh` | POST | `http://ms-autenticacion:8080/auth/refresh` | no |
| `/cargas` | POST | `http://ms-control:8080/cargas` | `AuthenticationOptions.AuthenticationProviderKeys = ["Bearer"]` |

- Sin `RouteClaimsRequirement`: el rol lo autoriza Control (Q2).
- Sin `RateLimitOptions`, `QoSOptions`, `FileCacheOptions` ni `ServiceDiscoveryProvider`. [Dec §9]
- `GlobalConfiguration.BaseUrl` = `http://localhost:8080`.
- Ocelot reenvía por defecto el método, el cuerpo en flujo (también el multipart) y las cabeceras, incluida `Authorization`. No se añaden ni se transforman cabeceras (FR-007). *Sin verif*: que el multipart de 10 MiB llegue íntegro.

## D-04 Validación del JWT [Dec Q2] [F005]

- Esquema `Bearer` con `AddJwtBearer`. Los `TokenValidationParameters` son idénticos a los de Control:
  - emisor, audiencia, vigencia y `RequireExpirationTime`;
  - clave simétrica UTF-8 y `ValidAlgorithms = [HS256]`;
  - `ClockSkew = 0`, `MapInboundClaims = false`, `NameClaimType = "sub"` y `RoleClaimType = "role"`;
  - `OnTokenValidated` exige `sub` entero > 0.
- Ocelot autentica las rutas protegidas con ese esquema. Un fallo da 401 y la solicitud no se reenvía.
- Un refresh token no tiene el formato de un JWT, así que da 401 (F008 FR-012).

## D-05 Errores propios del Gateway [Dec Q3]

- Ocelot responde a sus propios errores con un estado y **sin cuerpo**:
  - 401: sin token o token inválido.
  - 404: ruta o método no configurados.
  - 502: servicio de destino inaccesible.
  - 500: error interno.
- `UseStatusCodePages`, registrado antes de `UseOcelot`, convierte esas respuestas sin cuerpo en `Response<object>.Fail(code, mensaje)` con el mismo `code`. Es el mismo mecanismo que ya usan los servicios.
  - Mensajes: 401 "No autorizado", 404 "No encontrado", 502 "Servicio no disponible" y, en cualquier otro caso, "Error".
  - El middleware solo actúa si la respuesta no se ha iniciado y no tiene cuerpo. Las respuestas de los servicios ya llevan su `Response<T>` y Ocelot las copia, así que no se vuelven a envolver.
  - *Sin verif*: que Ocelot deje la respuesta sin iniciar en sus errores.
- Servicio no disponible = **502** (valor por defecto de Ocelot para un servicio inaccesible); no se reasigna a 503.
- `UseExceptionHandler` con un 500 `Response<T>` genérico, sin detalles internos. [Spec FR-009]

## D-06 Orden del pipeline

1. `UseExceptionHandler`.
2. `UseStatusCodePages`.
3. `UseRouting`.
4. `UseEndpoints(MapControllers)`: solo `/health`.
5. `UseAuthentication`.
6. `await UseOcelot()`: terminal.

Así `/health` responde sin pasar por Ocelot y el resto lo resuelve Ocelot.
- *Sin verif*: que `UseEndpoints` no deje pasar `/health` a Ocelot.
- Alternativa descartada: `MapWhen` para `/health`, porque los demás servicios ya usan un controlador.

## D-07 Configuración, Compose y logs

- **Compose**, servicio `api-gateway`:
  - build `./api-gateway`, imagen `carga-masiva-api-gateway:local`;
  - `depends_on` `ms-autenticacion` y `ms-control` con `service_healthy`;
  - `Jwt__Clave`, `Jwt__Emisor` y `Jwt__Audiencia` desde `.env`, igual que Control;
  - puertos `8080:8080`, volumen `logs-gateway:/app/logs`, `healthcheck: *app-healthcheck` y `restart: "no"`.
  - No necesita variables nuevas en `.env`. [Spec FR-011]
- **Serilog**: el mismo `appsettings.json` que los servicios, con `Application` = `api-gateway` y archivo `logs/gateway-.log`. Override `Ocelot` en `Warning` para no registrar cada solicitud. No hay registro de solicitudes ni de cabeceras. [Spec FR-010]
- **`ocelot.json`**: se copia al publicar. No hay variantes por entorno, porque solo existe Compose.

## Abiertos (se verifican en la implementación)

1. Que el multipart de 10 MiB llegue íntegro a través del Gateway.
2. Que los errores de Ocelot salgan sin cuerpo, para que `UseStatusCodePages` los envuelva.
3. Que el orden del pipeline deje `/health` fuera de Ocelot.
4. Validación en el equipo de evaluación: no realizada.
