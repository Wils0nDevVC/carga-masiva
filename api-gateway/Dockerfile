# Imagen del API Gateway (contexto de construcción = este directorio; no incluye otros servicios).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ ./src/
RUN dotnet publish src/Acity.Api/Acity.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
# Directorio de logs propiedad del usuario de ejecución (no root); se monta un volumen sobre él.
RUN mkdir -p /app/logs && chown $APP_UID /app/logs
COPY --from=build /app/publish .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Acity.Api.dll"]
