#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:$RUNTIME_IMAGE_ARCH_TAG AS base
WORKDIR /app
EXPOSE 8099

FROM mcr.microsoft.com/dotnet/sdk:$SDK_IMAGE_ARCH_TAG AS build
WORKDIR /src
COPY ["onedrive-backup.csproj", "."]
RUN dotnet restore "./onedrive-backup.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "onedrive-backup.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "onedrive-backup.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "onedrive-backup.dll"]

ARG VERSION
LABEL \
  io.hass.version="$VERSION" \
  io.hass.type="addon" \
  io.hass.arch="armhf|aarch64|amd64|armv7"

