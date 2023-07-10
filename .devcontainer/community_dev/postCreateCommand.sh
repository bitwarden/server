#!/usr/bin/env bash
export DEV_DIR=/workspace/dev
export CONTAINER_CONFIG=/workspace/.devcontainer/community_dev
git config --global --add safe.directory /workspace

get_installation_id_and_key() {
    pushd ./dev >/dev/null || exit
    echo "Please enter your installation id and key from https://bitwarden.com/host:"
    read -r -p "Installation id: " INSTALLATION_ID
    read -r -p "Installation key: " INSTALLATION_KEY
    jq ".globalSettings.installation.id = \"$INSTALLATION_ID\" |
        .globalSettings.installation.key = \"$INSTALLATION_KEY\"" \
        secrets.json.example >secrets.json # create/overwrite secrets.json
    popd >/dev/null || exit
}

configure_other_vars() {
    pushd ./dev >/dev/null || exit
    cp secrets.json .secrets.json.tmp
    # set DB_PASSWORD equal to .services.mssql.environment.MSSQL_SA_PASSWORD, accounting for quotes
    DB_PASSWORD="$(grep -oP 'MSSQL_SA_PASSWORD=["'"'"']?\K[^"'"'"'\s]+' $DEV_DIR/.env)"
    CERT_OUTPUT="$(./create_certificates_linux.sh)"
    #shellcheck disable=SC2086
    IDENTITY_SERVER_FINGERPRINT="$(echo $CERT_OUTPUT | awk -F 'Identity Server Dev: ' '{match($2, /[[:alnum:]]+/); print substr($2, RSTART, RLENGTH)}')"
    #shellcheck disable=SC2086
    DATA_PROTECTION_FINGERPRINT="$(echo $CERT_OUTPUT | awk -F 'Data Protection Dev: ' '{match($2, /[[:alnum:]]+/); print substr($2, RSTART, RLENGTH)}')"
    echo "Identity Server Dev: $IDENTITY_SERVER_FINGERPRINT"
    echo "Data Protection Dev: $DATA_PROTECTION_FINGERPRINT"
    jq \
        ".globalSettings.sqlServer.connectionString = \"Server=localhost;Database=vault_dev;User Id=SA;Password=$DB_PASSWORD;Encrypt=True;TrustServerCertificate=True\" |
        .globalSettings.postgreSql.connectionString = \"Host=localhost;Username=postgres;Password=$DB_PASSWORD;Database=vault_dev;Include Error Detail=true\" |
        .globalSettings.mySql.connectionString = \"server=localhost;uid=root;pwd=$DB_PASSWORD;database=vault_dev\" |
        .globalSettings.identityServer.certificateThumbprint = \"$IDENTITY_SERVER_FINGERPRINT\" |
        .globalSettings.dataProtection.certificateThumbprint = \"$DATA_PROTECTION_FINGERPRINT\"" \
        .secrets.json.tmp >secrets.json
    rm -f .secrets.json.tmp
    popd >/dev/null || exit
}

one_time_setup() {
    read -r -p \
        "Would you like to configure your secrets and certificates for the first time?
WARNING: This will overwrite any existing secrets.json and certificate files.
Proceed? [y/N] " response
    if [[ "$response" =~ ^([yY][eE][sS]|[yY])+$ ]]; then
        echo "Running one-time setup script..."
        sleep 1
        get_installation_id_and_key
        configure_other_vars
        pushd ./dev >/dev/null || exit
        pwsh ./setup_secrets.ps1 || true
        popd >/dev/null || exit
    fi
}

# main
one_time_setup
