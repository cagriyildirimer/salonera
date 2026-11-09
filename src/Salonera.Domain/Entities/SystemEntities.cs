using Salonera.Domain.Common;
using Salonera.Domain.Enums;

namespace Salonera.Domain.Entities;

public sealed class Attachment : AuditableEntity
{
    public Guid BranchId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Checksum { get; set; } = string.Empty;
}

public sealed class Notification : AuditableEntity
{
    public Guid BranchId { get; set; }
    public Guid? UserId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset DueAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public string? RelatedEntityName { get; set; }
    public Guid? RelatedEntityId { get; set; }
}

public sealed class ApplicationSetting : AuditableEntity
{
    public Guid? BranchId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool IsEncrypted { get; set; }
}

public sealed class BackupRecord : AuditableEntity
{
    public Guid BranchId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string? Checksum { get; set; }
    public long? FileSize { get; set; }
    public bool IsSuccessful { get; set; }
    public bool IntegrityVerified { get; set; }
    public string? ErrorMessage { get; set; }
}
