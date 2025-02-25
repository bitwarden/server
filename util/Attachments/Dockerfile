FROM bitwarden/server:latest as build
ARG TARGETPLATFORM
ARG BUILDPLATFORM
ARG PROJECT_NAME=Attachments

RUN mkdir -p {/storage/attachments,/bitwarden_server,/config} \
  && chown -R app:app {/storage/attachments,/bitwarden_server,/config}

EXPOSE 5000
HEALTHCHECK CMD curl -f http://localhost:5000/alive || exit 1

USER app
ENTRYPOINT ["/bitwarden_server/Server", "/contentRoot=/config/core/attachments", "/webRoot=.", "/serveUnknown=true"]
