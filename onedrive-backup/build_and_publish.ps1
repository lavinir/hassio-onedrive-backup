param(
    [Parameter(Mandatory)]
    [string]$version
)

# Write-Host "Building and Publishing linux-x64"
# docker build -t "ghcr.io/lavinir/amd64-hassonedrive:$($version)" --build-arg BUILD_ARCH=linux-x64 --build-arg SDK_IMAGE_ARCH_TAG=6.0.402-alpine3.16-amd64 --build-arg RUNTIME_IMAGE_ARCH_TAG=6.0.10-alpine3.16-amd64 . && docker push "ghcr.io/lavinir/amd64-hassonedrive:$($version)"

Write-Host "Building and Publishing linux-arm"
docker build -t "ghcr.io/lavinir/armv7-hassonedrive:$($version)" --build-arg SDK_IMAGE_ARCH_TAG=6.0.402-alpine3.16-arm32v7 --build-arg RUNTIME_IMAGE_ARCH_TAG=6.0.10-alpine3.16-arm32v7 --build-arg BUILD_ARCH=linux-arm  . && docker push "ghcr.io/lavinir/armv7-hassonedrive:$($version)" `

# docker build -t "ghcr.io/lavinir/armhf-hassonedrive:$($version)" --build-arg SDK_IMAGE_ARCH_TAG=6.0.402-alpine3.16-arm32v7 --build-arg RUNTIME_IMAGE_ARCH_TAG=6.0.10-alpine3.16-arm32v7 --build-arg BUILD_ARCH=linux-arm  . && docker push "ghcr.io/lavinir/armhf-hassonedrive:$($version)"
   
# Write-Host "Building and Publishing linux-arm64"
# docker build -t "ghcr.io/lavinir/aarch64-hassonedrive:$($version)" --build-arg SDK_IMAGE_ARCH_TAG=6.0.402-alpine3.16-arm64v8 --build-arg RUNTIME_IMAGE_ARCH_TAG=6.0.10-alpine3.16-arm64v8 --build-arg  BUILD_ARCH=linux-arm64 . && docker push "ghcr.io/lavinir/aarch64-hassonedrive:$($version)"
