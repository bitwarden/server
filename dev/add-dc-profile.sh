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
fi
