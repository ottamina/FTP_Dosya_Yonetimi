FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Backend/FtpManager.Api/FtpManager.Api.csproj Backend/FtpManager.Api/
RUN dotnet restore Backend/FtpManager.Api/FtpManager.Api.csproj
COPY Backend/FtpManager.Api/ Backend/FtpManager.Api/
RUN dotnet publish Backend/FtpManager.Api/FtpManager.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG TARGETARCH
RUN apt-get update \
    && apt-get install --yes --no-install-recommends openssh-server openssh-client curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /run/sshd /app/logs /app/uploads \
    && case "$TARGETARCH" in amd64|arm64) ;; *) echo "Unsupported architecture: $TARGETARCH"; exit 1 ;; esac \
    && curl --fail --location --silent --show-error \
        "https://bin.equinox.io/c/bNyj1mQVY4c/ngrok-v3-stable-linux-${TARGETARCH}.tgz" \
        --output /tmp/ngrok.tgz \
    && tar -xzf /tmp/ngrok.tgz -C /usr/local/bin \
    && rm /tmp/ngrok.tgz \
    && ngrok version
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    FTP_MANAGER_DOCKER=true \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
ENTRYPOINT ["dotnet", "FtpManager.Api.dll"]
