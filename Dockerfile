FROM mcr.microsoft.com/dotnet/sdk:9.0.318-bookworm-slim AS build

WORKDIR /src

COPY src/MediaTools.Api/MediaTools.Api.csproj src/MediaTools.Api/
RUN dotnet restore src/MediaTools.Api/MediaTools.Api.csproj

COPY src/MediaTools.Api/ src/MediaTools.Api/
RUN dotnet publish src/MediaTools.Api/MediaTools.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:9.0.19-bookworm-slim AS runtime

RUN apt-get update \
    && apt-get install --yes --no-install-recommends ffmpeg curl \
    && rm -rf /var/lib/apt/lists/* \
    && test -x /usr/bin/ffmpeg \
    && test -x /usr/bin/ffprobe \
    && test -x /usr/bin/curl

ENV ASPNETCORE_HTTP_PORTS=8080

WORKDIR /app
RUN mkdir --parents /media && chown app:app /media

COPY --from=build /app/publish .

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD ["curl", "--fail", "--silent", "http://127.0.0.1:8080/health"]

USER $APP_UID
ENTRYPOINT ["dotnet", "MediaTools.Api.dll"]
