# Build stage
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY src/Domain/PaperAPI.Domain.csproj ./src/Domain/
COPY src/Application/PaperAPI.Application.csproj ./src/Application/
COPY src/Infrastructure/PaperAPI.Infrastructure.csproj ./src/Infrastructure/
COPY src/PdfApi/PaperAPI.PdfApi.csproj ./src/PdfApi/
COPY src/WebCommon/PaperAPI.WebCommon.csproj ./src/WebCommon/

ENV NUGET_HTTP_RETRY_COUNT=10 \
    NUGET_HTTP_RETRY_DELAY_SECONDS=5

RUN dotnet restore ./src/PdfApi/PaperAPI.PdfApi.csproj --disable-parallel

COPY . .
RUN dotnet publish ./src/PdfApi/PaperAPI.PdfApi.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ARG WKHTMLTOPDF_VERSION=0.12.6.1-3
ARG WKHTMLTOPDF_PACKAGE=wkhtmltox_${WKHTMLTOPDF_VERSION}.bookworm_amd64.deb
ARG WKHTMLTOPDF_URL=https://github.com/wkhtmltopdf/packaging/releases/download/${WKHTMLTOPDF_VERSION}/${WKHTMLTOPDF_PACKAGE}

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        fontconfig \
        libfreetype6 \
        libjpeg62-turbo \
        libpng16-16 \
        libx11-6 \
        libxcb1 \
        libxext6 \
        libxrender1 \
        xfonts-75dpi \
        xfonts-base \
        xz-utils \
    && curl -fsSL "${WKHTMLTOPDF_URL}" -o /tmp/wkhtmltox.deb \
    && apt-get install -y --no-install-recommends /tmp/wkhtmltox.deb \
    && ln -sf /usr/local/bin/wkhtmltopdf /usr/bin/wkhtmltopdf \
    && ln -sf /usr/local/bin/wkhtmltoimage /usr/bin/wkhtmltoimage \
    && rm -rf /var/lib/apt/lists/* /tmp/wkhtmltox.deb

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8086
EXPOSE 8086

ENTRYPOINT ["dotnet", "PaperAPI.PdfApi.dll"]
