DROP FUNCTION IF EXISTS user_delete_by_id (UUID);

CREATE OR REPLACE FUNCTION user_delete_by_id 
(
    _id UUID
)
RETURNS VOID
LANGUAGE 'plpgsql'
AS
$BODY$
BEGIN
    -- functions dont support commit/rollback transactions, only v11+ procedures can do this
    DELETE FROM
        cipher
    WHERE
        user_id = _id;
    
    -- Delete folders
    DELETE FROM
        folder
    WHERE
        user_id = _id;

    -- Delete devices
    DELETE FROM
        device
    WHERE
        user_id = _id;

    -- Delete collection users
    DELETE FROM
        collection_user CU
    USING
        organization_user OU 
    WHERE
        OU.id = CU.organization_user_id
    AND
        OU.user_id = _id;

    -- Delete group users
    DELETE FROM
        group_user GU
    USING
        organization_user OU 
    WHERE
        OU.id = GU.organization_user_id
    AND
        OU.user_id = _id;

    -- Delete organization users
    DELETE
    FROM
        organization_user
    WHERE
        user_id = _id;

    -- Delete U2F logins
    DELETE
    FROM
        u2f
    WHERE
        user_id = _id;

    -- Finally, delete the user
    DELETE
    FROM
        "user"
    WHERE
        id = _id;
END
$BODY$
