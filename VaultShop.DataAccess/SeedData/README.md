# SeedData / sucursales.json

Snapshot de sucursales de **Correo Argentino** usado para `PostalAgency`.

- **Fuente**: gist [`aaron-marco`](https://gist.github.com/aaron-marco/bee99bd47333f7e44e291aa45f100643) (2020) como base (única con CPA y calle/número separados) + scrape del buscador oficial `https://www.correoargentino.com.ar/formularios/sucursales` (`wsFacade.php`, sin auth) que aporta coords reales y existencia actual.
- **Curaduría**: `py -3 tools/refresh_sucursales.py` (re-ejecutable mensual; ver `tools/README.md`). Match por `Code` fuera de CABA es por (nombre, localidad) porque el sitio solo expone código en CABA. Filas con `source="correo"` = confirmadas (`lastVerifiedUtc`); `source="gist"` = no vistas en el sitio (candidatas a cerrada).
- **Count**: 1.361 hoy (926 verificadas, 435 solo-gist, 2 nuevas `C4932`/`C5259`). Última verificación: 2026-09-19.
- **Pendiente (1.8)**: columna `Services` (ids del `rel` oficial) + `Kind` e ingesta de puntos UP con códigos sintéticos `UP-*` (hoy el JSON trae solo SUCURSAL).
- **Coords**: no vienen en la fuente. Se generaron de forma determinística a partir del centroide de cada provincia ±0.8° (jitter por MD5 del código), ~≤80 km de error. Ver `ponytail:` en `DbInitializer.cs`.
- **Count**: ~1.359 hoy. El sitio oficial declara ">3.300 puntos de venta" — discrepancia conocida; el gist es parcial.
- **Reemplazo previsto**: feed PAQ.AR / `wsFacade.php` con credenciales cuando esté disponible (geocoding preciso vía Georef `lotes` si hace falta).
- **Uso**: seed idempotente (upsert por `Code`) en `DbInitializer.EnsurePostalAgencies()`, copiado a output vía `<None Update="SeedData\sucursales.json" CopyToOutputDirectory="PreserveNewest" />`.
