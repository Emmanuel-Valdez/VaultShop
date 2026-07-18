# Shared Platform Compose

`docker-compose.yml` remains the local, single-store stack. It keeps its current named volumes and is still started with:

```sh
docker compose --env-file .env.compose up --build
```

The shared VPS layout uses one private platform and two independent web deployments:

| Store | Web bind | Database | MinIO bucket |
| --- | --- | --- | --- |
| VaultShop | `127.0.0.1:8080` | `vaultshop` | `product-images` |
| UkiyoStudio | `127.0.0.1:8081` | `ukiyostudio` | `ukiyostudio-images` |

PostgreSQL and MinIO have no host port in this layout. Web containers connect to them through the external `vaultshop-net` Docker network.

## Private configuration

Keep these files outside the repository with restrictive permissions:

```sh
install -d -m 700 /opt/vaultshop /opt/ukiyostudio
install -m 600 .env.platform.example /opt/vaultshop/.platform.env
install -m 600 .env.compose.example /opt/vaultshop/.env.compose
install -m 600 ukiyo.env.compose.example /opt/ukiyostudio/.env.compose
```

Set `ConnectionStrings__DefaultConnection=Host=postgres;...` and `ImageStorage__Minio__Endpoint=minio:9000` in both store files. The VaultShop file needs `DATA_PROTECTION_VOLUME_NAME=vaultshop_data-protection-keys`; the UkiyoStudio sample already uses `ukiyostudio_data-protection-keys`.

`.platform.env` owns only shared infrastructure credentials and preserves the existing VaultShop volume names. It also creates the UkiyoStudio database and role if they do not already exist. Existing roles are never altered by the initializer.

## Database and object storage isolation

Both stores share one PostgreSQL server and one MinIO server, but each gets its own database/role and bucket/user with no cross-store access:

- `postgres-init` creates the `ukiyostudio` database owned by `ukiyostudio_app`, then revokes PostgreSQL's default `CONNECT` grant to `PUBLIC` on both the `vaultshop` and `ukiyostudio` databases and grants `CONNECT` back only to each database's own role. Without this, any Postgres role can open a connection to any database on the server by default — ownership alone does not block that.
- `minio-init` creates one MinIO user per store (`vaultshop_app`, `ukiyostudio_app` by default) with a policy restricted to `GetBucketLocation`/`ListBucket` on its own bucket and `GetObject`/`PutObject`/`DeleteObject` on that bucket's objects only — no `ListAllMyBuckets`, no access to the other store's bucket. Public read access for serving images stays on via `mc anonymous set download`, unrelated to these scoped users.
- Set `VAULTSHOP_MINIO_PASSWORD` and `UKIYO_MINIO_PASSWORD` in `.platform.env`, then point each store's `ImageStorage__Minio__AccessKey`/`SecretKey` at its own user, never at `MINIO_ROOT_USER`/`MINIO_ROOT_PASSWORD`. The root credentials should only ever be used interactively (`mc alias set` for admin work), not by the running application.
- If VaultShop's `/opt/vaultshop/.env.compose` currently uses the MinIO root credentials (true before this change), rotate `ImageStorage__Minio__AccessKey`/`SecretKey` to `VAULTSHOP_MINIO_USER`/`VAULTSHOP_MINIO_PASSWORD` and restart the VaultShop web container. This is a manual step outside the repo since that file is private.

To verify cross-store access is actually denied after `postgres-init`/`minio-init` run:

```sh
# Postgres: vaultshop_app should be refused when connecting to ukiyostudio, and vice versa.
docker compose --env-file /opt/vaultshop/.platform.env -f docker-compose.platform.yml exec postgres \
  psql -U vaultshop_app -d ukiyostudio -c '\conninfo'
docker compose --env-file /opt/vaultshop/.platform.env -f docker-compose.platform.yml exec postgres \
  psql -U ukiyostudio_app -d vaultshop -c '\conninfo'

# MinIO: each store's user should be denied against the other store's bucket.
# MinIO has no published host port in this layout, so run mc from a throwaway
# container on the shared network instead of from the host.
docker run --rm --network vaultshop-net minio/mc:latest sh -c '
  mc alias set vaultshop-check http://minio:9000 vaultshop_app "<VAULTSHOP_MINIO_PASSWORD>"
  mc ls vaultshop-check/ukiyostudio-images
'   # expect AccessDenied
docker run --rm --network vaultshop-net minio/mc:latest sh -c '
  mc alias set ukiyo-check http://minio:9000 ukiyostudio_app "<UKIYO_MINIO_PASSWORD>"
  mc ls ukiyo-check/product-images
'   # expect AccessDenied
```

Both `psql` attempts should fail with `FATAL: permission denied for database`, and both `mc ls` attempts should fail with `AccessDenied`.

## Applying UkiyoStudio migrations

