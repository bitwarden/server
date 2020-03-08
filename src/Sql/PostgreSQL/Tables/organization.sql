DROP TABLE IF EXISTS organization;

CREATE TABLE IF NOT EXISTS organization (
    id                      UUID          NOT NULL,
    name                    VARCHAR (50)  NOT NULL,
    business_name           VARCHAR (50)  NULL,
    business_address_1      VARCHAR (50)  NULL,
    business_address_2      VARCHAR (50)  NULL,
    business_address_3      VARCHAR (50)  NULL,
    business_country        VARCHAR (2)   NULL,
    business_tax_number     VARCHAR (30)  NULL,
    billing_email           VARCHAR (50)  NOT NULL,
    plan                    VARCHAR (50)  NOT NULL,
    plan_type               SMALLINT      NOT NULL,
    seats                   SMALLINT      NULL,
    max_collections         SMALLINT      NULL,
    use_groups              BIT           NOT NULL,
    use_directory           BIT           NOT NULL,
    use_events              BIT           NOT NULL,
    use_totp                BIT           NOT NULL,
    use_2fa                 BIT           NOT NULL,
    use_api                 BIT           NOT NULL,
    self_host               BIT           NOT NULL,
    users_get_premium       BIT           NOT NULL,
    storage                 BIGINT        NULL,
    max_storage_gb          SMALLINT      NULL,
    gateway                 SMALLINT      NULL,
    gateway_customer_id     VARCHAR (50)  NULL,
    gateway_subscription_id VARCHAR (50)  NULL,
    enabled                 BIT           NOT NULL,
    license_key             VARCHAR (100) NULL,
    api_key                 VARCHAR (30)  NOT NULL,
    two_factor_providers    TEXT          NULL,
    expiration_date         TIMESTAMPTZ   NULL,
    creation_date           TIMESTAMPTZ   NOT NULL,
    revision_date           TIMESTAMPTZ   NOT NULL,
    CONSTRAINT pk_organization PRIMARY KEY (id)
);


CREATE INDEX ix_organization_enabled
    ON organization(id ASC, enabled ASC)
    INCLUDE (use_totp);

