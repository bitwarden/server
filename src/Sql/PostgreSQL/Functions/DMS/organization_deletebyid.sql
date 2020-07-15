CREATE OR REPLACE PROCEDURE organization_deletebyid(par_id uuid)
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    var_BatchSize NUMERIC(10, 0) DEFAULT 100;
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    CALL user_bumpaccountrevisiondatebyorganizationid(par_Id);

    WHILE var_BatchSize > 0 LOOP
        /*
        [7807 - Severity CRITICAL - PostgreSQL does not support explicit transaction management in functions. Perform a manual conversion.]
        BEGIN TRANSACTION Organization_DeleteById_Ciphers
        */
        /*
        [7798 - Severity CRITICAL - PostgreSQL doesn't support TOP option in the operator DELETE. Perform a manual conversion.]
        DELETE TOP(@BatchSize)
                FROM
                    [dbo].[Cipher]
                WHERE
                    [UserId] IS NULL
                    AND [OrganizationId] = @Id
        */
        GET DIAGNOSTICS var_BatchSize = ROW_COUNT;
        COMMIT;
    END LOOP;
    DELETE FROM organization
        WHERE id = par_Id;
END;
$procedure$
;
