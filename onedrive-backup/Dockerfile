#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.
ARG SDK_IMAGE_ARCH_TAG
ARG RUNTIME_IMAGE_ARCH_TAG
FROM mcr.microsoft.com/dotnet/aspnet:$RUNTIME_IMAGE_ARCH_TAG AS base
RUN apk add --no-cache tzdata
WORKDIR /app
EXPOSE 8099

FROM mcr.microsoft.com/dotnet/sdk:$SDK_IMAGE_ARCH_TAG AS build
ARG BUILD_ARCH
WORKDIR /src
COPY ["onedrive-backup.csproj", "."]
RUN apk add --no-cache nodejs npm
RUN dotnet restore "./onedrive-backup.csproj" -r $BUILD_ARCH
COPY . .
WORKDIR "/src/."
RUN dotnet build "onedrive-backup.csproj" -c Release -o /app/build -r $BUILD_ARCH

FROM build AS publish
ARG BUILD_ARCH
RUN dotnet publish "onedrive-backup.csproj" -c Release -o /app/publish -r $BUILD_ARCH --no-self-contained

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "onedrive-backup.dll"]

ARG VERSION
LABEL \
  io.hass.version="$VERSION" \
  io.hass.type="addon" \
  io.hass.arch="armhf|aarch64|amd64|armv7"

