CREATE OR REPLACE FUNCTION bitwarden.user_read_by_id
(
    _id uuid
)
RETURNS SETOF bitwarden."user"
LANGUAGE 'plpgsql'
AS 
$BODY$
BEGIN
    return query
    SELECT
        *
    FROM
        bitwarden."user"
    WHERE
        "id" = _id;
END
$BODY$;
