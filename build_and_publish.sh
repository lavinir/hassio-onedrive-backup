#!/bin/bash
set -e

REGISTRY="ghcr.io/lavinir"
VERSION="${1:-$(grep '^version:' config.yaml | awk '{print $2}' | tr -d '"')}"
PLATFORMS="linux/amd64,linux/arm/v7,linux/arm64"

echo "Building version ${VERSION} for platforms: ${PLATFORMS}"

# Ensure buildx builder with multi-platform support exists
docker buildx inspect multi-platform-builder > /dev/null 2>&1 || \
  docker buildx create --name multi-platform-builder --use

docker buildx use multi-platform-builder

# Build and push each arch-tagged image
for ARCH in amd64 armhf armv7 aarch64; do
  case "$ARCH" in
    amd64)   PLATFORM="linux/amd64" ;;
    armhf)   PLATFORM="linux/arm/v6" ;;
    armv7)   PLATFORM="linux/arm/v7" ;;
    aarch64) PLATFORM="linux/arm64" ;;
  esac

  IMAGE="${REGISTRY}/${ARCH}-hassonedrive:${VERSION}"
  echo "Building ${IMAGE} (${PLATFORM})"

  docker buildx build \
    --platform "${PLATFORM}" \
    --build-arg VERSION="${VERSION}" \
    --tag "${IMAGE}" \
    --push \
    .
done

echo "All images published for version ${VERSION}"
