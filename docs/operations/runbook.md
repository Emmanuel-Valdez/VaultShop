# VaultShop Operations Runbook

This runbook documents the current lightweight operations process for the VaultShop VPS deployment. It is intentionally simple: the goal is to keep the public demo recoverable and explainable without over-engineering.

## Deployment Shape

Two independent web deployments share one private platform (PostgreSQL + MinIO) on the same VPS:

| Store | Domain | App path | Web bind | Database | MinIO bucket | Backup dir |
| --- | --- | --- | --- | --- | --- | --- |
| VaultShop (demo) | `https://vaultshop.evaldez.ar` | `/opt/vaultshop` | `127.0.0.1:8080` | `vaultshop` | `product-images` | `~/vaultshop-backups/` |
| UkiyoStudio | `https://ukiyostudio.evaldez.ar` | `/opt/ukiyostudio` | `127.0.0.1:8083` | `ukiyostudio` | `ukiyostudio-images` | `~/ukiyostudio-backups/` |

- VPS OS: Ubuntu 24.04
- Public ingress: Nginx on `80/443`
- PostgreSQL: Docker Compose private service (no host port)
- MinIO: Docker Compose private service, API bound to loopback `127.0.0.1:9000`, served publicly through Nginx for product images
- SSH/admin access: Tailscale/private access
- Real database/user/bucket names live in the private `/opt/vaultshop/.platform.env`; the backup scripts read them automatically, manual commands below use placeholders.

Do not expose PostgreSQL, MinIO API, or the MinIO console directly to the public internet.

## Quick Health Check

Run on the VPS:

```
cd /opt/vaultshop
docker compose --env-file /opt/vaultshop/.platform.env -f docker-compose.platform.yml ps
docker compose --env-file /opt/vaultshop/.env.compose -f docker-compose.store.yml ps
docker compose --env-file /opt/ukiyostudio/.env.compose -f docker-compose.store.yml ps
curl -I https://vaultshop.evaldez.ar
curl -I https://ukiyostudio.evaldez.ar
df -h
```

Expected result:

- Platform (postgres, minio) and both store web containers are running.
- Both `curl` calls return successful HTTPS responses.
- Disk usage is not close to full.

## Restart Recovery Check

Containers should use `restart: unless-stopped`.

Verify from the VPS repo root:

```
cd /opt/vaultshop
docker compose --env-file /opt/vaultshop/.platform.env -f docker-compose.platform.yml ps
docker ps --format '{{.Names}} {{.HostConfig.RestartPolicy.Name}}' | grep -E '(postgres|minio|-web)'
```

Expected:

- Platform and both store web containers are running.
- Each inspected container reports `unless-stopped`.
- Container names follow the stable Compose project names (`vaultshop-platform`, `vaultshop`, `ukiyostudio`); keep them stable after first deployment so volumes do not silently change.

After a VPS reboot:

```
cd /opt/vaultshop
docker compose --env-file /opt/vaultshop/.platform.env -f docker-compose.platform.yml ps
docker compose --env-file /opt/vaultshop/.env.compose -f docker-compose.store.yml ps
docker compose --env-file /opt/ukiyostudio/.env.compose -f docker-compose.store.yml ps
curl -I https://vaultshop.evaldez.ar
curl -I https://ukiyostudio.evaldez.ar
```

Then verify manually in the browser that both stores' product pages and uploaded images still load.

## PostgreSQL Backup

The platform compose runs PostgreSQL without a host port, so dump from inside the `postgres` container. Each store dumps only its own database into a separate file under its own backup dir.

The `postgres` container is created by the postgres image with `POSTGRES_USER` as the cluster superuser, so that single credential can dump any database on the server. `<db_name>` below is the store's real database name from `/opt/vaultshop/.platform.env` (`POSTGRES_DB` for VaultShop, `UKIYO_POSTGRES_DATABASE` for UkiyoStudio).

Manual, per store:

