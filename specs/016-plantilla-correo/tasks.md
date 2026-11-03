# Tasks: Plantilla del correo de notificación

**Input**: `specs/016-plantilla-correo/` (plan.md, spec.md, research.md, data-model.md, contracts/correo-notificacion.md, quickstart.md)

**Alcance**: `ms-notificaciones/`, `compose.yaml` (una variable de ms-notificaciones) y `.env.example`.

**Reglas**:
- Sin dependencias, motores de plantillas ni cambios de esquema.
- Sin pruebas automatizadas.
- Verificación sobre `CargaMasivaApp_F016`; borrar **solo** los correos de la prueba.
- `[x]` solo para lo ejecutado.
- Commit en español, sin `Co-Authored-By`.

## Phase 1: Foundational

- [x] T001 [P] Añadir `public string NombreArchivo { get; set; } = null!;` a `ms-notificaciones/src/Acity.Domain/Entities/CargaArchivo.cs` y `HasMaxLength(260)` en `ms-notificaciones/src/Acity.Infrastructure/Persistence/Configurations/CargaArchivoConfiguration.cs` (nvarchar(260)).
- [x] T002 [P] Crear `ms-notificaciones/src/Acity.Domain/Entities/CargaPeriodo.cs` (`long Id`, `long CargaId`, `string Periodo`, `string Resultado`) y `ms-notificaciones/src/Acity.Infrastructure/Persistence/Configurations/CargaPeriodoConfiguration.cs`:
  - `ToTable("CargaPeriodo")`;
  - `Periodo` con `HasMaxLength(7)`, `IsUnicode(false)` e `IsFixedLength()`;
  - `Resultado` con `HasMaxLength(20)` e `IsUnicode(false)`;
  - FK `CargaId` hacia `CargaArchivo`, sin navegación.
  - Contrastar con el catálogo (`sys.columns`), igual que en la Feature 010.
- [x] T003 [P] Crear los records de data-model.md en `ms-notificaciones/src/Acity.Application/Notificaciones/Correo/`:
  - `ResumenCarga.cs`, que también contiene `PeriodoResumen`, `TipoRecuento` e `IncidenciaResumen`;
  - `CorreoNotificacion.cs`;
  - `ConfiguracionCorreo.cs` (`string? UrlFrontend`).
- [x] T004 Añadir `Task<ResumenCarga> ObtenerResumenAsync(long idCarga, CancellationToken ct)` a `ms-notificaciones/src/Acity.Application/Interfaces/Services/ICargasConsulta.cs` e implementarlo en `ms-notificaciones/src/Acity.Infrastructure/Persistence/CargasConsulta.cs`:
  - dentro del pipeline `sql` y con `AsNoTracking`;
  - nombre del archivo;
  - periodos ordenados por `Periodo`;
  - recuento por `Tipo`, ordenado por recuento descendente;
  - el total;
  - las primeras 20 por `Id` (research D-01).
- [x] T005 Cambiar `IEnvioCorreo.EnviarAsync` a `(string destinatario, string asunto, string texto, string html, CancellationToken ct)` en `ms-notificaciones/src/Acity.Application/Interfaces/Services/IEnvioCorreo.cs`. En `ms-notificaciones/src/Acity.Infrastructure/Mail/EnvioCorreoMailKit.cs`, usar `mensaje.Body = new BodyBuilder { TextBody = texto, HtmlBody = html }.ToMessageBody()` (research D-03).

## Phase 2: US1 - Recibir un correo informativo (P1) 🎯 MVP

**Prueba independiente**: los escenarios 1 a 7 del quickstart.

