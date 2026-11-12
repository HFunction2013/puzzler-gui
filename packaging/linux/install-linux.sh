#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
app_root="$(cd "$script_dir/.." && pwd)"
data_home="${XDG_DATA_HOME:-$HOME/.local/share}"
bin_home="${XDG_BIN_HOME:-$HOME/.local/bin}"
install_dir="$data_home/puzzler-gui"

mkdir -p "$install_dir" "$bin_home" \
  "$data_home/applications" \
  "$data_home/mime/packages" \
  "$data_home/icons/hicolor/256x256/apps"

find "$app_root" -mindepth 1 -maxdepth 1 ! -name linux -exec cp -R {} "$install_dir/" \;

cat > "$bin_home/puzzler-gui" <<EOF
#!/bin/sh
exec "$install_dir/PuzzlerGui" "\$@"
EOF
chmod +x "$bin_home/puzzler-gui"

sed "s|@EXEC@|$bin_home/puzzler-gui|" \
  "$script_dir/puzzler-gui.desktop.in" > "$data_home/applications/puzzler-gui.desktop"

cp "$script_dir/puzzler-gui.xml" "$data_home/mime/packages/puzzler-gui.xml"
cp "$app_root/puzzler-gui.png" "$data_home/icons/hicolor/256x256/apps/puzzler-gui.png" 2>/dev/null || true

update-mime-database "$data_home/mime" 2>/dev/null || true
update-desktop-database "$data_home/applications" 2>/dev/null || true

echo "Installed puzzler-gui to $install_dir"
echo "Launch it with: puzzler-gui"
