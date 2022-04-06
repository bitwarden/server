#!/bin/sh

ufw allow ssh
ufw allow 'Bitwarden'

ufw --force enable
