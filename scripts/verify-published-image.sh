#!/usr/bin/env bash
set -euo pipefail

tagged_image=$1
expected_revision=$2
digest=$(timeout 30s docker buildx imagetools inspect "$tagged_image" --format '{{json .Manifest}}' \
    | jq --exit-status --raw-output '.digest | select(test("^sha256:[0-9a-f]{64}$"))')
image="${tagged_image%:*}@$digest"

timeout 180s docker pull --platform linux/amd64 "$image" >&2
revision=$(docker image inspect --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$image")
if [[ "$revision" != "$expected_revision" ]]; then
    printf 'Image %s has revision %s, expected %s.\n' "$image" "$revision" "$expected_revision" >&2
    exit 1
fi

printf '%s\n' "$image"
