# Implementation Plan: API Gateway mínimo con Ocelot

**Branch**: `009-api-gateway` (trabajo en `main`) | **Date**: 2026-11-03 | **Spec**: [spec.md](spec.md)

**Input**: `specs/009-api-gateway/spec.md`

**Procedencia**: `Dec` = decisión del usuario o constitución v3.5.0; `Spec`; `F005/F008`; `Plan` = decisión técnica (detalle en `research.md`); `Verif` = comprobado en esta etapa.

## Summary

Nuevo servicio `api-gateway`: un solo proyecto `Acity.Api` con Ocelot 25.0.2. Reenvía a los servicios tres rutas:
- `POST /auth/login` y `POST /auth/refresh`, sin token, a ms-autenticacion.
- `POST /cargas`, con JWT, a ms-control.

El JWT se valida con los mismos parámetros que Control y sin comprobar el rol. Los errores propios del Gateway, que Ocelot genera sin cuerpo (401, 404 y 502), se convierten en `Response<T>`. Las respuestas de los servicios pasan sin cambios. El Gateway tiene su propio Dockerfile y servicio en Compose (puerto 8080), y escribe logs con Serilog. [Spec] [Dec Q1–Q3] [Plan]

## Technical Context

**Language/Version**: C# / .NET 10.

**Primary Dependencies**: Ocelot 25.0.2 (MIT; `net10.0`; Verif), `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.14 y `Serilog.AspNetCore` 10.0.0 (las mismas versiones que Control).

**Storage**: ninguno.

**Testing**: manual, con `quickstart.md`. No hay tests automatizados (§11).

**Target Platform**: Docker Compose.

**Project Type**: servicio de entrada. Toca `api-gateway/` y `compose.yaml`: el servicio `api-gateway` y el volumen `logs-gateway`.

**Constraints**:
- Sin Rate Limiting, QoS, caché, descubrimiento de servicios ni Swagger.
- Sin cabeceras propias ni comprobación de rol.
- Autenticación, Control, el esquema y `.env` no cambian.

## Constitution Check

Constitución v3.5.0.

| Principio | Verificación | Resultado |
|---|---|---|
| §1 Alcance | Solo las 3 rutas existentes y `/health` propio. La referencia solo orienta la estructura | Pasa |
| §2/§3 Plataforma | `api-gateway/`, .NET 10, Ocelot; `/health` mediante un controlador | Pasa |
| §4 Capas | Un solo proyecto `Acity.Api`; no se crean capas vacías (el Gateway no tiene casos de uso, dominio ni persistencia) | Pasa (justificado en Complexity Tracking) |
| §5 Contrato | Errores propios con `Response<T>` y `Code` igual al estado real; las respuestas de los servicios no se envuelven | Pasa |
| §9 Gateway | Valida el JWT de Autenticación; Control sigue validando el JWT y el rol; sin Rate Limiting; sin Happy, Header/IdentityPool, Reec, AWS, SignalR, WebSockets, Swagger, Emissary ni Kubernetes | Pasa |
| §10 Logging | Serilog con archivo propio; sin tokens ni cabeceras | Pasa |
| §11/§12 | Dockerfile propio, Compose y healthcheck; 3 paquetes con versión y licencia verificadas | Pasa |

Tras la Phase 1: sin violaciones.

## Project Structure

```text
specs/009-api-gateway/
├── plan.md, research.md, data-model.md, quickstart.md
├── contracts/gateway-api.md
├── checklists/requirements.md
└── tasks.md            # /speckit-tasks (no creado aquí)

compose.yaml                         # + servicio api-gateway (8080:8080, Jwt__* desde .env, depends_on healthy) y volumen logs-gateway

api-gateway/
├── Dockerfile                       # mismo patrón que los servicios
└── src/
    ├── Acity.Gateway.slnx
    └── Acity.Api/
        ├── Acity.Api.csproj         # Ocelot 25.0.2, JwtBearer 10.0.14, Serilog.AspNetCore 10.0.0
        ├── Program.cs               # Serilog, JwtOptions, AddJwtBearer("Bearer"), AddOcelot, pipeline (research D-06)
        ├── ocelot.json              # 3 rutas (research D-03)
        ├── appsettings.json         # Serilog (gateway-.log)
        ├── Options/JwtOptions.cs
        ├── Helpers/Response.cs
        └── Controllers/HealthController.cs
```

## Complexity Tracking

| Elemento | Por qué | Alternativa descartada |
|---|---|---|
| Un solo proyecto (sin Application, Domain, Infrastructure ni Common) | El Gateway solo reenvía y valida; §4 prohíbe crear capas vacías | Cinco proyectos vacíos |
| Copia de `Response<T>` en `Acity.Api/Helpers` | §5 exige el mismo contrato y no existe una biblioteca compartida | Crear `Acity.Common` solo para un `record` |
| `UseStatusCodePages` antes de Ocelot | Ocelot genera sus errores sin cuerpo; así cumplen Q3 sin tocar las respuestas de los servicios | Un delegating handler o middleware propio de Ocelot (más código) |

## Pendientes tras el plan

Ninguno que requiera decisión. "Servicio no disponible" se devuelve como **502**, que es el valor de Ocelot, y no se reasigna a 503 (research D-05).

En la implementación se comprobarán los Abiertos 1 a 3 de `research.md`: el multipart íntegro, los errores sin cuerpo y que `/health` quede fuera de Ocelot. La validación en el equipo de evaluación no se ha realizado.
