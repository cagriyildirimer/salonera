IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [ApplicationSettings] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NULL,
        [Key] nvarchar(200) NOT NULL,
        [Value] nvarchar(max) NOT NULL,
        [IsEncrypted] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ApplicationSettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [Attachments] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [EntityName] nvarchar(500) NOT NULL,
        [EntityId] uniqueidentifier NOT NULL,
        [FileName] nvarchar(260) NOT NULL,
        [StoredPath] nvarchar(1000) NOT NULL,
        [ContentType] nvarchar(500) NOT NULL,
        [FileSize] bigint NOT NULL,
        [Checksum] nvarchar(500) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Attachments] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NULL,
        [UserId] uniqueidentifier NULL,
        [Action] nvarchar(100) NOT NULL,
        [EntityName] nvarchar(200) NOT NULL,
        [EntityId] nvarchar(500) NULL,
        [OldValuesJson] nvarchar(max) NULL,
        [NewValuesJson] nvarchar(max) NULL,
        [IpAddress] nvarchar(500) NULL,
        [Succeeded] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [BackupRecords] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [StartedAt] datetimeoffset NOT NULL,
        [CompletedAt] datetimeoffset NULL,
        [FilePath] nvarchar(1000) NOT NULL,
        [Checksum] nvarchar(500) NULL,
        [FileSize] bigint NULL,
        [IsSuccessful] bit NOT NULL,
        [IntegrityVerified] bit NOT NULL,
        [ErrorMessage] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_BackupRecords] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [BankAccounts] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [BankName] nvarchar(500) NULL,
        [Iban] nvarchar(34) NULL,
        [Currency] int NOT NULL,
        [OpeningBalance] decimal(18,2) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_BankAccounts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [Branches] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Address] nvarchar(500) NULL,
        [Phone] nvarchar(500) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Branches] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [CashAccounts] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Currency] int NOT NULL,
        [OpeningBalance] decimal(18,2) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CashAccounts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [ExpenseCategories] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ExpenseCategories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [FinancialTransactions] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [AccountType] int NOT NULL,
        [AccountId] uniqueidentifier NOT NULL,
        [TransactionType] int NOT NULL,
        [TransactionDate] datetimeoffset NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Currency] int NOT NULL,
        [ExchangeRate] decimal(18,2) NOT NULL,
        [AmountTry] decimal(18,2) NOT NULL,
        [SourceType] nvarchar(500) NOT NULL,
        [SourceId] uniqueidentifier NULL,
        [DocumentNumber] nvarchar(500) NULL,
        [Description] nvarchar(500) NULL,
        [IsCancelled] bit NOT NULL,
        [ReversalTransactionId] uniqueidentifier NULL,
        [IdempotencyKey] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_FinancialTransactions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [IncomeCategories] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_IncomeCategories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [Type] int NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Message] nvarchar(500) NOT NULL,
        [DueAt] datetimeoffset NOT NULL,
        [ReadAt] datetimeoffset NULL,
        [RelatedEntityName] nvarchar(500) NULL,
        [RelatedEntityId] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [Suppliers] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [ContactPerson] nvarchar(500) NULL,
        [Phone] nvarchar(500) NULL,
        [Email] nvarchar(500) NULL,
        [TaxOffice] nvarchar(500) NULL,
        [TaxNumber] nvarchar(500) NULL,
        [Address] nvarchar(500) NULL,
        [BankInformation] nvarchar(500) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Suppliers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [Transfers] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [FromAccountType] int NOT NULL,
        [FromAccountId] uniqueidentifier NOT NULL,
        [ToAccountType] int NOT NULL,
        [ToAccountId] uniqueidentifier NOT NULL,
        [TransferDate] datetimeoffset NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Currency] int NOT NULL,
        [ExchangeRate] decimal(18,2) NOT NULL,
        [Description] nvarchar(500) NULL,
        [IsCancelled] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Transfers] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Transfers_DifferentAccounts] CHECK ([FromAccountId] <> [ToAccountId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [Customers] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [Phone] nvarchar(30) NOT NULL,
        [SecondaryPhone] nvarchar(500) NULL,
        [Email] nvarchar(500) NULL,
        [Address] nvarchar(500) NULL,
        [NationalIdentityNumber] nvarchar(11) NULL,
        [BrideName] nvarchar(500) NULL,
        [GroomName] nvarchar(500) NULL,
        [ContactPerson] nvarchar(500) NULL,
        [Notes] nvarchar(max) NULL,
        [KvkkConsentNote] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Customers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Customers_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [EventTypes] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_EventTypes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EventTypes_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [ExtraServices] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [SalesPrice] decimal(18,2) NOT NULL,
        [CostPrice] decimal(18,2) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ExtraServices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExtraServices_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [Halls] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Capacity] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Halls] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Halls_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [Packages] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [IncludedGuestCount] int NOT NULL,
        [SalesPrice] decimal(18,2) NOT NULL,
        [CostPrice] decimal(18,2) NOT NULL,
        [PricePerPerson] decimal(18,2) NULL,
        [ValidFrom] date NULL,
        [ValidUntil] date NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Packages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Packages_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [Username] nvarchar(100) NOT NULL,
        [PasswordHash] nvarchar(500) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [IsActive] bit NOT NULL,
        [MustChangePassword] bit NOT NULL,
        [FailedLoginCount] int NOT NULL,
        [LastLoginAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Users_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [Events] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [HallId] uniqueidentifier NOT NULL,
        [CustomerId] uniqueidentifier NOT NULL,
        [EventTypeId] uniqueidentifier NOT NULL,
        [EventNumber] nvarchar(50) NOT NULL,
        [StartsAt] datetimeoffset NOT NULL,
        [EndsAt] datetimeoffset NOT NULL,
        [OptionExpiresAt] datetimeoffset NULL,
        [GuestCount] int NOT NULL,
        [Status] int NOT NULL,
        [SalesRepresentativeId] uniqueidentifier NULL,
        [SpecialRequests] nvarchar(max) NULL,
        [InternalNotes] nvarchar(max) NULL,
        [CancellationDate] datetimeoffset NULL,
        [CancellationReason] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Events] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Events_TimeRange] CHECK ([EndsAt] > [StartsAt]),
        CONSTRAINT [FK_Events_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Events_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Events_EventTypes_EventTypeId] FOREIGN KEY ([EventTypeId]) REFERENCES [EventTypes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Events_Halls_HallId] FOREIGN KEY ([HallId]) REFERENCES [Halls] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [PackageItems] (
        [Id] uniqueidentifier NOT NULL,
        [PackageId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [Quantity] decimal(18,2) NOT NULL,
        [UnitCost] decimal(18,2) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PackageItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PackageItems_Packages_PackageId] FOREIGN KEY ([PackageId]) REFERENCES [Packages] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [UserRoles] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [Contracts] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [EventId] uniqueidentifier NOT NULL,
        [ContractNumber] nvarchar(50) NOT NULL,
        [ContractDate] datetimeoffset NOT NULL,
        [GrossAmount] decimal(18,2) NOT NULL,
        [DiscountAmount] decimal(18,2) NOT NULL,
        [VatRate] decimal(18,2) NOT NULL,
        [VatAmount] decimal(18,2) NOT NULL,
        [NetAmount] decimal(18,2) NOT NULL,
        [Currency] int NOT NULL,
        [ExchangeRate] decimal(18,2) NOT NULL,
        [NetAmountTry] decimal(18,2) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Contracts] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Contracts_ExchangeRate] CHECK ([ExchangeRate] > 0),
        CONSTRAINT [FK_Contracts_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Contracts_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [Events] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [EventServices] (
        [Id] uniqueidentifier NOT NULL,
        [EventId] uniqueidentifier NOT NULL,
        [PackageId] uniqueidentifier NULL,
        [ExtraServiceId] uniqueidentifier NULL,
        [DescriptionSnapshot] nvarchar(500) NOT NULL,
        [Quantity] decimal(18,2) NOT NULL,
        [UnitSalesPriceSnapshot] decimal(18,2) NOT NULL,
        [UnitCostSnapshot] decimal(18,2) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_EventServices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EventServices_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [Events] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EventServices_ExtraServices_ExtraServiceId] FOREIGN KEY ([ExtraServiceId]) REFERENCES [ExtraServices] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EventServices_Packages_PackageId] FOREIGN KEY ([PackageId]) REFERENCES [Packages] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [EventStatusHistories] (
        [Id] uniqueidentifier NOT NULL,
        [EventId] uniqueidentifier NOT NULL,
        [PreviousStatus] int NOT NULL,
        [NewStatus] int NOT NULL,
        [Reason] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_EventStatusHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EventStatusHistories_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [Events] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [Expenses] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [ExpenseCategoryId] uniqueidentifier NOT NULL,
        [SupplierId] uniqueidentifier NULL,
        [EventId] uniqueidentifier NULL,
        [ExpenseDate] datetimeoffset NOT NULL,
        [DueDate] date NULL,
        [PaidAt] datetimeoffset NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PaidAmount] decimal(18,2) NOT NULL,
        [VatRate] decimal(18,2) NOT NULL,
        [VatAmount] decimal(18,2) NOT NULL,
        [Currency] int NOT NULL,
        [ExchangeRate] decimal(18,2) NOT NULL,
        [AmountTry] decimal(18,2) NOT NULL,
        [Status] int NOT NULL,
        [ExpenseType] int NOT NULL,
        [IsRecurring] bit NOT NULL,
        [FinancialAccountId] uniqueidentifier NULL,
        [DocumentNumber] nvarchar(500) NULL,
        [Description] nvarchar(500) NULL,
        [AttachmentPath] nvarchar(500) NULL,
        [IsCancelled] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Expenses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Expenses_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [Events] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Expenses_ExpenseCategories_ExpenseCategoryId] FOREIGN KEY ([ExpenseCategoryId]) REFERENCES [ExpenseCategories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Expenses_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [SupplierTransactions] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [SupplierId] uniqueidentifier NOT NULL,
        [EventId] uniqueidentifier NULL,
        [TransactionDate] datetimeoffset NOT NULL,
        [Debit] decimal(18,2) NOT NULL,
        [Credit] decimal(18,2) NOT NULL,
        [Currency] int NOT NULL,
        [ExchangeRate] decimal(18,2) NOT NULL,
        [DocumentNumber] nvarchar(500) NULL,
        [Description] nvarchar(500) NULL,
        [IsCancelled] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_SupplierTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SupplierTransactions_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [Events] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SupplierTransactions_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [PaymentPlans] (
        [Id] uniqueidentifier NOT NULL,
        [ContractId] uniqueidentifier NOT NULL,
        [DueDate] date NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Currency] int NOT NULL,
        [Status] int NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PaymentPlans] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentPlans_Contracts_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [Contracts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [Collections] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [ContractId] uniqueidentifier NOT NULL,
        [PaymentPlanId] uniqueidentifier NULL,
        [ReceiptNumber] nvarchar(50) NOT NULL,
        [CollectedAt] datetimeoffset NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Currency] int NOT NULL,
        [ExchangeRate] decimal(18,2) NOT NULL,
        [AmountTry] decimal(18,2) NOT NULL,
        [PaymentMethod] int NOT NULL,
        [FinancialAccountId] uniqueidentifier NOT NULL,
        [IsCancelled] bit NOT NULL,
        [CancellationReason] nvarchar(500) NULL,
        [IdempotencyKey] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Collections] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Collections_Contracts_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [Contracts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Collections_PaymentPlans_PaymentPlanId] FOREIGN KEY ([PaymentPlanId]) REFERENCES [PaymentPlans] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [Incomes] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [IncomeCategoryId] uniqueidentifier NOT NULL,
        [EventId] uniqueidentifier NULL,
        [CustomerId] uniqueidentifier NULL,
        [CollectionId] uniqueidentifier NULL,
        [IncomeDate] datetimeoffset NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [VatRate] decimal(18,2) NOT NULL,
        [VatAmount] decimal(18,2) NOT NULL,
        [Currency] int NOT NULL,
        [ExchangeRate] decimal(18,2) NOT NULL,
        [AmountTry] decimal(18,2) NOT NULL,
        [PaymentMethod] int NOT NULL,
        [FinancialAccountId] uniqueidentifier NOT NULL,
        [DocumentNumber] nvarchar(500) NULL,
        [Description] nvarchar(500) NULL,
        [AttachmentPath] nvarchar(500) NULL,
        [IsCancelled] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Incomes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Incomes_Collections_CollectionId] FOREIGN KEY ([CollectionId]) REFERENCES [Collections] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Incomes_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Incomes_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [Events] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Incomes_IncomeCategories_IncomeCategoryId] FOREIGN KEY ([IncomeCategoryId]) REFERENCES [IncomeCategories] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE TABLE [Refunds] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [ContractId] uniqueidentifier NOT NULL,
        [CollectionId] uniqueidentifier NULL,
        [RefundedAt] datetimeoffset NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Currency] int NOT NULL,
        [ExchangeRate] decimal(18,2) NOT NULL,
        [AmountTry] decimal(18,2) NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [FinancialAccountId] uniqueidentifier NOT NULL,
        [IsCancelled] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Refunds] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Refunds_Collections_CollectionId] FOREIGN KEY ([CollectionId]) REFERENCES [Collections] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Refunds_Contracts_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [Contracts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Address', N'Code', N'CreatedAt', N'CreatedBy', N'IsActive', N'IsDeleted', N'Name', N'Phone', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Branches]'))
        SET IDENTITY_INSERT [Branches] ON;
    EXEC(N'INSERT INTO [Branches] ([Id], [Address], [Code], [CreatedAt], [CreatedBy], [IsActive], [IsDeleted], [Name], [Phone], [UpdatedAt], [UpdatedBy])
    VALUES (''10000000-0000-0000-0000-000000000001'', NULL, N''MRKZ'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, CAST(1 AS bit), CAST(0 AS bit), N''Merkez Şube'', NULL, NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Address', N'Code', N'CreatedAt', N'CreatedBy', N'IsActive', N'IsDeleted', N'Name', N'Phone', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Branches]'))
        SET IDENTITY_INSERT [Branches] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BranchId', N'CreatedAt', N'CreatedBy', N'IsActive', N'IsDeleted', N'Name', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ExpenseCategories]'))
        SET IDENTITY_INSERT [ExpenseCategories] ON;
    EXEC(N'INSERT INTO [ExpenseCategories] ([Id], [BranchId], [CreatedAt], [CreatedBy], [IsActive], [IsDeleted], [Name], [UpdatedAt], [UpdatedBy])
    VALUES (''40000000-0000-0000-0000-000000000001'', ''10000000-0000-0000-0000-000000000001'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, CAST(1 AS bit), CAST(0 AS bit), N''Diğer Gider'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BranchId', N'CreatedAt', N'CreatedBy', N'IsActive', N'IsDeleted', N'Name', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[ExpenseCategories]'))
        SET IDENTITY_INSERT [ExpenseCategories] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BranchId', N'CreatedAt', N'CreatedBy', N'IsActive', N'IsDeleted', N'Name', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[IncomeCategories]'))
        SET IDENTITY_INSERT [IncomeCategories] ON;
    EXEC(N'INSERT INTO [IncomeCategories] ([Id], [BranchId], [CreatedAt], [CreatedBy], [IsActive], [IsDeleted], [Name], [UpdatedAt], [UpdatedBy])
    VALUES (''30000000-0000-0000-0000-000000000001'', ''10000000-0000-0000-0000-000000000001'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, CAST(1 AS bit), CAST(0 AS bit), N''Diğer Gelir'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BranchId', N'CreatedAt', N'CreatedBy', N'IsActive', N'IsDeleted', N'Name', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[IncomeCategories]'))
        SET IDENTITY_INSERT [IncomeCategories] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAt', N'CreatedBy', N'IsDeleted', N'Name', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Roles]'))
        SET IDENTITY_INSERT [Roles] ON;
    EXEC(N'INSERT INTO [Roles] ([Id], [Code], [CreatedAt], [CreatedBy], [IsDeleted], [Name], [UpdatedAt], [UpdatedBy])
    VALUES (''20000000-0000-0000-0000-000000000001'', N''ADMIN'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, CAST(0 AS bit), N''Yönetici'', NULL, NULL),
    (''20000000-0000-0000-0000-000000000002'', N''SALES'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, CAST(0 AS bit), N''Satış'', NULL, NULL),
    (''20000000-0000-0000-0000-000000000003'', N''ACCOUNTING'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, CAST(0 AS bit), N''Muhasebe'', NULL, NULL),
    (''20000000-0000-0000-0000-000000000004'', N''VIEWER'', ''2026-01-01T00:00:00.0000000+00:00'', NULL, CAST(0 AS bit), N''Görüntüleme'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAt', N'CreatedBy', N'IsDeleted', N'Name', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Roles]'))
        SET IDENTITY_INSERT [Roles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ApplicationSettings_BranchId_Key] ON [ApplicationSettings] ([BranchId], [Key]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Attachments_EntityName_EntityId] ON [Attachments] ([EntityName], [EntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_BranchId_CreatedAt] ON [AuditLogs] ([BranchId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_UserId_CreatedAt] ON [AuditLogs] ([UserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BackupRecords_BranchId_StartedAt] ON [BackupRecords] ([BranchId], [StartedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_BankAccounts_BranchId_Code] ON [BankAccounts] ([BranchId], [Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_BankAccounts_Iban] ON [BankAccounts] ([Iban]) WHERE [Iban] IS NOT NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Branches_Code] ON [Branches] ([Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CashAccounts_BranchId_Code] ON [CashAccounts] ([BranchId], [Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Collections_BranchId_ReceiptNumber] ON [Collections] ([BranchId], [ReceiptNumber]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Collections_CollectedAt] ON [Collections] ([CollectedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Collections_ContractId] ON [Collections] ([ContractId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Collections_IdempotencyKey] ON [Collections] ([IdempotencyKey]) WHERE [IdempotencyKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Collections_PaymentPlanId] ON [Collections] ([PaymentPlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Contracts_BranchId_ContractNumber] ON [Contracts] ([BranchId], [ContractNumber]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Contracts_EventId] ON [Contracts] ([EventId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Customers_BranchId_Phone] ON [Customers] ([BranchId], [Phone]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Customers_NationalIdentityNumber] ON [Customers] ([NationalIdentityNumber]) WHERE [NationalIdentityNumber] IS NOT NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Events_BranchId_EventNumber] ON [Events] ([BranchId], [EventNumber]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Events_CustomerId] ON [Events] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Events_EventTypeId] ON [Events] ([EventTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Events_HallId_StartsAt_EndsAt_Status] ON [Events] ([HallId], [StartsAt], [EndsAt], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Events_StartsAt] ON [Events] ([StartsAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EventServices_EventId] ON [EventServices] ([EventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EventServices_ExtraServiceId] ON [EventServices] ([ExtraServiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EventServices_PackageId] ON [EventServices] ([PackageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EventStatusHistories_EventId_CreatedAt] ON [EventStatusHistories] ([EventId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_EventTypes_BranchId_Name] ON [EventTypes] ([BranchId], [Name]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ExpenseCategories_BranchId_Name] ON [ExpenseCategories] ([BranchId], [Name]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Expenses_DocumentNumber] ON [Expenses] ([DocumentNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Expenses_DueDate] ON [Expenses] ([DueDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Expenses_EventId] ON [Expenses] ([EventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Expenses_ExpenseCategoryId] ON [Expenses] ([ExpenseCategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Expenses_ExpenseDate] ON [Expenses] ([ExpenseDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Expenses_SupplierId] ON [Expenses] ([SupplierId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ExtraServices_BranchId_Name] ON [ExtraServices] ([BranchId], [Name]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_FinancialTransactions_AccountId_TransactionDate] ON [FinancialTransactions] ([AccountId], [TransactionDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_FinancialTransactions_DocumentNumber] ON [FinancialTransactions] ([DocumentNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_FinancialTransactions_IdempotencyKey] ON [FinancialTransactions] ([IdempotencyKey]) WHERE [IdempotencyKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Halls_BranchId_Code] ON [Halls] ([BranchId], [Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_IncomeCategories_BranchId_Name] ON [IncomeCategories] ([BranchId], [Name]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Incomes_CollectionId] ON [Incomes] ([CollectionId]) WHERE [CollectionId] IS NOT NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Incomes_CustomerId] ON [Incomes] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Incomes_DocumentNumber] ON [Incomes] ([DocumentNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Incomes_EventId] ON [Incomes] ([EventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Incomes_IncomeCategoryId] ON [Incomes] ([IncomeCategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Incomes_IncomeDate] ON [Incomes] ([IncomeDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId_DueAt_ReadAt] ON [Notifications] ([UserId], [DueAt], [ReadAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PackageItems_PackageId] ON [PackageItems] ([PackageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Packages_BranchId_Name] ON [Packages] ([BranchId], [Name]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PaymentPlans_ContractId] ON [PaymentPlans] ([ContractId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PaymentPlans_DueDate] ON [PaymentPlans] ([DueDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Refunds_CollectionId] ON [Refunds] ([CollectionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Refunds_ContractId] ON [Refunds] ([ContractId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Refunds_RefundedAt] ON [Refunds] ([RefundedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Roles_Code] ON [Roles] ([Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Suppliers_BranchId_Name] ON [Suppliers] ([BranchId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Suppliers_TaxNumber] ON [Suppliers] ([TaxNumber]) WHERE [TaxNumber] IS NOT NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SupplierTransactions_DocumentNumber] ON [SupplierTransactions] ([DocumentNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SupplierTransactions_EventId] ON [SupplierTransactions] ([EventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SupplierTransactions_SupplierId_TransactionDate] ON [SupplierTransactions] ([SupplierId], [TransactionDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Transfers_TransferDate] ON [Transfers] ([TransferDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_UserRoles_UserId_RoleId] ON [UserRoles] ([UserId], [RoleId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Users_BranchId_Username] ON [Users] ([BranchId], [Username]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109114516_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109114516_InitialCreate', N'10.0.14');
END;

COMMIT;
GO

