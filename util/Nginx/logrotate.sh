#!/bin/sh

while [ "$NGINX_LOGROTATE" != "0" ]
do
  sleep $((24 * 3600 - (`date +%H` * 3600 + `date +%M` * 60 + `date +%S`)))
  ts=$(date +%Y%m%d_%H%M%S)
  mv /var/log/nginx/access.log /var/log/nginx/access.$ts.log
  mv /var/log/nginx/error.log /var/log/nginx/error.$ts.log
  kill -USR1 `cat /var/run/nginx/nginx.pid`
  sleep 1
  gzip /var/log/nginx/access.$ts.log
  gzip /var/log/nginx/error.$ts.log
  find /var/log/nginx/ -name "*.gz" -mtime +32 -delete
done
