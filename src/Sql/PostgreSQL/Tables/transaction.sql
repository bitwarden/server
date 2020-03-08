DROP TABLE IF EXISTS "transaction";

CREATE TABLE IF NOT EXISTS "transaction" (
    id                  UUID            NOT NULL,
    user_id             UUID            NULL,
    organization_id     UUID            NULL,
    type                SMALLINT        NOT NULL,
    amount              NUMERIC (19,4)  NOT NULL,
    refunded            BIT             NULL,
    refunded_amount     NUMERIC (19,4)  NULL,
    details             VARCHAR(100)    NULL,
    payment_method_type SMALLINT        NULL,
    gateway             SMALLINT        NULL,
    gateway_id          VARCHAR(50)     NULL,
    creation_date       TIMESTAMPTZ     NOT NULL,
    CONSTRAINT pk_transaction PRIMARY KEY  (id),
    CONSTRAINT fk_transaction_user FOREIGN KEY (user_id) REFERENCES "user" (id) ON DELETE CASCADE,
    CONSTRAINT fk_transaction_organization FOREIGN KEY (organization_id) REFERENCES organization (id) ON DELETE CASCADE
);


CREATE UNIQUE INDEX ix_transaction_gateway_gatewayid
    ON "transaction"(gateway ASC, gateway_id ASC)
    WHERE gateway IS NOT NULL AND gateway_id IS NOT NULL;


CREATE INDEX ix_transaction_user_id_organization_id_creation_date
    ON "transaction"(user_id ASC, Organization_id ASC, Creation_date ASC);

