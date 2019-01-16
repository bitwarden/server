CREATE OR REPLACE FUNCTION user_read_by_id
(
    id uuid
)
RETURNS SETOF "user"
LANGUAGE 'sql'
AS $BODY$
    SELECT
        *
    FROM
        "user"
    WHERE
        "id" = id;
$BODY$;
