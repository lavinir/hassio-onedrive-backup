#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["hassio-onedrive-backup.csproj", "."]
RUN dotnet restore "./hassio-onedrive-backup.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "hassio-onedrive-backup.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "hassio-onedrive-backup.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "hassio-onedrive-backup.dll"]

LABEL \
  io.hass.version="0.1a" \
  io.hass.type="addon" \
  io.hass.arch="armhf|aarch64|i386|amd64"
