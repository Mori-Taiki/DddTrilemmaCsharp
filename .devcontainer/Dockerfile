FROM mcr.microsoft.com/dotnet/sdk:11.0.100

RUN apt-get update \
    && apt-get install -y --no-install-recommends git curl python3 \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --create-home --shell /bin/bash vscode

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

USER vscode
