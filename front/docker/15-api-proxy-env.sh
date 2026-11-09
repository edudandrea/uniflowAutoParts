#!/bin/sh
set -eu

: "${API_PROXY_TARGET:?Configure API_PROXY_TARGET com a origem do backend AutoParts, sem /api.}"

# Keep the full /api path and reject paths or config directives in this value.
if ! printf '%s\n' "$API_PROXY_TARGET" | grep -Eq '^https?://[a-zA-Z0-9.-]+(:[0-9]+)?/?$'; then
    echo 'API_PROXY_TARGET deve ser uma origem HTTP(S), sem caminho, query ou credenciais.' >&2
    exit 1
fi
export API_PROXY_TARGET="${API_PROXY_TARGET%/}"

# Entrypoint scripts run in separate processes. Render here after normalization.
envsubst '${API_PROXY_TARGET}' < /etc/nginx/api-proxy.conf.template > /etc/nginx/conf.d/default.conf
