# VaultShop Operations Runbook

This runbook documents the current lightweight operations process for the VaultShop VPS deployment. It is intentionally simple: the goal is to keep the public demo recoverable and explainable without over-engineering.

## Deployment Shape

- Public URL: `https://vaultshop.evaldez.ar`
- VPS OS: Ubuntu 24.04
- App path on VPS: `/opt/vaultshop`
- Public ingress: Nginx on `80/443`
- App container bind: `127.0.0.1:8080`
- PostgreSQL: Docker Compose private service
- MinIO: Docker Compose private service, served publicly through Nginx for product images
- SSH/admin access: Tailscale/private access

Do not expose PostgreSQL, MinIO API, or the MinIO console directly to the public internet.

## Quick Health Check

Run on the VPS:

```
cd /opt/vaultshop
docker compose --env-file .env.compose ps
curl -I https://vaultshop.evaldez.ar
df -h
```

Expected result:

- App, PostgreSQL, and MinIO containers are running.
- `curl` returns a successful HTTP response, redirect, or otherwise valid HTTPS response.
- Disk usage is not close to full.

## Restart Recovery Check

Containers should use `restart: unless-stopped`.

Verify from the VPS repo root:

```
cd /opt/vaultshop
docker compose --env-file .env.compose ps web postgres minio
docker compose --env-file .env.compose ps -q web postgres minio | xargs -r docker inspect --format '{{.Name}} {{.HostConfig.RestartPolicy.Name}}'
```

Expected:

- Web, PostgreSQL, and MinIO are running.
- Each inspected container reports `unless-stopped`.
- Container names follow the active `COMPOSE_PROJECT_NAME`; keep that name stable after first deployment so volumes do not silently change.

After a VPS reboot:

```
cd /opt/vaultshop
docker compose --env-file .env.compose ps
curl -I https://vaultshop.evaldez.ar
```

Then verify manually in the browser that product pages and uploaded images still load.

## PostgreSQL Backup

Create a compressed custom-format PostgreSQL dump on the VPS:

```
mkdir -p ~/vaultshop-backups/postgres
cd /opt/vaultshop
docker compose --env-file .env.compose exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > ~/vaultshop-backups/postgres/vaultshop_$(date +%F_%H%M).dump
ls -lh ~/vaultshop-backups/postgres
```

Copy it to the local PC from PowerShell:

```powershell
mkdir ~/Backups/VaultShop/Postgres
scp ubuntu@<vps-tailscale-ip>:/home/ubuntu/vaultshop-backups/postgres/*.dump ~/Backups/VaultShop/Postgres/
dir ~/Backups/VaultShop/Postgres
```

Success criteria:

- The `.dump` file exists locally.
- File size is not `0`.
- Restore has been tested at least once after changing the backup process.

## PostgreSQL Restore Test

Start a clean local PostgreSQL container:

```powershell
docker rm -f vaultshop-restore-postgres
docker run --name vaultshop-restore-postgres -e POSTGRES_USER=vaultshop_app -e POSTGRES_PASSWORD=restoretest -e POSTGRES_DB=vaultshop_restore -p 55432:5432 -d postgres:16
```

Copy the dump into the container:

```powershell
docker cp ~/Backups/VaultShop/Postgres/vaultshop_YYYY-MM-DD_HHMM.dump vaultshop-restore-postgres:/backup.dump
```

Restore:

```powershell
docker exec vaultshop-restore-postgres pg_restore -U vaultshop_app -d vaultshop_restore /backup.dump
```

Verify tables:

```powershell
docker exec vaultshop-restore-postgres psql -U vaultshop_app -d vaultshop_restore -c "\dt"
```

Verify counts, preserving PostgreSQL's quoted table names through the container shell:

```powershell
docker exec vaultshop-restore-postgres sh -c 'psql -U vaultshop_app -d vaultshop_restore -c "SELECT COUNT(*) FROM \"Products\";"'
docker exec vaultshop-restore-postgres sh -c 'psql -U vaultshop_app -d vaultshop_restore -c "SELECT COUNT(*) FROM \"ProductImages\";"'
docker exec vaultshop-restore-postgres sh -c 'psql -U vaultshop_app -d vaultshop_restore -c "SELECT COUNT(*) FROM \"AspNetUsers\";"'
```

Important: PostgreSQL restores can depend on roles/owners. If the dump contains objects owned by `vaultshop_app`, create the restore database with `POSTGRES_USER=vaultshop_app` or restore with appropriate ownership options.

## MinIO Backup

Keep `COMPOSE_PROJECT_NAME=vaultshop` stable after first deployment. With that project name, the MinIO Docker volume is `vaultshop_minio-data`. If the project name is changed, use the matching `<project>_minio-data` volume instead.

Verify volumes:

```
docker volume ls | grep minio-data
```

Create a read-only archive of the MinIO data volume:

```
mkdir -p ~/vaultshop-backups/minio
docker run --rm -v vaultshop_minio-data:/data:ro -v ~/vaultshop-backups/minio:/backup alpine tar czf /backup/minio_$(date +%F_%H%M).tar.gz -C /data .
ls -lh ~/vaultshop-backups/minio
tar tzf ~/vaultshop-backups/minio/*.tar.gz | head
```

