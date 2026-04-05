# Stage 1: Build React frontend
FROM node:22-alpine AS frontend-build
WORKDIR /app/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# Stage 2: Build .NET backend
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS backend-build
WORKDIR /app/backend
COPY backend/*.csproj ./
RUN dotnet restore
COPY backend/ ./
RUN dotnet publish -c Release -o /app/publish

# Stage 3: Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
RUN apk add --no-cache tzdata
WORKDIR /app
EXPOSE 8099

COPY --from=backend-build /app/publish ./
COPY --from=frontend-build /app/frontend/dist ./wwwroot

ARG VERSION
LABEL \
  io.hass.version="${VERSION}" \
  io.hass.type="addon" \
  io.hass.arch="amd64|aarch64|armhf|armv7"

ENTRYPOINT ["dotnet", "HassioOneDriveBackup.dll"]
