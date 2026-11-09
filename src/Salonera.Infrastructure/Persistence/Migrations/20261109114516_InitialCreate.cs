using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Salonera.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ApplicationSettings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Value = table.Column<string>(type: "nvarchar(max)", maxLength: 500, nullable: false),
                IsEncrypted = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApplicationSettings", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Attachments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EntityName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                StoredPath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                ContentType = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                FileSize = table.Column<long>(type: "bigint", nullable: false),
                Checksum = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Attachments", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AuditLogs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                EntityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                EntityId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                OldValuesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 500, nullable: true),
                NewValuesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 500, nullable: true),
                IpAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Succeeded = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditLogs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "BackupRecords",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                FilePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                Checksum = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                FileSize = table.Column<long>(type: "bigint", nullable: true),
                IsSuccessful = table.Column<bool>(type: "bit", nullable: false),
                IntegrityVerified = table.Column<bool>(type: "bit", nullable: false),
                ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BackupRecords", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "BankAccounts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                BankName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Iban = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: true),
                Currency = table.Column<int>(type: "int", nullable: false),
                OpeningBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BankAccounts", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Branches",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Phone = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Branches", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "CashAccounts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Currency = table.Column<int>(type: "int", nullable: false),
                OpeningBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CashAccounts", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ExpenseCategories",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExpenseCategories", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "FinancialTransactions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AccountType = table.Column<int>(type: "int", nullable: false),
                AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TransactionType = table.Column<int>(type: "int", nullable: false),
                TransactionDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<int>(type: "int", nullable: false),
                ExchangeRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                AmountTry = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                SourceType = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                DocumentNumber = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                IsCancelled = table.Column<bool>(type: "bit", nullable: false),
                ReversalTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FinancialTransactions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "IncomeCategories",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IncomeCategories", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Notifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Type = table.Column<int>(type: "int", nullable: false),
                Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ReadAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                RelatedEntityName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                RelatedEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Notifications", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Roles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Roles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Suppliers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ContactPerson = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Phone = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Email = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                TaxOffice = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                TaxNumber = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                BankInformation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Suppliers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Transfers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FromAccountType = table.Column<int>(type: "int", nullable: false),
                FromAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ToAccountType = table.Column<int>(type: "int", nullable: false),
                ToAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TransferDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<int>(type: "int", nullable: false),
                ExchangeRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                IsCancelled = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Transfers", x => x.Id);
                table.CheckConstraint("CK_Transfers_DifferentAccounts", "[FromAccountId] <> [ToAccountId]");
            });

        migrationBuilder.CreateTable(
            name: "Customers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                SecondaryPhone = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Email = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                NationalIdentityNumber = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: true),
                BrideName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                GroomName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                ContactPerson = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Notes = table.Column<string>(type: "nvarchar(max)", maxLength: 500, nullable: true),
                KvkkConsentNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Customers", x => x.Id);
                table.ForeignKey(
                    name: "FK_Customers_Branches_BranchId",
                    column: x => x.BranchId,
                    principalTable: "Branches",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "EventTypes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EventTypes", x => x.Id);
                table.ForeignKey(
                    name: "FK_EventTypes_Branches_BranchId",
                    column: x => x.BranchId,
                    principalTable: "Branches",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ExtraServices",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                SalesPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                CostPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExtraServices", x => x.Id);
                table.ForeignKey(
                    name: "FK_ExtraServices_Branches_BranchId",
                    column: x => x.BranchId,
                    principalTable: "Branches",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Halls",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Capacity = table.Column<int>(type: "int", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Halls", x => x.Id);
                table.ForeignKey(
                    name: "FK_Halls_Branches_BranchId",
                    column: x => x.BranchId,
                    principalTable: "Branches",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Packages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                IncludedGuestCount = table.Column<int>(type: "int", nullable: false),
                SalesPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                CostPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                PricePerPerson = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                ValidFrom = table.Column<DateOnly>(type: "date", nullable: true),
                ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Packages", x => x.Id);
                table.ForeignKey(
                    name: "FK_Packages_Branches_BranchId",
                    column: x => x.BranchId,
                    principalTable: "Branches",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                FailedLoginCount = table.Column<int>(type: "int", nullable: false),
                LastLoginAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.Id);
                table.ForeignKey(
                    name: "FK_Users_Branches_BranchId",
                    column: x => x.BranchId,
                    principalTable: "Branches",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Events",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                HallId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EventTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EventNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                OptionExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                GuestCount = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                SalesRepresentativeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SpecialRequests = table.Column<string>(type: "nvarchar(max)", maxLength: 500, nullable: true),
                InternalNotes = table.Column<string>(type: "nvarchar(max)", maxLength: 500, nullable: true),
                CancellationDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Events", x => x.Id);
                table.CheckConstraint("CK_Events_TimeRange", "[EndsAt] > [StartsAt]");
                table.ForeignKey(
                    name: "FK_Events_Branches_BranchId",
                    column: x => x.BranchId,
                    principalTable: "Branches",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Events_Customers_CustomerId",
                    column: x => x.CustomerId,
                    principalTable: "Customers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Events_EventTypes_EventTypeId",
                    column: x => x.EventTypeId,
                    principalTable: "EventTypes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Events_Halls_HallId",
                    column: x => x.HallId,
                    principalTable: "Halls",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PackageItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Quantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                UnitCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PackageItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_PackageItems_Packages_PackageId",
                    column: x => x.PackageId,
                    principalTable: "Packages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "UserRoles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserRoles", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserRoles_Roles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "Roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_UserRoles_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Contracts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ContractNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                ContractDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                GrossAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                VatRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                VatAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                NetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<int>(type: "int", nullable: false),
                ExchangeRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                NetAmountTry = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Contracts", x => x.Id);
                table.CheckConstraint("CK_Contracts_ExchangeRate", "[ExchangeRate] > 0");
                table.ForeignKey(
                    name: "FK_Contracts_Branches_BranchId",
                    column: x => x.BranchId,
                    principalTable: "Branches",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Contracts_Events_EventId",
                    column: x => x.EventId,
                    principalTable: "Events",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "EventServices",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ExtraServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                DescriptionSnapshot = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                Quantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                UnitSalesPriceSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                UnitCostSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EventServices", x => x.Id);
                table.ForeignKey(
                    name: "FK_EventServices_Events_EventId",
                    column: x => x.EventId,
                    principalTable: "Events",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_EventServices_ExtraServices_ExtraServiceId",
                    column: x => x.ExtraServiceId,
                    principalTable: "ExtraServices",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_EventServices_Packages_PackageId",
                    column: x => x.PackageId,
                    principalTable: "Packages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "EventStatusHistories",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PreviousStatus = table.Column<int>(type: "int", nullable: false),
                NewStatus = table.Column<int>(type: "int", nullable: false),
                Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EventStatusHistories", x => x.Id);
                table.ForeignKey(
                    name: "FK_EventStatusHistories_Events_EventId",
                    column: x => x.EventId,
                    principalTable: "Events",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Expenses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ExpenseCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ExpenseDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                PaidAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                PaidAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                VatRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                VatAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<int>(type: "int", nullable: false),
                ExchangeRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                AmountTry = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                ExpenseType = table.Column<int>(type: "int", nullable: false),
                IsRecurring = table.Column<bool>(type: "bit", nullable: false),
                FinancialAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                DocumentNumber = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                AttachmentPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                IsCancelled = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Expenses", x => x.Id);
                table.ForeignKey(
                    name: "FK_Expenses_Events_EventId",
                    column: x => x.EventId,
                    principalTable: "Events",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Expenses_ExpenseCategories_ExpenseCategoryId",
                    column: x => x.ExpenseCategoryId,
                    principalTable: "ExpenseCategories",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Expenses_Suppliers_SupplierId",
                    column: x => x.SupplierId,
                    principalTable: "Suppliers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "SupplierTransactions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                TransactionDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Debit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Credit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<int>(type: "int", nullable: false),
                ExchangeRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                DocumentNumber = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                IsCancelled = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SupplierTransactions", x => x.Id);
                table.ForeignKey(
                    name: "FK_SupplierTransactions_Events_EventId",
                    column: x => x.EventId,
                    principalTable: "Events",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SupplierTransactions_Suppliers_SupplierId",
                    column: x => x.SupplierId,
                    principalTable: "Suppliers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PaymentPlans",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PaymentPlans", x => x.Id);
                table.ForeignKey(
                    name: "FK_PaymentPlans_Contracts_ContractId",
                    column: x => x.ContractId,
                    principalTable: "Contracts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Collections",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PaymentPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReceiptNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                CollectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<int>(type: "int", nullable: false),
                ExchangeRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                AmountTry = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                PaymentMethod = table.Column<int>(type: "int", nullable: false),
                FinancialAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                IsCancelled = table.Column<bool>(type: "bit", nullable: false),
                CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Collections", x => x.Id);
                table.ForeignKey(
                    name: "FK_Collections_Contracts_ContractId",
                    column: x => x.ContractId,
                    principalTable: "Contracts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Collections_PaymentPlans_PaymentPlanId",
                    column: x => x.PaymentPlanId,
                    principalTable: "PaymentPlans",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Incomes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                IncomeCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IncomeDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                VatRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                VatAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<int>(type: "int", nullable: false),
                ExchangeRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                AmountTry = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                PaymentMethod = table.Column<int>(type: "int", nullable: false),
                FinancialAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DocumentNumber = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                AttachmentPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                IsCancelled = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Incomes", x => x.Id);
                table.ForeignKey(
                    name: "FK_Incomes_Collections_CollectionId",
                    column: x => x.CollectionId,
                    principalTable: "Collections",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Incomes_Customers_CustomerId",
                    column: x => x.CustomerId,
                    principalTable: "Customers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Incomes_Events_EventId",
                    column: x => x.EventId,
                    principalTable: "Events",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Incomes_IncomeCategories_IncomeCategoryId",
                    column: x => x.IncomeCategoryId,
                    principalTable: "IncomeCategories",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Refunds",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RefundedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<int>(type: "int", nullable: false),
                ExchangeRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                AmountTry = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                FinancialAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                IsCancelled = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Refunds", x => x.Id);
                table.ForeignKey(
                    name: "FK_Refunds_Collections_CollectionId",
                    column: x => x.CollectionId,
                    principalTable: "Collections",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Refunds_Contracts_ContractId",
                    column: x => x.ContractId,
                    principalTable: "Contracts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.InsertData(
            table: "Branches",
            columns: new[] { "Id", "Address", "Code", "CreatedAt", "CreatedBy", "IsActive", "IsDeleted", "Name", "Phone", "UpdatedAt", "UpdatedBy" },
            values: new object[] { new Guid("10000000-0000-0000-0000-000000000001"), null, "MRKZ", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, false, "Merkez Şube", null, null, null });

        migrationBuilder.InsertData(
            table: "ExpenseCategories",
            columns: new[] { "Id", "BranchId", "CreatedAt", "CreatedBy", "IsActive", "IsDeleted", "Name", "UpdatedAt", "UpdatedBy" },
            values: new object[] { new Guid("40000000-0000-0000-0000-000000000001"), new Guid("10000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, false, "Diğer Gider", null, null });

        migrationBuilder.InsertData(
            table: "IncomeCategories",
            columns: new[] { "Id", "BranchId", "CreatedAt", "CreatedBy", "IsActive", "IsDeleted", "Name", "UpdatedAt", "UpdatedBy" },
            values: new object[] { new Guid("30000000-0000-0000-0000-000000000001"), new Guid("10000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, false, "Diğer Gelir", null, null });

        migrationBuilder.InsertData(
            table: "Roles",
            columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "IsDeleted", "Name", "UpdatedAt", "UpdatedBy" },
            values: new object[,]
            {
                { new Guid("20000000-0000-0000-0000-000000000001"), "ADMIN", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, "Yönetici", null, null },
                { new Guid("20000000-0000-0000-0000-000000000002"), "SALES", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, "Satış", null, null },
                { new Guid("20000000-0000-0000-0000-000000000003"), "ACCOUNTING", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, "Muhasebe", null, null },
                { new Guid("20000000-0000-0000-0000-000000000004"), "VIEWER", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, false, "Görüntüleme", null, null }
            });

        migrationBuilder.CreateIndex(
            name: "IX_ApplicationSettings_BranchId_Key",
            table: "ApplicationSettings",
            columns: new[] { "BranchId", "Key" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_Attachments_EntityName_EntityId",
            table: "Attachments",
            columns: new[] { "EntityName", "EntityId" });

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_BranchId_CreatedAt",
            table: "AuditLogs",
            columns: new[] { "BranchId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_UserId_CreatedAt",
            table: "AuditLogs",
            columns: new[] { "UserId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_BackupRecords_BranchId_StartedAt",
            table: "BackupRecords",
            columns: new[] { "BranchId", "StartedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_BankAccounts_BranchId_Code",
            table: "BankAccounts",
            columns: new[] { "BranchId", "Code" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_BankAccounts_Iban",
            table: "BankAccounts",
            column: "Iban",
            filter: "[Iban] IS NOT NULL AND [IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_Branches_Code",
            table: "Branches",
            column: "Code",
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_CashAccounts_BranchId_Code",
            table: "CashAccounts",
            columns: new[] { "BranchId", "Code" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_Collections_BranchId_ReceiptNumber",
            table: "Collections",
            columns: new[] { "BranchId", "ReceiptNumber" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_Collections_CollectedAt",
            table: "Collections",
            column: "CollectedAt");

        migrationBuilder.CreateIndex(
            name: "IX_Collections_ContractId",
            table: "Collections",
            column: "ContractId");

        migrationBuilder.CreateIndex(
            name: "IX_Collections_IdempotencyKey",
            table: "Collections",
            column: "IdempotencyKey",
            unique: true,
            filter: "[IdempotencyKey] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Collections_PaymentPlanId",
            table: "Collections",
            column: "PaymentPlanId");

        migrationBuilder.CreateIndex(
            name: "IX_Contracts_BranchId_ContractNumber",
            table: "Contracts",
            columns: new[] { "BranchId", "ContractNumber" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_Contracts_EventId",
            table: "Contracts",
            column: "EventId",
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_Customers_BranchId_Phone",
            table: "Customers",
            columns: new[] { "BranchId", "Phone" });

        migrationBuilder.CreateIndex(
            name: "IX_Customers_NationalIdentityNumber",
            table: "Customers",
            column: "NationalIdentityNumber",
            filter: "[NationalIdentityNumber] IS NOT NULL AND [IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_Events_BranchId_EventNumber",
            table: "Events",
            columns: new[] { "BranchId", "EventNumber" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_Events_CustomerId",
            table: "Events",
            column: "CustomerId");

        migrationBuilder.CreateIndex(
            name: "IX_Events_EventTypeId",
            table: "Events",
            column: "EventTypeId");

        migrationBuilder.CreateIndex(
            name: "IX_Events_HallId_StartsAt_EndsAt_Status",
            table: "Events",
            columns: new[] { "HallId", "StartsAt", "EndsAt", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_Events_StartsAt",
            table: "Events",
            column: "StartsAt");

        migrationBuilder.CreateIndex(
            name: "IX_EventServices_EventId",
            table: "EventServices",
            column: "EventId");

        migrationBuilder.CreateIndex(
            name: "IX_EventServices_ExtraServiceId",
            table: "EventServices",
            column: "ExtraServiceId");

        migrationBuilder.CreateIndex(
            name: "IX_EventServices_PackageId",
            table: "EventServices",
            column: "PackageId");

        migrationBuilder.CreateIndex(
            name: "IX_EventStatusHistories_EventId_CreatedAt",
            table: "EventStatusHistories",
            columns: new[] { "EventId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_EventTypes_BranchId_Name",
            table: "EventTypes",
            columns: new[] { "BranchId", "Name" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_ExpenseCategories_BranchId_Name",
            table: "ExpenseCategories",
            columns: new[] { "BranchId", "Name" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_Expenses_DocumentNumber",
            table: "Expenses",
            column: "DocumentNumber");

        migrationBuilder.CreateIndex(
            name: "IX_Expenses_DueDate",
            table: "Expenses",
            column: "DueDate");

        migrationBuilder.CreateIndex(
            name: "IX_Expenses_EventId",
            table: "Expenses",
            column: "EventId");

        migrationBuilder.CreateIndex(
            name: "IX_Expenses_ExpenseCategoryId",
            table: "Expenses",
            column: "ExpenseCategoryId");

        migrationBuilder.CreateIndex(
            name: "IX_Expenses_ExpenseDate",
            table: "Expenses",
            column: "ExpenseDate");

        migrationBuilder.CreateIndex(
            name: "IX_Expenses_SupplierId",
            table: "Expenses",
            column: "SupplierId");

        migrationBuilder.CreateIndex(
            name: "IX_ExtraServices_BranchId_Name",
            table: "ExtraServices",
            columns: new[] { "BranchId", "Name" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_FinancialTransactions_AccountId_TransactionDate",
            table: "FinancialTransactions",
            columns: new[] { "AccountId", "TransactionDate" });

        migrationBuilder.CreateIndex(
            name: "IX_FinancialTransactions_DocumentNumber",
            table: "FinancialTransactions",
            column: "DocumentNumber");

        migrationBuilder.CreateIndex(
            name: "IX_FinancialTransactions_IdempotencyKey",
            table: "FinancialTransactions",
            column: "IdempotencyKey",
            unique: true,
            filter: "[IdempotencyKey] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Halls_BranchId_Code",
            table: "Halls",
            columns: new[] { "BranchId", "Code" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_IncomeCategories_BranchId_Name",
            table: "IncomeCategories",
            columns: new[] { "BranchId", "Name" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_Incomes_CollectionId",
            table: "Incomes",
            column: "CollectionId",
            unique: true,
            filter: "[CollectionId] IS NOT NULL AND [IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_Incomes_CustomerId",
            table: "Incomes",
            column: "CustomerId");

        migrationBuilder.CreateIndex(
            name: "IX_Incomes_DocumentNumber",
            table: "Incomes",
            column: "DocumentNumber");

        migrationBuilder.CreateIndex(
            name: "IX_Incomes_EventId",
            table: "Incomes",
            column: "EventId");

        migrationBuilder.CreateIndex(
            name: "IX_Incomes_IncomeCategoryId",
            table: "Incomes",
            column: "IncomeCategoryId");

        migrationBuilder.CreateIndex(
            name: "IX_Incomes_IncomeDate",
            table: "Incomes",
            column: "IncomeDate");

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_UserId_DueAt_ReadAt",
            table: "Notifications",
            columns: new[] { "UserId", "DueAt", "ReadAt" });

        migrationBuilder.CreateIndex(
            name: "IX_PackageItems_PackageId",
            table: "PackageItems",
            column: "PackageId");

        migrationBuilder.CreateIndex(
            name: "IX_Packages_BranchId_Name",
            table: "Packages",
            columns: new[] { "BranchId", "Name" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_PaymentPlans_ContractId",
            table: "PaymentPlans",
            column: "ContractId");

        migrationBuilder.CreateIndex(
            name: "IX_PaymentPlans_DueDate",
            table: "PaymentPlans",
            column: "DueDate");

        migrationBuilder.CreateIndex(
            name: "IX_Refunds_CollectionId",
            table: "Refunds",
            column: "CollectionId");

        migrationBuilder.CreateIndex(
            name: "IX_Refunds_ContractId",
            table: "Refunds",
            column: "ContractId");

        migrationBuilder.CreateIndex(
            name: "IX_Refunds_RefundedAt",
            table: "Refunds",
            column: "RefundedAt");

        migrationBuilder.CreateIndex(
            name: "IX_Roles_Code",
            table: "Roles",
            column: "Code",
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_Suppliers_BranchId_Name",
            table: "Suppliers",
            columns: new[] { "BranchId", "Name" });

        migrationBuilder.CreateIndex(
            name: "IX_Suppliers_TaxNumber",
            table: "Suppliers",
            column: "TaxNumber",
            filter: "[TaxNumber] IS NOT NULL AND [IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_SupplierTransactions_DocumentNumber",
            table: "SupplierTransactions",
            column: "DocumentNumber");

        migrationBuilder.CreateIndex(
            name: "IX_SupplierTransactions_EventId",
            table: "SupplierTransactions",
            column: "EventId");

        migrationBuilder.CreateIndex(
            name: "IX_SupplierTransactions_SupplierId_TransactionDate",
            table: "SupplierTransactions",
            columns: new[] { "SupplierId", "TransactionDate" });

        migrationBuilder.CreateIndex(
            name: "IX_Transfers_TransferDate",
            table: "Transfers",
            column: "TransferDate");

        migrationBuilder.CreateIndex(
            name: "IX_UserRoles_RoleId",
            table: "UserRoles",
            column: "RoleId");

        migrationBuilder.CreateIndex(
            name: "IX_UserRoles_UserId_RoleId",
            table: "UserRoles",
            columns: new[] { "UserId", "RoleId" },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_Users_BranchId_Username",
            table: "Users",
            columns: new[] { "BranchId", "Username" },
            unique: true,
            filter: "[IsDeleted] = 0");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ApplicationSettings");

        migrationBuilder.DropTable(
            name: "Attachments");

        migrationBuilder.DropTable(
            name: "AuditLogs");

        migrationBuilder.DropTable(
            name: "BackupRecords");

        migrationBuilder.DropTable(
            name: "BankAccounts");

        migrationBuilder.DropTable(
            name: "CashAccounts");

        migrationBuilder.DropTable(
            name: "EventServices");

        migrationBuilder.DropTable(
            name: "EventStatusHistories");

        migrationBuilder.DropTable(
            name: "Expenses");

        migrationBuilder.DropTable(
            name: "FinancialTransactions");

        migrationBuilder.DropTable(
            name: "Incomes");

        migrationBuilder.DropTable(
            name: "Notifications");

        migrationBuilder.DropTable(
            name: "PackageItems");

        migrationBuilder.DropTable(
            name: "Refunds");

        migrationBuilder.DropTable(
            name: "SupplierTransactions");

        migrationBuilder.DropTable(
            name: "Transfers");

        migrationBuilder.DropTable(
            name: "UserRoles");

        migrationBuilder.DropTable(
            name: "ExtraServices");

        migrationBuilder.DropTable(
            name: "ExpenseCategories");

        migrationBuilder.DropTable(
            name: "IncomeCategories");

        migrationBuilder.DropTable(
            name: "Packages");

        migrationBuilder.DropTable(
            name: "Collections");

        migrationBuilder.DropTable(
            name: "Suppliers");

        migrationBuilder.DropTable(
            name: "Roles");

        migrationBuilder.DropTable(
            name: "Users");

        migrationBuilder.DropTable(
            name: "PaymentPlans");

        migrationBuilder.DropTable(
            name: "Contracts");

        migrationBuilder.DropTable(
            name: "Events");

        migrationBuilder.DropTable(
            name: "Customers");

        migrationBuilder.DropTable(
            name: "EventTypes");

        migrationBuilder.DropTable(
            name: "Halls");

        migrationBuilder.DropTable(
            name: "Branches");
    }
}
