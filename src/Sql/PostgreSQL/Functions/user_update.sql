DROP FUNCTION IF EXISTS user_update(UUID,VARCHAR,VARCHAR,BOOLEAN,VARCHAR,VARCHAR,VARCHAR,VARCHAR,TEXT,VARCHAR,TEXT,TEXT,TIMESTAMPTZ,TEXT,TEXT,TEXT,BOOLEAN,TIMESTAMPTZ,TIMESTAMPTZ,BIGINT,SMALLINT,SMALLINT,VARCHAR,VARCHAR,VARCHAR,SMALLINT,INT,TIMESTAMPTZ,TIMESTAMPTZ)

CREATE OR REPLACE FUNCTION user_update
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
    _private_key                        TEXT ,
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
LANGUAGE plpgsql
AS
$$
begin

  UPDATE
        "user"
    SET name = _name,
        email = _email,
        email_verified = _email_verified,
        master_password = _master_password,
        master_password_hint = _master_password_hint,
        culture = _culture,
        security_stamp = _security_stamp,
        two_factor_providers = _two_factor_providers,
        two_factor_recovery_code = _two_factor_recovery_code,
        equivalent_domains = _equivalent_domains,
        excluded_global_equivalent_domains = _excluded_global_equivalent_domains,
        account_revision_date = _account_revision_date,
        key = _key,
        public_key = _public_key,
        private_key = _private_key,
        premium = _premium,
        premium_expiration_date = _premium_expiration_date,
        renewal_reminder_date = _renewal_reminder_date,
        storage = _storage,
        max_storage_gb = _max_storage_gb,
        gateway = _gateway,
        gateway_customer_id = _gateway_customer_id,
        gateway_subscription_id = _gateway_subscription_id,
        license_key = _license_key,
        kdf = _kdf,
        kdf_iterations = _kdf_iterations,
        creation_date = _creation_date,
        revision_date = _revision_date
    WHERE
        id = _id
        ;
end;
$$
