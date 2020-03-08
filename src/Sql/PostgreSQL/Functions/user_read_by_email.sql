DROP FUNCTION IF EXISTS user_read_by_email (VARCHAR);

CREATE OR REPLACE FUNCTION user_read_by_email 
(
    _email VARCHAR
)
RETURNS SETOF user_view
LANGUAGE 'plpgsql'
AS
$BODY$
BEGIN
    RETURN QUERY
    SELECT
        *
    FROM
        user_view
    WHERE
        email = _email;
END
$BODY$
