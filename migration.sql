CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064059_InitialCreate') THEN
    CREATE TABLE "Companies" (
        "Id" uuid NOT NULL,
        "Name" character varying(160) NOT NULL,
        "Kind" integer NOT NULL,
        "BaseCurrency" character varying(3) NOT NULL,
        CONSTRAINT "PK_Companies" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064059_InitialCreate') THEN
    CREATE TABLE "Counterparties" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "Name" character varying(180) NOT NULL,
        "Kind" integer NOT NULL,
        "RewardKind" integer NOT NULL,
        "RewardRate" numeric(24,8) NOT NULL,
        "AgentId" uuid,
        CONSTRAINT "PK_Counterparties" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Counterparties_Counterparties_AgentId" FOREIGN KEY ("AgentId") REFERENCES "Counterparties" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064059_InitialCreate') THEN
    CREATE TABLE "Expenses" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "AccountId" uuid NOT NULL,
        "OccurredAt" timestamp with time zone NOT NULL,
        "Category" character varying(120) NOT NULL,
        "Amount" numeric(24,8) NOT NULL,
        "Currency" character varying(5) NOT NULL,
        "BaseCurrencyAmount" numeric(24,8) NOT NULL,
        "Note" character varying(300),
        CONSTRAINT "PK_Expenses" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064059_InitialCreate') THEN
    CREATE TABLE "Accounts" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "Name" character varying(160) NOT NULL,
        "Kind" integer NOT NULL,
        "Currency" character varying(5) NOT NULL,
        "OpeningBalance" numeric(24,8) NOT NULL,
        "IsActive" boolean NOT NULL,
        CONSTRAINT "PK_Accounts" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Accounts_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064059_InitialCreate') THEN
    CREATE TABLE "Operations" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "CounterpartyId" uuid,
        "TypeCode" character varying(40) NOT NULL,
        "OccurredAt" timestamp with time zone NOT NULL,
        "DueAt" timestamp with time zone,
        "SellCurrency" character varying(5) NOT NULL,
        "SellAmount" numeric(24,8) NOT NULL,
        "BuyCurrency" character varying(5) NOT NULL,
        "BuyAmount" numeric(24,8) NOT NULL,
        "FeeAmount" numeric(24,8) NOT NULL,
        "FeeCurrency" character varying(5) NOT NULL,
        "BaseCurrencyProfit" numeric(24,8) NOT NULL,
        "Status" integer NOT NULL,
        "Note" character varying(500),
        CONSTRAINT "PK_Operations" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Operations_Counterparties_CounterpartyId" FOREIGN KEY ("CounterpartyId") REFERENCES "Counterparties" ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064059_InitialCreate') THEN
    CREATE TABLE "Settlements" (
        "Id" uuid NOT NULL,
        "OperationId" uuid NOT NULL,
        "AccountId" uuid NOT NULL,
        "OccurredAt" timestamp with time zone NOT NULL,
        "Amount" numeric(24,8) NOT NULL,
        "Currency" character varying(5) NOT NULL,
        "Note" character varying(300),
        CONSTRAINT "PK_Settlements" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Settlements_Accounts_AccountId" FOREIGN KEY ("AccountId") REFERENCES "Accounts" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_Settlements_Operations_OperationId" FOREIGN KEY ("OperationId") REFERENCES "Operations" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064059_InitialCreate') THEN
    CREATE INDEX "IX_Accounts_CompanyId" ON "Accounts" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064059_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_Companies_Name" ON "Companies" ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064059_InitialCreate') THEN
    CREATE INDEX "IX_Counterparties_AgentId" ON "Counterparties" ("AgentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064059_InitialCreate') THEN
    CREATE INDEX "IX_Operations_CompanyId_OccurredAt" ON "Operations" ("CompanyId", "OccurredAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064059_InitialCreate') THEN
    CREATE INDEX "IX_Operations_CounterpartyId" ON "Operations" ("CounterpartyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064059_InitialCreate') THEN
    CREATE INDEX "IX_Settlements_AccountId_OccurredAt" ON "Settlements" ("AccountId", "OccurredAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064059_InitialCreate') THEN
    CREATE INDEX "IX_Settlements_OperationId" ON "Settlements" ("OperationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064059_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260816064059_InitialCreate', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064452_AddIdentityAuthorization') THEN
    CREATE TABLE "AspNetRoles" (
        "Id" text NOT NULL,
        "Name" character varying(256),
        "NormalizedName" character varying(256),
        "ConcurrencyStamp" text,
        CONSTRAINT "PK_AspNetRoles" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064452_AddIdentityAuthorization') THEN
    CREATE TABLE "AspNetUsers" (
        "Id" text NOT NULL,
        "UserName" character varying(256),
        "NormalizedUserName" character varying(256),
        "Email" character varying(256),
        "NormalizedEmail" character varying(256),
        "EmailConfirmed" boolean NOT NULL,
        "PasswordHash" text,
        "SecurityStamp" text,
        "ConcurrencyStamp" text,
        "PhoneNumber" text,
        "PhoneNumberConfirmed" boolean NOT NULL,
        "TwoFactorEnabled" boolean NOT NULL,
        "LockoutEnd" timestamp with time zone,
        "LockoutEnabled" boolean NOT NULL,
        "AccessFailedCount" integer NOT NULL,
        CONSTRAINT "PK_AspNetUsers" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064452_AddIdentityAuthorization') THEN
    CREATE TABLE "AspNetRoleClaims" (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "RoleId" text NOT NULL,
        "ClaimType" text,
        "ClaimValue" text,
        CONSTRAINT "PK_AspNetRoleClaims" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AspNetRoleClaims_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064452_AddIdentityAuthorization') THEN
    CREATE TABLE "AspNetUserClaims" (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "UserId" text NOT NULL,
        "ClaimType" text,
        "ClaimValue" text,
        CONSTRAINT "PK_AspNetUserClaims" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AspNetUserClaims_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064452_AddIdentityAuthorization') THEN
    CREATE TABLE "AspNetUserLogins" (
        "LoginProvider" text NOT NULL,
        "ProviderKey" text NOT NULL,
        "ProviderDisplayName" text,
        "UserId" text NOT NULL,
        CONSTRAINT "PK_AspNetUserLogins" PRIMARY KEY ("LoginProvider", "ProviderKey"),
        CONSTRAINT "FK_AspNetUserLogins_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064452_AddIdentityAuthorization') THEN
    CREATE TABLE "AspNetUserRoles" (
        "UserId" text NOT NULL,
        "RoleId" text NOT NULL,
        CONSTRAINT "PK_AspNetUserRoles" PRIMARY KEY ("UserId", "RoleId"),
        CONSTRAINT "FK_AspNetUserRoles_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_AspNetUserRoles_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064452_AddIdentityAuthorization') THEN
    CREATE TABLE "AspNetUserTokens" (
        "UserId" text NOT NULL,
        "LoginProvider" text NOT NULL,
        "Name" text NOT NULL,
        "Value" text,
        CONSTRAINT "PK_AspNetUserTokens" PRIMARY KEY ("UserId", "LoginProvider", "Name"),
        CONSTRAINT "FK_AspNetUserTokens_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064452_AddIdentityAuthorization') THEN
    CREATE INDEX "IX_AspNetRoleClaims_RoleId" ON "AspNetRoleClaims" ("RoleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064452_AddIdentityAuthorization') THEN
    CREATE UNIQUE INDEX "RoleNameIndex" ON "AspNetRoles" ("NormalizedName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064452_AddIdentityAuthorization') THEN
    CREATE INDEX "IX_AspNetUserClaims_UserId" ON "AspNetUserClaims" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064452_AddIdentityAuthorization') THEN
    CREATE INDEX "IX_AspNetUserLogins_UserId" ON "AspNetUserLogins" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064452_AddIdentityAuthorization') THEN
    CREATE INDEX "IX_AspNetUserRoles_RoleId" ON "AspNetUserRoles" ("RoleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064452_AddIdentityAuthorization') THEN
    CREATE INDEX "EmailIndex" ON "AspNetUsers" ("NormalizedEmail");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064452_AddIdentityAuthorization') THEN
    CREATE UNIQUE INDEX "UserNameIndex" ON "AspNetUsers" ("NormalizedUserName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816064452_AddIdentityAuthorization') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260816064452_AddIdentityAuthorization', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816121838_AddDirectoriesAndClients') THEN
    ALTER TABLE "Counterparties" ADD "Code" character varying(80);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816121838_AddDirectoriesAndClients') THEN
    ALTER TABLE "Counterparties" ADD "CreatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816121838_AddDirectoriesAndClients') THEN
    ALTER TABLE "Counterparties" ADD "IsActive" boolean NOT NULL DEFAULT TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816121838_AddDirectoriesAndClients') THEN
    ALTER TABLE "Counterparties" ADD "Note" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816121838_AddDirectoriesAndClients') THEN
    CREATE TABLE "Currencies" (
        "Code" character varying(5) NOT NULL,
        "Name" character varying(80) NOT NULL,
        "Symbol" character varying(8) NOT NULL,
        "Precision" integer NOT NULL,
        "IsActive" boolean NOT NULL,
        CONSTRAINT "PK_Currencies" PRIMARY KEY ("Code")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816121838_AddDirectoriesAndClients') THEN
    CREATE UNIQUE INDEX "IX_Counterparties_CompanyId_Name" ON "Counterparties" ("CompanyId", "Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816121838_AddDirectoriesAndClients') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260816121838_AddDirectoriesAndClients', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816125806_ImportHistoricalExcelData') THEN
    ALTER TABLE "Operations" ADD "DestinationAccount" character varying(160);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816125806_ImportHistoricalExcelData') THEN
    ALTER TABLE "Operations" ADD "ExchangeRate" numeric(30,15);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816125806_ImportHistoricalExcelData') THEN
    ALTER TABLE "Operations" ADD "ImportKey" character varying(300);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816125806_ImportHistoricalExcelData') THEN
    ALTER TABLE "Operations" ADD "SourceAccount" character varying(160);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816125806_ImportHistoricalExcelData') THEN
    ALTER TABLE "Expenses" ADD "ImportKey" character varying(300);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816125806_ImportHistoricalExcelData') THEN
    CREATE TABLE "HistoricalImportRecords" (
        "Id" uuid NOT NULL,
        "SourceKey" character varying(300) NOT NULL,
        "SourceFile" character varying(180) NOT NULL,
        "SourceSheet" character varying(120) NOT NULL,
        "SourceRow" integer NOT NULL,
        "RecordType" character varying(40) NOT NULL,
        "DataJson" text NOT NULL,
        "ImportedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_HistoricalImportRecords" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816125806_ImportHistoricalExcelData') THEN
    CREATE UNIQUE INDEX "IX_Operations_ImportKey" ON "Operations" ("ImportKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816125806_ImportHistoricalExcelData') THEN
    CREATE UNIQUE INDEX "IX_Expenses_ImportKey" ON "Expenses" ("ImportKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816125806_ImportHistoricalExcelData') THEN
    CREATE UNIQUE INDEX "IX_HistoricalImportRecords_SourceKey" ON "HistoricalImportRecords" ("SourceKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816125806_ImportHistoricalExcelData') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260816125806_ImportHistoricalExcelData', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816130624_AddFinancialInstitutions') THEN
    CREATE TABLE "FinancialInstitutions" (
        "Id" uuid NOT NULL,
        "Name" character varying(160) NOT NULL,
        "Kind" integer NOT NULL,
        "IsActive" boolean NOT NULL,
        "Note" character varying(300),
        CONSTRAINT "PK_FinancialInstitutions" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816130624_AddFinancialInstitutions') THEN
    CREATE UNIQUE INDEX "IX_FinancialInstitutions_Name" ON "FinancialInstitutions" ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816130624_AddFinancialInstitutions') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260816130624_AddFinancialInstitutions', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816151917_AddExchangeRates') THEN
    CREATE TABLE "ExchangeRates" (
        "Id" uuid NOT NULL,
        "Currency" character varying(5) NOT NULL,
        "EffectiveAt" timestamp with time zone NOT NULL,
        "RateToUsd" numeric(30,15) NOT NULL,
        "Note" character varying(300),
        "ImportKey" character varying(300),
        CONSTRAINT "PK_ExchangeRates" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816151917_AddExchangeRates') THEN
    CREATE INDEX "IX_ExchangeRates_Currency_EffectiveAt" ON "ExchangeRates" ("Currency", "EffectiveAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816151917_AddExchangeRates') THEN
    CREATE UNIQUE INDEX "IX_ExchangeRates_ImportKey" ON "ExchangeRates" ("ImportKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816151917_AddExchangeRates') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260816151917_AddExchangeRates', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816154514_AddExchangeRateSourceOrder') THEN
    ALTER TABLE "ExchangeRates" ADD "SourceOrder" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816154514_AddExchangeRateSourceOrder') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260816154514_AddExchangeRateSourceOrder', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816162828_AddMonthlyCurrencyResults') THEN
    CREATE TABLE "MonthlyCurrencyResults" (
        "Id" uuid NOT NULL,
        "Period" timestamp with time zone NOT NULL,
        "Currency" character varying(5) NOT NULL,
        "NetAmount" numeric(24,8) NOT NULL,
        "EquivalentUsdt" numeric(24,8) NOT NULL,
        "ImportKey" character varying(300) NOT NULL,
        CONSTRAINT "PK_MonthlyCurrencyResults" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816162828_AddMonthlyCurrencyResults') THEN
    CREATE UNIQUE INDEX "IX_MonthlyCurrencyResults_ImportKey" ON "MonthlyCurrencyResults" ("ImportKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816162828_AddMonthlyCurrencyResults') THEN
    CREATE UNIQUE INDEX "IX_MonthlyCurrencyResults_Period_Currency" ON "MonthlyCurrencyResults" ("Period", "Currency");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816162828_AddMonthlyCurrencyResults') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260816162828_AddMonthlyCurrencyResults', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816164351_AddMonthlyExcelSummaryTables') THEN
    CREATE TABLE "MonthlyBalanceSnapshots" (
        "Id" uuid NOT NULL,
        "Period" timestamp with time zone NOT NULL,
        "Currency" character varying(5) NOT NULL,
        "OpeningAmount" numeric(24,8) NOT NULL,
        "ClosingAmount" numeric(24,8) NOT NULL,
        "OpeningEquivalentUsdt" numeric(24,8) NOT NULL,
        "ClosingEquivalentUsdt" numeric(24,8) NOT NULL,
        "ImportKey" character varying(300) NOT NULL,
        CONSTRAINT "PK_MonthlyBalanceSnapshots" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816164351_AddMonthlyExcelSummaryTables') THEN
    CREATE TABLE "MonthlyExpenseTotals" (
        "Id" uuid NOT NULL,
        "Period" timestamp with time zone NOT NULL,
        "Currency" character varying(5) NOT NULL,
        "Amount" numeric(24,8) NOT NULL,
        "EquivalentUsdt" numeric(24,8) NOT NULL,
        "ImportKey" character varying(300) NOT NULL,
        CONSTRAINT "PK_MonthlyExpenseTotals" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816164351_AddMonthlyExcelSummaryTables') THEN
    CREATE TABLE "MonthlyPurchaseTotals" (
        "Id" uuid NOT NULL,
        "Period" timestamp with time zone NOT NULL,
        "Pair" character varying(20) NOT NULL,
        "ReceivedAmount" numeric(24,8) NOT NULL,
        "ReceivedCurrency" character varying(5) NOT NULL,
        "GivenAmount" numeric(24,8) NOT NULL,
        "GivenCurrency" character varying(5) NOT NULL,
        "ImportKey" character varying(300) NOT NULL,
        CONSTRAINT "PK_MonthlyPurchaseTotals" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816164351_AddMonthlyExcelSummaryTables') THEN
    CREATE TABLE "MonthlySaleTotals" (
        "Id" uuid NOT NULL,
        "Period" timestamp with time zone NOT NULL,
        "Pair" character varying(20) NOT NULL,
        "ReceivedAmount" numeric(24,8) NOT NULL,
        "ReceivedCurrency" character varying(5) NOT NULL,
        "GivenAmount" numeric(24,8) NOT NULL,
        "GivenCurrency" character varying(5) NOT NULL,
        "ImportKey" character varying(300) NOT NULL,
        CONSTRAINT "PK_MonthlySaleTotals" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816164351_AddMonthlyExcelSummaryTables') THEN
    CREATE UNIQUE INDEX "IX_MonthlyBalanceSnapshots_ImportKey" ON "MonthlyBalanceSnapshots" ("ImportKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816164351_AddMonthlyExcelSummaryTables') THEN
    CREATE UNIQUE INDEX "IX_MonthlyExpenseTotals_ImportKey" ON "MonthlyExpenseTotals" ("ImportKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816164351_AddMonthlyExcelSummaryTables') THEN
    CREATE UNIQUE INDEX "IX_MonthlyPurchaseTotals_ImportKey" ON "MonthlyPurchaseTotals" ("ImportKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816164351_AddMonthlyExcelSummaryTables') THEN
    CREATE UNIQUE INDEX "IX_MonthlySaleTotals_ImportKey" ON "MonthlySaleTotals" ("ImportKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816164351_AddMonthlyExcelSummaryTables') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260816164351_AddMonthlyExcelSummaryTables', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817073752_AddBalanceInstitutions') THEN
    ALTER TABLE "Accounts" ADD "FinancialInstitutionId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817073752_AddBalanceInstitutions') THEN
    CREATE UNIQUE INDEX "IX_Accounts_FinancialInstitutionId_Currency" ON "Accounts" ("FinancialInstitutionId", "Currency");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817073752_AddBalanceInstitutions') THEN
    ALTER TABLE "Accounts" ADD CONSTRAINT "FK_Accounts_FinancialInstitutions_FinancialInstitutionId" FOREIGN KEY ("FinancialInstitutionId") REFERENCES "FinancialInstitutions" ("Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817073752_AddBalanceInstitutions') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260817073752_AddBalanceInstitutions', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817082605_AddCompanyWorkspacesAndMovements') THEN
    DROP INDEX "IX_Accounts_CompanyId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817082605_AddCompanyWorkspacesAndMovements') THEN
    DROP INDEX "IX_Accounts_FinancialInstitutionId_Currency";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817082605_AddCompanyWorkspacesAndMovements') THEN
    CREATE TABLE "AccountMovements" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "AccountId" uuid NOT NULL,
        "GroupId" uuid NOT NULL,
        "Kind" integer NOT NULL,
        "OccurredAt" timestamp with time zone NOT NULL,
        "Amount" numeric(24,8) NOT NULL,
        "Currency" character varying(5) NOT NULL,
        "Note" character varying(300),
        CONSTRAINT "PK_AccountMovements" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AccountMovements_Accounts_AccountId" FOREIGN KEY ("AccountId") REFERENCES "Accounts" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817082605_AddCompanyWorkspacesAndMovements') THEN
    CREATE UNIQUE INDEX "IX_Accounts_CompanyId_FinancialInstitutionId_Currency" ON "Accounts" ("CompanyId", "FinancialInstitutionId", "Currency");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817082605_AddCompanyWorkspacesAndMovements') THEN
    CREATE INDEX "IX_Accounts_FinancialInstitutionId" ON "Accounts" ("FinancialInstitutionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817082605_AddCompanyWorkspacesAndMovements') THEN
    CREATE INDEX "IX_AccountMovements_AccountId_OccurredAt" ON "AccountMovements" ("AccountId", "OccurredAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817082605_AddCompanyWorkspacesAndMovements') THEN
    CREATE INDEX "IX_AccountMovements_GroupId" ON "AccountMovements" ("GroupId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817082605_AddCompanyWorkspacesAndMovements') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260817082605_AddCompanyWorkspacesAndMovements', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907131942_AddNbkrExchangeRates') THEN
    CREATE TABLE "NbkrExchangeRates" (
        "Id" uuid NOT NULL,
        "Currency" character varying(5) NOT NULL,
        "EffectiveAt" timestamp with time zone NOT NULL,
        "Nominal" numeric(30,15) NOT NULL,
        "ValueInKgs" numeric(30,15) NOT NULL,
        "Feed" character varying(20) NOT NULL,
        "SyncedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_NbkrExchangeRates" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907131942_AddNbkrExchangeRates') THEN
    CREATE UNIQUE INDEX "IX_NbkrExchangeRates_Currency_EffectiveAt" ON "NbkrExchangeRates" ("Currency", "EffectiveAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907131942_AddNbkrExchangeRates') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260907131942_AddNbkrExchangeRates', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916110025_AllowMultipleAccountsPerInstitution') THEN
    DROP INDEX "IX_Accounts_CompanyId_FinancialInstitutionId_Currency";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916110025_AllowMultipleAccountsPerInstitution') THEN
    CREATE INDEX "IX_Accounts_CompanyId_FinancialInstitutionId_Currency" ON "Accounts" ("CompanyId", "FinancialInstitutionId", "Currency");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916110025_AllowMultipleAccountsPerInstitution') THEN
    CREATE INDEX "IX_Accounts_CompanyId_Name_Currency" ON "Accounts" ("CompanyId", "Name", "Currency");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916110025_AllowMultipleAccountsPerInstitution') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260916110025_AllowMultipleAccountsPerInstitution', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005052715_AddOperationCreatedAt') THEN
    ALTER TABLE "Operations" ADD "CreatedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005052715_AddOperationCreatedAt') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261005052715_AddOperationCreatedAt', '8.0.8');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261009120236_AddEditableOwnTransfers') THEN
    ALTER TABLE "AccountMovements" ALTER COLUMN "Note" TYPE character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261009120236_AddEditableOwnTransfers') THEN
    ALTER TABLE "AccountMovements" ADD "IsCancelled" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261009120236_AddEditableOwnTransfers') THEN
    ALTER TABLE "AccountMovements" ADD "Revision" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261009120236_AddEditableOwnTransfers') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261009120236_AddEditableOwnTransfers') THEN
    CREATE UNIQUE INDEX "IX_TransferRevisions_TransferId_Revision" ON "TransferRevisions" ("TransferId", "Revision");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261009120236_AddEditableOwnTransfers') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261009120236_AddEditableOwnTransfers') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261009120236_AddEditableOwnTransfers', '8.0.8');
    END IF;
END $EF$;
COMMIT;

