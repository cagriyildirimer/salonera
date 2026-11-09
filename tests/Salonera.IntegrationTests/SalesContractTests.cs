using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Salonera.Application.Common.Security;
using Salonera.Application.Common.Time;
using Salonera.Application.Features.Sales;
using Salonera.Application.Features.Payments;
using Salonera.Application.Features.Finance;
using Salonera.Application.Features.Events;
using Salonera.Application.Features.Reports;
using Salonera.Domain.Entities;
using Salonera.Domain.Enums;
using Salonera.Infrastructure.Documents;
using Salonera.Infrastructure.Features;
using Salonera.Infrastructure.Persistence;
using Salonera.Infrastructure.Security;

namespace Salonera.IntegrationTests;

public sealed class SalesContractTests
{
    [Fact]
    public void DiscountVatAndExchangeRateAreCalculatedWithDecimalPrecision()
    {
        var service = new ContractPricingService();

        var result = service.Calculate(
            [new PriceLine("Paket", 1m, 1000m, 600m), new PriceLine("Fotoğraf", 1m, 200m, 100m)],
            100m,
            20m,
            CurrencyCode.Eur,
            40m);

        Assert.Equal(1200m, result.GrossAmount);
        Assert.Equal(1100m, result.TaxableAmount);
        Assert.Equal(220m, result.VatAmount);
        Assert.Equal(1320m, result.NetAmount);
        Assert.Equal(52800m, result.NetAmountTry);
    }

    [Fact]
    public async Task PackagePricesAreSnapshottedWhenContractIsCreated()
    {
        var setup = await CreateSetupAsync();
        await using var provider = setup.Provider;
        var service = provider.GetRequiredService<ISalesContractService>();
        var contractId = await service.CreateContractAsync(new CreateContractRequest(
            setup.EventId,
            setup.PackageId,
            [setup.ExtraServiceId],
            100m,
            20m,
            CurrencyCode.Try,
            1m));

        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
            var package = await context.Packages.SingleAsync(item => item.Id == setup.PackageId);
            package.SalesPrice = 9999m;
            await context.SaveChangesAsync();
        }

        var document = await service.GetContractDocumentAsync(contractId);

