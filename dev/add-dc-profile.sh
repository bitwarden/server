#!/usr/bin/env bash
if [[ -z "${CODESPACES}" ]]; then
  if [[ -z "${HOSTNAME}" ]]; then
    # Add docker compose profile using local strategy
    echo "Locally"
  else
    # Add docker compose profile to dev container
    echo "In DevContainer"
  fi
else
  # Add docker compose profile to codespace
  echo "In Codespaces"
  source_dir="$( dirname "${BASH_SOURCE[0]}")"
  echo $source_dir
  docker compose -p bitwarden_common --profile "$1" -f "${source_dir}/docker-compose.yml" up -d
fi
