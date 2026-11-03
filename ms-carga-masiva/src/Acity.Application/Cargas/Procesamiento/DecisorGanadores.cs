namespace Acity.Application.Cargas.Procesamiento;

/// <summary>Fila que se registra como Existente, con el periodo de la propia fila.</summary>
public sealed record FilaExistente(FilaValida Fila, string Motivo);

/// <summary>Lo que confirma la transacción de un periodo Admitido (F001 R-05 paso 3).</summary>
public sealed class PlanPeriodo
{
    public List<FilaValida> Productos { get; } = [];

    /// <summary>Existentes del periodo frente a otras cargas y duplicadas (de cualquier periodo) de sus ganadores.</summary>
    public List<FilaExistente> Existentes { get; } = [];

    public List<FilaOmitida> Omitidas { get; } = [];
}

/// <summary>
/// Decisión de ganadores sobre todo el archivo antes de persistir (F001 R-05, FR-045): entre las filas válidas de
/// periodos Admitido o Completado de esta carga gana, por código, la de menor número de fila; los códigos confirmados
/// por otras cargas son Existente en el periodo de la fila; las duplicadas de un ganador son Existente en la
/// transacción del ganador. Si el periodo del ganador ya está Completado (recuperación), ganador y duplicadas ya están
/// confirmados y se omiten. Clase pura.
/// </summary>
public static class DecisorGanadores
{
    public const string MotivoOtraCarga = "Código de producto ya registrado por otra carga.";
    public const string MotivoDuplicado = "Código de producto ya aceptado en una fila anterior del archivo (fila {0}).";
    public const string MotivoGanadorNoPersistido = "No procesado: la fila ganadora del código no se persistió (fila {0}).";

    /// <param name="validas">Filas válidas del archivo.</param>
    /// <param name="omitidas">Filas omitidas con periodo válido (se asignan a su periodo si está Admitido).</param>
    /// <param name="resultados">Resultado de cada periodo de la carga (Admitido, Completado, Rechazado, Bloqueado).</param>
    /// <param name="codigosOtrasCargas">Códigos ya confirmados por otras cargas.</param>
    public static Decision Decidir(
        IReadOnlyList<FilaValida> validas,
        IReadOnlyList<FilaOmitida> omitidas,
        IReadOnlyDictionary<string, string> resultados,
        IReadOnlySet<string> codigosOtrasCargas)
    {
        var planes = resultados
            .Where(r => r.Value == ResultadosPeriodo.Admitido)
            .ToDictionary(r => r.Key, _ => new PlanPeriodo());
        var duplicadasPorPeriodoGanador = new Dictionary<string, List<(FilaValida Duplicada, FilaValida Ganadora)>>();

        foreach (var omitida in omitidas)
        {
            if (omitida.PeriodoValido is { } periodo && planes.TryGetValue(periodo, out var plan))
            {
                plan.Omitidas.Add(omitida);
            }
        }

        var elegibles = validas
            .Where(f => resultados.TryGetValue(f.Periodo, out var r) && r is ResultadosPeriodo.Admitido or ResultadosPeriodo.Completado)
            .GroupBy(f => f.CodigoProducto, StringComparer.OrdinalIgnoreCase);

        foreach (var grupo in elegibles)
        {
            var ordenadas = grupo.OrderBy(f => f.NumeroFila).ToList();

            if (codigosOtrasCargas.Contains(grupo.Key))
            {
                foreach (var fila in ordenadas.Where(f => planes.ContainsKey(f.Periodo)))
                {
                    planes[fila.Periodo].Existentes.Add(new FilaExistente(fila, MotivoOtraCarga));
                }

                continue;
            }

            var ganadora = ordenadas[0];
            if (!planes.TryGetValue(ganadora.Periodo, out var planGanador))
            {
                // Periodo del ganador ya Completado: producto y Existentes de sus duplicadas ya confirmados.
                continue;
            }

            planGanador.Productos.Add(ganadora);
            foreach (var duplicada in ordenadas.Skip(1))
            {
                planGanador.Existentes.Add(new FilaExistente(duplicada, string.Format(MotivoDuplicado, ganadora.NumeroFila)));
                if (!duplicadasPorPeriodoGanador.TryGetValue(ganadora.Periodo, out var lista))
                {
                    duplicadasPorPeriodoGanador[ganadora.Periodo] = lista = [];
                }

                lista.Add((duplicada, ganadora));
            }
        }

        return new Decision(planes, duplicadasPorPeriodoGanador);
    }

    /// <summary>Planes por periodo Admitido y duplicadas que dependen de cada periodo ganador.</summary>
    public sealed class Decision(
        IReadOnlyDictionary<string, PlanPeriodo> planes,
        IReadOnlyDictionary<string, List<(FilaValida Duplicada, FilaValida Ganadora)>> duplicadas)
    {
        public IReadOnlyDictionary<string, PlanPeriodo> Planes { get; } = planes;

        /// <summary>
        /// Tras un fallo terminal: duplicadas situadas en periodos Completado cuyo ganador estaba en un periodo no
        /// persistido → NoProcesado "ganador no persistido" (F001 FR-049). Las de periodos no completados las cubre el
        /// NoProcesado del periodo.
        /// </summary>
        public IEnumerable<(FilaValida Duplicada, FilaValida Ganadora)> DuplicadasSinGanador(
            IReadOnlySet<string> periodosNoPersistidos, IReadOnlySet<string> periodosCompletados)
            => duplicadas
                .Where(d => periodosNoPersistidos.Contains(d.Key))
                .SelectMany(d => d.Value)
                .Where(d => periodosCompletados.Contains(d.Duplicada.Periodo));
    }
}

/// <summary>Valores de <c>CargaPeriodo.Resultado</c> (CHECK del esquema).</summary>
public static class ResultadosPeriodo
{
    public const string Admitido = "Admitido";
    public const string Completado = "Completado";
    public const string Rechazado = "Rechazado";
    public const string Bloqueado = "Bloqueado";
}