        Assert.Equal(1000m, document.Lines.Single(line => line.Description == "Düğün Paketi").UnitPrice);
        Assert.Equal(1320m, document.Totals.NetAmount);
        await using var verificationScope = provider.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var organizationEvent = await verificationContext.Events.SingleAsync(item => item.Id == setup.EventId);
        Assert.Equal(EventStatus.Contracted, organizationEvent.Status);
        Assert.Contains(
            await verificationContext.EventStatusHistories.ToListAsync(),
            item => item.EventId == setup.EventId && item.NewStatus == EventStatus.Contracted);
    }

    [Fact]
    public async Task ContractPdfRendersAsValidPdfFile()
    {
        var requestedOutput = Environment.GetEnvironmentVariable("SALONERA_PDF_OUTPUT");
        var outputPath = requestedOutput ?? Path.Combine(
            Path.GetTempPath(),
            "salonera-tests",
            $"contract-{Guid.NewGuid():N}.pdf");
        try
        {
            var renderer = new ContractPdfService(new UnusedSalesContractService());
            await renderer.RenderAsync(SampleDocument(), outputPath);

            var bytes = await File.ReadAllBytesAsync(outputPath);
            Assert.True(bytes.Length > 5000);
            Assert.Equal("%PDF-"u8.ToArray(), bytes[..5]);
        }
        finally
        {
            if (requestedOutput is null && File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public async Task PaymentsPreserveBalanceAndCreateReversingAccountMovements()
    {
        var setup = await CreateSetupAsync();
        await using var provider = setup.Provider;
        var contractId = await provider.GetRequiredService<ISalesContractService>().CreateContractAsync(
            new CreateContractRequest(setup.EventId, setup.PackageId, [], 0m, 0m, CurrencyCode.Try, 1m));
        var payments = provider.GetRequiredService<IPaymentService>();
        await payments.SetPlanAsync(contractId,
        [new NewInstallment(new DateOnly(2026, 11, 10), 200m),
         new NewInstallment(new DateOnly(2026, 12, 10), 800m)]);
        var initial = await payments.GetBalanceAsync(contractId);
        var firstPlan = initial.Installments[0].Id;
        var key = Guid.NewGuid();
        var receiptId = await payments.CollectAsync(
            new CollectPaymentRequest(contractId, firstPlan, 100m, 1m, PaymentMethod.Cash, key));
        Assert.Equal(receiptId, await payments.CollectAsync(
            new CollectPaymentRequest(contractId, firstPlan, 100m, 1m, PaymentMethod.Cash, key)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => payments.CollectAsync(
            new CollectPaymentRequest(contractId, firstPlan, 101m, 1m, PaymentMethod.Cash, Guid.NewGuid())));
        var partial = await payments.GetBalanceAsync(contractId);
        Assert.Equal(900m, partial.Remaining);
        Assert.Equal(PaymentStatus.PartiallyPaid, partial.Installments[0].Status);
        await payments.RefundAsync(new RefundPaymentRequest(receiptId, 40m, "Müşteri talebi"));
        var refunded = await payments.GetBalanceAsync(contractId);
        Assert.Equal(940m, refunded.Remaining);
        Assert.Equal(60m, refunded.Installments[0].Paid);
        await payments.CancelReceiptAsync(receiptId, "Yanlış kayıt");
        await payments.CancelReceiptAsync(receiptId, "Tekrar iptal");
        var afterRefundedCancellation = await payments.GetBalanceAsync(contractId);
        Assert.Equal(1000m, afterRefundedCancellation.Remaining);
        Assert.Equal(0m, afterRefundedCancellation.Refunded);
        Assert.Equal(0m, afterRefundedCancellation.Installments[0].Paid);
        Assert.True(afterRefundedCancellation.Receipts.Single(x => x.Id == receiptId).IsCancelled);
        Assert.Equal(40m, afterRefundedCancellation.Receipts.Single(x => x.Id == receiptId).Refunded);
        var secondId = await payments.CollectAsync(
            new CollectPaymentRequest(contractId, firstPlan, 50m, 1m, PaymentMethod.Bank, Guid.NewGuid()));
        await payments.CancelReceiptAsync(secondId, "Yanlış hesap");
        var final = await payments.GetBalanceAsync(contractId);
        Assert.Equal(1000m, final.Remaining);
        var fullyRefundedId = await payments.CollectAsync(
            new CollectPaymentRequest(contractId, firstPlan, 30m, 1m, PaymentMethod.Cash, Guid.NewGuid()));
        await payments.RefundAsync(new RefundPaymentRequest(fullyRefundedId, 30m, "Tam iade"));
        await payments.CancelReceiptAsync(fullyRefundedId, "Tam iadeli tahsilat iptali");
        Assert.Equal(1000m, (await payments.GetBalanceAsync(contractId)).Remaining);
        var pdfPath = Path.Combine(Path.GetTempPath(), $"salonera-receipt-{Guid.NewGuid():N}.pdf");
        try
        {
            await new ReceiptPdfService(payments).GenerateAsync(receiptId, pdfPath);
            Assert.Equal("%PDF-"u8.ToArray(), (await File.ReadAllBytesAsync(pdfPath))[..5]);
        }
        finally
        {
            if (File.Exists(pdfPath)) File.Delete(pdfPath);
        }
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var movements = await db.Set<FinancialTransaction>().ToListAsync();
        Assert.Equal(7, movements.Count);
        Assert.Equal(0m, movements.Sum(x => x.Amount));
    }

    [Fact]
    public async Task SqlServerSchedulingAndContractWorkWithRetryStrategy()
    {
        var server = Environment.GetEnvironmentVariable("SALONERA_SQL_TEST_SERVER");
        if (string.IsNullOrWhiteSpace(server)) return;
        var databaseName = $"SaloneraTest_{Guid.NewGuid():N}";
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = databaseName,
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            MultipleActiveResultSets = true,
        };
        try
        {
            var setup = await CreateSetupAsync(builder.ConnectionString, enableRetry: true);
            await using var provider = setup.Provider;
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
            var seededEvent = await db.Events.SingleAsync(x => x.Id == setup.EventId);
            var request = new ScheduleEventRequest(
                seededEvent.HallId,
                seededEvent.CustomerId,
                seededEvent.EventTypeId,
                new DateTimeOffset(2027, 6, 13, 18, 0, 0, TimeSpan.FromHours(3)),
                new DateTimeOffset(2027, 6, 13, 22, 0, 0, TimeSpan.FromHours(3)),
                150,
                EventStatus.Contracted);
            var scheduling = provider.GetRequiredService<IEventSchedulingService>();
            var createdId = await scheduling.ScheduleAsync(request);

            Assert.NotEqual(Guid.Empty, createdId);
            Assert.Equal(2, await db.Events.CountAsync());
            await Assert.ThrowsAsync<SchedulingConflictException>(() => scheduling.ScheduleAsync(request));
            Assert.Equal(2, await db.Events.CountAsync());

            var contracts = provider.GetRequiredService<ISalesContractService>();
            var contractId = await contracts.CreateContractAsync(new CreateContractRequest(
                createdId, setup.PackageId, [], 0m, 20m, CurrencyCode.Try, 1m));
            Assert.NotEqual(Guid.Empty, contractId);
            Assert.Equal(contractId, await contracts.FindContractIdForEventAsync(createdId));
            Assert.Equal(contractId, (await db.Contracts.SingleAsync()).Id);
            Assert.Equal(EventStatus.Contracted, (await db.Events.AsNoTracking().SingleAsync(x => x.Id == createdId)).Status);
            var pdfPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Salonera", "Sozlesmeler", $"sozlesme-{contractId:N}.pdf");
            try
            {
                await new ContractPdfService(contracts).GenerateAsync(contractId, pdfPath);
                Assert.Equal("%PDF-"u8.ToArray(), (await File.ReadAllBytesAsync(pdfPath))[..5]);
            }
            finally
            {
                if (File.Exists(pdfPath)) File.Delete(pdfPath);
            }
            await Assert.ThrowsAsync<InvalidOperationException>(() => contracts.CreateContractAsync(
                new CreateContractRequest(createdId, setup.PackageId, [], 0m, 20m, CurrencyCode.Try, 1m)));
            Assert.Equal(1, await db.Contracts.CountAsync());
        }
        finally
        {
            var master = new SqlConnectionStringBuilder(builder.ConnectionString)
            { InitialCatalog = "master", Pooling = false };
            await using var connection = new SqlConnection(master.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"IF DB_ID('{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END";
            await command.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task SqlServerPaymentWritesReceiptAndAccountMovementTogether()
    {
        var server = Environment.GetEnvironmentVariable("SALONERA_SQL_TEST_SERVER");
        if (string.IsNullOrWhiteSpace(server)) return;
        var databaseName = $"SaloneraTest_{Guid.NewGuid():N}";
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = databaseName,
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            MultipleActiveResultSets = true,
        };
        try
        {
            var setup = await CreateSetupAsync(builder.ConnectionString);
            await using var provider = setup.Provider;
            var contractId = await provider.GetRequiredService<ISalesContractService>().CreateContractAsync(
                new CreateContractRequest(setup.EventId, setup.PackageId, [], 0m, 0m, CurrencyCode.Try, 1m));
            var payments = provider.GetRequiredService<IPaymentService>();
            var selectedCashId = await provider.GetRequiredService<IFinanceService>()
                .CreateAccountAsync(new NewFinancialAccount(AccountType.Cash, "MANUAL-CASH",
                    "Tahsilat Test Kasası", CurrencyCode.Try, 0m));
            await payments.SetPlanAsync(contractId,
            [new NewInstallment(new DateOnly(2026, 11, 10), 200m),
             new NewInstallment(new DateOnly(2026, 12, 10), 800m)]);
            var planId = (await payments.GetBalanceAsync(contractId)).Installments[0].Id;
            var receiptId = await payments.CollectAsync(new CollectPaymentRequest(
                contractId, planId, 200m, 1m, PaymentMethod.Cash, Guid.NewGuid(), selectedCashId));
            var receiptPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Salonera", "Makbuzlar", $"makbuz-{receiptId:N}.pdf");
            try
            {
                await new ReceiptPdfService(payments).GenerateAsync(receiptId, receiptPath);
                Assert.Equal("%PDF-"u8.ToArray(), (await File.ReadAllBytesAsync(receiptPath))[..5]);
            }
            finally
            {
                if (File.Exists(receiptPath)) File.Delete(receiptPath);
            }
            var balance = await payments.GetBalanceAsync(contractId);
            Assert.Equal(800m, balance.Remaining);
            Assert.Contains(balance.Receipts, x => x.Id == receiptId);
            await payments.RefundAsync(new RefundPaymentRequest(receiptId, 50m, "İade testi"));
            await payments.CancelReceiptAsync(receiptId, "İadeli tahsilatı iptal testi");
            Assert.Equal(1000m, (await payments.GetBalanceAsync(contractId)).Remaining);
            Assert.Equal(0m, (await payments.GetBalanceAsync(contractId)).Installments[0].Paid);
            var secondId = await payments.CollectAsync(new CollectPaymentRequest(
                contractId, null, 50m, 1m, PaymentMethod.Bank, Guid.NewGuid()));
            await payments.CancelReceiptAsync(secondId, "İptal testi");
            Assert.Equal(1000m, (await payments.GetBalanceAsync(contractId)).Remaining);
            Assert.Equal(0m, (await payments.GetBalanceAsync(contractId)).Installments[0].Paid);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
            Assert.Equal(selectedCashId, (await db.Collections.SingleAsync(x => x.Id == receiptId)).FinancialAccountId);
            Assert.Equal(5, await db.Set<FinancialTransaction>().CountAsync());
            Assert.Equal(0m, await db.Set<FinancialTransaction>().SumAsync(x => x.Amount));
            Assert.Single(await db.CashAccounts.ToListAsync());
            await RunFinanceScenarioAsync(provider, setup.EventId);
            var reports = provider.GetRequiredService<IReportService>();
            var customerBalances = await reports.GetAsync(ReportKind.CustomerBalances,
                new ReportFilter(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)));
            Assert.Contains(customerBalances.Rows, x => x.Cells[^1] == "1.000,00");
            var eventProfit = await reports.GetAsync(ReportKind.EventProfit,
                new ReportFilter(new DateOnly(2027, 6, 1), new DateOnly(2027, 6, 30)));
            Assert.Contains(eventProfit.Rows, x => x.Cells[^1] == "400,00");
            var incomeExpense = await reports.GetAsync(ReportKind.IncomeExpense,
                new ReportFilter(new DateOnly(2027, 6, 1), new DateOnly(2027, 6, 30)));
            Assert.Contains(incomeExpense.Rows, x => x.Cells[^1] == "400,00");
            var suppliers = await reports.GetAsync(ReportKind.SupplierBalances,
                new ReportFilter(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)));
            Assert.All(suppliers.Rows, x => Assert.Equal("0,00", x.Cells[^1]));
            var dailyCash = await reports.GetAsync(ReportKind.DailyCash,
                new ReportFilter(new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 9)));
            Assert.Contains(dailyCash.Rows, x => x.Cells[1] == "Test Kasa" && x.Cells[^1] == "1.000,00");
        }
        finally
        {
            var master = new SqlConnectionStringBuilder(builder.ConnectionString)
            { InitialCatalog = "master", Pooling = false };
            await using var connection = new SqlConnection(master.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"IF DB_ID('{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END";
            await command.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task ReportExportsCreateValidCsvExcelAndPdf()
    {
        var report = new ReportTable("Türkçe Rapor", ["Ad", "Tutar (TRY)"],
            [new ReportRow(["Düğün", "1.234,50"])], "Test");
        var exporter = new ReportExportService();
        var directory = Path.Combine(Path.GetTempPath(), "salonera-report-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var csv = await exporter.ExportAsync(report, Path.Combine(directory, "report.csv"));
            Assert.Contains("Düğün", await File.ReadAllTextAsync(csv));
            var excel = await exporter.ExportAsync(report, Path.Combine(directory, "report.xlsx"));
            await exporter.ExportAsync(report, excel);
            using (var zip = System.IO.Compression.ZipFile.OpenRead(excel))
            {
                var sheet = zip.GetEntry("xl/worksheets/sheet1.xml");
                Assert.NotNull(sheet);
                using var reader = new StreamReader(sheet.Open());
                Assert.Contains("<v>1234.50</v>", await reader.ReadToEndAsync());
            }
            var pdf = await exporter.ExportAsync(report, Path.Combine(directory, "report.pdf"));
            Assert.Equal("%PDF-"u8.ToArray(), (await File.ReadAllBytesAsync(pdf))[..5]);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task FinanceEntriesBalanceAccountsAndSupplierLedger()
    {
        var setup = await CreateSetupAsync();
        await using var provider = setup.Provider;
        await RunFinanceScenarioAsync(provider, setup.EventId);
    }

    private static async Task RunFinanceScenarioAsync(ServiceProvider provider, Guid eventId)
    {
        var finance = provider.GetRequiredService<IFinanceService>();
        var cashId = await finance.CreateAccountAsync(new NewFinancialAccount(
            AccountType.Cash, $"KASA-{Guid.NewGuid():N}"[..18], "Test Kasa", CurrencyCode.Try, 1000m));
        var bankId = await finance.CreateAccountAsync(new NewFinancialAccount(
            AccountType.Bank, $"BANKA-{Guid.NewGuid():N}"[..18], "Test Banka", CurrencyCode.Try, 0m));
        var supplierId = await finance.CreateSupplierAsync(new NewSupplier("Test Tedarikçisi"));
        var incomeCategoryId = await finance.CreateIncomeCategoryAsync("Test Gelir");
        var expenseCategoryId = await finance.CreateExpenseCategoryAsync("Test Gider");
        var income = new NewIncome(Guid.NewGuid(), incomeCategoryId, cashId, AccountType.Cash,
            100m, CurrencyCode.Try, 1m, 20m);
        var incomeId = await finance.CreateIncomeAsync(income);
        Assert.Equal(incomeId, await finance.CreateIncomeAsync(income));
        var expenseId = await finance.CreateExpenseAsync(new NewExpense(
            Guid.NewGuid(), expenseCategoryId, 600m, CurrencyCode.Try, 1m, 20m,
            supplierId, eventId, new DateOnly(2026, 11, 15), "Test organizasyon gideri"));
        var payment = new PayExpense(expenseId, cashId, AccountType.Cash, 300m, Guid.NewGuid());
        await finance.PayExpenseAsync(payment);
        await finance.PayExpenseAsync(payment);
        Assert.Equal(300m, (await finance.ListSuppliersAsync()).Single(x => x.Id == supplierId).BalanceTry);
        Assert.Equal(600m, (await finance.ListEventCostsAsync()).Single(x => x.EventId == eventId).DirectExpenseTry);
        var transfer = new NewTransfer(Guid.NewGuid(), cashId, AccountType.Cash,
            bankId, AccountType.Bank, 200m, 1m);
        var transferId = await finance.TransferAsync(transfer);
        Assert.Equal(transferId, await finance.TransferAsync(transfer));
        await Assert.ThrowsAsync<InvalidOperationException>(() => finance.TransferAsync(
            new NewTransfer(Guid.NewGuid(), cashId, AccountType.Cash,
                bankId, AccountType.Bank, 10000m, 1m)));
        var accounts = await finance.ListAccountsAsync();
        Assert.Equal(600m, accounts.Single(x => x.Id == cashId).Balance);
        Assert.Equal(200m, accounts.Single(x => x.Id == bankId).Balance);
        await finance.CancelIncomeAsync(incomeId, "Test iptal");
        await finance.CancelTransferAsync(transferId, "Test iptal");
        await finance.CancelExpenseAsync(expenseId, "Test iptal");
        accounts = await finance.ListAccountsAsync();
        Assert.Equal(1000m, accounts.Single(x => x.Id == cashId).Balance);
        Assert.Equal(0m, accounts.Single(x => x.Id == bankId).Balance);
        Assert.Equal(0m, (await finance.ListSuppliersAsync()).Single(x => x.Id == supplierId).BalanceTry);
        Assert.All(await finance.ListEventCostsAsync(), x => Assert.Equal(0m, x.DirectExpenseTry));
    }

    private static ContractDocument SampleDocument()
    {
        var lines = new[]
        {
            new ContractLineDetail("Salon ve yemek paketi", 1m, 85000m, 85000m),
            new ContractLineDetail("Fotoğraf ve video", 1m, 12000m, 12000m),
            new ContractLineDetail("Özel masa süslemesi", 1m, 8000m, 8000m),
        };
        var totals = new PriceCalculation(
            105000m,
            5000m,
            100000m,
            20m,
            20000m,
            120000m,
            CurrencyCode.Try,
            1m,
            120000m);
        return new ContractDocument(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "SOZ-20261109-ORNEK1",
            new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.FromHours(3)),
            "Merkez Şube",
            "Deniz Yılmaz",
            "0500 000 00 00",
            "Büyük Salon",
            "Düğün",
            new DateTimeOffset(2027, 6, 12, 18, 0, 0, TimeSpan.FromHours(3)),
            new DateTimeOffset(2027, 6, 12, 23, 0, 0, TimeSpan.FromHours(3)),
            350,
            lines,
            totals);
    }

    private static async Task<SalesSetup> CreateSetupAsync(string? sqlConnectionString = null, bool enableRetry = false)
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddSingleton<IClock>(new FixedClock(new DateTimeOffset(2026, 11, 9, 10, 0, 0, TimeSpan.Zero)));
        services.AddSingleton<CurrentUser>();
        services.AddSingleton<ICurrentUser>(provider => provider.GetRequiredService<CurrentUser>());
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<IAuthorizationService, AuthorizationService>();
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<IContractPricingService, ContractPricingService>();
        services.AddSingleton<ISalesContractService, SalesContractService>();
        services.AddSingleton<IEventSchedulingService, EventSchedulingService>();
        services.AddSingleton<IPaymentService, PaymentService>();
        services.AddSingleton<IFinanceService, FinanceService>();
        services.AddSingleton<IReportService, ReportService>();
        services.AddDbContext<SaloneraDbContext>(options =>
        {
            if (sqlConnectionString is null) options.UseInMemoryDatabase(databaseName);
            else options.UseSqlServer(sqlConnectionString, sql =>
            {
                if (enableRetry) sql.EnableRetryOnFailure(3);
            });
        });
        var provider = services.BuildServiceProvider(validateScopes: true);

        var passwordService = provider.GetRequiredService<IPasswordService>();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        if (sqlConnectionString is not null) await context.Database.MigrateAsync();
        var branch = new Branch { Code = "TEST", Name = "Anonim Test Şubesi" };
        var hall = new Hall { Branch = branch, Code = "A", Name = "Salon A", Capacity = 500 };
        var customer = new Customer { Branch = branch, FirstName = "Anonim", LastName = "Müşteri", Phone = "05000000000" };
        var eventType = new EventType { Branch = branch, Name = "Düğün" };
        var package = new Package
        {
            Branch = branch,
            Name = "Düğün Paketi",
            IncludedGuestCount = 300,
            SalesPrice = 1000m,
            CostPrice = 600m,
        };
        var extra = new ExtraService { Branch = branch, Name = "Fotoğraf", SalesPrice = 200m, CostPrice = 100m };
        var role = sqlConnectionString is null
            ? new Role { Code = RoleCodes.Administrator, Name = "Yönetici" }
            : await context.Roles.SingleAsync(item => item.Code == RoleCodes.Administrator);
        var user = new User
        {
            Branch = branch,
            Username = "admin",
            DisplayName = "Test Yöneticisi",
            PasswordHash = passwordService.HashPassword("Anonim!Test2026"),
            MustChangePassword = false,
        };
        user.UserRoles.Add(new UserRole { User = user, Role = role });
        var organizationEvent = new OrganizationEvent
        {
            Branch = branch,
            Hall = hall,
            Customer = customer,
            EventType = eventType,
            EventNumber = "ORG-TEST-1",
            StartsAt = new DateTimeOffset(2027, 6, 12, 18, 0, 0, TimeSpan.FromHours(3)),
            EndsAt = new DateTimeOffset(2027, 6, 12, 23, 0, 0, TimeSpan.FromHours(3)),
            GuestCount = 300,
            Status = EventStatus.Option,
        };
        context.AddRange(branch, hall, customer, eventType, package, extra, user, organizationEvent);
        if (sqlConnectionString is null) context.Add(role);
        await context.SaveChangesAsync();

        var login = await provider.GetRequiredService<IAuthenticationService>().LoginAsync("admin", "Anonim!Test2026");
        Assert.True(login.Succeeded);
        return new SalesSetup(provider, organizationEvent.Id, package.Id, extra.Id);
    }

    private sealed record SalesSetup(ServiceProvider Provider, Guid EventId, Guid PackageId, Guid ExtraServiceId);

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
        public DateTimeOffset TurkeyNow => now.ToOffset(TimeSpan.FromHours(3));
    }

    private sealed class UnusedSalesContractService : ISalesContractService
    {
        public Task<Guid> CreateContractAsync(CreateContractRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Guid?> FindContractIdForEventAsync(Guid eventId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ContractDocument> GetContractDocumentAsync(Guid contractId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
