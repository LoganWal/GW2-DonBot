#!/bin/sh
API_BASE="${NUXT_PUBLIC_API_BASE:-http://localhost:5001}"
echo "window.__APP_CONFIG__ = { apiBase: '${API_BASE}' };" > /usr/share/nginx/html/config.js
exec nginx -g 'daemon off;'
