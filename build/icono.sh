#!/usr/bin/env bash
# Genera los iconos de la aplicación a partir de icono.ps (vectorial).
#   Salida: icono-1024.png, CompresorPdf.icns (macOS), CompresorPdf.ico (Windows)
set -euo pipefail
cd "$(dirname "$0")"

command -v gs >/dev/null || { echo "Falta Ghostscript para generar el icono."; exit 1; }

echo "→ Renderizando icono base 1024x1024"
gs -sDEVICE=pngalpha -dGraphicsAlphaBits=4 -dTextAlphaBits=4 -dNOPAUSE -dBATCH -dQUIET \
   -g1024x1024 -dDEVICEWIDTHPOINTS=1024 -dDEVICEHEIGHTPOINTS=1024 -dFIXEDMEDIA \
   -sOutputFile=icono-1024.png icono.ps

if command -v iconutil >/dev/null; then
  echo "→ Construyendo CompresorPdf.icns"
  rm -rf CompresorPdf.iconset && mkdir CompresorPdf.iconset
  # macOS exige exactamente estos nombres y tamaños.
  for par in "16 16x16" "32 16x16@2x" "32 32x32" "64 32x32@2x" \
             "128 128x128" "256 128x128@2x" "256 256x256" "512 256x256@2x" \
             "512 512x512" "1024 512x512@2x"; do
    set -- $par
    sips -z "$1" "$1" icono-1024.png --out "CompresorPdf.iconset/icon_$2.png" >/dev/null
  done
  iconutil -c icns CompresorPdf.iconset -o CompresorPdf.icns
  rm -rf CompresorPdf.iconset
fi

# .ico para Windows: un contenedor con los tamaños que usa el Explorador.
echo "→ Construyendo CompresorPdf.ico"
python3 - <<'PY'
import struct, subprocess, pathlib
tamanos = [16, 24, 32, 48, 64, 128, 256]
pngs = []
for t in tamanos:
    salida = pathlib.Path(f"_ico_{t}.png")
    subprocess.run(["sips", "-z", str(t), str(t), "icono-1024.png", "--out", str(salida)],
                   check=True, capture_output=True)
    pngs.append((t, salida.read_bytes()))

# Formato ICO con entradas PNG embebidas (soportado desde Windows Vista).
cabecera = struct.pack("<HHH", 0, 1, len(pngs))
desplazamiento = 6 + 16 * len(pngs)
entradas, cuerpo = b"", b""
for t, datos in pngs:
    entradas += struct.pack("<BBBBHHII", t if t < 256 else 0, t if t < 256 else 0,
                            0, 0, 1, 32, len(datos), desplazamiento)
    cuerpo += datos
    desplazamiento += len(datos)
pathlib.Path("CompresorPdf.ico").write_bytes(cabecera + entradas + cuerpo)
for _, _ in pngs: pass
for t in tamanos: pathlib.Path(f"_ico_{t}.png").unlink()
print(f"   {len(pngs)} tamaños embebidos")
PY

echo "✓ Iconos generados"
