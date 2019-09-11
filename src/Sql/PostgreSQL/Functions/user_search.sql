DROP FUNCTION IF EXISTS user_search;

CREATE OR REPLACE FUNCTION user_search 
(
    _email VARCHAR(50),
    _skip  INT DEFAULT 0,
    _take  INT DEFAULT 25
)
RETURNS SETOF user_view
LANGUAGE 'plpgsql'
AS
$BODY$
DECLARE 
    email_like_search VARCHAR(55) = _email || '%';

BEGIN
    RETURN QUERY
    SELECT
        *
    FROM
        user_view
    WHERE
        email IS NULL 
    OR 
        email LIKE email_like_search
    ORDER BY email ASC
    OFFSET _skip ROWS
    FETCH NEXT _take ROWS only;
end
$BODY$
