FROM golang:1.26.10-bookworm@sha256:632dff7351799a5a9954a48dc06670ade72ee44e8c351cdc0e5a57af2fea21af AS build

ENV CGO_ENABLED=0 GOMAXPROCS=2 GOMEMLIMIT=768MiB GOFLAGS=-p=2
# Upstream RELEASE.2025-10-15T17-29-55Z; build from immutable source.
RUN go install -trimpath -ldflags="-s -w" github.com/minio/minio@9e49d5e7a648f00e26f2246f4dc28e6b07f8c84a
RUN mkdir -p /runtime/data /runtime/tmp /runtime/etc/ssl/certs \
    && chmod 1777 /runtime/tmp \
    && chown 10001:10001 /runtime/data \
    && cp /etc/ssl/certs/ca-certificates.crt /runtime/etc/ssl/certs/

FROM scratch
COPY --from=build /runtime/ /
COPY --from=build /go/bin/minio /minio
ENV MINIO_BROWSER=off HOME=/tmp
USER 10001:10001
EXPOSE 9000
ENTRYPOINT ["/minio"]
