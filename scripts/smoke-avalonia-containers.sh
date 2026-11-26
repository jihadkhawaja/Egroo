#!/usr/bin/env bash
set -euo pipefail

api_image=$1
browser_image=$2
api_url=http://127.0.0.1:18081
browser_url=http://127.0.0.1:18080
api_container=egroo-api-smoke
browser_container=egroo-browser-smoke
browser_config=$(mktemp)
browser_dom=$(mktemp)

cleanup() {
    local status=$?
    if (( status != 0 )); then
        docker logs --tail 50 "$api_container" || true
        docker logs --tail 50 "$browser_container" || true
    fi
    docker rm --force "$api_container" "$browser_container" >/dev/null 2>&1 || true
    rm -f "$browser_config" "$browser_dom"
}
trap cleanup EXIT

jq --arg apiUrl "$api_url/" -n '{apiUrl: $apiUrl}' > "$browser_config"
chmod a+r "$browser_config"
docker run --detach --name "$api_container" --publish 127.0.0.1:18081:8080 \
    --env "Secrets__Jwt=$(openssl rand -hex 32)" \
    --env "Encryption__Key=$(openssl rand -hex 16)" \
    --env "Encryption__IV=$(openssl rand -hex 8)" \
    --env "Api__AllowedOrigins__0=$browser_url" \
    "$api_image"
docker run --detach --name "$browser_container" --publish 127.0.0.1:18080:8080 \
    --mount "type=bind,source=$browser_config,target=/usr/share/nginx/html/egroo-config.json,readonly" \
    "$browser_image"

curl --fail --silent --show-error --retry 15 --retry-all-errors --retry-delay 1 \
    --max-time 5 "$api_url/runtime/index.html" >/dev/null
curl --fail --silent --show-error --retry 5 --retry-all-errors --retry-delay 1 "$browser_url/" | grep '<title>Egroo</title>' >/dev/null
curl --fail --silent --show-error "$browser_url/egroo-config.json" \
    | jq --exit-status --arg apiUrl "$api_url/" '.apiUrl == $apiUrl' >/dev/null

timeout 90s google-chrome --headless --no-sandbox --disable-dev-shm-usage \
    --disable-background-networking --use-angle=swiftshader --enable-unsafe-swiftshader \
    --virtual-time-budget=30000 --dump-dom "$browser_url/" > "$browser_dom"
if ! grep --fixed-strings 'data-egroo-ready="true"' "$browser_dom" >/dev/null; then
    cat "$browser_dom"
    exit 1
fi

signup="$api_url/api/v1/Auth/signup"
headers=$(curl --fail --silent --show-error --dump-header - --output /dev/null \
    --request OPTIONS \
    --header "Origin: $browser_url" \
    --header 'Access-Control-Request-Method: POST' \
    --header 'Access-Control-Request-Headers: content-type' \
    "$signup")
printf '%s\n' "$headers" | tr -d '\r' \
    | grep --ignore-case --fixed-strings --line-regexp "access-control-allow-origin: $browser_url" >/dev/null

curl --fail --silent --show-error --header "Origin: $browser_url" \
    --json '{"username":"containersmoke","password":"SmokeTest1234!"}' "$signup" \
    | jq --exit-status '.success and (.token | type == "string" and length > 0)' >/dev/null
