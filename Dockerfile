# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

COPY *.sln ./
COPY src/ClinicaPsi.Shared/*.csproj ./src/ClinicaPsi.Shared/
COPY src/ClinicaPsi.Infrastructure/*.csproj ./src/ClinicaPsi.Infrastructure/
COPY src/ClinicaPsi.Application/*.csproj ./src/ClinicaPsi.Application/
COPY src/ClinicaPsi.Web/*.csproj ./src/ClinicaPsi.Web/

RUN dotnet restore --verbosity minimal

COPY src/ ./src/

WORKDIR /source/src/ClinicaPsi.Web
RUN dotnet publish -c Release -o /app/publish \
    --no-restore \
    --verbosity minimal \
    /p:PublishTrimmed=false \
    /p:PublishSingleFile=false

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

RUN mkdir -p /app/data /app/keys \
    && chmod 777 /app/data /app/keys

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENV PORT=8080
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV LANG=pt_BR.UTF-8
ENV LC_ALL=pt_BR.UTF-8

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "ClinicaPsi.Web.dll"]
