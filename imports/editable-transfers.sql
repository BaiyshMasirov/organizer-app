START TRANSACTION;

ALTER TABLE "AccountMovements" ALTER COLUMN "Note" TYPE character varying(500);

ALTER TABLE "AccountMovements" ADD "IsCancelled" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE "AccountMovements" ADD "Revision" integer NOT NULL DEFAULT 0;

CREATE TABLE "TransferRevisions" (
    "Id" uuid NOT NULL,
    "CompanyId" uuid NOT NULL,
    "TransferId" uuid NOT NULL,
    "Revision" integer NOT NULL,
    "Action" character varying(40) NOT NULL,
    "Actor" character varying(180) NOT NULL,
    "ChangedAt" timestamp with time zone NOT NULL,
    "OccurredAt" timestamp with time zone NOT NULL,
    "FromAccount" character varying(330) NOT NULL,
    "ToAccount" character varying(330) NOT NULL,
    "Currency" character varying(5) NOT NULL,
    "Amount" numeric(24,8) NOT NULL,
    "Note" character varying(500),
    "IsCancelled" boolean NOT NULL,
    CONSTRAINT "PK_TransferRevisions" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX "IX_TransferRevisions_TransferId_Revision" ON "TransferRevisions" ("TransferId", "Revision");

WITH valid_groups AS (
    SELECT "GroupId" FROM "AccountMovements"
    GROUP BY "GroupId"
    HAVING COUNT(*) = 2 AND MIN("Kind") = 0 AND MAX("Kind") = 0
       AND SUM("Amount") = 0 AND MIN("Amount") < 0 AND MAX("Amount") > 0
       AND COUNT(DISTINCT "CompanyId") = 1 AND COUNT(DISTINCT "AccountId") = 2
       AND COUNT(DISTINCT "Currency") = 1 AND COUNT(DISTINCT "OccurredAt") = 1
)
INSERT INTO "Operations" ("Id", "CompanyId", "TypeCode", "OccurredAt", "CreatedAt",
    "SellCurrency", "SellAmount", "BuyCurrency", "BuyAmount", "FeeAmount", "FeeCurrency",
    "BaseCurrencyProfit", "Status", "Note", "SourceAccount", "DestinationAccount")
SELECT outgoing."GroupId", outgoing."CompanyId", 'OWN_TRANSFER', outgoing."OccurredAt", outgoing."OccurredAt",
    outgoing."Currency", -outgoing."Amount", incoming."Currency", incoming."Amount", 0, outgoing."Currency",
    0, 3, outgoing."Note", source."Name", destination."Name"
FROM valid_groups groups
JOIN "AccountMovements" outgoing ON outgoing."GroupId" = groups."GroupId" AND outgoing."Amount" < 0
JOIN "AccountMovements" incoming ON incoming."GroupId" = groups."GroupId" AND incoming."Amount" > 0
JOIN "Accounts" source ON source."Id" = outgoing."AccountId" AND source."CompanyId" = outgoing."CompanyId" AND source."Currency" = outgoing."Currency"
JOIN "Accounts" destination ON destination."Id" = incoming."AccountId" AND destination."CompanyId" = incoming."CompanyId" AND destination."Currency" = incoming."Currency"
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "TransferRevisions" ("Id", "CompanyId", "TransferId", "Revision", "Action", "Actor", "ChangedAt",
    "OccurredAt", "FromAccount", "ToAccount", "Currency", "Amount", "Note", "IsCancelled")
SELECT "Id", "CompanyId", "Id", 0, 'Существующий перевод', 'Миграция (автор неизвестен)', CURRENT_TIMESTAMP,
    "OccurredAt", "SourceAccount", "DestinationAccount", "SellCurrency", "SellAmount", "Note", false
FROM "Operations" WHERE "TypeCode" = 'OWN_TRANSFER'
ON CONFLICT ("TransferId", "Revision") DO NOTHING;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261009120236_AddEditableOwnTransfers', '8.0.8');

COMMIT;

