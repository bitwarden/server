CREATE OR REPLACE PROCEDURE user_deletebyid(par_id uuid)
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    var_BatchSize NUMERIC(10, 0) DEFAULT 100;
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    /* Delete ciphers */
    WHILE var_BatchSize > 0 LOOP
        /*
        [7807 - Severity CRITICAL - PostgreSQL does not support explicit transaction management in functions. Perform a manual conversion.]
        BEGIN TRANSACTION User_DeleteById_Ciphers
        */
        /*
        [7798 - Severity CRITICAL - PostgreSQL doesn't support TOP option in the operator DELETE. Perform a manual conversion.]
        DELETE TOP(@BatchSize)
                FROM
                    [dbo].[Cipher]
                WHERE
                    [UserId] = @Id
        */
        GET DIAGNOSTICS var_BatchSize = ROW_COUNT;
        COMMIT;
    END LOOP;
    /*
    [7807 - Severity CRITICAL - PostgreSQL does not support explicit transaction management in functions. Perform a manual conversion.]
    BEGIN TRANSACTION User_DeleteById
    */
    /* Delete folders */
    DELETE FROM folder
        WHERE userid = par_Id;
    /* Delete devices */
    DELETE FROM device
        WHERE userid = par_Id;
    /* Delete collection users */
    DELETE FROM collectionuser AS cu
    USING organization_user AS ou
        WHERE ou.userid = par_Id AND ou.id = cu.organization_userid;
    /* Delete group users */
    DELETE FROM groupuser AS gu
    USING organization_user AS ou
        WHERE ou.userid = par_Id AND ou.id = gu.organization_userid;
    /* Delete organization users */
    DELETE FROM organization_user
        WHERE userid = par_Id;
    /* Delete U2F logins */
    DELETE FROM u2f
        WHERE userid = par_Id;
    /* Finally, delete the user */
    DELETE FROM "User"
        WHERE id = par_Id;
    COMMIT;
END;
$procedure$
;
