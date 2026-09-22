"""Monthly refresh of VaultShop sucursales.json from Correo Argentino branch finder.

Usage:
    py -3 tools/refresh_sucursales.py [--provinces C,M] [--throttle 0.3]
                                      [--timeout 30] [--dry-run]
                                      [--json VaultShop.DataAccess/SeedData/sucursales.json]

Stdlib only (urllib, json, re, argparse, time, datetime).
Idempotent: re-runs merge by Code; gist-only fields are never overwritten.
Confirm-only: rows are never deleted here — unverified rows go to a parallel
<json-stem>-changelist.json for human review (quarantine), the main file only
gains rows or updates fields. Branch hours (`horario`) are carried through
every merge; fresh scraped rows without hours get the `no informa` sentinel.
"""
import argparse
import datetime
import hashlib
import json
import re
import time
import unicodedata
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

HORARIO_SENTINEL = "no informa"


def ensure_horario(row: dict) -> dict:
    """Carry branch hours through merges; sentinel when the source has none."""
    row.setdefault("horario", HORARIO_SENTINEL)
    return row


WS_URL = "https://www.correoargentino.com.ar/sites/all/modules/custom/ca_forms/api/wsFacade.php"
FORM_URL = "https://www.correoargentino.com.ar/formularios/sucursales"
UA = {"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) VaultShop-monthly-refresh"}

FALLBACK_PROVINCES = {  # code -> display name
    "A": "Salta", "B": "Buenos Aires", "C": "CABA", "D": "San Luis",
    "E": "Entre R\u00edos", "F": "La Rioja", "G": "Santiago del Estero",
    "H": "Chaco", "J": "San Juan", "K": "Catamarca", "L": "La Pampa",
    "M": "Mendoza", "N": "Misiones", "P": "Formosa", "Q": "Neuqu\u00e9n",
    "R": "R\u00edo Negro", "S": "Santa Fe", "T": "Tucum\u00e1n",
    "U": "Chubut", "V": "Tierra del Fuego", "W": "Corrientes",
    "X": "C\u00f3rdoba", "Y": "Jujuy", "Z": "Santa Cruz",
}

LATLNG_RE = re.compile(r"L\.marker\(\[\s*(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)\s*\]")
CONTENIDO_RE = re.compile(r"var\s+contenido\s*=\s*(.*?)</div>';", re.DOTALL)
STRONG_RE = re.compile(r"<strong>(.*?)</strong>", re.DOTALL)
SPAN_RE = re.compile(r"<span[^>]*>(.*?)</span>", re.DOTALL)
STREET_NUM_RE = re.compile(r"^(.*?)\s*N\u00b0\s*(\d+)\s*$")
TAG_RE = re.compile(r"<[^>]+>")


def norm(s):
    return re.sub(r"\s+", " ", (s or "").upper().strip())


def decode(raw: bytes) -> str:
    if raw.startswith(b"\xef\xbb\xbf"):
        return raw.decode("utf-8-sig")
    try:
        return raw.decode("utf-8")
    except UnicodeDecodeError:
        return raw.decode("windows-1252")


def post_form(fields: dict, timeout: float) -> str:
    data = urllib.parse.urlencode(fields).encode("ascii")
    last = None
    for attempt in range(3):
        req = urllib.request.Request(WS_URL, data=data, headers={**UA, "Content-Type": "application/x-www-form-urlencoded"})
        try:
            with urllib.request.urlopen(req, timeout=timeout) as r:
                if r.status >= 500:
                    raise urllib.error.HTTPError(WS_URL, r.status, "5xx", r.headers, None)
                return decode(r.read())
        except (urllib.error.HTTPError, urllib.error.URLError, TimeoutError) as e:
            code = getattr(e, "code", None)
            retryable = isinstance(e, (urllib.error.URLError, TimeoutError)) or (code is not None and code >= 500)
            last = e
            if not retryable or attempt == 2:
                raise
            time.sleep(2 ** attempt)
    raise last


