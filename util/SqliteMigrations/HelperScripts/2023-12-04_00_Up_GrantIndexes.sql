ALTER TABLE
"Grant" RENAME TO "Old_Grant";

CREATE TABLE "Grant"
(
  "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
  "Key" TEXT NOT NULL,
  "Type" TEXT NOT NULL,
  "SubjectId" TEXT NULL,
  "SessionId" TEXT NULL,
  "ClientId" TEXT NOT NULL,
  "Description" TEXT NULL,
  "CreationDate" TEXT NOT NULL,
  "ExpirationDate" TEXT NULL,
  "ConsumedDate" TEXT NULL,
  "Data" TEXT NOT NULL
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
