DROP TABLE IF EXISTS "user" CASCADE; 

CREATE TABLE "user" (
    id                                 UUID          NOT NULL,
    name                               VARCHAR (50)  NULL,
    email                              VARCHAR (50)  NOT NULL,
    email_verified                     BOOLEAN       NOT NULL,
    master_password                    VARCHAR (300) NOT NULL,
    master_password_hint               VARCHAR (50)  NULL,
    culture                            VARCHAR (10)  NOT NULL,
    security_stamp                     VARCHAR (50)  NOT NULL,
    two_factor_providers               TEXT          NULL,
    two_factor_recovery_code           VARCHAR (32)  NULL,
    equivalent_domains                 TEXT          NULL,
    excluded_global_equivalent_domains TEXT          NULL,
    account_revision_date              TIMESTAMPTZ   NOT NULL,
    key                                TEXT          NULL,
    public_key                         TEXT          NULL,
    private_key                        TEXT          NULL,
    premium                            BOOLEAN       NOT NULL,
    premium_expiration_date            TIMESTAMPTZ   NULL,
    renewal_reminder_date              TIMESTAMPTZ   NULL,
    storage                            BIGINT        NULL,
    max_storage_gb                     SMALLINT      NULL,
    gateway                            SMALLINT      NULL,
    gateway_customer_id                VARCHAR (50)  NULL,
    gateway_subscription_id            VARCHAR (50)  NULL,
    license_key                        VARCHAR (100) NULL,
    kdf                                SMALLINT      NOT NULL,
    kdf_iterations                     INT           NOT NULL,
    creation_date                      TIMESTAMPTZ   NOT NULL,
    revision_date                      TIMESTAMPTZ   NOT NULL,
    CONSTRAINT pk_user PRIMARY KEY (id)
);


CREATE UNIQUE INDEX ix_user_email
    ON "user"(email ASC);


CREATE INDEX ix_user_premium_premium_expiration_date_renewal_reminder_date
    ON "user"(premium ASC, premium_expiration_date ASC, renewal_reminder_date ASC);