```
mkdir -p ~/vaultshop-backups/postgres ~/ukiyostudio-backups/postgres
cd /opt/vaultshop
docker compose --env-file /opt/vaultshop/.platform.env -f docker-compose.platform.yml \
  exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "<db_name>" -Fc' \
  > ~/<store>-backups/postgres/<store>_$(date +%F_%H%M).dump
ls -lh ~/<store>-backups/postgres
```

Prefer `do-backup.sh <store>` (see Automated Backup) — it reads the real names from `.platform.env` and produces both files for the store.

Copy VaultShop dumps to the local PC from PowerShell:

```powershell
mkdir ~/Backups/VaultShop/Postgres
scp ubuntu@<vps-tailscale-ip>:/home/ubuntu/vaultshop-backups/postgres/*.dump $HOME\Backups\VaultShop\Postgres\
dir ~/Backups/VaultShop/Postgres
```

Copy UkiyoStudio dumps the same way, own dir and own files:

```powershell
mkdir ~/Backups/UkiyoStudio/Postgres
scp ubuntu@<vps-tailscale-ip>:/home/ubuntu/ukiyostudio-backups/postgres/*.dump $HOME\Backups\UkiyoStudio\Postgres\
dir ~/Backups/UkiyoStudio/Postgres
```

Success criteria:

- One `.dump` file per store exists locally, from the matching store's backup dir.
- File sizes are not `0`.
- Each store's restore has been tested at least once after changing the backup process (see below).

## PostgreSQL Restore Test

Restore each store's dump into its own clean container so the two stores stay independent. The dump records each object's original owner role, so restore with `--no-owner` in the throwaway container to avoid role-mismatch failures.

Start a clean local PostgreSQL container per store:

```powershell
docker rm -f vaultshop-restore-postgres
docker run --name vaultshop-restore-postgres -e POSTGRES_USER=vaultshop_app -e POSTGRES_PASSWORD=restoretest -e POSTGRES_DB=vaultshop_restore -p 55432:5432 -d postgres:16

docker rm -f ukiyostudio-restore-postgres
docker run --name ukiyostudio-restore-postgres -e POSTGRES_USER=ukiyostudio_restore -e POSTGRES_PASSWORD=restoretest -e POSTGRES_DB=ukiyostudio_restore -p 55433:5432 -d postgres:16
```

Copy each dump into its own container:

```powershell
docker cp $HOME\Backups\VaultShop\Postgres\vaultshop_YYYY-MM-DD_HHMM.dump vaultshop-restore-postgres:/backup.dump
docker cp $HOME\Backups\UkiyoStudio\Postgres\ukiyostudio_YYYY-MM-DD_HHMM.dump ukiyostudio-restore-postgres:/backup.dump
```

Restore:

```powershell
docker exec vaultshop-restore-postgres pg_restore --no-owner -U vaultshop_app -d vaultshop_restore /backup.dump
docker exec ukiyostudio-restore-postgres pg_restore --no-owner -U ukiyostudio_restore -d ukiyostudio_restore /backup.dump
```

Verify each restored database holds only its own store's expected tables and data:

```powershell
docker exec vaultshop-restore-postgres psql -U vaultshop_app -d vaultshop_restore -c "\dt"
docker exec vaultshop-restore-postgres sh -c 'psql -U vaultshop_app -d vaultshop_restore -c "SELECT COUNT(*) FROM \"Products\";"'
docker exec ukiyostudio-restore-postgres psql -U ukiyostudio_restore -d ukiyostudio_restore -c "\dt"
```

Independence check: `\dt` in the VaultShop container must show only VaultShop data and nothing belonging to UkiyoStudio, and vice versa.

Important: PostgreSQL restores depend on roles/owners. The `--no-owner` flag above is for throwaway containers. A production restore onto the real server should recreate the store's real owner role first (the dumps carry ownership references, not role definitions) or else use `--no-owner`.

## MinIO Backup

MinIO runs on the shared network with the API bound to loopback `127.0.0.1:9000` and holds both stores' buckets in one data volume, so back each bucket up separately with `mc mirror` instead of tarring the whole volume. Each store backs up with its own scoped MinIO user (`VAULTSHOP_MINIO_USER`/`UKIYO_MINIO_USER` and their passwords from `.platform.env`), never root — a scoped user can only read its own bucket, so a store's backup can never pull the other store's objects.