def get_page(url: str, timeout: float) -> str:
    last = None
    for attempt in range(3):
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=timeout) as r:
                if r.status >= 500:
                    raise urllib.error.HTTPError(url, r.status, "5xx", r.headers, None)
                return decode(r.read())
        except (urllib.error.HTTPError, urllib.error.URLError, TimeoutError) as e:
            code = getattr(e, "code", None)
            retryable = isinstance(e, (urllib.error.URLError, TimeoutError)) or (code is not None and code >= 500)
            last = e
            if not retryable or attempt == 2:
                raise
            time.sleep(2 ** attempt)
    raise last


def fetch_provinces(timeout: float) -> dict:
    """Parse #provincias options dynamically; fallback to hardcoded list."""
    try:
        html = get_page(FORM_URL, timeout)
        sel = re.search(r'<select[^>]*id="provincias"[^>]*>(.*?)</select>', html, re.DOTALL | re.IGNORECASE)
        if not sel:
            raise ValueError("no #provincias select")
        opts = re.findall(r'<option[^>]*value="([A-Z])"[^>]*>(.*?)</option>', sel.group(1), re.DOTALL | re.IGNORECASE)
        provs = {c: TAG_RE.sub("", n).strip() for c, n in opts if c.strip()}
        if len(provs) >= 20:
            return provs
    except Exception as e:
        print(f"warn: province list fetch failed ({e}); using fallback")
    return dict(FALLBACK_PROVINCES)


CARD_RE = re.compile(r"<strong>(SUCURSAL|UNIDAD POSTAL): (.*?)</strong>")
SERV_RE = re.compile(r'<a class="accordion-toggle servicios"[^>]*rel="([^"]*)"')
SEC_CODE_RE = re.compile(r'id="accordion(?!Serv)([A-Z]\d+)"')


def up_code(prov, name, locality, street):
    """Stable synthetic code over normalized name|locality|street."""
    h = hashlib.sha1(f"{name}|{locality}|{street}".encode("utf-8")).hexdigest()[:6].upper()
    return f"UP-{prov}-{h}"


def parse_fragment(html: str):
    """Zip L.markers + cards + servicios + secundarios-codes in document order.

    Returns (rows, warnings). Each row: dict(kind,name,addrline,lat,lng,services,site_code).
    Keeps SUCURSAL and UNIDAD POSTAL; AGENCIA-labeled pins (map-only dupes) are skipped.
    site_code comes from the secundarios accordion id (CABA only; elsewhere "") and is
    only meaningful for SUCURSAL — UP rows get synthetic UP-{prov}-{hash6} codes.
    """
    warnings = []
    latlngs = list(LATLNG_RE.finditer(html))
    contenidos = list(CONTENIDO_RE.finditer(html))
    cards = list(CARD_RE.finditer(html))
    servs = list(SERV_RE.finditer(html))
    codes = list(SEC_CODE_RE.finditer(html))
    n = len(latlngs)
    if not (len(contenidos) == len(cards) == len(servs) == n):
        warnings.append(
            f"count mismatch: {n} L.marker vs {len(contenidos)} contenido vs {len(cards)} cards vs {len(servs)} servicios")
    elif codes and len(codes) != n:
        warnings.append(f"count mismatch: {n} markers vs {len(codes)} secundarios codes")
    rows = []
    for i in range(min(n, len(contenidos), len(cards), len(servs))):
        lat, lng = float(latlngs[i].group(1)), float(latlngs[i].group(2))
        contenido = contenidos[i].group(1)
        strong = STRONG_RE.search(contenido)
        span = SPAN_RE.search(contenido)
        mtitle = norm(TAG_RE.sub("", strong.group(1)).strip() if strong else "")
        addr = TAG_RE.sub("", span.group(1)).strip() if span else ""
        cardtitle = norm(cards[i].group(1) + " " + cards[i].group(2))
        if mtitle != cardtitle:
            warnings.append(f"order mismatch #{i}: marker {mtitle!r} vs card {cardtitle!r}")
            continue
        kind = "UP" if mtitle.startswith("UNIDAD POSTAL ") else ("SUCURSAL" if mtitle.startswith("SUCURSAL ") else "")
        if not kind:
            warnings.append(f"skipped non-branch pin #{i}: {mtitle!r}")
            continue
        rel = servs[i].group(1)
        if "|" in rel and rel.split("|")[1] != str(i + 1):
            warnings.append(f"servicios order mismatch #{i}: rel suffix {rel!r}")
        # Non-CABA fragments leave the {nis} template unsubstituted -> code ""
        rows.append({"kind": kind,
                      "code": codes[i].group(1) if (kind == "SUCURSAL" and i < len(codes)) else "",
                      "name": mtitle.split(" ", 1)[1].strip(),
                      "addrline": addr, "lat": lat, "lng": lng,
                      "services": rel.split("|")[0]})
    return rows, warnings


