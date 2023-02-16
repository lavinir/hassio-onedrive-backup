param(
    [Parameter(Mandatory)]
    [string]$version,

    [Parameter()]
    [switch]$signImages,

    [Parameter()]
    [string]$casApiKey
)

$env:CAS_API_KEY = $casApiKey
& cas-v1.0.3-windows-amd64.exe login

##################  linux-x64
Write-Host "Building linux-x64"
docker build -t "ghcr.io/lavinir/amd64-hassonedrive:$($version)" --build-arg BUILD_ARCH=linux-x64 --build-arg SDK_IMAGE_ARCH_TAG=6.0-alpine --build-arg RUNTIME_IMAGE_ARCH_TAG=7.0.3-alpine3.17-amd64 . 

if ($signImages.IsPresent) {
    Write-Host "Signing linux-x64 Image"
    & cas-v1.0.3-windows-amd64.exe notarize --bom "docker://ghcr.io/lavinir/amd64-hassonedrive:$($version)"
}

Write-Host "Publishing linux-x64 Image"
docker push "ghcr.io/lavinir/amd64-hassonedrive:$($version)"

##################  linux-arm
Write-Host "Building linux-arm"
docker build -t "ghcr.io/lavinir/armv7-hassonedrive:$($version)" --build-arg SDK_IMAGE_ARCH_TAG=6.0-alpine --build-arg RUNTIME_IMAGE_ARCH_TAG=7.0.3-alpine3.17-arm32v7 --build-arg BUILD_ARCH=linux-arm  .  
docker build -t "ghcr.io/lavinir/armhf-hassonedrive:$($version)" --build-arg SDK_IMAGE_ARCH_TAG=6.0-alpine --build-arg RUNTIME_IMAGE_ARCH_TAG=7.0.3-alpine3.17-arm32v7 --build-arg BUILD_ARCH=linux-arm  . 

if ($signImages.IsPresent) {
    Write-Host "Signing linux-arm Images"
    & cas-v1.0.3-windows-amd64.exe notarize --bom "docker://ghcr.io/lavinir/armv7-hassonedrive:$($version)"
    & cas-v1.0.3-windows-amd64.exe notarize --bom "docker://ghcr.io/lavinir/armhf-hassonedrive:$($version)"
}
Write-Host "Publishing linux-arm Images"
docker push "ghcr.io/lavinir/armhf-hassonedrive:$($version)"
docker push "ghcr.io/lavinir/armv7-hassonedrive:$($version)" 

##################  linux-arm64
Write-Host "Building linux-arm64"
docker build -t "ghcr.io/lavinir/aarch64-hassonedrive:$($version)" --build-arg SDK_IMAGE_ARCH_TAG=6.0-alpine --build-arg RUNTIME_IMAGE_ARCH_TAG=7.0.3-alpine3.17-arm64v8 --build-arg  BUILD_ARCH=linux-arm64 . 

if ($signImages.IsPresent) {
    Write-Host "Signing linux-arm64 Image"
    & cas-v1.0.3-windows-amd64.exe notarize --bom "docker://ghcr.io/lavinir/aarch64-hassonedrive:$($version)"
}
Write-Host "Publishing linux-arm64 Image"
docker push "ghcr.io/lavinir/aarch64-hassonedrive:$($version)"