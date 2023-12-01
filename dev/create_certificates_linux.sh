#!/usr/bin/env bash
# Script for generating and installing the Bitwarden development certificates on Linux.

IDENTITY_SERVER_KEY=identity_server_dev.key
IDENTITY_SERVER_CERT=identity_server_dev.crt
IDENTITY_SERVER_CN="Bitwarden Identity Server Dev"
DATA_PROTECTION_KEY=data_protection_dev.key
DATA_PROTECTION_CERT=data_protection_dev.crt
DATA_PROTECTION_CN="Bitwarden Data Protection Dev"

# Detect management command to trust generated certificates.
if [ -x "$(command -v update-ca-certificates)" ]; then
  # Debian based
  CA_CERT_DIR=/usr/local/share/ca-certificates/
  UPDATE_CA_CMD=update-ca-certificates
elif [ -x "$(command -v update-ca-trust)" ]; then
  # Redhat based
  CA_CERT_DIR=/etc/pki/ca-trust/source/anchors/
  UPDATE_CA_CMD=update-ca-trust
else
  echo 'Error: Update manager for CA certificates not found!'
  exit 1
fi


openssl req -x509 -newkey rsa:4096 -sha256 -nodes -days 3650 \
    -keyout $IDENTITY_SERVER_KEY \
    -out $IDENTITY_SERVER_CERT \
    -subj "/CN=$IDENTITY_SERVER_CN"

sudo cp $IDENTITY_SERVER_CERT $CA_CERT_DIR

openssl req -x509 -newkey rsa:4096 -sha256 -nodes -days 3650 \
    -keyout $DATA_PROTECTION_KEY \
    -out $DATA_PROTECTION_CERT \
    -subj "/CN=$DATA_PROTECTION_CN"

sudo cp $DATA_PROTECTION_CERT $CA_CERT_DIR

sudo $UPDATE_CA_CMD

identity=($(openssl x509 -in $IDENTITY_SERVER_CERT -outform der | sha1sum | tr a-z A-Z))
data=($(openssl x509 -in $DATA_PROTECTION_CERT -outform der | sha1sum | tr a-z A-Z))

echo "Certificate fingerprints:"

echo "Identity Server Dev: ${identity}"
echo "Data Protection Dev: ${data}"