Copy it to the local PC from PowerShell:

```powershell
mkdir ~/Backups/VaultShop/MinIO
scp ubuntu@<vps-tailscale-ip>:/home/ubuntu/vaultshop-backups/minio/*.tar.gz ~/Backups/VaultShop/MinIO/
dir ~/Backups/VaultShop/MinIO
```

Success criteria:

- The `.tar.gz` file exists locally.
- File size is not `0`.
- Archive contents can be listed with `tar tzf`.

## MinIO Restore Test

Create a clean local Docker volume:

```powershell
docker volume create vaultshop-restore-minio-data
```

Restore the archive into the clean volume:

```powershell
docker run --rm -v vaultshop-restore-minio-data:/data -v ~/Backups/VaultShop/MinIO:/backup alpine sh -c "tar xzf /backup/minio_YYYY-MM-DD_HHMM.tar.gz -C /data"
```

Start a temporary local MinIO instance:

```powershell
docker run --name vaultshop-restore-minio -p 9100:9000 -p 9101:9001 -v vaultshop-restore-minio-data:/data -e MINIO_ROOT_USER=restoreadmin -e MINIO_ROOT_PASSWORD=restorepassword minio/minio server /data --console-address ":9001"
```

Open:

```text
http://localhost:9101
```

Login:

```text
User: restoreadmin
Password: restorepassword
```

Success criteria:

- MinIO starts.
- The restored bucket exists.
- Product image objects are visible.

Clean up when finished:

```powershell
docker rm -f vaultshop-restore-minio
docker volume rm vaultshop-restore-minio-data
```

## Automated Backup Freshness And Disk Checks

Two scripts live in `~/vaultshop-backups/` on the VPS and should run daily via cron.

### check-backup-freshness.sh

Verifies the latest PostgreSQL dump and MinIO archive exist and are newer than a threshold (default: 48 hours).

```
#!/usr/bin/env bash
set -euo pipefail

BACKUP_DIR="$HOME/vaultshop-backups"
MAX_AGE_HOURS="${1:-48}"
THRESHOLD_SECONDS=$((MAX_AGE_HOURS * 3600))
EXIT_CODE=0

echo "=== Backup Freshness Check ==="
echo "Max age: ${MAX_AGE_HOURS}h"
echo ""

# --- PostgreSQL ---
PG_DIR="$BACKUP_DIR/postgres"
if [ ! -d "$PG_DIR" ]; then
    echo "FAIL: PostgreSQL backup dir '$PG_DIR' not found"
    EXIT_CODE=1
else
    LATEST_PG=$(ls -t "$PG_DIR"/*.dump 2>/dev/null | head -1)
    if [ -z "$LATEST_PG" ]; then
        echo "FAIL: No PostgreSQL dump found in $PG_DIR"
        EXIT_CODE=1
    else
        AGE=$(($(date +%s) - $(stat -c %Y "$LATEST_PG")))
        AGE_HOURS=$((AGE / 3600))
        if [ "$AGE" -le "$THRESHOLD_SECONDS" ]; then
            echo "OK:   PostgreSQL dump is ${AGE_HOURS}h old ($(basename "$LATEST_PG"))"
        else
            echo "FAIL: PostgreSQL dump is ${AGE_HOURS}h old (max ${MAX_AGE_HOURS}h) ($(basename "$LATEST_PG"))"
            EXIT_CODE=1
        fi
    fi
fi

# --- MinIO ---
MINIO_DIR="$BACKUP_DIR/minio"
if [ ! -d "$MINIO_DIR" ]; then
    echo "FAIL: MinIO backup dir '$MINIO_DIR' not found"
    EXIT_CODE=1
else
    LATEST_MINIO=$(ls -t "$MINIO_DIR"/*.tar.gz 2>/dev/null | head -1)
    if [ -z "$LATEST_MINIO" ]; then
        echo "FAIL: No MinIO archive found in $MINIO_DIR"
        EXIT_CODE=1
    else
        AGE=$(($(date +%s) - $(stat -c %Y "$LATEST_MINIO")))
        AGE_HOURS=$((AGE / 3600))
        if [ "$AGE" -le "$THRESHOLD_SECONDS" ]; then
            echo "OK:   MinIO archive is ${AGE_HOURS}h old ($(basename "$LATEST_MINIO"))"
        else
            echo "FAIL: MinIO archive is ${AGE_HOURS}h old (max ${MAX_AGE_HOURS}h) ($(basename "$LATEST_MINIO"))"
            EXIT_CODE=1
        fi
    fi
fi

echo ""
echo "Exit code: $EXIT_CODE"
exit $EXIT_CODE
```

Expected output (healthy):
```
=== Backup Freshness Check ===
Max age: 168h

OK:   PostgreSQL dump is 0h old (vaultshop_2026-07-06_0432.dump)
OK:   MinIO archive is 0h old (minio_2026-07-06_0432.tar.gz)

Exit code: 0
```
### check-disk.sh

Reports disk usage and warns above configurable thresholds.

