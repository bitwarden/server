dotnet publish ../src/Api/Api.csproj -f netcoreapp2.0 -o obj/Docker/publish -c "Release"
dotnet publish ../src/Identity/Identity.csproj -f netcoreapp2.0 -o obj/Docker/publish -c "Release"

docker-compose pull
docker-compose down

#mkdir -p c:/bitwarden/letsencrypt/live
#docker run -it --rm -p 80:80 -v c:/bitwarden/letsencrypt:/etc/letsencrypt/ certbot/certbot certonly --standalone --noninteractive --preferred-challenges http --email kyle.spearrin@gmail.com --agree-tos -d bw.kylespearrin.com
#openssl dhparam -out c:/bitwarden/letsencrypt/live/bw.kylespearrin.com/dhparam.pem 2048

docker-compose up -d
