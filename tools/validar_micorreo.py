#!/usr/bin/env python3
"""Valida sucursales.json contra la fuente de verdad de MiCorreo (1 a 1 por codigo).

Uso:
    py tools/validar_micorreo.py data/micorreo-BA.json --province B

- Filas de la provincia con codigo real (letra + digitos) presentes en MiCorreo:
  se marcan source="micorreo" + lastVerifiedUtc=ahora (UTC).
- Filas de la provincia con codigo real AUSENTES en MiCorreo:
  se mueven a VaultShop.DataAccess/SeedData/sucursales-pendientes.json
  (para revision manual) y se quitan de sucursales.json.
- Filas UP-* (codigos sinteticos nuestros): no comparables 1:1 por codigo
  (MiCorreo no expone coordenadas/direccion) — se dejan intactas y se reportan.
- Codigos de MiCorreo ausentes en nuestro archivo: se guardan en
  data/micorreo-faltantes-{PROV}.json para decidir altas luego.
- Resto de provincias: intacto. Re-ejecutable por provincia.

Solo stdlib. No toca codigo C# ni base de datos (el seed upsertea por Code).
"""
import argparse
import json
import re
import sys
import unicodedata
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SUCURSALES = ROOT / "VaultShop.DataAccess" / "SeedData" / "sucursales.json"
PENDIENTES = ROOT / "VaultShop.DataAccess" / "SeedData" / "sucursales-pendientes.json"
REAL_CODE = re.compile(r"^[A-Z]\d+$")  # namespace de codigos oficiales (B6633, O7870...)


def load_json(path):
    """Acepta un JSON o varios bloques pegados separados por ';'
    (formato manual provincia por provincia): devuelve lista de bloques."""
    text = Path(path).read_text(encoding="utf-8")
    try:
        return [json.loads(text)]
    except json.JSONDecodeError:
        pass
    blocks = []
    blocks = []
    depth = in_str = esc = 0
    start = 0
    for i, ch in enumerate(text):
        if in_str:
            if esc:
                esc = False
            elif ch == "\\":
                esc = True
            elif ch == '"':
                in_str = False
        elif ch == '"':
            in_str = True
        elif ch in "{[":
            depth += 1
        elif ch in "}]":
            depth -= 1
        elif ch == ";" and depth == 0:
            chunk = text[start:i].strip()
            if chunk:
                blocks.append(json.loads(chunk))
            start = i + 1
    tail = text[start:].strip()
    if tail:
        blocks.append(json.loads(tail))
    return blocks


def dump_json(path, data):
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write("\n")


def norm_tokens(s):
    s = "".join(c for c in unicodedata.normalize("NFD", (s or "").upper()) if unicodedata.category(c) != "Mn")
    return set(re.findall(r"[A-Z0-9]+", s))


def base_letters(s):
    """Ñ→N, tildes→vocal base. U+FFFD (ya roto en origen) queda igual: irrecuperable."""
    return "".join(c for c in unicodedata.normalize("NFD", (s or "")) if unicodedata.category(c) != "Mn")


def split_domicilio(dom):
    dom = re.sub(r"\s+", " ", (dom or "").strip()).upper()
    m = re.match(r"^(.*?)\s+(\d+)\s*$", dom)
    if m:
        return m.group(1).strip(), int(m.group(2))
    return dom, None


