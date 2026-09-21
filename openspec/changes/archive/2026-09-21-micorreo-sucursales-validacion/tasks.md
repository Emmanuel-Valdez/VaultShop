## 1. Validate all provinces against MiCorreo truth

- [x] 1.1 Build `tools/validar_micorreo.py` (stdlib, per-province, `;`-separated multi-block truth files, detail enrichment, superseded detection)
- [x] 1.2 Validate 24/24 provinces (3765/3767 codes; O6123/Y3119 without coords)
- [x] 1.3 Normalize names to base letters file-wide
- [x] 1.4 Split: `sucursales.json` (micorreo only) + `sucursales-dudosas.json` (3198 reference rows)

## 2. App serving truth only

- [x] 2.1 `NearestAgencyService.IsCandidate` includes `micorreo` + test
- [x] 2.2 `EnsurePostalAgencies` deletes DB codes absent from JSON
- [x] 2.3 `dotnet build` 0 errors + `dotnet test` 344 passed
