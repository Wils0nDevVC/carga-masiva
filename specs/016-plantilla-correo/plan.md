# Implementation Plan: Plantilla del correo de notificación

**Branch**: `016-plantilla-correo` (trabajo en `main`) | **Date**: 2026-11-03 | **Spec**: [spec.md](spec.md)

## Summary

ms-notificaciones, después de comprobar que la carga está Finalizado:
- lee un `ResumenCarga`: nombre del archivo, periodos, total de incidencias, recuento por tipo y las 20 primeras;
- construye con `PlantillaCorreo` (función pura de Application) un HTML con estilos integrados y colores, más su texto plano equivalente, con «Ver detalle» si `Notificaciones__UrlFrontend` está configurada;
- lo envía como `multipart/alternative` con MailKit.

Se conservan los reintentos, `ErrorNotificacion`, el descarte y el paso a Notificado de la 007. Los mapeos nuevos (`NombreArchivo` y `CargaPeriodo`) son de solo lectura. Sin dependencias ni cambios de esquema. [Spec] [Dec Q1–Q3] [research D-01 a D-04]

## Technical Context

**Language/Version**: C# / .NET 10.

**Primary Dependencies**: las existentes (MailKit 4.18.2 con MimeKit, EF Core 10.0.14 y Polly.Core 8.9.0); ninguna nueva.

**Storage**: `CargaMasivaApp` sin cambios.

**Testing**: manual con Mailpit (`quickstart.md`); sin tests (§11).

**Project Type**: microservicio. Toca `ms-notificaciones/` y `compose.yaml` (una variable), y documenta `FRONTEND_URL` en `.env.example`.

**Constraints**: sin motores de plantillas, imágenes ni adjuntos.

## Constitution Check

Constitución v3.9.0.

| Principio | Verificación | Resultado |
|---|---|---|
| §1 Alcance | Solo el contenido del correo autorizado | Pasa |
| §3/§4 Capas | Plantilla y records en Application; consultas EF y MailKit en Infrastructure; entidad en Domain | Pasa |
| §6 Persistencia | Mapeo mínimo de lectura; sin esquema; ingeniería inversa o contraste con el catálogo | Pasa |
| §8 Correo | Contenido de la v3.9.0; asunto, reintentos, `ErrorNotificacion` y Notificado sin cambios; no dice «sin errores» | Pasa |
| §10 Logs | No se registra el cuerpo | Pasa |
| §11/§12 | URL por entorno; sin dependencias nuevas | Pasa |

## Project Structure

```text
ms-notificaciones/src/
├── Acity.Api/Program.cs                                        # registra ConfiguracionCorreo desde Notificaciones:UrlFrontend
├── Acity.Application/
│   ├── Notificaciones/Correo/{ResumenCarga, PlantillaCorreo, CorreoNotificacion, ConfiguracionCorreo}.cs
│   ├── Notificaciones/Commands/NotificarCarga/NotificarCargaCommandHandler.cs   # lee el resumen y construye el correo
│   └── Interfaces/Services/{ICargasConsulta (+ObtenerResumenAsync), IEnvioCorreo (texto + html)}.cs
├── Acity.Domain/Entities/{CargaArchivo (+NombreArchivo), CargaPeriodo}.cs
└── Acity.Infrastructure/
    ├── Persistence/{CargasConsulta.cs, Configurations/{CargaArchivoConfiguration, CargaPeriodoConfiguration}.cs}
    └── Mail/EnvioCorreoMailKit.cs                               # BodyBuilder (multipart/alternative)
compose.yaml                                                     # ms-notificaciones: Notificaciones__UrlFrontend: ${FRONTEND_URL:-http://localhost:3000}
.env.example                                                     # FRONTEND_URL documentada
```

## Complexity Tracking

| Elemento | Por qué | Alternativa descartada |
|---|---|---|
| `PlantillaCorreo` hecha a mano | 4 secciones fijas; sin motor de plantillas (§12) | Razor o Scriban (dependencia nueva) |
| `CargaPeriodo` en ms-notificaciones | Resultado por periodo (Spec FR-002) | Pedir los datos a Control (dependencia entre servicios) |
