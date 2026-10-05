#!/bin/zsh
# Publishes the newest APK where shared builds update from: uploads Kartel-<code>.apk and version.json to the public
# R2 bucket. Every installed build checks version.json on launch (AppUpdate) and offers a newer one on the title.
#   ./tools/publish-apk.sh            the APK for ProjectSettings/versionCode.txt (build it first)
# Needs: wrangler logged in (cd worker && npx wrangler whoami), the bucket below, and its public URL in BASE (the
# same URL AppUpdate.Manifest points at, minus /version.json).
set -e
cd "$(dirname "$0")/.."
BUCKET=${BUCKET:-kartel}
BASE=${BASE:-}
[[ -n "$BASE" ]] || BASE=$(sed -n 's|.*Manifest = "\(.*\)/version.json".*|\1|p' Assets/_Game/Scripts/Boot/AppUpdate.cs)
[[ -n "$BASE" ]] || { echo "no public URL: set AppUpdate.Manifest (or BASE=https://pub-....r2.dev)"; exit 2; }
CODE=$(cat ProjectSettings/versionCode.txt)
APK=$(ls Builds/Android/kartcup-*-"$CODE".apk)
NAME="Kartel-$CODE.apk"
TMP=$(mktemp -d)
cd worker
npx wrangler r2 object put "$BUCKET/$NAME" --file "../$APK" --content-type application/vnd.android.package-archive --remote
print -r -- "{\"versionCode\": $CODE, \"version\": \"$CODE\", \"url\": \"$BASE/$NAME\"}" > "$TMP/version.json"
npx wrangler r2 object put "$BUCKET/version.json" --file "$TMP/version.json" --content-type application/json --cache-control "no-cache" --remote
# the newest build under a fixed name too, for links that should never go stale (the invite page's "Get Kartel")
npx wrangler r2 object put "$BUCKET/Kartel.apk" --file "../$APK" --content-type application/vnd.android.package-archive --cache-control "no-cache" --remote
echo "published build $CODE: $BASE/$NAME  (always newest: $BASE/Kartel.apk)"
