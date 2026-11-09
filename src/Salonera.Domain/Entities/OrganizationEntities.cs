using Salonera.Domain.Common;
using Salonera.Domain.Enums;

namespace Salonera.Domain.Entities;

public sealed class Branch : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Hall> Halls { get; set; } = [];
}

public sealed class Hall : AuditableEntity
{
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Customer : AuditableEntity
{
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? SecondaryPhone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? NationalIdentityNumber { get; set; }
    public string? BrideName { get; set; }
    public string? GroomName { get; set; }
    public string? ContactPerson { get; set; }
    public string? Notes { get; set; }
    public string? KvkkConsentNote { get; set; }
}

public sealed class EventType : AuditableEntity
{
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class OrganizationEvent : AuditableEntity
{
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public Guid HallId { get; set; }
    public Hall Hall { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid EventTypeId { get; set; }
    public EventType EventType { get; set; } = null!;
    public string EventNumber { get; set; } = string.Empty;
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public DateTimeOffset? OptionExpiresAt { get; set; }
    public int GuestCount { get; set; }
    public EventStatus Status { get; set; }
    public Guid? SalesRepresentativeId { get; set; }
    public string? SpecialRequests { get; set; }
    public string? InternalNotes { get; set; }
    public DateTimeOffset? CancellationDate { get; set; }
    public string? CancellationReason { get; set; }
    public ICollection<EventStatusHistory> StatusHistory { get; set; } = [];
    public ICollection<EventService> Services { get; set; } = [];
    public Contract? Contract { get; set; }
}

public sealed class EventStatusHistory : AuditableEntity
{
    public Guid EventId { get; set; }
    public OrganizationEvent Event { get; set; } = null!;
    public EventStatus PreviousStatus { get; set; }
    public EventStatus NewStatus { get; set; }
    public string? Reason { get; set; }
}

public sealed class Package : AuditableEntity
{
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public int IncludedGuestCount { get; set; }
    public decimal SalesPrice { get; set; }
    public decimal CostPrice { get; set; }
    public decimal? PricePerPerson { get; set; }
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidUntil { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<PackageItem> Items { get; set; } = [];
}

public sealed class PackageItem : AuditableEntity
{
    public Guid PackageId { get; set; }
    public Package Package { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}

public sealed class ExtraService : AuditableEntity
{
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public decimal SalesPrice { get; set; }
    public decimal CostPrice { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class EventService : AuditableEntity
{
    public Guid EventId { get; set; }
    public OrganizationEvent Event { get; set; } = null!;
    public Guid? PackageId { get; set; }
    public Package? Package { get; set; }
    public Guid? ExtraServiceId { get; set; }
    public ExtraService? ExtraService { get; set; }
    public string DescriptionSnapshot { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitSalesPriceSnapshot { get; set; }
    public decimal UnitCostSnapshot { get; set; }
}

public sealed class Contract : AuditableEntity
{
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public Guid EventId { get; set; }
    public OrganizationEvent Event { get; set; } = null!;
    public string ContractNumber { get; set; } = string.Empty;
    public DateTimeOffset ContractDate { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal NetAmount { get; set; }
    public CurrencyCode Currency { get; set; }
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal NetAmountTry { get; set; }
    public ICollection<PaymentPlan> PaymentPlans { get; set; } = [];
}
