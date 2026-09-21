# MiCorreo Sucursales Validation

Manual data-curation change (no C# behavior except the seed_Delete noted below).
Validated `sucursales.json` 1:1 against MiCorreo network responses
(`codigoSucursal`, 24 provinces, 3767 codes) via `tools/validar_micorreo.py`.

Outcome:
- `sucursales.json`: 3765 rows, all `source=micorreo` (O6123/Y3119 listed
  by MiCorreo without coords, not importable).
- `sucursales-dudosas.json`: 3198 reference rows (2122 superseded UP-*,
  735 unreviewed synthetics, 341 old codes absent from MiCorreo).
- Names normalized to base letters (Ñ→N, accents stripped) file-wide.
- `NearestAgencyService.IsCandidate`: `micorreo` rows are checkout-eligible
  even without parcel service 40 (MiCorreo validates eligibility itself).
- `EnsurePostalAgencies`: deletes DB rows whose Code left `sucursales.json`
  (seed previously never deleted; orders keep snapshot strings, no FK).