- [x] T006 [US1] Crear `ms-notificaciones/src/Acity.Application/Notificaciones/Correo/PlantillaCorreo.cs` (research D-02; `contracts/correo-notificacion.md`) con un método estático `Construir(long idCarga, DateTime fechaFin, ResumenCarga resumen, string? urlFrontend)` que devuelve `CorreoNotificacion`:
  - **Asunto**: `Carga {idCarga} finalizada`.
  - **HTML**:
    - estilos `style` en línea y una tabla de 600 px;
    - cabecera; datos (archivo, carga y fecha de fin UTC `yyyy-MM-dd HH:mm:ss`);
    - tabla de periodos con colores (Completado `#166534`/`#dcfce7`; Rechazado y Fallido `#991b1b`/`#fee2e2`; Bloqueado y NoProcesado `#92400e`/`#fef3c7`; otro `#374151`/`#f3f4f6`) o «Sin periodos procesados»;
    - «Total: N» con el recuento por tipo, la tabla de las primeras 20 y «y {N−20} más; consulte el detalle de la carga», o «Sin incidencias»;
    - botón «Ver detalle» a `{url.TrimEnd('/')}/historial/{idCarga}` si `urlFrontend` no está vacía.
  - **Texto**: las mismas secciones en texto plano.
  - **Escapado**: `WebUtility.HtmlEncode` en todos los valores.
  - Nunca dice «correctamente» ni «sin errores».
- [x] T007 [US1] En `ms-notificaciones/src/Acity.Application/Notificaciones/Commands/NotificarCarga/NotificarCargaCommandHandler.cs`:
  - inyectar `ConfiguracionCorreo`;
  - tras comprobar Finalizado, `var resumen = await cargas.ObtenerResumenAsync(request.IdCarga, CancellationToken.None);` (si falla, la excepción sube y el mensaje no se confirma, como al leer el estado);
  - `var correoNotif = PlantillaCorreo.Construir(request.IdCarga, request.FechaFin, resumen, configuracion.UrlFrontend);`
  - `await correo.EnviarAsync(request.Usuario, correoNotif.Asunto, correoNotif.Texto, correoNotif.Html, …)`;
  - el resto (`ErrorNotificacion` y Notificado) sin cambios;
  - actualizar el comentario de Q1 indicando que se sustituye por la Feature 016.
- [x] T008 [US1] Configuración de la URL del frontend (research D-04):
  - **`ms-notificaciones/src/Acity.Api/Program.cs`**:
    - leer `Notificaciones:UrlFrontend` y aceptarla solo si es una URI absoluta http o https; si no, null, con un log Warning si estaba informada;
    - registrar `services.AddSingleton(new ConfiguracionCorreo(url))`.
  - **`compose.yaml`**: añadir en ms-notificaciones `Notificaciones__UrlFrontend: ${FRONTEND_URL:-http://localhost:3000}`.
  - **`.env.example`**: documentar `FRONTEND_URL=http://localhost:3000` como opcional.
  - Comprobar con `docker compose config -q`.
- [x] T009 [US1] `dotnet build ms-notificaciones/src/Acity.Notificaciones.slnx` con 0 advertencias y 0 errores.
- [x] T010 [US1] Verificar los escenarios 1 a 7 del quickstart en Mailpit, sobre `CargaMasivaApp_F016`; borrar solo los correos de la prueba y registrar los resultados en `specs/016-plantilla-correo/quickstart.md`.

## Estado (2026-11-03)

- **10 de 10 tareas hechas.** `dotnet build`: 0 advertencias, 0 errores; `docker compose config -q` correcto.
- **Mapeo de `CargaPeriodo`**: idéntico al de Control, ya contrastado con el catálogo en la Feature 010 (misma tabla; columnas `Id`, `CargaId`, `Periodo` char(7) y `Resultado` varchar(20)).
- **Ajuste respecto a T006**: en la versión de texto, el contexto de cada incidencia (fila, código, periodo) se omite si no tiene datos, para no mostrar `()` vacío.

## Dependencias y paralelismo

- T001, T002 y T003 en paralelo.
- T004 depende de T001 a T003. T005 es independiente.
- T006 depende de T003. T007 depende de T004 a T006. T008 depende de T003. Después, T009 y T010.

## Estrategia

Una sola historia, que es el MVP.
