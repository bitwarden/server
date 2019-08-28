drop table if exists "user" CASCADE;

CREATE TABLE "user" (
    id                                 uuid          not null,
    name                               varchar (50)  null,
    email                              varchar (50)  not null,
    email_verified                     bit           not null,
    master_password                    varchar (300) not null,
    master_password_hint               varchar (50)  null,
    culture                            varchar (10)  not null,
    security_stamp                     varchar (50)  not null,
    two_factor_providers               text          null,
    two_factor_recovery_code           varchar (32)  null,
    equivalent_domains                 text          null,
    excluded_global_equivalent_domains text          null,
    account_revision_date              timestamptz   not null,
    key                                text          null,
    public_key                         text          null,
    private_key                        text          null,
    premium                            bit           not null,
    premium_expiration_date            timestamptz   null,
    renewal_reminder_date              timestamptz   null,
    storage                            bigint        null,
    max_storage_gb                     smallint      null,
    gateway                            smallint      null,
    gateway_customer_id                varchar (50)  null,
    gateway_subscription_id            varchar (50)  null,
    license_key                        varchar (100) null,
    kdf                                smallint      not null,
    kdf_iterations                     int           not null,
    creation_date                      timestamptz   not null,
    revision_date                      timestamptz   not null,
    constraint pk_user primary key (id)
);


create unique index ix_user_email
    on "user"(email asc);


create index ix_user_premium_premium_expiration_date_renewal_reminder_date
    on "user"(premium asc, premium_expiration_date asc, renewal_reminder_date asc);

