ALTER TABLE
"Grant" RENAME TO "Old_Grant";

CREATE TABLE "Grant"
(
  "Key" TEXT NOT NULL CONSTRAINT "PK_Grant" PRIMARY KEY,
  "Type" TEXT NULL,
  "SubjectId" TEXT NULL,
  "SessionId" TEXT NULL,
  "ClientId" TEXT NULL,
  "Description" TEXT NULL,
  "CreationDate" TEXT NOT NULL,
  "ExpirationDate" TEXT NULL,
  "ConsumedDate" TEXT NULL,
  "Data" TEXT NULL
);

INSERT INTO
"Grant"
  (
  "Key",
  "Type",
  "SubjectId",
  "SessionId",
  "ClientId",
  "Description",
  "CreationDate",
  "ExpirationDate",
  "ConsumedDate",
  "Data"
  )
SELECT
  "Key",
  "Type",
  "SubjectId",
  "SessionId",
  "ClientId",
  "Description",
  "CreationDate",
  "ExpirationDate",
  "ConsumedDate",
  "Data"
FROM "Old_Grant";

DROP TABLE "Old_Grant";
