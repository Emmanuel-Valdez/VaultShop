# VaultShop Operations Runbook

This runbook documents the current lightweight operations process for the VaultShop VPS deployment. It is intentionally simple: the goal is to keep the public demo recoverable and explainable without over-engineering.

## Deployment Shape

- Public URL: `https://vaultshop.evaldez.ar`
- VPS OS: Ubuntu 24.04
- App path on VPS: `/opt/vaultshop/VaultShop`
- Public ingress: Nginx on `80/443`
- App container bind: `127.0.0.1:8080`
- PostgreSQL: Docker Compose private service
- MinIO: Docker Compose private service, served publicly through Nginx for product images
- SSH/admin access: Tailscale/private access

Do not expose PostgreSQL, MinIO API, or the MinIO console directly to the public internet.

## Quick Health Check

Run on the VPS:

```bash
cd /opt/vaultshop/VaultShop
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

```bash
cd /opt/vaultshop/VaultShop
docker compose --env-file .env.compose ps web postgres minio
docker compose --env-file .env.compose ps -q web postgres minio | xargs -r docker inspect --format '{{.Name}} {{.HostConfig.RestartPolicy.Name}}'
```

Expected:

- Web, PostgreSQL, and MinIO are running.
- Each inspected container reports `unless-stopped`.
- Container names follow the active `COMPOSE_PROJECT_NAME`; keep that name stable after first deployment so volumes do not silently change.

After a VPS reboot:

```bash
cd /opt/vaultshop/VaultShop
docker compose --env-file .env.compose ps
curl -I https://vaultshop.evaldez.ar
```

Then verify manually in the browser that product pages and uploaded images still load.

## PostgreSQL Backup

Create a compressed custom-format PostgreSQL dump on the VPS:

```bash
mkdir -p ~/vaultshop-backups/postgres
cd /opt/vaultshop/VaultShop
docker compose --env-file .env.compose exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > ~/vaultshop-backups/postgres/vaultshop_$(date +%F_%H%M).dump
ls -lh ~/vaultshop-backups/postgres
```

Copy it to the local PC from PowerShell:

```powershell
mkdir C:\Users\evald\Backups\VaultShop\Postgres
scp ubuntu@100.91.22.124:/home/ubuntu/vaultshop-backups/postgres/*.dump C:\Users\evald\Backups\VaultShop\Postgres\
dir C:\Users\evald\Backups\VaultShop\Postgres
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
docker cp C:\Users\evald\Backups\VaultShop\Postgres\vaultshop_YYYY-MM-DD_HHMM.dump vaultshop-restore-postgres:/backup.dump
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

```bash
docker volume ls | grep minio-data
```

Create a read-only archive of the MinIO data volume:

```bash
mkdir -p ~/vaultshop-backups/minio
docker run --rm -v vaultshop_minio-data:/data:ro -v ~/vaultshop-backups/minio:/backup alpine tar czf /backup/minio_$(date +%F_%H%M).tar.gz -C /data .
ls -lh ~/vaultshop-backups/minio
tar tzf ~/vaultshop-backups/minio/*.tar.gz | head
```

Copy it to the local PC from PowerShell:

```powershell
mkdir C:\Users\evald\Backups\VaultShop\MinIO
scp ubuntu@100.91.22.124:/home/ubuntu/vaultshop-backups/minio/*.tar.gz C:\Users\evald\Backups\VaultShop\MinIO\
dir C:\Users\evald\Backups\VaultShop\MinIO
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
docker run --rm -v vaultshop-restore-minio-data:/data -v C:\Users\evald\Backups\VaultShop\MinIO:/backup alpine sh -c "tar xzf /backup/minio_YYYY-MM-DD_HHMM.tar.gz -C /data"
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

```bash
cd /opt/vaultshop/VaultShop
docker compose --env-file .env.compose logs --tail=100 web
```

PostgreSQL logs:

```bash
docker compose --env-file .env.compose logs --tail=100 postgres
```

MinIO logs:

```bash
docker compose --env-file .env.compose logs --tail=100 minio
```

Nginx status and recent logs:

```bash
sudo systemctl status nginx --no-pager
sudo journalctl -u nginx --since "1 hour ago" --no-pager
```

## Critical Warning

Do not run this on the VPS unless intentionally deleting persisted data:

```bash
docker compose down -v
```

The `-v` flag deletes Docker volumes, including PostgreSQL and MinIO data.

Use this to stop containers without deleting data:

```bash
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
