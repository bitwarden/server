FROM microsoft/dotnet:2.0.0-preview2-runtime-jessie

# FROM https://github.com/aspnet/aspnet-docker/blob/master/1.1/jessie/runtime/Dockerfile

# set up network
ENV ASPNETCORE_URLS http://+:80

# set env var for packages cache
ENV DOTNET_HOSTING_OPTIMIZATION_CACHE /packagescache

# set up package cache and other tools
RUN for version in '1.1.2' '1.1.3'; do \
        curl -o /tmp/aspnetcore.cache.$version.tar.gz \
            https://dist.asp.net/packagecache/$version/debian.8-x64/aspnetcore.cache.tar.gz \
        && mkdir -p /packagescache && cd /packagescache \
        && tar xf /tmp/aspnetcore.cache.$version.tar.gz \
        && rm /tmp/aspnetcore.cache.$version.tar.gz; \
done

# Custom

WORKDIR /app
EXPOSE 80
COPY obj/Docker/publish .
ENTRYPOINT ["dotnet", "Api.dll"]
