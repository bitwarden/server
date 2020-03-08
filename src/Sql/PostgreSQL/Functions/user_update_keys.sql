DROP FUNCTION IF EXISTS user_update_keys (UUID,VARCHAR,TEXT,TEXT,TIMESTAMPTZ);

CREATE OR REPLACE FUNCTION user_update_keys
(
    _id             UUID,
    _security_stamp VARCHAR,
    _key            TEXT,
    _private_key    TEXT,
    _revision_date  TIMESTAMPTZ
)
RETURNS VOID
LANGUAGE 'plpgsql'
AS
$BODY$
BEGIN
    UPDATE
        "user"
    SET
        security_stamp = _security_stamp,
        key = _key,
        private_key = _private_key,
        revision_date = _revision_date,
        account_revision_date = _revision_date
    WHERE
        id = _id;
END
$BODY$
