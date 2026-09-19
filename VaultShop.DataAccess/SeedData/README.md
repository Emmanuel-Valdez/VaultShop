# SeedData / sucursales.json

Snapshot de sucursales de **Correo Argentino** usado para `PostalAgency`.

- **Fuente**: gist [`aaron-marco`](https://gist.github.com/aaron-marco/bee99bd47333f7e44e291aa45f100643) (1.359 filas, campos `CODIGONIS`/`DENOMINACION`/`CALLE`/`NUM`/`CPA`/`LOCALIDAD`/`PROVINCIA`). Datos públicos de direcciones de sucursales; el gist es de 2020.
- **Coords**: no vienen en la fuente. Se generaron de forma determinística a partir del centroide de cada provincia ±0.8° (jitter por MD5 del código), ~≤80 km de error. Ver `ponytail:` en `DbInitializer.cs`.
- **Count**: ~1.359 hoy. El sitio oficial declara ">3.300 puntos de venta" — discrepancia conocida; el gist es parcial.
- **Reemplazo previsto**: feed PAQ.AR / `wsFacade.php` con credenciales cuando esté disponible (geocoding preciso vía Georef `lotes` si hace falta).
- **Uso**: seed idempotente (upsert por `Code`) en `DbInitializer.EnsurePostalAgencies()`, copiado a output vía `<None Update="SeedData\sucursales.json" CopyToOutputDirectory="PreserveNewest" />`.
