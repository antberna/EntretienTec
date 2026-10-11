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

# Dépendances natives : libgssapi (Npgsql) + tzdata (fuseau America/New_York)
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 tzdata \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ARG ENTRYPOINT_DLL
ENV ENTRYPOINT_DLL=${ENTRYPOINT_DLL}

# `$@` transmet les arguments (ex. --once live) au binaire.
ENTRYPOINT ["sh", "-c", "exec dotnet \"${ENTRYPOINT_DLL}\" \"$@\"", "--"]