def split_street(full: str):
    full = norm(full)
    m = STREET_NUM_RE.match(full)
    if m:
        return m.group(1).strip(), int(m.group(2))
    return full, None


# --- second-pass address matching (task 1.7): same address = same branch ---
# Canonical token map: abbreviations and full words both collapse to one token.
_CANON = {
    "AVENIDA": "AV", "AVDA": "AV", "AVD": "AV", "AV": "AV",
    "BOULEVARD": "BV", "BVAR": "BV", "BVARD": "BV", "BLVD": "BV", "BV": "BV",
    "PASAJE": "PJE", "PJE": "PJE", "PSJE": "PJE",
    "GENERAL": "GRAL", "GRAL": "GRAL", "GRL": "GRAL",
    "SANTA": "STA", "STA": "STA",
    "SANTO": "STO", "STO": "STO",
    "DOCTOR": "DR", "DR": "DR", "DRA": "DR", "DOCTORA": "DR",
    "INGENIERO": "ING", "INGENIERA": "ING", "ING": "ING",
}

_SN_RE = re.compile(r"\bS\s*/\s*N\b|\bSIN\s+NUMERO\b|\bSN\b")
_KM_NUM_RE = re.compile(r"\bKM\.?\s*(\d)")

# Manual corrections over site spellings (obvious typos); site still wins everywhere else.
NAME_OVERRIDES = {
    "E0124": "SAN JAIME DE LA FRONTERA",  # ponytail: site spells "3SAN JAIME..." (typo)
}


def norm_addr_text(s):
    """Uppercase, strip accents, expand abbrevs both ways, drop N°/punct, collapse."""
    s = "".join(c for c in unicodedata.normalize("NFD", (s or "").upper())
                if unicodedata.category(c) != "Mn")
    s = _SN_RE.sub(" ", s)
    s = re.sub(r"\bKILOMETROS?\b", "KM", s)
    s = _KM_NUM_RE.sub(r"KM \1", s)
    s = re.sub(r"\bNUMEROS?\b|\bNUM\b", " ", s)
    s = re.sub(r"\bN\b(?=\s*\d)", " ", s)  # ponytail: lone N only when it prefixes digits ("N 3702")
    s = re.sub(r"[^A-Z0-9 ]", " ", s)
    toks = [_CANON.get(t, t) for t in s.split()]
    return re.sub(r"\s+", " ", " ".join(toks)).strip()


def addr_key(street, number, locality):
    """(norm_street, number, norm_locality); S/N street forces number None."""
    raw = (street or "").upper()
    sn = bool(_SN_RE.search(
        "".join(c for c in unicodedata.normalize("NFD", raw)
                if unicodedata.category(c) != "Mn")))
    return (norm_addr_text(street), None if sn else number, norm_addr_text(locality))


