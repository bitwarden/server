#!/usr/bin/env bash

cp /etc/bitwarden/nginx/default.conf /etc/nginx/conf.d/default.conf
nginx -g 'daemon off;'