Manual, from the VPS (the backup dir is mounted read-write so `mc` can write the mirror):

```
mkdir -p ~/vaultshop-backups/minio ~/ukiyostudio-backups/minio
docker run --rm --network host -v ~/<store>-backups/minio:/backup \
  -e MINIO_USER="<store minio user>" -e MINIO_PASSWORD="<store minio password>" \
  --user "$(id -u):$(id -g)" \
  --entrypoint sh \
  -e MC_CONFIG_DIR=/tmp/mc \
  -e BUCKET_NAME="<store bucket>" minio/mc:latest -ec '
    mc alias set local http://127.0.0.1:9000 "$MINIO_USER" "$MINIO_PASSWORD" >/dev/null
    mc mirror --overwrite "local/$BUCKET_NAME" "/backup/$BUCKET_NAME"
  '
tar czf ~/<store>-backups/minio/<bucket>_$(date +%F_%H%M).tar.gz -C ~/<store>-backups/minio <bucket>
rm -rf ~/<store>-backups/minio/<bucket>
ls -lh ~/<store>-backups/minio
```

The archive keeps the bucket name as its top-level directory, so extracting it into a MinIO data volume recreates that bucket (see MinIO Restore Test below).

Prefer `do-backup.sh <store>` — it does the mirror and the archive in one step, reading names and credentials from `.platform.env`.

Copy each store's archives to the local PC from PowerShell:

```powershell
mkdir ~/Backups/VaultShop/MinIO
scp ubuntu@<vps-tailscale-ip>:/home/ubuntu/vaultshop-backups/minio/*.tar.gz $HOME\Backups\VaultShop\MinIO\
mkdir ~/Backups/UkiyoStudio/MinIO
scp ubuntu@<vps-tailscale-ip>:/home/ubuntu/ukiyostudio-backups/minio/*.tar.gz $HOME\Backups\UkiyoStudio\MinIO\
dir ~/Backups/VaultShop/MinIO; dir ~/Backups/UkiyoStudio/MinIO
```

Success criteria:

- One `.tar.gz` per store exists locally, from the matching store's backup dir.
- File sizes are not `0`.
- `tar tzf` of each archive lists only that store's bucket directory.

## MinIO Restore Test

Restore by uploading the archive contents through the S3 API with `mc mirror`, so MinIO writes its own per-object metadata (modern MinIO uses the XL format and will not index files extracted directly into the data volume).

Create clean local Docker volumes, extract each archive to a temp dir, and start a temporary local MinIO instance per store:

```powershell
docker volume create vaultshop-restore-minio-data
docker run -d --name vaultshop-restore-minio -p 9100:9000 -p 9101:9001 -v vaultshop-restore-minio-data:/data -e MINIO_ROOT_USER=restoreadmin -e MINIO_ROOT_PASSWORD=restorepassword minio/minio server /data --console-address ":9001"
mkdir C:\vaultshop-restore-tmp
tar xzf $HOME\Backups\VaultShop\MinIO\product-images_YYYY-MM-DD_HHMM.tar.gz -C C:\vaultshop-restore-tmp

docker volume create ukiyostudio-restore-minio-data
docker run -d --name ukiyostudio-restore-minio -p 9102:9000 -p 9103:9001 -v ukiyostudio-restore-minio-data:/data -e MINIO_ROOT_USER=restoreadmin -e MINIO_ROOT_PASSWORD=restorepassword minio/minio server /data --console-address ":9001"
mkdir C:\ukiyostudio-restore-tmp
tar xzf $HOME\Backups\UkiyoStudio\MinIO\ukiyostudio-images_YYYY-MM-DD_HHMM.tar.gz -C C:\ukiyostudio-restore-tmp
```

Upload each store's files into its bucket (this creates the bucket and the objects via the API):

