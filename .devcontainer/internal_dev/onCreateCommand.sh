#!/usr/bin/env bash
export REPO_ROOT="$(git rev-parse --show-toplevel)"

file="$REPO_ROOT/dev/custom-root-ca.crt"

if [ -e "$file" ]; then
  echo "Adding custom root CA"
  sudo cp "$file" /usr/local/share/ca-certificates/
  sudo update-ca-certificates
else
  echo "No custom root CA found, skipping..."
fi