def scrape_province(code: str, timeout: float, throttle: float):
    """Returns (rows, warnings). rows: list of scraped SUCURSAL dicts with code."""
    warnings = []
    loc_raw = post_form({"action": "localidadesconsucursales", "provincia": code}, timeout)
    try:
        locs = json.loads(loc_raw).get("Localidades", {}).get("lista", []) or []
    except json.JSONDecodeError:
        warnings.append(f"{code}: localidades JSON parse failed")
        return [], warnings
    rows_all = []
    for loc in locs:
        lid = str(loc.get("id", ""))
        time.sleep(throttle)
        html = post_form({"action": "sucursales", "provincia": code, "localidad": lid,
                          "departamento": "", "nis": "", "servicios": ""}, timeout)
        frag_rows, w = parse_fragment(html)
        warnings += [f"{code}/{lid}: {x}" for x in w]
        rows_all.extend(frag_rows)
    rows = []
    for mk in rows_all:
        street_full, locality = (mk["addrline"].split("|", 1) + [""])[:2]
        street, number = split_street(street_full)
        rows.append({"kind": mk["kind"], "code": mk["code"], "name": norm(mk["name"]), "street": street, "number": number,
                     "locality": norm(locality), "lat": mk["lat"], "lng": mk["lng"], "services": mk["services"]})
    return rows, warnings


