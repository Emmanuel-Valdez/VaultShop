# refresh_sucursales.py — monthly Correo Argentino branch refresh

Re-scrapes public branch finder into
`VaultShop.DataAccess/SeedData/sucursales.json` (merge by `code`;
gist CPA/postal codes never overwritten).

```bash
py -3 tools/refresh_sucursales.py                 # all provinces
py -3 tools/refresh_sucursales.py --provinces C   # single / comma list
py -3 tools/refresh_sucursales.py --dry-run       # no write, report only
```

Notes:

- `--throttle 0.3` (s) between requests, `--timeout 30` (s), 3x backoff
  retry on timeout/5xx. Keep the throttle; don't hammer F5 in parallel.
- Only `SUCURSAL` markers kept; `UNIDAD POSTAL` (Punto Correo, no PAQ
  code) skipped. Codes paired to markers in document order.
- Stdlib only: `urllib, json, re, argparse, time, datetime`.