def ingest_detail(path, prov, kept, by_kept, truth_by_code, now, moved_rows):
    """Enriquece con getCoordenadas: coords+domicilio oficiales, alta de faltantes,
    y mueve a pendientes los UP-* sinteticos reemplazados por un codigo real."""
    raw = load_json(path)
    entries = []
    for b in raw:
        items = b if isinstance(b, list) else [b]
        for it in items:
            # ponytail: getCoordenadas viene envuelto [{..., sucursales_provincia:[...]}]; tambien acepta agencia plana
            entries.extend(it.get("sucursales_provincia", []) if isinstance(it, dict) else [])
    cp_map = {c: (t.get("cp") or "") for c, t in truth_by_code.items()}
    prov_name = next((r.get("province") for r in kept if (r.get("provinceCode") or "").upper() == prov), "")

    corrected = altas = superseded = 0
    for e in entries:
        code = (e.get("codigoSucursal") or "").strip().upper()
        if not code:
            continue
        if (e.get("codProvincia") or "").strip().upper() not in ("", prov):
            continue  # ponytail: el detalle puede acumular varias provincias; solo la pedida
        try:
            lat, lon = float(e["latitud"]), float(e["longitud"])
        except (KeyError, TypeError, ValueError):
            print(f"  ! {code} sin coords, omitida")
            continue
        street, number = split_domicilio(e.get("domicilio"))
        loc = base_letters(re.sub(r"\s+", " ", (e.get("localidad") or "").strip()).upper())
        hor = re.sub(r"\s+", " ", (e.get("horario") or "").strip())
        row = by_kept.get(code)
        if row is None:
            row = {
                "code": code, "name": base_letters((e.get("descripcion") or "").strip().upper()),
                "street": base_letters(street), "number": number, "locality": loc, "city": loc,
                "province": prov_name or (e.get("nombreProvincia") or "").strip().upper(),
                "provinceCode": prov, "postalCode": cp_map.get(code, ""),
                "latitude": lat, "longitude": lon,
                "source": "micorreo", "lastVerifiedUtc": now, "services": "",
                "kind": "UP" if (e.get("tipoSucursalLocker") or "").strip().upper() == "UP" else "SUCURSAL",
                "horario": hor,
            }
            kept.append(row)
            by_kept[code] = row
            altas += 1
            print(f"  + alta {code} | {row['name']} | {loc} | {lat},{lon}")
        else:
            dlat = abs((row.get("latitude") or 0) - lat)
            dlon = abs((row.get("longitude") or 0) - lon)
            if dlat > 0.01 or dlon > 0.01:
                print(f"  ~ {code} coords {row.get('latitude')},{row.get('longitude')} -> {lat},{lon}")
                corrected += 1
            row.update(latitude=lat, longitude=lon, street=base_letters(street), number=number,
                       locality=loc or row.get("locality"), horario=hor,
                       source="micorreo", lastVerifiedUtc=now)
        find_superseded(kept, row, now, moved_rows)
        superseded += 1 if row.pop("_sup", None) else 0
    return corrected, altas, superseded


