DROP FUNCTION IF EXISTS user_read_by_id;

CREATE OR REPLACE FUNCTION user_read_by_id
(
    _id uuid
)
RETURNS SETOF "user"
LANGUAGE 'plpgsql'
AS 
$BODY$
BEGIN
    RETURN QUERY
    SELECT
        *
    FROM
        "user"
    WHERE
        "id" = _id;
END
$BODY$;
