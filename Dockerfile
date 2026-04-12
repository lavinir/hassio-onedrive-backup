# Stage 1: Build React frontend (always on native platform)
FROM --platform=$BUILDPLATFORM node:22-alpine AS frontend-build
WORKDIR /app/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# Stage 2: Build .NET backend (always on native platform, cross-compile for target)
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS backend-build
ARG TARGETARCH
WORKDIR /app/backend
COPY backend/*.csproj ./
RUN dotnet restore
COPY backend/ ./
RUN dotnet publish -c Release -o /app/publish \
    --runtime $(case "$TARGETARCH" in \
      amd64) echo linux-x64 ;; \
      arm64) echo linux-arm64 ;; \
      arm)   echo linux-arm ;; \
      *)     echo linux-x64 ;; \
    esac) \
    --self-contained false

# Stage 3: Runtime image (target platform)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
RUN apk add --no-cache tzdata
WORKDIR /app
ENV HOME=/data
EXPOSE 8099

COPY --from=backend-build /app/publish ./
COPY --from=frontend-build /app/frontend/dist ./wwwroot

ARG VERSION
LABEL \
  io.hass.version="${VERSION}" \
  io.hass.type="addon" \
  io.hass.arch="amd64|aarch64|armhf|armv7"

ENTRYPOINT ["dotnet", "HassioOneDriveBackup.dll"]
