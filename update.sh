#!/bin/sh
# Run this from /mnt/user/appdata/unicorn-game/ to pull the latest code and rebuild.
# Usage: sh update.sh
set -e

DEST=$(dirname "$(readlink -f "$0")")
TMP=$(mktemp -d)

echo "Downloading latest code..."
wget -q -O "$TMP/u.zip" \
  "https://github.com/perdeg/ellies_unicorn_game/archive/refs/heads/claude/unicorn-game-development-sWgKZ.zip"

echo "Extracting..."
unzip -q -o "$TMP/u.zip" -d "$TMP"

echo "Copying files..."
SRC=$(ls -d "$TMP"/ellies_unicorn_game-*/)
cp -r "${SRC}public"  "$DEST/"
cp -r "${SRC}server"  "$DEST/"

rm -rf "$TMP"

echo "Rebuilding container..."
cd "$DEST"
docker compose down
docker compose up -d --build

echo "Done! Game is running."