```powershell
docker run --rm --network host -v C:\vaultshop-restore-tmp:/src -e MC_CONFIG_DIR=/tmp/mc --entrypoint sh minio/mc:latest -ec "mc alias set local http://127.0.0.1:9100 restoreadmin restorepassword >/dev/null && mc mb local/product-images && mc mirror --overwrite /src/product-images local/product-images"
docker run --rm --network host -v C:\ukiyostudio-restore-tmp:/src -e MC_CONFIG_DIR=/tmp/mc --entrypoint sh minio/mc:latest -ec "mc alias set local http://127.0.0.1:9102 restoreadmin restorepassword >/dev/null && mc mb local/ukiyostudio-images && mc mirror --overwrite /src/ukiyostudio-images local/ukiyostudio-images"
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

Independence check: the VaultShop instance must expose only the `product-images` bucket (and vice versa with `ukiyostudio-images`). If boundary-crossing object keys appear in either archive, the store separation is broken.

Success criteria:

- MinIO starts.
- The restored bucket exists.
- Product image objects are visible.
- The VaultShop archive contains no UkiyoStudio objects and vice versa.

Clean up when finished:

```powershell
docker rm -f vaultshop-restore-minio
docker volume rm vaultshop-restore-minio-data
Remove-Item -Recurse -Force C:\vaultshop-restore-tmp
```

Repeat cleanup for the UkiyoStudio container, volume, and temp dir (`ukiyostudio-restore-minio`, `ukiyostudio-restore-minio-data`, `C:\ukiyostudio-restore-tmp`).

## Automated Backup Freshness And Disk Checks

Source scripts live in `docs/operations/` in this repo. `do-backup.sh <store>` and `check-backup-freshness.sh <store> [hours]` are store-parametric: the same files back up only their own store's database and bucket, using only that store's scoped MinIO user — a VaultShop backup can never read UkiyoStudio objects and vice versa. VaultShop (demo) backs up weekly; UkiyoStudio (production) backs up daily.

Host-level orchestration (off-site `rsync` to the phone, Kuma push, disk check, Kuma and Vaultwarden configs) lives in the separate **server-ops** repo, not here. That repo owns `/opt/server-ops/` and the authoritative cron.

Install/update the store scripts on the VPS:

```
cd /opt/vaultshop
mkdir -p ~/vaultshop-backups ~/ukiyostudio-backups
install -m 755 docs/operations/do-backup.sh ~/vaultshop-backups/do-backup.sh
install -m 755 docs/operations/do-backup.sh ~/ukiyostudio-backups/do-backup.sh
sudo install -d -m 755 /opt/server-ops/scripts
sudo chown -R "$USER":"$USER" /opt/server-ops
install -m 755 docs/operations/check-backup-freshness.sh /opt/server-ops/scripts/check-backup-freshness.sh
install -m 755 scripts/ops/check-docker-storage.sh /opt/server-ops/scripts/check-docker-storage.sh
```

### do-backup.sh

Usage: `do-backup.sh <vaultshop|ukiyostudio>`.

Reads the real database, bucket, and scoped MinIO user/password names from `/opt/vaultshop/.platform.env` (no secrets in the repo), then:

1. Dumps the store's database with `pg_dump -Fc` through the platform `postgres` container into `~/<store>-backups/postgres/<store>_<date>.dump`.
2. Mirrors the store's bucket with `mc mirror`, run inside a throwaway `minio/mc` container against the loopback-bound MinIO API `127.0.0.1:9000`, then archives it as `~/<store>-backups/minio/<bucket>_<date>.tar.gz`.
3. Deletes artifacts older than 60 days.

Expected output (healthy):

```
[2026-08-04 06:32] === ukiyostudio Backup ===
[06:32] PostgreSQL dump (ukiyostudio)...
OK:   3.1M
[06:32] MinIO bucket mirror (ukiyostudio-images)...
OK:   1.2M
[2026-08-04 06:32] ukiyostudio backup complete
```

### check-backup-freshness.sh

Usage: `check-backup-freshness.sh <vaultshop|ukiyostudio> [max-age-hours]`.

Checks that the store's newest PostgreSQL dump and MinIO archive exist and are newer than the threshold (default: 48 hours; cron passes `168` for VaultShop's weekly backup and `48` for UkiyoStudio's daily backup). Exits `0` when both pass, `1` when either is missing or stale.

Expected output (healthy):

```
=== Backup Freshness Check (vaultshop) ===
Max age: 168h

