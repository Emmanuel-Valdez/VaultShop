#!/usr/bin/env bash
# Alert when the VPS is building images again or the build cache regrows.
# Cron: 20 6 * * * /opt/server-ops/scripts/check-docker-storage.sh >> /opt/server-ops/logs/checks.log 2>&1
set -uo pipefail

# ponytail: fixed threshold; override only if 10 GB proves wrong.
cache_threshold_gb=${CACHE_THRESHOLD_GB:-10}
kuma_file=/opt/server-ops/secrets/kuma-push.url
problems=()

cache_raw=$(docker system df --format '{{.Type}}|{{.Size}}' 2>/dev/null | awk -F'|' '$1=="Build Cache"{print $2}')
cache_gb=$(awk -v s="${cache_raw:-0B}" 'BEGIN{
  if (s ~ /GB$/)      { sub(/GB$/,"",s); printf "%.2f", s+0 }
  else if (s ~ /MB$/) { sub(/MB$/,"",s); printf "%.2f", (s+0)/1024 }
  else if (s ~ /kB$/) { sub(/kB$/,"",s); printf "%.2f", (s+0)/1048576 }
  else                { printf "%.2f", 0 }
}')

if awk -v g="$cache_gb" -v t="$cache_threshold_gb" 'BEGIN{exit !(g>t)}'; then
  problems+=("build cache ${cache_gb}GB > ${cache_threshold_gb}GB")
fi

for container in vaultshop-web ukiyostudio-web; do
  image=$(docker inspect --format '{{.Config.Image}}' "$container" 2>/dev/null || true)
  case "$image" in
    ghcr.io/*) ;;
    *) problems+=("$container uses non-GHCR image: ${image:-missing}") ;;
  esac
done

msg=ok
if ((${#problems[@]})); then
  printf -v msg '%s; ' "${problems[@]}"
  msg=${msg%; }
  echo "ALERT: $msg" >&2
fi

if [[ -r "$kuma_file" ]]; then
  url=$(head -n 1 "$kuma_file")
  status=up
  ((${#problems[@]})) && status=down
  curl -fsS --max-time 10 --get \
    --data-urlencode "status=$status" \
    --data-urlencode "msg=$msg" "$url" >/dev/null 2>&1 || true
fi

echo "$(date -u +%FT%TZ) cache=${cache_gb}GB problems=${#problems[@]}"

((${#problems[@]})) && exit 1
exit 0
