#!/usr/bin/env bash
# Runner unico de scripts versionados (constitucion v3.0.0; Feature 003). Docker Compose lo ejecuta
# una vez por "up", despues de SQL Server y antes de los microservicios.
#  - Un solo responsable del esquema: aplica Vnnn__*.sql en orden y los registra en SchemaMigraciones.
#  - Antes de crear nada comprueba la base: no se aplica sobre una base con historial de EF Core
#    (__EFMigrationsHistory) ni con tablas sin registro del runner (no se mezclan historiales).
#  - Una sola sesion sqlcmd mantiene un cerrojo de aplicacion (sp_getapplock, Session, timeout 0).
#  - Cada script corre en su propia transaccion con XACT_ABORT; si falla, sqlcmd sale con error
#    (:on error exit), la conexion se cierra, SQL Server revierte y el script no queda registrado.
#  - Falla si el checksum de un script aplicado cambio o si un script nuevo tiene numero menor
#    que el ultimo aplicado.
# Secretos: solo por variables de entorno (MSSQL_SA_PASSWORD), nunca en archivos.
set -euo pipefail

SQLCMD="${SQLCMD:-/opt/mssql-tools18/bin/sqlcmd}"
DB_HOST="${DB_HOST:-sqlserver}"
DB_PORT="${DB_PORT:-1433}"
DB_USER="${DB_USER:-sa}"
SCRIPTS_DIR="${SCRIPTS_DIR:-/database/scripts}"
BOOTSTRAP_SQL="${BOOTSTRAP_SQL:-/database/bootstrap.sql}"

# Sin valor por defecto: el nombre de la base destino siempre es explicito (no se opera por descuido
# sobre una base existente).
: "${DB_NAME:?Defina DB_NAME (variable de entorno)}"
: "${MSSQL_SA_PASSWORD:?Defina MSSQL_SA_PASSWORD (variable de entorno)}"
export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"   # evita pasar la contrasena como argumento

if [[ ! "$DB_NAME" =~ ^[A-Za-z0-9_]+$ ]]; then
  echo "DB_NAME invalido: solo letras, digitos y guion bajo." >&2; exit 2
fi

# -C: confia en el certificado autofirmado del contenedor; -b: salir con codigo de error;
# -I: QUOTED_IDENTIFIER ON; -f 65001: scripts en UTF-8.
run() { "$SQLCMD" -C -b -I -f 65001 -S "$DB_HOST,$DB_PORT" -U "$DB_USER" "$@"; }

echo "Esperando a SQL Server en $DB_HOST:$DB_PORT ..."
for i in $(seq 1 60); do
  if run -d master -Q "SELECT 1" >/dev/null 2>&1; then break; fi
  if [[ "$i" == 60 ]]; then echo "SQL Server no responde tras 60 intentos." >&2; exit 1; fi
  sleep 2
done

echo "Comprobando la base $DB_NAME ..."
state="$(run -d master -h -1 -W -Q "SET NOCOUNT ON; IF DB_ID(N'$DB_NAME') IS NULL SELECT 'NUEVA' ELSE IF OBJECT_ID(N'[$DB_NAME].dbo.__EFMigrationsHistory', N'U') IS NOT NULL SELECT 'EF' ELSE IF OBJECT_ID(N'[$DB_NAME].dbo.SchemaMigraciones', N'U') IS NULL AND EXISTS (SELECT 1 FROM [$DB_NAME].sys.tables) SELECT 'SINREGISTRO' ELSE SELECT 'OK'" | tr -d '[:space:]')"
case "$state" in
  NUEVA|OK) ;;
  EF)
    echo "ERROR: la base $DB_NAME tiene historial de EF Core (__EFMigrationsHistory). No se mezclan historiales: se usa una base nueva y vacia. La base no se modifico." >&2; exit 1 ;;
  SINREGISTRO)
    echo "ERROR: la base $DB_NAME tiene tablas sin registro del runner (SchemaMigraciones). No se aplica nada sobre ella: se usa una base nueva y vacia. La base no se modifico." >&2; exit 1 ;;
  *)
    echo "ERROR: no se pudo determinar el estado de la base $DB_NAME." >&2; exit 1 ;;
esac

echo "Bootstrap (base $DB_NAME y SchemaMigraciones) ..."
run -d master -v DB_NAME="$DB_NAME" -i "$BOOTSTRAP_SQL"

declare -A APPLIED=()
last_applied=""
while IFS='|' read -r name sum; do
  [[ -z "${name:-}" ]] && continue
  APPLIED["$name"]="$sum"
  [[ "$name" > "$last_applied" ]] && last_applied="$name"
done < <(run -d "$DB_NAME" -h -1 -W -Q "SET NOCOUNT ON; SELECT Script + '|' + Checksum FROM dbo.SchemaMigraciones ORDER BY Script")

shopt -s nullglob
files=("$SCRIPTS_DIR"/V[0-9][0-9][0-9]__*.sql)
IFS=$'\n' files=($(printf '%s\n' "${files[@]}" | sort)); unset IFS

pending=()
for f in "${files[@]}"; do
  name="$(basename "$f")"
  sum="$(sha256sum "$f" | cut -d' ' -f1)"
  if [[ -n "${APPLIED[$name]:-}" ]]; then
    if [[ "${APPLIED[$name]}" != "$sum" ]]; then
      echo "ERROR: el script aplicado $name cambio (checksum distinto). No se reescribe en silencio." >&2; exit 1
    fi
  else
    if [[ -n "$last_applied" && "$name" < "$last_applied" ]]; then
      echo "ERROR: $name tiene numero menor que el ultimo aplicado ($last_applied)." >&2; exit 1
    fi
    pending+=("$name|$sum")
  fi
done

if [[ ${#pending[@]} -eq 0 ]]; then
  echo "Sin scripts pendientes."; exit 0
fi

master="$(mktemp)"; trap 'rm -f "$master"' EXIT
{
  echo ":on error exit"
  echo "SET XACT_ABORT ON;"
  echo "SET QUOTED_IDENTIFIER ON;"
  echo "GO"
  echo "DECLARE @r INT;"
  echo "EXEC @r = sys.sp_getapplock @Resource = N'carga-masiva:database-runner', @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0;"
  echo "IF @r < 0 THROW 50001, N'Otro runner mantiene el cerrojo de scripts.', 1;"
  echo "GO"
  for item in "${pending[@]}"; do
    name="${item%%|*}"; sum="${item##*|}"
    echo "IF EXISTS (SELECT 1 FROM dbo.SchemaMigraciones WHERE Script = N'$name') THROW 50002, N'$name ya fue aplicado por otro runner; reintente.', 1;"
    echo "GO"
    echo "BEGIN TRANSACTION;"
    echo "GO"
    echo ":r $SCRIPTS_DIR/$name"
    echo ""
    echo "GO"
    echo "INSERT INTO dbo.SchemaMigraciones (Script, Checksum, FechaAplicado) VALUES (N'$name', '$sum', SYSUTCDATETIME());"
    echo "GO"
    echo "COMMIT TRANSACTION;"
    echo "GO"
    echo "PRINT N'Aplicado: $name';"
    echo "GO"
  done
} > "$master"

echo "Aplicando ${#pending[@]} script(s) en una sola sesion ..."
run -d "$DB_NAME" -i "$master"
echo "Listo."
