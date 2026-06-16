###############################################
#           Node.js build stage               #
###############################################
# Chainguard's minimal `node:latest` image has no shell, no npm, and runs as a
# nonroot user, so it cannot run `npm ci`/`npm run build`. The `-dev` variant
# includes npm + a shell and runs as root, which is what a build stage needs.
# Nothing from this stage ships in the runtime image — only the built wwwroot is
# copied forward — so the larger dev image has no impact on the final image.
FROM --platform=$BUILDPLATFORM cgr.dev/chainguard/node:latest-dev AS node-build

USER root
WORKDIR /app
COPY src/Admin/package*.json ./
COPY /src/Admin/ .
RUN npm ci
RUN npm run build

###############################################
#                 Build stage                 #
###############################################
# `-dev` is required: every RUN below is shell-form (RID detection, sourcing
# /tmp/rid.txt, dotnet restore/publish). The minimal `dotnet-sdk:latest` has no
# shell and runs as nonroot, so those steps would fail.
FROM --platform=$BUILDPLATFORM cgr.dev/chainguard/dotnet-sdk:latest-dev AS build

USER root

# Docker buildx supplies the value for this arg
ARG TARGETPLATFORM

# Determine proper runtime value for .NET.
# Chainguard (Wolfi) is glibc-based, so we target the glibc RIDs (linux-x64),
# NOT the musl RIDs the Alpine-based image used — a musl self-contained binary
# will not run on the glibc aspnet-runtime base below.
RUN if [ "$TARGETPLATFORM" = "linux/amd64" ]; then \
  RID=linux-x64 ; \
  elif [ "$TARGETPLATFORM" = "linux/arm64" ]; then \
  RID=linux-arm64 ; \
  elif [ "$TARGETPLATFORM" = "linux/arm/v7" ]; then \
  RID=linux-arm ; \
  fi \
  && echo "RID=$RID" > /tmp/rid.txt

# Copy required project files
WORKDIR /source
COPY . ./

# Restore project dependencies and tools
WORKDIR /source/src/Admin
RUN . /tmp/rid.txt && dotnet restore -r $RID

# Build project
RUN . /tmp/rid.txt && dotnet publish \
  -c release \
  --no-restore \
  --self-contained \
  /p:PublishSingleFile=true \
  -r $RID \
  -o out

###############################################
#                  App stage                  #
###############################################
# `-dev` is required: entrypoint.sh runs as root, uses /bin/sh and the shadow
# utilities (groupadd/usermod/mkhomedir_helper) plus gosu to step down to the
# bitwarden user. The minimal `aspnet-runtime:latest` (distroless, nonroot, no
# shell) cannot execute this entrypoint. Using `-dev` re-introduces a shell,
# package manager, and root — so much of the distroless hardening is traded
# away; running as the built-in nonroot user instead would be the longer-term
# Chainguard-native path, but that requires reworking entrypoint.sh.
FROM cgr.dev/chainguard/aspnet-runtime:latest-dev

USER root

ARG TARGETPLATFORM
LABEL com.bitwarden.product="bitwarden"
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5000
ENV SSL_CERT_DIR=/etc/bitwarden/ca-certificates
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
EXPOSE 5000

# Wolfi packages (apk), not Alpine: `icu-libs` -> `icu`, and the Alpine
# edge/community repo used for gosu is dropped (its keys/repo are incompatible
# with Wolfi's apk). gosu is pulled from Wolfi's default repos.
RUN apk add --no-cache curl \
  icu \
  tzdata \
  krb5 \
  shadow \
  gosu

# Copy app from the build stage
WORKDIR /app
COPY --from=build /source/src/Admin/out /app
COPY --from=node-build /app/wwwroot /app/wwwroot
COPY ./src/Admin/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
HEALTHCHECK CMD curl -f http://localhost:5000/alive || exit 1

ENTRYPOINT ["/entrypoint.sh"]
