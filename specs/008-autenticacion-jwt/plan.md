# Implementation Plan: Autenticación con JWT y refresh tokens en ms-autenticacion

**Branch**: `008-autenticacion-jwt` (trabajo en `main`) | **Date**: 2026-11-03 | **Spec**: [spec.md](spec.md)

**Input**: `specs/008-autenticacion-jwt/spec.md`

**Procedencia**: `Dec` = decisión del usuario o constitución v3.5.0; `Spec`; `F004/F005`; `Plan` = decisión técnica (detalle en `research.md`); `Verif` = comprobado en esta etapa.

## Summary

ms-autenticacion expone dos endpoints, `POST /auth/login` y `POST /auth/refresh`, ambos con MediatR y `Response<T>`. [Spec] [Dec Q1–Q3] [Plan]

- **Login**: busca el usuario por correo y verifica la contraseña con `PasswordHasher`. Si es correcta, emite un JWT HS256 (`sub` = Id, `role`) válido 60 minutos, que Control ya acepta, junto con un refresh token opaco. Del refresh token solo se guarda el hash SHA-256, en la tabla nueva `RefreshToken` (script V005); dura 1 hora y rota en cada uso.
- **Errores**: credenciales o refresh token inválidos devuelven 401 genérico.
- **Arranque**: crea los usuarios iniciales (Administrador y Operador) a partir de variables de entorno, sin duplicarlos ni sobrescribirlos.

## Technical Context

**Language/Version**: C# / .NET 10.

**Primary Dependencies**:
- Existentes: MediatR 12.5.0, FluentValidation 12.2.0, EF Core SqlServer 10.0.14 y Serilog.
- Nuevas: `Microsoft.IdentityModel.JsonWebTokens` 8.19.2 (MIT; la misma versión que valida Control) y `FrameworkReference Microsoft.AspNetCore.App` en Infrastructure, para `PasswordHasher` y el enlace de opciones. [Verif]

**Storage**:
- `CargaMasivaApp`, con una tabla nueva `RefreshToken` creada por V005, que aplica el runner.
- `Usuario` y `Rol` no cambian.

**Testing**: manual, según `quickstart.md`, con Postman o curl. No hay tests automatizados (§11).

**Target Platform**: Docker Compose.

**Project Type**: microservicio. Toca `ms-autenticacion/`, `database/scripts/V005__refresh_token.sql`, `compose.yaml` (variables de ms-autenticacion) y `.env.example`.

**Constraints**:
- Sin Identity completo, JwtBearer, Polly, Rate Limiting en el login, transacciones explícitas, limpieza de tokens ni detección de familias.
- Control no cambia.

## Constitution Check

Constitución v3.5.0.

| Principio | Verificación | Resultado |
|---|---|---|
| §1 Alcance | Solo login, refresh y usuarios iniciales. Sin registro, administración, logout ni MFA. Las referencias GitHub no aportan nada que copiar | Pasa |
| §3/§4 Capas | Controller en Api; comandos, handlers, validadores e interfaces en Application; `RefreshToken` en Domain; EF, emisor de tokens y `PasswordHasher` en Infrastructure. Los DTO son `record` | Pasa |
| §5 Contrato | `Response<T>` en todos los casos. `InvalidModelStateResponseFactory` evita los `ProblemDetails` automáticos | Pasa |
| §6 Persistencia e identidad | Esquema creado por V005 con el runner; ningún servicio crea esquema al arrancar. `PasswordHasher` de Identity; usuarios iniciales por configuración externa, repetibles y sin sobrescribir; sin contraseñas en scripts | Pasa |
| §9 JWT | `sub` y `role`, HS256 con `JWT_CLAVE`/`EMISOR`/`AUDIENCIA`; acceso de 60 min; refresh de 1 h, guardado como hash y rotado; 401 genérico | Pasa |
| §10 Logging | Sin contraseñas, hashes, tokens ni clave (research D-07) | Pasa |
| §11/§12 | Variables en Compose; el Dockerfile ya existe; un solo paquete nuevo; sin tests | Pasa |

Tras la Phase 1: sin violaciones.

## Project Structure

```text
specs/008-autenticacion-jwt/
├── plan.md, research.md, data-model.md, quickstart.md
├── contracts/{auth-api.md, v005-refresh-token.md}
├── checklists/requirements.md
└── tasks.md            # /speckit-tasks (no creado aquí)

database/scripts/V005__refresh_token.sql                     # tabla RefreshToken (contracts/v005-refresh-token.md)
compose.yaml                                                 # ms-autenticacion: Jwt__* y UsuariosIniciales__* desde .env
.env.example                                                 # JWT_VIGENCIA_*, ADMIN_*, OPERADOR_* sin valores secretos

ms-autenticacion/src/
├── Acity.Api/
│   ├── Program.cs                                           # opciones, ApiBehavior 400, creación de usuarios iniciales antes de Run
│   ├── Options/UsuariosInicialesOptions.cs
│   └── Controllers/AuthController.cs                        # POST /auth/login, POST /auth/refresh
├── Acity.Application/
│   ├── Autenticacion/
│   │   ├── Dtos/TokensDto.cs                                # accessToken, expiraEn, refreshToken, refreshExpiraEn
│   │   ├── Commands/IniciarSesion/{IniciarSesionCommand, IniciarSesionCommandHandler, IniciarSesionCommandValidator}.cs
│   │   ├── Commands/RenovarToken/{RenovarTokenCommand, RenovarTokenCommandHandler, RenovarTokenCommandValidator}.cs
│   │   └── Commands/CrearUsuariosIniciales/{CrearUsuariosInicialesCommand, CrearUsuariosInicialesCommandHandler}.cs
│   └── Interfaces/
│       ├── Services/{IEmisorTokens, IHashContrasena}.cs
│       └── Repositories/IRefreshTokenRepository.cs          # MarcarUsadoAsync (UPDATE condicional)
├── Acity.Domain/Entities/RefreshToken.cs
└── Acity.Infrastructure/
    ├── Acity.Infrastructure.csproj                          # + JsonWebTokens 8.19.2, FrameworkReference
    ├── DependencyInjection.cs
    ├── Persistence/Configurations/RefreshTokenConfiguration.cs
    ├── Repositories/RefreshTokenRepository.cs
    └── Security/{JwtOptions, EmisorTokens, HashContrasena}.cs
```

Los demás servicios no cambian.

## Complexity Tracking

| Elemento | Por qué | Alternativa descartada |
|---|---|---|
| Tabla `RefreshToken` (V005) | Decisiones Q2 y Q3: guardar el hash y rechazar un token ya usado | Token sin estado (no se puede rotar) |
| `IRefreshTokenRepository` específico | El `UPDATE` condicional atómico para marcar el uso no cabe en el repositorio genérico | Leer y escribir con el repositorio genérico (permite que dos usos simultáneos tengan éxito) |
| `IEmisorTokens` / `IHashContrasena` | Mantienen la criptografía y `PasswordHasher` fuera de Application | Usar las bibliotecas directamente en los handlers (rompe las capas) |

## Pendientes tras el plan

Ninguno que requiera decisión.

- Antes de implementar, el usuario debe definir en `.env` los valores de `ADMIN_*` y `OPERADOR_*`; el agente no los escribirá sin autorización.
- La ingeniería inversa de `RefreshToken`, una vez aplicada V005, es manual o requiere autorización expresa.
- En la implementación se comprobarán los mensajes del modelo ante un JSON inválido.
- Validación en el equipo de evaluación: no realizada.
