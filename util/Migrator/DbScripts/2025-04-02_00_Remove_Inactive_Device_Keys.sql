UPDATE [dbo].[Device]
SET 
    EncryptedUserKey = NULL,
    EncryptedPublicKey = NULL,
    EncryptedPrivateKey = NULL
WHERE Active = 0
  AND (
      EncryptedUserKey IS NOT NULL OR
      EncryptedPublicKey IS NOT NULL OR
      EncryptedPrivateKey IS NOT NULL
  );
GO;