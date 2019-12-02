DROP FUNCTION IF EXISTS user_create(UUID,VARCHAR,VARCHAR,BOOLEAN,VARCHAR,VARCHAR,VARCHAR,VARCHAR,TEXT,VARCHAR,TEXT,TEXT,TIMESTAMPTZ,TEXT,TEXT,TEXT,BOOLEAN,TIMESTAMPTZ,TIMESTAMPTZ,BIGINT,SMALLINT,SMALLINT,VARCHAR,VARCHAR,VARCHAR,SMALLINT,INT,TIMESTAMPTZ,TIMESTAMPTZ);

CREATE OR REPLACE FUNCTION user_create
(
    _id                                 UUID,
    _name                               VARCHAR,
    _email                              VARCHAR,
    _email_verified                     BOOLEAN,
    _master_password                    VARCHAR,
    _master_password_hint               VARCHAR,
    _culture                            VARCHAR,
    _security_stamp                     VARCHAR,
    _two_factor_providers               TEXT,
    _two_factor_recovery_code           VARCHAR,
    _equivalent_domains                 TEXT,
    _excluded_global_equivalent_domains TEXT,
    _account_revision_date              TIMESTAMPTZ,
    _key                                TEXT,
    _public_key                         TEXT,
    _private_key                        TEXT,
    _premium                            BOOLEAN,
    _premium_expiration_date            TIMESTAMPTZ,
    _renewal_reminder_date              TIMESTAMPTZ,
    _storage                            BIGINT,
    _max_storage_gb                     SMALLINT,
    _gateway                            SMALLINT,
    _gateway_customer_id                VARCHAR,
    _gateway_subscription_id            VARCHAR,
    _license_key                        VARCHAR,
    _kdf                                SMALLINT,
    _kdf_iterations                     INT,
    _creation_date                      TIMESTAMPTZ,
    _revision_date                      TIMESTAMPTZ
)
RETURNS VOID
LANGUAGE 'plpgsql'
AS
$$
BEGIN
    INSERT INTO "user"
    (
        id,
        name,
        email,
        email_verified,
        master_password,
        master_password_hint,
        culture,
        security_stamp,
        two_factor_providers,
        two_factor_recovery_code,
        equivalent_domains,
        excluded_global_equivalent_domains,
        account_revision_date,
        key,
        public_key,
        private_key,
        premium,
        premium_expiration_date,
        renewal_reminder_date,
        storage,
        max_storage_gb,
        gateway,
        gateway_customer_id,
        gateway_subscription_id,
        license_key,
        kdf,
        kdf_iterations,
        creation_date,
        revision_date
    )
    VALUES
    (
        _id,
        _name,
        _email,
        _email_verified,
        _master_password,
        _master_password_hint,
        _culture,
        _security_stamp,
        _two_factor_providers,
        _two_factor_recovery_code,
        _equivalent_domains,
        _excluded_global_equivalent_domains,
        _account_revision_date,
        _key,
        _public_key,
        _private_key,
        _premium,
        _premium_expiration_date,
        _renewal_reminder_date,
        _storage,
        _max_storage_gb,
        _gateway,
        _gateway_customer_id,
        _gateway_subscription_id,
        _license_key,
        _kdf,
        _kdf_iterations,
        _creation_date,
        _revision_date
    );
END
$$
