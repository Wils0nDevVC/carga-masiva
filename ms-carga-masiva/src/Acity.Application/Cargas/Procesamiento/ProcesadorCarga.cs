using Acity.Application.Interfaces.Repositories;
using Acity.Application.Interfaces.Services;
using Acity.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Acity.Application.Cargas.Procesamiento;

/// <summary>
/// Procesa una carga ya En proceso (research D-05): intento → descarga → lectura → validación → reserva de periodos →
/// decisión de ganadores → una transacción por periodo → Cargado → Finalizado → notificación. Aplica las reglas de la
/// constitución §7–§8 y F001 sin reinterpretarlas. Desde aquí las escrituras no usan la cancelación del host.
/// </summary>
public sealed class ProcesadorCarga(
    IRepositorioProcesamiento repositorio,
    IProcedimientosCarga procedimientos,
    IAlmacenamientoArchivos almacenamiento,
    ILectorExcel lectorExcel,
    IPublicadorNotificaciones publicador,
    ICerrojoConsumidor cerrojo,
    ProcesamientoOptions opciones,
    ILogger<ProcesadorCarga> logger)
{
    public const string TipoFilaOmitida = "FilaOmitida";
    public const string TipoExistente = "Existente";
    public const string TipoNoProcesado = "NoProcesado";
    public const string TipoErrorTecnico = "ErrorTecnico";
    public const string TipoArchivoInvalido = "ArchivoInvalido";

    public const string MotivoNotificacionSinConfirmar =
        "No se obtuvo confirmación del intermediario para el mensaje de notificación; no se garantiza que se haya publicado ni que se notifique después.";

    private static readonly CancellationToken SinCancelacion = CancellationToken.None;

    /// <summary>Procesa (o reanuda) una carga En proceso hasta Finalizado o Fallido.</summary>
    public async Task ProcesarAsync(long cargaId, string rutaArchivo)
    {
        var estado = new EstadoEjecucion();
        try
        {
            string? motivoFallo;
            try
            {
                motivoFallo = await EjecutarAsync(cargaId, rutaArchivo, estado);
            }
            catch (Exception ex) when (ex is CerrojoPerdidoException or DependenciaNoDisponibleException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Agotamiento de reintentos de una operación ejecutada o error no reintentable: fallo terminal real.
                logger.LogError(ex, "Fallo técnico en la carga {IdCarga} durante {Operacion}", cargaId, estado.Operacion);
                motivoFallo = $"Fallo técnico durante {estado.Operacion}: {ex.GetType().Name}.";
            }

            if (motivoFallo is not null)
            {
                // Una sola vez; si este registro falla, la excepción sube y el mensaje no se confirma.
                await RegistrarFalloAsync(cargaId, motivoFallo, estado);
                return;
            }

            // Cargado → Finalizado y notificación fuera del tratamiento de fallo terminal: si falla un cambio de estado,
            // la carga conserva su estado y la recuperación lo completa (constitución §8); la excepción sube sin confirmar.
            await FinalizarYNotificarAsync(cargaId, desdeEnProceso: true);
        }
        catch (DependenciaNoDisponibleException ex)
        {
            // Rechazo por circuito abierto (la operación no se ejecutó): no es fallo terminal, no marca Fallido ni
            // registra ErrorTecnico y no consume el intento (decisión H3; research D-08b). El mensaje no se confirma.
            logger.LogWarning("Carga {IdCarga}: {Operacion} no disponible (circuito abierto) durante {Fase}; se reintentará tras la espera",
                cargaId, ex.Operacion, estado.Operacion);
            if (estado.IntentoContado)
            {
                try
                {
                    await repositorio.DescontarIntentoAsync(cargaId, SinCancelacion);
                }
                catch (Exception exDescuento)
                {
                    logger.LogError(exDescuento, "No se pudo devolver el intento de la carga {IdCarga}; queda contado", cargaId);
                }
            }

            throw;
        }
    }

    /// <summary>Fases que pueden terminar en fallo técnico terminal. Devuelve el motivo del fallo o <c>null</c>.</summary>
    private async Task<string?> EjecutarAsync(long cargaId, string rutaArchivo, EstadoEjecucion estado)
    {
        estado.Operacion = "registro del intento";
        var intento = await repositorio.IncrementarIntentosAsync(cargaId, SinCancelacion);
        estado.IntentoContado = true;
        if (intento > opciones.MaxIntentos)
        {
            return $"Intentos de procesamiento agotados ({intento - 1} de {opciones.MaxIntentos}).";
        }

        logger.LogInformation("Procesando la carga {IdCarga} (intento {Intento})", cargaId, intento);

        estado.Operacion = "recuperación del archivo";
        byte[] contenido;
        try
        {
            contenido = await almacenamiento.DescargarAsync(rutaArchivo, SinCancelacion);
        }
        catch (ArchivoNoEncontradoException)
        {
            return "No se pudo recuperar el archivo: no existe en el almacenamiento.";
        }

        estado.Operacion = "lectura del archivo";
        var lectura = lectorExcel.Leer(contenido);
        if (lectura.MotivoArchivoInvalido is { } motivoInvalido)
        {
            estado.Operacion = "registro del archivo inválido";
            await RegistrarArchivoInvalidoAsync(cargaId, motivoInvalido);
            return null;
        }

        var validacion = ValidadorFilas.Validar(lectura.Filas);

        estado.Operacion = "registro de filas sin periodo válido";
        await RegistrarOmitidasSinPeriodoAsync(cargaId, validacion.Omitidas);

        estado.Operacion = "reserva de periodos";
        var enOrden = PeriodosEnOrden(validacion).ToList();
        foreach (var periodo in enOrden)
        {
            await procedimientos.ReservarPeriodoAsync(cargaId, periodo, SinCancelacion);
        }

        estado.Periodos = await repositorio.ObtenerPeriodosAsync(cargaId, SinCancelacion);
        estado.Completados.UnionWith(estado.Periodos.Where(p => p.Value.Resultado == ResultadosPeriodo.Completado).Select(p => p.Key));
        var resultados = estado.Periodos.ToDictionary(p => p.Key, p => p.Value.Resultado);

        estado.Operacion = "consulta de códigos existentes";
        var candidatos = validacion.Validas
            .Where(f => resultados.TryGetValue(f.Periodo, out var r) && r is ResultadosPeriodo.Admitido or ResultadosPeriodo.Completado)
            .Select(f => f.CodigoProducto)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var codigosOtrasCargas = await repositorio.ObtenerCodigosDeOtrasCargasAsync(cargaId, candidatos, SinCancelacion);

        estado.Decision = DecisorGanadores.Decidir(validacion.Validas, validacion.Omitidas, resultados, codigosOtrasCargas);

        foreach (var periodo in enOrden.Where(estado.Decision.Planes.ContainsKey))
        {
            // Un consumidor que perdió el cerrojo se detiene sin confirmar (no es un fallo de la carga).
            await cerrojo.ComprobarVigenteAsync(SinCancelacion);

            estado.PeriodoEnCurso = periodo;
            estado.Operacion = $"persistencia del periodo {periodo}";
            var resultado = await PersistirPeriodoAsync(cargaId, periodo, estado.Periodos, estado.Decision.Planes[periodo]);
            estado.Completados.Add(periodo);
            estado.PeriodoEnCurso = null;
            logger.LogInformation("Carga {IdCarga}: periodo {Periodo} {Resultado}", cargaId, periodo, resultado);
        }

        return null;
    }

    /// <summary>Lo conocido de la ejecución en curso, para registrar un fallo terminal.</summary>
    private sealed class EstadoEjecucion
    {
        public string Operacion { get; set; } = "procesamiento";

        /// <summary>Esta ejecución ya incrementó <c>IntentosProceso</c> (se devuelve si termina por circuito abierto).</summary>
        public bool IntentoContado { get; set; }

        public string? PeriodoEnCurso { get; set; }

        public IReadOnlyDictionary<string, (long Id, string Resultado)> Periodos { get; set; } = new Dictionary<string, (long, string)>();

        public HashSet<string> Completados { get; } = [];

        public DecisorGanadores.Decision? Decision { get; set; }
    }

    /// <summary>Cargado (si viene de En proceso) → Finalizado → notificación.</summary>
    public async Task FinalizarYNotificarAsync(long cargaId, bool desdeEnProceso)
    {
        if (desdeEnProceso)
        {
            await procedimientos.CambiarEstadoAsync(cargaId, "Cargado", null, null, SinCancelacion);
        }

        await procedimientos.CambiarEstadoAsync(cargaId, "Finalizado", null, null, SinCancelacion);
        logger.LogInformation("Carga {IdCarga} finalizada", cargaId);

        var (email, fechaFin) = await repositorio.ObtenerDatosNotificacionAsync(cargaId, SinCancelacion);
        try
        {
            await publicador.PublicarAsync(new MensajeNotificacion(cargaId, email, fechaFin ?? DateTime.UtcNow), SinCancelacion);
            logger.LogInformation("Notificación de la carga {IdCarga} publicada", cargaId);
        }
        catch (Exception ex)
        {
            // Sin confirmación no se afirma que no llegó; la carga conserva Finalizado (constitución §8).
            logger.LogError(ex, "Notificación de la carga {IdCarga} sin confirmación del intermediario", cargaId);
            await repositorio.RegistrarIncidenciasAsync([Incidencia(cargaId, null, TipoErrorTecnico, MotivoNotificacionSinConfirmar)], SinCancelacion);
        }
    }

    private async Task<ResultadoPersistenciaPeriodo> PersistirPeriodoAsync(
        long cargaId, string periodo, IReadOnlyDictionary<string, (long Id, string Resultado)> periodos, PlanPeriodo plan)
    {
        var cargaPeriodoId = periodos[periodo].Id;
        var productos = plan.Productos
            .OrderBy(f => f.NumeroFila)
            .Select(f => new Producto
            {
                CodigoProducto = f.CodigoProducto,
                NombreProducto = f.NombreProducto,
                Precio = f.Precio,
                CargaPeriodoId = cargaPeriodoId
            })
            .ToList();
        var filasPorProducto = plan.Productos.ToDictionary(f => f.CodigoProducto, StringComparer.OrdinalIgnoreCase);

        var incidencias = new List<Incidencia>();
        incidencias.AddRange(plan.Omitidas.Select(o => new Incidencia
        {
            CargaId = cargaId,
            CargaPeriodoId = cargaPeriodoId,
            Tipo = TipoFilaOmitida,
            NumeroFila = o.NumeroFila,
            CodigoProducto = o.CodigoProducto,
            Motivo = o.Motivo,
            FechaRegistro = DateTime.UtcNow
        }));
        incidencias.AddRange(plan.Existentes.Select(e => new Incidencia
        {
            CargaId = cargaId,
            CargaPeriodoId = periodos[e.Fila.Periodo].Id,
            Tipo = TipoExistente,
            NumeroFila = e.Fila.NumeroFila,
            CodigoProducto = e.Fila.CodigoProducto,
            Motivo = e.Motivo,
            FechaRegistro = DateTime.UtcNow
        }));

        return await repositorio.PersistirPeriodoAsync(cargaPeriodoId, productos, incidencias,
            producto => new Incidencia
            {
                CargaId = cargaId,
                CargaPeriodoId = cargaPeriodoId,
                Tipo = TipoExistente,
                NumeroFila = filasPorProducto[producto.CodigoProducto].NumeroFila,
                CodigoProducto = producto.CodigoProducto,
                Motivo = "Código de producto ya registrado (detectado al confirmar).",
                FechaRegistro = DateTime.UtcNow
            },
            SinCancelacion);
    }

    /// <summary>
    /// Fallo técnico terminal (research D-14): incidencia técnica, Fallido (el procedimiento deja NoProcesado los demás
    /// Admitido y libera reservas) y NoProcesado de las duplicadas de periodos Completado sin ganador confirmado.
    /// Si este registro falla, la excepción sube: el mensaje no se confirma y la recuperación retoma la carga.
    /// </summary>
    private async Task RegistrarFalloAsync(long cargaId, string motivo, EstadoEjecucion estado)
    {
        long? periodoId = estado.PeriodoEnCurso is not null && estado.Periodos.TryGetValue(estado.PeriodoEnCurso, out var p) ? p.Id : null;
        await repositorio.RegistrarIncidenciasAsync([Incidencia(cargaId, periodoId, TipoErrorTecnico, motivo)], SinCancelacion);
        await procedimientos.CambiarEstadoAsync(cargaId, "Fallido", estado.PeriodoEnCurso, null, SinCancelacion);

        if (estado.Decision is not null)
        {
            var noPersistidos = estado.Decision.Planes.Keys.Where(k => !estado.Completados.Contains(k)).ToHashSet();
            var sinGanador = estado.Decision.DuplicadasSinGanador(noPersistidos, estado.Completados)
                .Select(d => new Incidencia
                {
                    CargaId = cargaId,
                    CargaPeriodoId = estado.Periodos[d.Duplicada.Periodo].Id,
                    Tipo = TipoNoProcesado,
                    NumeroFila = d.Duplicada.NumeroFila,
                    CodigoProducto = d.Duplicada.CodigoProducto,
                    Motivo = string.Format(DecisorGanadores.MotivoGanadorNoPersistido, d.Ganadora.NumeroFila),
                    FechaRegistro = DateTime.UtcNow
                })
                .ToList();
            await repositorio.RegistrarIncidenciasAsync(sinGanador, SinCancelacion);
        }

        logger.LogWarning("Carga {IdCarga} Fallido: {Motivo}", cargaId, motivo);
    }

    private async Task RegistrarArchivoInvalidoAsync(long cargaId, string motivo)
    {
        if (!await repositorio.ExisteIncidenciaSinPeriodoAsync(cargaId, TipoArchivoInvalido, SinCancelacion))
        {
            await repositorio.RegistrarIncidenciasAsync([Incidencia(cargaId, null, TipoArchivoInvalido, motivo)], SinCancelacion);
        }

        logger.LogWarning("Carga {IdCarga}: archivo inválido ({Motivo})", cargaId, motivo);
    }

    /// <summary>Filas sin periodo válido: todas juntas y una sola vez (idempotente en recuperación).</summary>
    private async Task RegistrarOmitidasSinPeriodoAsync(long cargaId, IReadOnlyList<FilaOmitida> omitidas)
    {
        var sinPeriodo = omitidas.Where(o => o.PeriodoValido is null).ToList();
        if (sinPeriodo.Count == 0 || await repositorio.ExisteIncidenciaSinPeriodoAsync(cargaId, TipoFilaOmitida, SinCancelacion))
        {
            return;
        }

        await repositorio.RegistrarIncidenciasAsync(sinPeriodo.Select(o => new Incidencia
        {
            CargaId = cargaId,
            CargaPeriodoId = null,
            Tipo = TipoFilaOmitida,
            NumeroFila = o.NumeroFila,
            CodigoProducto = o.CodigoProducto,
            PeriodoTexto = o.PeriodoTexto,
            Motivo = o.Motivo,
            FechaRegistro = DateTime.UtcNow
        }).ToList(), SinCancelacion);
    }

    /// <summary>Periodos válidos distintos en orden de primera aparición en el archivo.</summary>
    private static IEnumerable<string> PeriodosEnOrden(ResultadoValidacion validacion)
        => validacion.Validas.Select(f => (f.NumeroFila, Periodo: (string?)f.Periodo))
            .Concat(validacion.Omitidas.Select(o => (o.NumeroFila, Periodo: o.PeriodoValido)))
            .Where(x => x.Periodo is not null)
            .OrderBy(x => x.NumeroFila)
            .Select(x => x.Periodo!)
            .Distinct(StringComparer.Ordinal);

    private static Incidencia Incidencia(long cargaId, long? cargaPeriodoId, string tipo, string motivo) => new()
    {
        CargaId = cargaId,
        CargaPeriodoId = cargaPeriodoId,
        Tipo = tipo,
        Motivo = motivo.Length > 500 ? motivo[..500] : motivo,
        FechaRegistro = DateTime.UtcNow
    };
}
