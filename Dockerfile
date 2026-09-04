# Reproduction image for the thesis results (chapter 7, "Evalvacija").
#
# It carries the three things the harness needs and nothing else: the .NET 10
# SDK, a `git` on PATH (every real-code corpus is shallow-cloned at its pinned
# release tag rather than vendored), and the repository itself, already built.
FROM mcr.microsoft.com/dotnet/sdk:10.0

# The SDK images normally ship git; install it only if this one does not
RUN if ! command -v git >/dev/null 2>&1; then \
        apt-get update \
        && apt-get install -y --no-install-recommends git ca-certificates \
        && rm -rf /var/lib/apt/lists/*; \
    fi

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    NUGET_XMLDOC_MODE=skip \
    LANG=C.UTF-8 \
    LC_ALL=C.UTF-8

WORKDIR /src

# Restore first, from the project files alone, so editing source does not re-download the package graph on every rebuild.
COPY docker/nuget.config ./NuGet.config
COPY DesignPatternDetection.slnx ./
COPY DesignPatternDetection/DesignPatternDetection.csproj DesignPatternDetection/
COPY DesignPatternDetection.Evaluation/DesignPatternDetection.Evaluation.csproj DesignPatternDetection.Evaluation/
COPY DesignPatternDetection.Tests/DesignPatternDetection.Tests.csproj DesignPatternDetection.Tests/
RUN dotnet restore DesignPatternDetection.slnx

COPY . .

# Build once, at image build time, so a run is only detection
RUN dotnet build DesignPatternDetection.slnx -c Release --no-restore

COPY docker/entrypoint.sh /usr/local/bin/entrypoint.sh
RUN sed -i 's/\r$//' /usr/local/bin/entrypoint.sh && chmod +x /usr/local/bin/entrypoint.sh

# Reports land here
RUN mkdir -p /out
ENV OUT_DIR=/out

# The entrypoint defaults to the structural run on its own, which keeps `docker run <image>` meaningful while 
# letting every compose service bake its subcommand into ENTRYPOINT - so arguments passed to `docker compose run` are appended to that subcommand.
ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
