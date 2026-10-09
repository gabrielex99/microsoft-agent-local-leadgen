FROM mcr.microsoft.com/devcontainers/dotnet:9.0-bookworm

# Install prerequisite packages and Playwright/Chromium dependencies for Linux
RUN apt-get update && export DEBIAN_FRONTEND=noninteractive \
    && apt-get install -y --no-install-recommends \
        curl \
        wget \
        gnupg \
        software-properties-common \
        libnss3 \
        libnspr4 \
        libatk1.0-0 \
        libatk-bridge2.0-0 \
        libcups2 \
        libdrm2 \
        libxkbcommon0 \
        libxcomposite1 \
        libxdamage1 \
        libxfixes3 \
        libxrandr2 \
        libgbm1 \
        libasound2 \
        libpango-1.0-0 \
        libcairo2 \
        libx11-6 \
        libx11-xcb1 \
        libxcb1 \
        libxext6 \
        fonts-liberation \
        libappindicator3-1 \
        xdg-utils \
        sqlite3 \
    && apt-get clean -y && rm -rf /var/lib/apt/lists/*

# Install PowerShell for Playwright CLI tool execution
RUN wget -q "https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb" \
    && dpkg -i packages-microsoft-prod.deb \
    && rm packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y --no-install-recommends powershell \
    && apt-get clean -y && rm -rf /var/lib/apt/lists/*

# Set environment variables for Playwright in container
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
RUN mkdir -p /ms-playwright && chown -R vscode:vscode /ms-playwright

USER vscode
