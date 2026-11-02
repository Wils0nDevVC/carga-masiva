# Imagen del runner de scripts: reutiliza la imagen del servidor (ya incluye sqlcmd en
# /opt/mssql-tools18/bin; research R-11). Misma plataforma que el motor (linux/amd64).
FROM mcr.microsoft.com/mssql/server:2022-CU27-ubuntu-22.04

COPY --chown=mssql:root run-scripts.sh bootstrap.sql /database/
COPY --chown=mssql:root scripts/ /database/scripts/

ENTRYPOINT ["/bin/bash", "/database/run-scripts.sh"]
CMD []
