## Context

See proposal.md (Why). Current state: `PostalAgency` has no hours (JSON `horario` is dropped at seed); `OrderHeader` snapshots code/name/address only; checkout uses geocoded nearest-5 (`GetNearestAgencies` + `GeorefAddressService` + `NearestAgencyService.FindNearest` with a full-table `ToList`); admin Details shows 3 readonly pickup inputs and `UpdateOrderDetail` ignores them; shipped orders remain admin-editable behind a confirm dialog; confirmation/shipping emails never mention pickup. Single codebase, two stores with isolated databases seeded from the same checked-in `sucursales.json` (3765 rows, all with `horario`; 24 province codes A–Z minus I/Ñ/O, `CAPITAL FEDERAL` separate from `BUENOS AIRES`).

## Goals / Non-Goals

**Goals:** hours end-to-end (seed → snapshot → all surfaces); one deterministic cascade reused in checkout and admin; shipped-frozen enforcement; pickup-aware emails; seed hardening without new infrastructure.

**Non-Goals:** no shared branch DB or branch API (rejected as overengineering — see proposal impact); no `HoursOverride` column (scrape/dev-wins); no correction email on admin branch change; no no-JS full-parity cascade (safe-degraded: server validation + WhatsApp hint); no parsing of free-text hours.

## Decisions

- **Hours mapping**: `[JsonPropertyName("horario")]` on `PostalAgency.Hours` (one line, cannot be forgotten per-path) over a DTO + manual copy. Sentinel `no informa` as an app constant mirrored as the DB column DEFAULT; stored Spanish is accepted (single-locale domain data, never round-tripped through resx); the UI always renders the hours row (no hide-on-empty rule, which would be dead code under NOT NULL).
- **Order snapshot**: new `OrderHeader.PickupAgencyHours NOT NULL DEFAULT 'no informa'`; pre-change orders backfill to the sentinel. History is decoupled from branch-table drift by design.
- **Cascade endpoints**: three filtered `IQueryable` GETs (`provinces`, `localities?provinceCode=`, `branches?provinceCode=&locality=`) with `IsCandidate` as a single SQL-translatable `Expression` reused in all three (no phantom options) plus `GetByCode`; 2 new indexes (`ProvinceCode`, `(ProvinceCode, Locality)` covering Code/Name). Locality is always scoped by province (homonym collisions). Anonymous GET, short cache on provinces/localities.
- **Reusable partial** `Views/Shared/_BranchCascadePicker.cshtml` + `BranchCascadePickerVM` (`FieldName`, `IdPrefix`, server-rendered provinces, posted selection), own resx pair, `fieldset/legend`, one `aria-live` status region, native selects + radios, client-side filter only when branches > 10, `AbortController` for stale fetches. Failed POSTs rehydrate via `ViewData` selection + options (extends today's `RestorePostedHeaderAndCandidates` pattern one level up).
- **Shipped freeze**: single predicate `IsFrozen = IsTerminal || Shipped`, called first in `UpdateOrderDetail` and `CancelOrder` (cancel/refund frozen too, closing the forged-POST hole); view flags gain `&& !shipped`; delete `confirmShippedOrderUpdate` + onclick; invert the admin-shipped-edit test; delete the 3 dead `UpdateShippedOrder*` resx keys per language.
- **Seed hardening**: `Hours` added to the reseed field compare (plus completing the compare for all copied fields); stale-delete guard (abort + `LogCritical` when stale > 10% or > 200 rows); `refresh_sucursales.py` becomes confirm-only (never deletes, emits a parallel changelist) and carries `horario` through merges.
- **Emails**: branch block + hours + 5-day policy paragraph in both buyer emails; shipping email switches to in-transit-to-branch copy with static Correo landing URL + plain-text code (no templated deep link — rots on Correo redesigns); HTML-encode admin-entered tracking/carrier at template time; FAQ/Terms gain the long pickup-policy item. Province list: `static readonly` code→display in Web/Utility with a CI test asserting seed codes ⊆ list (startup logs drift, never throws).

## Risks / Trade-offs

- [Risk] Truncated JSON mass-deletes branches → Mitigation: stale-delete guard + confirm-only refresh script.
- [Risk] `Hours` silently empty after migration (mapping missed, DEFAULT masks it) → Mitigation: seed test asserting seeded hours match JSON sample; verify `changed` count on first reseed.
- [Risk] Cascade shows branches the POST rejects → Mitigation: single `IsCandidate` expression in all endpoints and `GetByCode`.
- [Risk] 50+ branch radios in CABA/GBA → Mitigation: scroll container + client filter above 10; native radios keep keyboard semantics.
- [Risk] Buyer holds stale branch from confirmation email after admin correction → Mitigation: correction window is pre-shipment only; shipping email always carries the final snapshot; correction is logged for the WhatsApp trail.
- [Risk] Correo tracking landing URL rots → Mitigation: static landing URL + copy-paste code, accepted residual risk.

## Migration Plan

1. Deploy migration (2 new columns with defaults; pre-change orders backfill to sentinel) — additive, rollback = redeploy previous build (columns ignored).
2. Reseed on startup backfills `PostalAgency.Hours` from JSON; verify counts in logs.
3. Remove nearest-search code paths only after the cascade is verified in both cultures; dead resx/JS removed in the same change.
4. Rollback of behavior (if needed): revert commit; snapshots already written stay valid data.

## Open Questions

- None blocking. Copy review of the es-AR/en-US policy paragraphs can happen at implementation review.
