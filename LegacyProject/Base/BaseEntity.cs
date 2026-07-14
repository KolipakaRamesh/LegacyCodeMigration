namespace LegacyProject.Base;

/// <summary>
/// Abstract base class for all domain entities.
/// Provides identity, audit timestamps, and soft-delete support.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>Unique identifier for the entity.</summary>
    public Guid Id { get; protected set; } = Guid.NewGuid();

    /// <summary>UTC timestamp when the entity was created.</summary>
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the last update, or null if never updated.</summary>
    public DateTime? UpdatedAt { get; protected set; }

    /// <summary>Soft-delete flag. False means logically deleted.</summary>
    public bool IsActive { get; protected set; } = true;

    /// <summary>Sets the UpdatedAt timestamp to the current UTC time.</summary>
    public void MarkAsUpdated() => UpdatedAt = DateTime.UtcNow;

    /// <summary>Performs a soft delete by setting IsActive to false.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Reactivates a previously deactivated entity.</summary>
    public void Reactivate() => IsActive = true;
}
