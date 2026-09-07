#!/usr/bin/env bash
# Empaqueta la aplicación para macOS: .app + .dmg  (RNF-04)
#
#   ./publicar-macos.sh [arm64|x64|universal]
#
# Firma y notarización (opcionales, requieren cuenta Apple Developer):
#   IDENTIDAD_FIRMA="Developer ID Application: Tu Nombre (TEAMID)" ./publicar-macos.sh
#   PERFIL_NOTARIZACION="mi-perfil" ./publicar-macos.sh     # ver notarytool store-credentials
#
# IMPORTANTE: este paquete NO incluye Ghostscript, y no debe incluirlo (ADR-003, AGPL).
set -euo pipefail
cd "$(dirname "$0")/.."

ARQ="${1:-arm64}"
VERSION="$(grep -m1 '<Version>' src/CompresorPdf.App/CompresorPdf.App.csproj 2>/dev/null \
           | sed -E 's/.*<Version>(.*)<\/Version>.*/\1/' || true)"
VERSION="${VERSION:-1.0.0}"

NOMBRE_APP="Compresor de PDFs"
ID_PAQUETE="com.davidaucancela.compresorpdf"
SALIDA="artifacts/macos"
BUNDLE="$SALIDA/$NOMBRE_APP.app"

echo "══ Compresor de PDFs $VERSION · macOS/$ARQ ══"

# --- 1. Publicar el binario ------------------------------------------------
publicar() {  # $1 = RID, $2 = destino
  dotnet publish src/CompresorPdf.App/CompresorPdf.App.csproj \
    -c Release -r "$1" --self-contained true \
    -p:PublishSingleFile=false -p:DebugType=none \
    -o "$2" --nologo -v q
}

rm -rf "$SALIDA"
mkdir -p "$SALIDA"

if [ "$ARQ" = "universal" ]; then
  echo "→ Publicando arm64 y x64 para binario universal"
  publicar osx-arm64 "$SALIDA/_arm64"
  publicar osx-x64   "$SALIDA/_x64"
  cp -R "$SALIDA/_arm64" "$SALIDA/_app"
  # Sólo el ejecutable y las librerías nativas necesitan fusionarse.
  for f in "$SALIDA/_arm64"/*; do
    nombre="$(basename "$f")"
    if file "$f" 2>/dev/null | grep -q "Mach-O"; then
      lipo -create "$SALIDA/_arm64/$nombre" "$SALIDA/_x64/$nombre" \
           -output "$SALIDA/_app/$nombre" 2>/dev/null || true
    fi
  done
  rm -rf "$SALIDA/_arm64" "$SALIDA/_x64"
  CONTENIDO="$SALIDA/_app"
else
  echo "→ Publicando osx-$ARQ (self-contained)"
  publicar "osx-$ARQ" "$SALIDA/_app"
  CONTENIDO="$SALIDA/_app"
fi

# --- 2. Montar el bundle .app ----------------------------------------------
echo "→ Montando $NOMBRE_APP.app"
mkdir -p "$BUNDLE/Contents/MacOS" "$BUNDLE/Contents/Resources"
cp -R "$CONTENIDO"/* "$BUNDLE/Contents/MacOS/"
rm -rf "$CONTENIDO"

[ -f build/CompresorPdf.icns ] || build/icono.sh
cp build/CompresorPdf.icns "$BUNDLE/Contents/Resources/"

cat > "$BUNDLE/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key>              <string>$NOMBRE_APP</string>
  <key>CFBundleDisplayName</key>       <string>$NOMBRE_APP</string>
  <key>CFBundleIdentifier</key>        <string>$ID_PAQUETE</string>
  <key>CFBundleVersion</key>           <string>$VERSION</string>
  <key>CFBundleShortVersionString</key><string>$VERSION</string>
  <key>CFBundleExecutable</key>        <string>CompresorPdf</string>
  <key>CFBundleIconFile</key>          <string>CompresorPdf</string>
  <key>CFBundlePackageType</key>       <string>APPL</string>
  <key>LSMinimumSystemVersion</key>    <string>12.0</string>
  <key>NSHighResolutionCapable</key>   <true/>
  <key>LSApplicationCategoryType</key> <string>public.app-category.productivity</string>
  <key>CFBundleDocumentTypes</key>
  <array>
    <dict>
      <key>CFBundleTypeName</key>     <string>PDF</string>
      <key>CFBundleTypeRole</key>     <string>Editor</string>
      <key>LSItemContentTypes</key>   <array><string>com.adobe.pdf</string></array>
      <key>LSHandlerRank</key>        <string>Alternate</string>
    </dict>
  </array>
</dict>
</plist>
PLIST

chmod +x "$BUNDLE/Contents/MacOS/CompresorPdf"
# Refresca la caché de iconos de Finder para que el icono aparezca ya.
touch "$BUNDLE"

# --- 3. Firma (opcional) ----------------------------------------------------
if [ -n "${IDENTIDAD_FIRMA:-}" ]; then
  echo "→ Firmando con: $IDENTIDAD_FIRMA"
  cat > "$SALIDA/derechos.plist" <<'ENT'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <!-- .NET necesita JIT: sin estas dos, la app firmada no arranca. -->
  <key>com.apple.security.cs.allow-jit</key>                            <true/>
  <key>com.apple.security.cs.allow-unsigned-executable-memory</key>     <true/>
  <key>com.apple.security.cs.disable-library-validation</key>           <true/>
</dict>
</plist>
ENT
  find "$BUNDLE/Contents/MacOS" -type f \( -name "*.dylib" -o -perm +111 \) -print0 \
    | xargs -0 -I{} codesign --force --timestamp --options runtime \
        --entitlements "$SALIDA/derechos.plist" --sign "$IDENTIDAD_FIRMA" {} 2>/dev/null || true
  codesign --force --timestamp --options runtime \
    --entitlements "$SALIDA/derechos.plist" --sign "$IDENTIDAD_FIRMA" "$BUNDLE"
  codesign --verify --deep --strict --verbose=2 "$BUNDLE"
else
  echo "→ Sin firmar (define IDENTIDAD_FIRMA para firmar)."
  echo "  Gatekeeper la bloqueará en otros equipos: clic derecho › Abrir para saltarlo."
fi

# --- 4. Empaquetar el .dmg --------------------------------------------------
echo "→ Creando el .dmg"
DMG="$SALIDA/CompresorPdf-$VERSION-$ARQ.dmg"
STAGE="$SALIDA/_dmg"
rm -rf "$STAGE" && mkdir -p "$STAGE"
cp -R "$BUNDLE" "$STAGE/"
ln -s /Applications "$STAGE/Applications"      # el clásico "arrastra aquí"
hdiutil create -volname "$NOMBRE_APP" -srcfolder "$STAGE" -ov -format UDZO -quiet "$DMG"
rm -rf "$STAGE"

# --- 5. Notarización (opcional) ---------------------------------------------
if [ -n "${PERFIL_NOTARIZACION:-}" ]; then
  echo "→ Notarizando (puede tardar varios minutos)"
  xcrun notarytool submit "$DMG" --keychain-profile "$PERFIL_NOTARIZACION" --wait
  xcrun stapler staple "$DMG"
fi

echo
echo "✓ App: $BUNDLE"
echo "✓ DMG: $DMG  ($(du -h "$DMG" | cut -f1))"
echo
echo "Recuerda: el usuario necesita Ghostscript instalado (brew install ghostscript)."
