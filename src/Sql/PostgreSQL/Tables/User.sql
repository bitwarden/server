CREATE TABLE bitwarden.User (
    Id                              UUID          NOT NULL,
    Name                            VARCHAR (50)  NULL,
    Email                           VARCHAR (50)  NOT NULL,
    EmailVerified                   BIT           NOT NULL,
    MasterPassword                  VARCHAR (300) NOT NULL,
    MasterPasswordHint              VARCHAR (50)  NULL,
    Culture                         VARCHAR (10)  NOT NULL,
    SecurityStamp                   VARCHAR (50)  NOT NULL,
    TwoFactorProviders              TEXT          NULL,
    TwoFactorRecoveryCode           VARCHAR (32)  NULL,
    EquivalentDomains               TEXT          NULL,
    ExcludedGlobalEquivalentDomains TEXT          NULL,
    AccountRevisionDate             TIMESTAMPTZ   NOT NULL,
    Key                             TEXT          NULL,
    PublicKey                       TEXT          NULL,
    PrivateKey                      TEXT          NULL,
    Premium                         BIT           NOT NULL,
    PremiumExpirationDate           TIMESTAMPTZ   NULL,
    RenewalReminderDate             TIMESTAMPTZ   NULL,
    Storage                         BIGINT        NULL,
    MaxStorageGb                    SMALLINT      NULL,
    Gateway                         SMALLINT      NULL,
    GatewayCustomerId               VARCHAR (50)  NULL,
    GatewaySubscriptionId           VARCHAR (50)  NULL,
    LicenseKey                      VARCHAR (100) NULL,
    Kdf                             SMALLINT      NOT NULL,
    KdfIterations                   INT           NOT NULL,
    CreationDate                    TIMESTAMPTZ   NOT NULL,
    RevisionDate                    TIMESTAMPTZ   NOT NULL,
    CONSTRAINT PK_User PRIMARY KEY (Id)
);


CREATE UNIQUE INDEX IX_User_Email
    ON bitwarden.User(Email ASC);


CREATE INDEX IX_User_Premium_PremiumExpirationDate_RenewalReminderDate
    ON bitwarden.User(Premium ASC, PremiumExpirationDate ASC, RenewalReminderDate ASC);

