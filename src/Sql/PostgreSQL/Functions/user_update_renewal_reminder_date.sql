DROP FUNCTION IF EXISTS user_update_renewal_reminder_date (UUID,TIMESTAMPTZ);

CREATE OR REPLACE FUNCTION user_update_renewal_reminder_date 
(
    _id                     UUID,
    _renewal_reminder_date  TIMESTAMPTZ
)
RETURNS VOID
LANGUAGE 'plpgsql'
AS
$BODY$
BEGIN
    UPDATE
        "user"
    SET
        renewal_reminder_date = _renewal_reminder_date
    WHERE
        id = _id;
end
$BODY$
