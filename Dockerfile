# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# Build : publie le projet passé en --build-arg PROJECT
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT
WORKDIR /src
COPY . .
RUN dotnet publish "$PROJECT" -c Release -o /app/publish

# ---------------------------------------------------------------------------
# Runtime : assembly exécuté passé en --build-arg ENTRYPOINT_DLL
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ARG ENTRYPOINT_DLL
ENV ENTRYPOINT_DLL=${ENTRYPOINT_DLL}

ENTRYPOINT ["sh", "-c", "exec dotnet \"${ENTRYPOINT_DLL}\""]
