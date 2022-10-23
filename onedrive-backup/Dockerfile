#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.
ARG SDK_IMAGE_ARCH_TAG
ARG RUNTIME_IMAGE_ARCH_TAG
FROM mcr.microsoft.com/dotnet/runtime:$RUNTIME_IMAGE_ARCH_TAG AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:$SDK_IMAGE_ARCH_TAG AS build
ARG BUILD_ARCH
WORKDIR /src
COPY ["hassio-onedrive-backup.csproj", "."]
RUN dotnet restore "./hassio-onedrive-backup.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "hassio-onedrive-backup.csproj" -c Release -o /app/build -r $BUILD_ARCH --self-contained false

FROM build AS publish
RUN dotnet publish "hassio-onedrive-backup.csproj" -c Release -o /app/publish /p:UseAppHost=false --self-contained false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "hassio-onedrive-backup.dll"]

ARG VERSION
LABEL \
  io.hass.version="$VERSION" \
  io.hass.type="addon" \
  io.hass.arch="armhf|aarch64|amd64|armv7"