def find_superseded(kept, real, now, moved_rows):
    """Mueve a pendientes los UP-* sinteticos del mismo punto: igual localidad,
    igual numero (columna o embebido en calle) y >=1 token de calle largo compartido."""
    toks = {t for t in norm_tokens(real["street"]) if len(t) >= 4}
    real_num = real.get("number")
    for r in list(kept):
        if r is real or not (r.get("code") or "").startswith("UP-"):
            continue
        if (r.get("locality") or "").upper() != (real.get("locality") or "").upper():
            continue
        r_street, r_num = split_domicilio(r.get("street"))
        if (r.get("number") or r_num) != real_num or not real_num:
            continue
        if not (toks & {t for t in norm_tokens(r_street) if len(t) >= 4}):
            continue
        r["pendingReason"] = f"superseded-by-{real['code']}-2026-09"
        r["pendingUtc"] = now
        kept.remove(r)
        moved_rows.append(r)
        real["_sup"] = True
        print(f"  - {r['code']} reemplazada por {real['code']} -> pendientes")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("truth", help="Respuesta de MiCorreo guardada tal cual ({\"sucursales\": [...]})")
    ap.add_argument("--province", required=True, help="Codigo de provincia, ej. B")
    ap.add_argument("--detail", default=None, help="Respuesta getCoordenadas guardada (enriquece coords/domicilio, da de alta faltantes)")
    ap.add_argument("--sucursales", default=str(SUCURSALES))
    ap.add_argument("--pendientes", default=str(PENDIENTES))
    args = ap.parse_args()

    prov = args.province.strip().upper()
    truth_items = []
    for b in load_json(args.truth):
        truth_items += b["sucursales"] if isinstance(b, dict) else b
    truth_by_code = {}
    for t in truth_items:
        if not t.get("codigoSucursal"):
            continue
        # ponytail: el archivo acumula todas las provincias; solo la pedida
        if (t.get("codProvincia") or "").strip().upper() not in ("", prov):
            continue
        truth_by_code[t["codigoSucursal"].strip().upper()] = t
    print(f"MiCorreo: {len(truth_by_code)} codigos (provincia {prov})")

    rows = load_json(args.sucursales)[0]
    by_code = {r["code"]: r for r in rows}
    now = datetime.now(timezone.utc).isoformat()

    matched = moved = skipped_up = 0
    moved_rows = []
    for r in rows:
        if (r.get("provinceCode") or "").upper() != prov:
            continue
        if not REAL_CODE.match(r.get("code") or ""):
            skipped_up += 1  # ponytail: UP-* sinteticos no matchean por codigo; decision manual aparte
            continue
        if r["code"] in truth_by_code:
            r["source"] = "micorreo"
            r["lastVerifiedUtc"] = now
            matched += 1
        else:
            r["pendingReason"] = f"no-micorreo-{prov}-2026-09"
            r["pendingUtc"] = now
            moved_rows.append(r)
            moved += 1

    moved_codes = {r["code"] for r in moved_rows}
    kept = [r for r in rows if r["code"] not in moved_codes]
    by_kept = {r["code"]: r for r in kept}

    corrected = altas = superseded = 0
    if args.detail:
        corrected, altas, superseded = ingest_detail(args.detail, prov, kept, by_kept, truth_by_code, now, moved_rows)
    for r in kept:
        for k in ("name", "street", "locality", "city"):
            if r.get(k):
                r[k] = base_letters(r[k])
    dump_json(args.sucursales, kept)

    # pendientes: merge sin duplicar
    pendientes = []
    if Path(args.pendientes).exists():
        pendientes = load_json(args.pendientes)[0]
    seen = {r.get("code") for r in pendientes}
    pendientes.extend(r for r in moved_rows if r["code"] not in seen)
    dump_json(args.pendientes, pendientes)

    # verdad no encontrada en nuestro archivo -> candidatas a alta (post-ingest, para no listar las recient dadas de alta)
    final_codes = {r["code"] for r in kept}
    faltantes = [t for c, t in sorted(truth_by_code.items()) if c not in final_codes]
    falt_path = ROOT / "data" / f"micorreo-faltantes-{prov}.json"
    falt_path.parent.mkdir(exist_ok=True)
    dump_json(str(falt_path), faltantes)

    print(f"Marcadas micorreo: {matched}")
    print(f"Movidas a pendientes: {moved + superseded} -> {Path(args.pendientes).name} (total acumulado: {len(pendientes)})")
    if args.detail:
        print(f"Detalle: coords corregidas {corrected}, altas {altas}, sinteticas reemplazadas {superseded}")
    print(f"Sinteticas UP-* intactas (no comparables por codigo): {skipped_up}")
    print(f"Codigos MiCorreo ausentes en sucursales.json: {len(faltantes)} -> {falt_path.name}")

    # muestra para revision: no-matcheadas reales (primeras 15) y faltantes (primeras 15)
    print("\nMovidas (muestra):")
    for r in moved_rows[:15]:
        print(f"  {r['code']} | {r['name']} | {r.get('locality')} | src={r.get('source')}")
    print("\nFaltantes en nuestro archivo (muestra):")
    for t in faltantes[:15]:
        print(f"  {t['codigoSucursal']} | {t.get('descripcion')} | cp={t.get('cp')}")


if __name__ == "__main__":
    sys.exit(main())