def main():
    repo = Path(__file__).resolve().parent.parent
    ap = argparse.ArgumentParser()
    ap.add_argument("--provinces", default="all")
    ap.add_argument("--throttle", type=float, default=0.3)
    ap.add_argument("--timeout", type=float, default=30)
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--json", default=str(repo / "VaultShop.DataAccess/SeedData/sucursales.json"))
    a = ap.parse_args()

    provinces = fetch_provinces(a.timeout)
    want = list(provinces) if a.provinces == "all" else [p.strip().upper() for p in a.provinces.split(",") if p.strip()]
    unknown = [p for p in want if p not in provinces]
    if unknown:
        raise SystemExit(f"unknown province codes: {unknown} (valid: {sorted(provinces)})")

    jpath = Path(a.json)
    rows = json.loads(jpath.read_text(encoding="utf-8"))
    by_code = {r["code"]: r for r in rows}
    now = datetime.datetime.now(datetime.timezone.utc).isoformat()
    want_set = set(want)

    total_new, total_matched, total_renamed, total_ambig = [], 0, 0, 0
    all_renames, all_ambig = [], []
    for prov in want:
        scraped, warns = scrape_province(prov, a.timeout, a.throttle)
        matched, new, name_warns, unmatched = 0, [], [], []
        renamed, ambig_warns = [], []
        done = set()
        # gist index for code-less (non-CABA) matching: exact (name, locality)
        # first, then unique-name + street/number corroboration
        gist_prov = [r for r in by_code.values() if r.get("provinceCode") == prov]
        by_nl, by_n, up_by_key = {}, {}, {}
        for r in gist_prov:
            by_nl.setdefault((norm(r.get("name")), norm(r.get("locality"))), []).append(r["code"])
            by_n.setdefault(norm(r.get("name")), []).append(r["code"])
            if (r.get("kind") or "SUCURSAL") == "UP":
                up_by_key.setdefault(addr_key(r.get("street"), r.get("number"), r.get("locality")), []).append(r["code"])

        def verify(code, tier):
            r = by_code[code]
            r["latitude"], r["longitude"] = s["lat"], s["lng"]
            r["lastVerifiedUtc"], r["source"] = now, "correo"
            r["services"], r["kind"] = s["services"], "SUCURSAL"
            done.add(code)
            if tier:
                name_warns.append(f"{code}: name-tier match ({tier}) site {s['name']!r} (gist kept)")

        def new_row(code, kind):
            by_code[code] = ensure_horario({"code": code, "name": s["name"], "street": s["street"],
                             "number": s["number"], "locality": s["locality"], "city": s["locality"],
                             "province": provinces[prov], "provinceCode": prov, "postalCode": "",
                             "latitude": s["lat"], "longitude": s["lng"],
                             "services": s["services"], "kind": kind,
                             "lastVerifiedUtc": now, "source": "correo"})
            new.append(code)
            done.add(code)

        for s in scraped:
            if s["kind"] == "UP":
                code = up_code(prov, s["name"], s["locality"], s["street"])
                if code in by_code:
                    r = by_code[code]  # idempotent re-run: refresh in place, site wins
                    r.update({"name": s["name"], "street": s["street"], "number": s["number"],
                              "locality": s["locality"], "city": s["locality"],
                              "latitude": s["lat"], "longitude": s["lng"],
                              "services": s["services"], "kind": "UP",
                              "lastVerifiedUtc": now, "source": "correo"})
                    done.add(code)
                    matched += 1
                    continue
                reuse = up_by_key.get(addr_key(s["street"], s["number"], s["locality"]), [])
                if len(reuse) == 1 and reuse[0] not in done:
                    r = by_code[reuse[0]]  # site renamed it: keep stable code, take site fields
                    old = r.get("name")
                    r.update({"name": s["name"], "street": s["street"], "number": s["number"],
                              "locality": s["locality"], "city": s["locality"],
                              "latitude": s["lat"], "longitude": s["lng"],
                              "services": s["services"], "kind": "UP",
                              "lastVerifiedUtc": now, "source": "correo"})
                    done.add(reuse[0])
                    renamed.append(f"{old} -> {s['name']}")
                    matched += 1
                    continue
                if code in by_code:  # hash collision with a different address
                    name_warns.append(f"UP hash collision: {code} {s['name']!r}@{s['locality']!r} (skipped)")
                    continue
                new_row(code, "UP")
                continue
            if s["code"] and s["code"] in by_code:
                r = by_code[s["code"]]
                if norm(r.get("name")) != s["name"]:
                    name_warns.append(f'{s["code"]}: site {s["name"]!r} vs gist {r.get("name")!r} (gist kept)')
                verify(s["code"], "")
                matched += 1
            elif s["code"]:
                new_row(s["code"], "SUCURSAL")
            else:
                cands = by_nl.get((s["name"], s["locality"]), [])
                tier = ""
                if len(cands) != 1:
                    same = by_n.get(s["name"], [])
                    corroborated = [c for c in same
                                    if norm(by_code[c].get("locality")) == s["locality"]
                                    or norm(by_code[c].get("street")) == s["street"]
                                    or (by_code[c].get("number") is not None
                                        and by_code[c].get("number") == s["number"])]
                    if len(same) == 1 and len(corroborated) == 1:
                        cands, tier = corroborated, "unique-name+field"
                if len(cands) == 1:
                    verify(cands[0], tier)
                    matched += 1
                else:
                    unmatched.append(s)
                    name_warns.append(f"no gist match: site {s['name']!r}@{s['locality']!r} candidates={cands}")
        # second pass: address match for still-gist-only SUCURSAL rows vs unmatched
        # site entries — on one-to-one match rename to the site name (site wins)
        gist_left = [r for r in gist_prov if r["code"] not in done and r.get("source") != "correo"
                     and (r.get("kind") or "SUCURSAL") == "SUCURSAL"]
        g_by_key, s_by_key = {}, {}
        for r in gist_left:
            g_by_key.setdefault(addr_key(r.get("street"), r.get("number"), r.get("locality")), []).append(r["code"])
        for s in unmatched:
            s_by_key.setdefault(addr_key(s["street"], s["number"], s["locality"]), []).append(s)
        for key in sorted(set(g_by_key) & set(s_by_key)):
            g, sl = g_by_key[key], s_by_key[key]
            if len(g) == 1 and len(sl) == 1:
                r, site = by_code[g[0]], sl[0]
                old = r.get("name")
                r["name"] = NAME_OVERRIDES.get(g[0], site["name"])
                if r["name"] != site["name"]:
                    name_warns.append(f'{g[0]}: override {site["name"]!r} -> {r["name"]!r}')
                r["latitude"], r["longitude"] = site["lat"], site["lng"]
                r["lastVerifiedUtc"], r["source"] = now, "correo"
                r["services"], r["kind"] = site["services"], "SUCURSAL"
                done.add(g[0])
                renamed.append(f"{old} -> {site['name']}")
                matched += 1
            else:
                ambig_warns.append(f"ambiguous address {key}: gist={sorted(g)}"
                                   f" site={[x['name'] + '@' + x['locality'] for x in sl]}")
        # backfill gist source + services/kind/horario on untouched rows of processed provinces
        for r in by_code.values():
            if r.get("provinceCode") == prov:
                if r.get("source") not in ("correo", "gist"):
                    r["source"] = "gist"
                r.setdefault("services", "")
                r.setdefault("kind", "SUCURSAL")
                ensure_horario(r)
        total_matched += matched
        total_new += new
        total_renamed += len(renamed)
        total_ambig += len(ambig_warns)
        all_renames += [f"{prov}/{x}" for x in renamed]
        all_ambig += [f"{prov}: {x}" for x in ambig_warns]
        unmatched_names = [f'{s["name"]}@{s["locality"]}' for s in unmatched]
        unverified = sorted(c for c, r in by_code.items()
                            if r.get("provinceCode") == prov and r.get("source") != "correo")
        n_suc = sum(1 for s in scraped if s["kind"] == "SUCURSAL")
        n_up = len(scraped) - n_suc
        print(f"{prov} {provinces[prov]}: scraped={len(scraped)}(SUC={n_suc} UP={n_up}) matched={matched} new={len(new)}"
              + (f" new={new}" if new else "")
              + f" renamed={len(renamed)}"
              + (f" unmatched-site={len(unmatched_names)}" + (f" {unmatched_names[:10]}" if unmatched_names else "") if unmatched_names else "")
              + f" unverified={len(unverified)}" + (f" unverified={unverified[:30]}" + ("..." if len(unverified) > 30 else "") if unverified else ""))
        for x in renamed:
            print(f"  rename: {x}")
        for w in (warns + name_warns + ambig_warns)[:10]:
            print(f"  warn: {w}")
        if len(warns) + len(name_warns) + len(ambig_warns) > 10:
            print(f"  warn: ... +{len(warns) + len(name_warns) + len(ambig_warns) - 10} more")
        time.sleep(a.throttle)

    # rows of unprocessed provinces: source/services/kind/horario backfill only
    for r in by_code.values():
        if r.get("provinceCode") not in want_set:
            if r.get("source") not in ("correo", "gist"):
                r["source"] = "gist"
            r.setdefault("services", "")
            r.setdefault("kind", "SUCURSAL")
            ensure_horario(r)

    out = sorted(by_code.values(), key=lambda r: r["code"])
    # ponytail: confirm-only — this script never deletes rows; quarantine
    # candidates go to the sidecar changelist for human review instead.
    assert len(out) >= len(rows), f"refresh must never delete rows ({len(rows)} -> {len(out)})"
    changelist = {"generated_utc": now, "json": str(jpath), "provinces": want,
                  "candidates": [{"code": r["code"], "name": r.get("name"),
                                  "locality": r.get("locality"),
                                  "reason": "unverified-after-scrape"}
                                 for r in out
                                 if r.get("provinceCode") in want_set and r.get("source") != "correo"]}
    changelist_path = jpath.with_name(jpath.stem + "-changelist.json")
    n_correo = sum(1 for r in out if r.get("source") == "correo")
    n_up = sum(1 for r in out if r.get("kind") == "UP")
    n_40 = sum(1 for r in out if "40" in (r.get("services") or "").split(","))
    print(f"TOTAL rows={len(out)} verified(correo)={n_correo} gist-only={len(out) - n_correo} UP={n_up} with-service-40={n_40} new-this-run={len(total_new)} renamed={total_renamed} ambiguous={total_ambig}")
    if all_ambig:
        print(f"AMBIGUOUS ({len(all_ambig)}):")
        for w in all_ambig[:30]:
            print(f"  ambig: {w}")
        if len(all_ambig) > 30:
            print(f"  ambig: ... +{len(all_ambig) - 30} more")
    if a.dry_run:
        print("dry-run: no write")
        print(f"dry-run: changelist not written ({len(changelist['candidates'])} candidates)")
    else:
        jpath.write_text(json.dumps(out, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        print(f"wrote {jpath}")
        changelist_path.write_text(json.dumps(changelist, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        print(f"wrote {changelist_path} ({len(changelist['candidates'])} quarantine candidates)")


if __name__ == "__main__":
    main()
