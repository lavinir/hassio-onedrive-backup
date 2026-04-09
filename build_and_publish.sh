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
            echo "Usage: $0 [--version <version>] [--sign-images] [--cas-api-key <key>]"
            exit 1
            ;;
    esac
done

if [[ -z "$VERSION" ]]; then
    VERSION="$(grep '^version:' config.yaml | awk '{print $2}' | tr -d '"')"
fi

echo "Building version ${VERSION}"

if [[ "$SIGN_IMAGES" == true ]]; then
    export CAS_API_KEY="$CAS_API_KEY"
    cas login
fi

# Ensure buildx builder with multi-platform support exists and is active
docker buildx inspect multi-platform-builder > /dev/null 2>&1 || \
    docker buildx create --name multi-platform-builder
docker buildx use multi-platform-builder
docker buildx inspect --bootstrap > /dev/null

##################  linux-x64
echo "Building and publishing linux-x64"
docker buildx build \
    --platform linux/amd64 \
    --build-arg VERSION="${VERSION}" \
    --tag "ghcr.io/lavinir/amd64-hassonedrive:${VERSION}" \
    --push \
    .

if [[ "$SIGN_IMAGES" == true ]]; then
    echo "Signing linux-x64 Image"
    cas notarize --bom "docker://ghcr.io/lavinir/amd64-hassonedrive:${VERSION}"
fi

##################  linux-arm
echo "Building and publishing linux-arm (armv7)"
docker buildx build \
    --platform linux/arm/v7 \
    --build-arg VERSION="${VERSION}" \
    --tag "ghcr.io/lavinir/armv7-hassonedrive:${VERSION}" \
    --push \
    .

echo "Building and publishing linux-arm (armhf)"
docker buildx build \
    --platform linux/arm/v7 \
    --build-arg VERSION="${VERSION}" \
    --tag "ghcr.io/lavinir/armhf-hassonedrive:${VERSION}" \
    --push \
    .

if [[ "$SIGN_IMAGES" == true ]]; then
    echo "Signing linux-arm Images"
    cas notarize --bom "docker://ghcr.io/lavinir/armv7-hassonedrive:${VERSION}"
    cas notarize --bom "docker://ghcr.io/lavinir/armhf-hassonedrive:${VERSION}"
fi

##################  linux-arm64
echo "Building and publishing linux-arm64"
docker buildx build \
    --platform linux/arm64 \
    --build-arg VERSION="${VERSION}" \
    --tag "ghcr.io/lavinir/aarch64-hassonedrive:${VERSION}" \
    --push \
    .

if [[ "$SIGN_IMAGES" == true ]]; then
    echo "Signing linux-arm64 Image"
    cas notarize --bom "docker://ghcr.io/lavinir/aarch64-hassonedrive:${VERSION}"
fi

echo "All images published for version ${VERSION}"
