#!/usr/bin/env bash
# Deploy a store from the GHCR image published by GitHub Actions.
# Usage: deploy-store.sh <vaultshop|ukiyo> [sha|ref]
# Never builds on the VPS.
set -euo pipefail

usage() {
  echo "usage: $(basename "$0") <vaultshop|ukiyo> [sha|ref]" >&2
}

store="${1:-}"
sha_arg="${2:-}"

case "$store" in
  vaultshop) env_file=/opt/vaultshop/.env.compose; domain=vaultshop.evaldez.ar ;;
  ukiyo)     env_file=/opt/ukiyostudio/.env.compose; domain=ukiyostudio.evaldez.ar ;;
  *) usage; exit 2 ;;
esac

repo=/opt/vaultshop
compose_file=$repo/docker-compose.store.yml
log_dir=/opt/server-ops/logs
log_file=$log_dir/deploy.log
lock_file=/tmp/deploy-store.lock

# One deploy at a time.
exec 9>"$lock_file"
flock -n 9 || { echo "another deploy is running" >&2; exit 1; }

git -C "$repo" pull --ff-only

sha=$(git -C "$repo" rev-parse --verify --end-of-options "${sha_arg:-origin/main}^{commit}")
[[ $sha =~ ^[0-9a-f]{40}$ ]] || { echo "invalid sha: $sha" >&2; exit 1; }

image="ghcr.io/emmanuel-valdez/vaultshop-web:sha-$sha"

docker manifest inspect "$image" >/dev/null 2>&1 || {
  echo "image not published yet: $image" >&2
  exit 1
}

compose=(docker compose --env-file "$env_file" -f "$compose_file")

WEB_IMAGE="$image" "${compose[@]}" pull web
WEB_IMAGE="$image" "${compose[@]}" up -d --no-build web

for _ in {1..10}; do
  if curl -fsS --max-time 5 "https://$domain/health/ready" >/dev/null; then
    # Keep the env file in sync so `docker compose config` shows what runs.
    if grep -q '^WEB_IMAGE=' "$env_file"; then
      sed -i "s|^WEB_IMAGE=.*|WEB_IMAGE=$image|" "$env_file" || echo "warning: could not update $env_file" >&2
    else
      printf 'WEB_IMAGE=%s\n' "$image" >> "$env_file" || echo "warning: could not update $env_file" >&2
    fi
    mkdir -p "$log_dir" 2>/dev/null || true
    printf '%s %s %s\n' "$(date -u +%FT%TZ)" "$store" "$image" >> "$log_file" 2>/dev/null || true
    echo "OK $store $image"
    exit 0
  fi
  sleep 3
done

echo "health check failed: https://$domain/health/ready" >&2
exit 1
