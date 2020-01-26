CREATE OR REPLACE PROCEDURE vault_dbo.user_deletebyid(par_id uuid)
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
    DELETE FROM vault_dbo.folder
        WHERE userid = par_Id;
    /* Delete devices */
    DELETE FROM vault_dbo.device
        WHERE userid = par_Id;
    /* Delete collection users */
    DELETE FROM vault_dbo.collectionuser AS cu
    USING vault_dbo.organizationuser AS ou
        WHERE ou.userid = par_Id AND ou.id = cu.organizationuserid;
    /* Delete group users */
    DELETE FROM vault_dbo.groupuser AS gu
    USING vault_dbo.organizationuser AS ou
        WHERE ou.userid = par_Id AND ou.id = gu.organizationuserid;
    /* Delete organization users */
    DELETE FROM vault_dbo.organizationuser
        WHERE userid = par_Id;
    /* Delete U2F logins */
    DELETE FROM vault_dbo.u2f
        WHERE userid = par_Id;
    /* Finally, delete the user */
    DELETE FROM vault_dbo."User"
        WHERE id = par_Id;
    COMMIT;
END;
$procedure$
;