```
#!/usr/bin/env bash
set -euo pipefail

WARN_PCT="${1:-80}"
CRIT_PCT="${2:-90}"
EXIT_CODE=0

echo "=== Disk Usage Check ==="
echo "Warning:  ${WARN_PCT}%"
echo "Critical: ${CRIT_PCT}%"
echo ""

while IFS='' read -r line; do
    PCT=$(echo "$line" | awk '{print $2}' | tr -d '%')
    SOURCE=$(echo "$line" | awk '{print $1}')
    MOUNT=$(echo "$line" | awk '{print $3}')
    if [ "$PCT" -ge "$CRIT_PCT" ] 2>/dev/null; then
        echo "CRITICAL: $SOURCE ($MOUNT) at ${PCT}% (threshold: ${CRIT_PCT}%)"
        EXIT_CODE=2
    elif [ "$PCT" -ge "$WARN_PCT" ] 2>/dev/null; then
        echo "WARNING:  $SOURCE ($MOUNT) at ${PCT}% (threshold: ${WARN_PCT}%)"
        [ "$EXIT_CODE" -lt 1 ] && EXIT_CODE=1
    else
        echo "OK:       $SOURCE ($MOUNT) at ${PCT}%"
    fi
done < <(df -h --output=source,pcent,target | tail -n +2)

echo ""
echo "Exit code: $EXIT_CODE"
exit $EXIT_CODE
```

Expected output (healthy):
```
=== Disk Usage Check ===
Warning:  80%
Critical: 90%

OK:       /dev/sda2 (/) at 45%
OK:       /dev/sda1 (/boot) at 30%

Exit code: 0
```

### Cron Setup

Run the backup weekly (Sunday 6:00) and both checks daily:
```bash
crontab -e
```

Add:

```cron
# Weekly backup (Sunday 6:00)
0 6 * * 0 $HOME/vaultshop-backups/do-backup.sh >> $HOME/vaultshop-backups/checks.log 2>&1
# Daily freshness check (uses 168h threshold for weekly backups)
5 6 * * * $HOME/vaultshop-backups/check-backup-freshness.sh 168 >> $HOME/vaultshop-backups/checks.log 2>&1
# Daily disk check
10 6 * * * $HOME/vaultshop-backups/check-disk.sh >> $HOME/vaultshop-backups/checks.log 2>&1
```
```bash
tail -20 ~/vaultshop-backups/checks.log
```

Success criteria:

- Each script exits `0` when checks pass.
- Non-zero exit codes appear in the log and indicate what failed.
- The cron job runs daily without manual intervention.
- Check the log after the first cron execution to confirm output is correct.

### Container Restart Detection

Quick check for unexpected container restarts:

```
cd /opt/vaultshop
docker compose --env-file .env.compose ps -a
docker compose --env-file .env.compose logs --tail=20 --timestamps web postgres minio | grep -i "restart\|error\|warn\|killed\|oom"
```

Look for containers with unexpected exit codes or recent restart timestamps. This is a manual check for now; automated alerting can be added if restarts become frequent.

### Webhook Error Visibility

Stripe webhook errors appear in the app logs:

```
cd /opt/vaultshop
docker compose --env-file .env.compose logs --tail=100 web | grep -i "webhook\|stripe.*fail\|signature\|400\|401\|403"
```

Expected: no matching lines on a healthy system. If errors appear, check the Stripe Dashboard webhook logs for recent attempts.

## Monitoring

An external uptime/TLS monitor should check:

```text
https://vaultshop.evaldez.ar
```

Minimum expectations:

- The monitor reports `Up`.
- Email alerts are enabled.
- TLS/HTTPS expiry checks are enabled if supported by the provider.

This detects basic availability problems. It does not replace backup/restore or deeper application checks.

## Logs

App logs:

```
cd /opt/vaultshop
docker compose --env-file .env.compose logs --tail=100 web
```

PostgreSQL logs:

```
docker compose --env-file .env.compose logs --tail=100 postgres
```

MinIO logs:

```
docker compose --env-file .env.compose logs --tail=100 minio
```

Nginx status and recent logs:

```
sudo systemctl status nginx --no-pager
sudo journalctl -u nginx --since "1 hour ago" --no-pager
```

## Critical Warning

Do not run this on the VPS unless intentionally deleting persisted data:

```
docker compose down -v
```

The `-v` flag deletes Docker volumes, including PostgreSQL and MinIO data.

Use this to stop containers without deleting data:

```
docker compose --env-file .env.compose down
```

## Future Private Deployment Notes

VaultShop is the public portfolio/demo deployment. A future private/client deployment should use the same general pattern but with stronger separation:

- Separate domain/subdomain.
- Separate `.env` file.
- Separate Compose project name/volumes, kept stable after first deployment.
- Separate PostgreSQL database and user.
- Separate MinIO bucket and app-specific credentials.
- Separate Stripe keys and webhook secret.
- Separate backups and restore verification.
- No demo seed data in the real client deployment.
- Private branding assets configured through `Branding__...` and stored outside git.
- Separate `Theme__...` hex color values for the deployment.




