#!/usr/bin/env bash
set -e

VERSION=""
SIGN_IMAGES=false
CAS_API_KEY=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --version)
            VERSION="$2"
            shift 2
            ;;
        --sign-images)
            SIGN_IMAGES=true
            shift
            ;;
        --cas-api-key)
            CAS_API_KEY="$2"
            shift 2
            ;;
        *)
            echo "Unknown argument: $1"
            echo "Usage: $0 --version <version> [--sign-images] [--cas-api-key <key>]"
            exit 1
            ;;
    esac
done

if [[ -z "$VERSION" ]]; then
    echo "Error: --version is required"
    echo "Usage: $0 --version <version> [--sign-images] [--cas-api-key <key>]"
    exit 1
fi

if [[ "$SIGN_IMAGES" == true ]]; then
    export CAS_API_KEY="$CAS_API_KEY"
    cas login
fi

##################  linux-x64
echo "Building linux-x64"
docker build -t "ghcr.io/lavinir/amd64-hassonedrive:${VERSION}" \
    --platform linux/amd64 \
    --build-arg VERSION="${VERSION}" .

if [[ "$SIGN_IMAGES" == true ]]; then
    echo "Signing linux-x64 Image"
    cas notarize --bom "docker://ghcr.io/lavinir/amd64-hassonedrive:${VERSION}"
fi

echo "Publishing linux-x64 Image"
docker push "ghcr.io/lavinir/amd64-hassonedrive:${VERSION}"

##################  linux-arm
echo "Building linux-arm"
docker build -t "ghcr.io/lavinir/armv7-hassonedrive:${VERSION}" \
    --platform linux/arm/v7 \
    --build-arg VERSION="${VERSION}" .

docker build -t "ghcr.io/lavinir/armhf-hassonedrive:${VERSION}" \
    --platform linux/arm/v7 \
    --build-arg VERSION="${VERSION}" .

if [[ "$SIGN_IMAGES" == true ]]; then
    echo "Signing linux-arm Images"
    cas notarize --bom "docker://ghcr.io/lavinir/armv7-hassonedrive:${VERSION}"
    cas notarize --bom "docker://ghcr.io/lavinir/armhf-hassonedrive:${VERSION}"
fi

echo "Publishing linux-arm Images"
docker push "ghcr.io/lavinir/armhf-hassonedrive:${VERSION}"
docker push "ghcr.io/lavinir/armv7-hassonedrive:${VERSION}"

##################  linux-arm64
echo "Building linux-arm64"
docker build -t "ghcr.io/lavinir/aarch64-hassonedrive:${VERSION}" \
    --platform linux/arm64 \
    --build-arg VERSION="${VERSION}" .

if [[ "$SIGN_IMAGES" == true ]]; then
    echo "Signing linux-arm64 Image"
    cas notarize --bom "docker://ghcr.io/lavinir/aarch64-hassonedrive:${VERSION}"
fi

echo "Publishing linux-arm64 Image"
docker push "ghcr.io/lavinir/aarch64-hassonedrive:${VERSION}"
