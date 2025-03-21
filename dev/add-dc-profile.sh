#!/usr/bin/env bash
if [[ -z "${CODESPACES}" ]]; then
  if [[ -z "${HOSTNAME}" ]]; then
    # Add docker compose profile using local strategy
    echo "Locally"
    # No need to patch it into another compose project when ran locally
    docker compose --profile "$1" up -d
  else
    # Add docker compose profile to dev container
    # Is this definitely always the same thing as codespaces?
    echo "In DevContainer"
    source_dir="$( dirname "${BASH_SOURCE[0]}")"
    echo $source_dir
    docker compose -p bitwarden_common --profile "$1" -f "${source_dir}/docker-compose.yml" up -d
  fi
else
  # Add docker compose profile to codespace
  echo "In Codespaces"
  source_dir="$( dirname "${BASH_SOURCE[0]}")"
  echo $source_dir
  docker compose -p bitwarden_common --profile "$1" -f "${source_dir}/docker-compose.yml" up -d
fi
