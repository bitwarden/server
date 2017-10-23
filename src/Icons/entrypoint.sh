#!/bin/sh

/etc/iconserver/iconserver &
dotnet /app/Icons.dll iconsSettings:bestIconBaseUrl=http://localhost:8080