`DATABASE_RUN_MIGRATIONS_ON_STARTUP` stays `false` for both stores in production (per `CLAUDE.md`). Apply UkiyoStudio's EF Core migrations intentionally, once, before first launch:

```sh
dotnet ef database update \
  --project VaultShop.DataAccess --startup-project VaultShop.Web \
  --connection "Host=127.0.0.1;Port=<forwarded-postgres-port-or-tunnel>;Database=ukiyostudio;Username=ukiyostudio_app;Password=<UKIYO_POSTGRES_PASSWORD>"
```

Run this from a host that can reach the VPS's PostgreSQL (SSH tunnel or Tailscale), since PostgreSQL is not exposed publicly. Do not flip `DATABASE_RUN_MIGRATIONS_ON_STARTUP` to `true` in the private `.env.compose` for routine restarts — only for a deliberate one-off boot if `dotnet ef` access isn't practical, then set it back to `false` afterward. `DbInitializer` no longer seeds demo data (removed prior to Phase 6), so a normal startup only seeds roles/admin.

## Nginx routing

MinIO has no published host port in this layout (see the topology table above), so a host-level Nginx process cannot reach it. Nginx must itself run in Docker and join `vaultshop-net` to proxy `/product-images/`; do this before cutover. Each store's images must resolve through its own domain to its own bucket, never the other's:

```nginx
server {
    server_name vaultshop.evaldez.ar;
    location /product-images/ {
        proxy_pass http://minio:9000/product-images/;
    }
    location / {
        proxy_pass http://vaultshop-web:8080;
    }
}

server {
    server_name ukiyostudio.evaldez.ar;
    location /product-images/ {
        proxy_pass http://minio:9000/ukiyostudio-images/;
    }
    location / {
        proxy_pass http://ukiyostudio-web:8080;
    }
}
```

`vaultshop-web`/`ukiyostudio-web` are the stable `container_name: ${COMPOSE_PROJECT_NAME}-web` values set in `docker-compose.store.yml` (both listen on `8080` inside their container regardless of the host-published port) — resolvable by name on `vaultshop-net` without relying on Compose's default per-service DNS alias, which would otherwise collide since both stores use the service name `web` on the same shared network. Keep the MinIO API (`9000`) and console (`9001`) unpublished, as in `docker-compose.platform.yml` today — Nginx is the only path to bucket data, and each vhost must hard-code its own bucket name so one domain can never serve the other store's images.

## Migration

Do not run this migration without a verified PostgreSQL and MinIO backup.

1. Record the current names and labels before changing ownership. The expected existing VaultShop names are `vaultshop_postgres-data`, `vaultshop_minio-data`, and `vaultshop_data-protection-keys`.

   ```sh
   docker volume ls
   docker volume inspect vaultshop_postgres-data vaultshop_minio-data vaultshop_data-protection-keys
   ```

2. Set `POSTGRES_VOLUME_NAME` and `MINIO_VOLUME_NAME` in `/opt/vaultshop/.platform.env` to the inspected names. Do not create, rename, or remove either existing volume. Create only missing infrastructure for a fresh host:

   ```sh
   docker network create vaultshop-net
   docker volume create ukiyostudio_data-protection-keys
   ```

   `docker-compose.store.yml` includes a short-lived initializer that changes ownership only of the inspected data-protection volume to the .NET container user. Do not point `DATA_PROTECTION_VOLUME_NAME` at a PostgreSQL or MinIO volume.

3. Stop the old single stack without `-v`, then start the platform. On the first migration this reuses the inspected PostgreSQL and MinIO volumes.

   ```sh
   docker compose --env-file /opt/vaultshop/.env.compose down
   docker compose --env-file /opt/vaultshop/.platform.env -f docker-compose.platform.yml up -d
   ```

4. Start each web deployment. Both bind only to loopback for the host reverse proxy.

   ```sh
   docker compose --env-file /opt/vaultshop/.env.compose -f docker-compose.store.yml up -d --build
   docker compose --env-file /opt/ukiyostudio/.env.compose -f docker-compose.store.yml up -d --build
   ```

5. Verify the platform containers have no published ports and that the initializers completed successfully.

   ```sh
   docker compose --env-file /opt/vaultshop/.platform.env -f docker-compose.platform.yml ps
   docker compose --env-file /opt/vaultshop/.platform.env -f docker-compose.platform.yml logs postgres-init minio-init
   docker ps --format 'table {{.Names}}\t{{.Ports}}'
   ```

If Nginx proxies product images, it must itself run in Docker and join `vaultshop-net`; a host-level Nginx process cannot reach an unexposed MinIO port. Do this before cutover, then configure each public path to its own bucket.

## Rollback

The migration does not modify or recreate the existing data volumes. To return to the previous single stack, stop the platform without `-v`, then start `docker-compose.yml` with the previous `/opt/vaultshop/.env.compose`. Do not run `down -v` in either layout.
