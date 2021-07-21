#!/bin/bash

for vaule in {1..100}
do
    curl localhost:50024/www.spotify.com/icon.png --output /dev/null
done

echo "Testing 100"
