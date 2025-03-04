FROM bitwarden/server:latest AS build
ARG TARGETPLATFORM
ARG BUILDPLATFORM
ENV PROJECT_NAME=Attachments

EXPOSE 5000
HEALTHCHECK CMD curl -f http://localhost:5000/alive || exit 1

ENTRYPOINT ["/bitwarden_server/Server", "/contentRoot=/etc/bitwarden/core/attachments", "/webRoot=.", "/serveUnknown=true"]