OK:   PostgreSQL dump is 0h old (vaultshop_2026-08-04_0632.dump)
OK:   MinIO archive is 0h old (product-images_2026-08-04_0632.tar.gz)

Exit code: 0
```

### Disk check

The host disk check (`check-disk.sh`, 80/90 % thresholds, Kuma push) lives in the
**server-ops** repo at `/opt/server-ops/scripts/check-disk.sh`. It is host-level,
not store-level, so it is not maintained here.

### Cron Setup

VaultShop (demo) backs up weekly on Sunday 6:00; UkiyoStudio (production) backs up daily. The authoritative crontab — backups + off-site sync + Kuma push + freshness/disk/storage checks — is documented and installed from the **server-ops** repo (`/opt/server-ops`). Do not duplicate cron lines here.

Logs: `/opt/server-ops/logs/backup.log` (backups/sync) and `/opt/server-ops/logs/checks.log` (checks).

Success criteria:

- Each script exits `0` when checks pass.
- Non-zero exit codes appear in `/opt/server-ops/logs/checks.log` and indicate what failed.
- Both cron jobs (VaultShop weekly, UkiyoStudio daily) run without manual intervention.
- Check `/opt/server-ops/logs/backup.log` after the first cron execution of each store.

### Container Restart Detection

Quick check for unexpected container restarts (platform first, then each store):

```
cd /opt/vaultshop
docker compose --env-file /opt/vaultshop/.platform.env -f docker-compose.platform.yml ps -a
docker compose --env-file /opt/vaultshop/.platform.env -f docker-compose.platform.yml logs --tail=20 --timestamps postgres minio | grep -i "restart\|error\|warn\|killed\|oom"
docker compose --env-file /opt/vaultshop/.env.compose -f docker-compose.store.yml ps -a
docker compose --env-file /opt/vaultshop/.env.compose -f docker-compose.store.yml logs --tail=20 --timestamps web | grep -i "restart\|error\|warn\|killed\|oom"
docker compose --env-file /opt/ukiyostudio/.env.compose -f docker-compose.store.yml ps -a
docker compose --env-file /opt/ukiyostudio/.env.compose -f docker-compose.store.yml logs --tail=20 --timestamps web | grep -i "restart\|error\|warn\|killed\|oom"
```

Look for containers with unexpected exit codes or recent restart timestamps. This is a manual check for now; automated alerting can be added if restarts become frequent.

### Webhook Error Visibility

Stripe webhook errors appear in each store's app logs:

```
cd /opt/vaultshop
docker compose --env-file /opt/vaultshop/.env.compose -f docker-compose.store.yml logs --tail=100 web | grep -i "webhook\|stripe.*fail\|signature\|400\|401\|403"
docker compose --env-file /opt/ukiyostudio/.env.compose -f docker-compose.store.yml logs --tail=100 web | grep -i "webhook\|stripe.*fail\|signature\|400\|401\|403"
```

Expected: no matching lines on a healthy system. If errors appear, check the Stripe Dashboard webhook logs for recent attempts.

## Monitoring

### Uptime Kuma (self-hosted, on VPS)

Kuma runs on the VPS but its configuration (compose, monitors, push URLs, Telegram)
is owned by the separate **server-ops** repo. Store-relevant monitors: VaultShop
ready, UkiyoStudio ready, both home pages, TLS expiry, and the "Docker storage"
push from `check-docker-storage.sh`. Do not add Kuma/Vaultwarden config to this
repo.

### External uptime/TLS monitor

An external uptime/TLS monitor should also check both stores:

```text
https://vaultshop.evaldez.ar
https://ukiyostudio.evaldez.ar
```

Minimum expectations:

- The monitor reports `Up`.
- Email alerts are enabled.
- TLS/HTTPS expiry checks are enabled if supported by the provider.

This detects basic availability problems. It does not replace backup/restore or deeper application checks.

## Logs

App logs (each store):

```
cd /opt/vaultshop
docker compose --env-file /opt/vaultshop/.env.compose -f docker-compose.store.yml logs --tail=100 web
docker compose --env-file /opt/ukiyostudio/.env.compose -f docker-compose.store.yml logs --tail=100 web
```

Platform logs (shared):

```
cd /opt/vaultshop
docker compose --env-file /opt/vaultshop/.platform.env -f docker-compose.platform.yml logs --tail=100 postgres
docker compose --env-file /opt/vaultshop/.platform.env -f docker-compose.platform.yml logs --tail=100 minio
```

Nginx status and recent logs:

```
sudo systemctl status nginx --no-pager
sudo journalctl -u nginx --since "1 hour ago" --no-pager
```

## Critical Warning

Do not run this on the VPS unless intentionally deleting persisted data:

```
docker compose --env-file /opt/vaultshop/.platform.env -f docker-compose.platform.yml down -v
```

The `-v` flag deletes Docker volumes, including the shared PostgreSQL and MinIO data — both stores at once.

Use this to stop containers without deleting data:

```
cd /opt/vaultshop
docker compose --env-file /opt/vaultshop/.platform.env -f docker-compose.platform.yml down
docker compose --env-file /opt/vaultshop/.env.compose -f docker-compose.store.yml down
docker compose --env-file /opt/ukiyostudio/.env.compose -f docker-compose.store.yml down
```

## Deploy (one-command, no build on VPS)

Production images are built in GitHub Actions and published to GHCR (`ghcr.io/emmanuel-valdez/vaultshop-web:sha-<commit>`). The VPS never builds. Deploy with:

```bash
cd /opt/vaultshop
git pull --ff-only
# Deploy VaultShop
/opt/vaultshop/scripts/ops/deploy-store.sh vaultshop
# Deploy UkiyoStudio
/opt/vaultshop/scripts/ops/deploy-store.sh ukiyo
```

`deploy-store.sh` validates: git pull ff-only, valid SHA, GHCR manifest exists, pulls the image, runs `up -d --no-build`, probes `/health/ready`, and syncs `WEB_IMAGE` in the store's `.env.compose`. It rejects non-GHCR and local images by construction (the image is always `ghcr.io/...:sha-<commit>`).

Rollback: re-run with the previous SHA:
```bash
/opt/vaultshop/scripts/ops/deploy-store.sh vaultshop <previous-sha>
```

## Deployment Version Check

The app embeds the deployed commit (`APP_VERSION`) and build date (`APP_BUILD_DATE`) at image build time via Docker build args, and exposes them at `Admin → Versión` (`/Admin/System/Version`, admin-only) with a GitHub `main` comparison (cached 5 min, never breaks the page on API failure).

Build with stamping — run from the repo root before `up --build` so the image carries the right commit:

```
cd /opt/vaultshop
git pull
export GIT_COMMIT=$(git rev-parse --short HEAD)
export GIT_BUILD_DATE=$(date -u +%Y-%m-%dT%H:%MZ)
docker compose --env-file /opt/vaultshop/.env.compose -f docker-compose.store.yml up -d --build
docker compose --env-file /opt/ukiyostudio/.env.compose -f docker-compose.store.yml up -d --build
```

If built without `GIT_COMMIT`, the page shows `unknown` — rebuild with the exports above.

## Future Private Deployment Notes

UkiyoStudio already follows the two-store pattern above: shared platform (PostgreSQL + MinIO), separate store stacks (web + env + compose project + domain), separate database/bucket/scoped credentials, separate backups. A future private/client deployment should do the same, plus:

- No demo seed data in the real client deployment.
- Private branding assets configured through `Branding__...` and stored outside git.
- Separate `Theme__...` hex color values for the deployment.
- Its own backup dir, cron lines, and restore tests (copy `do-backup.sh`/`check-backup-freshness.sh` with the new store name).
- If the client needs full isolation (own VM or Kubernetes), the shared platform pattern does not apply — use separate infrastructure instead.




