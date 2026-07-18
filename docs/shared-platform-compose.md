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
