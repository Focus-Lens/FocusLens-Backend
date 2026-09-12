using System.ComponentModel.DataAnnotations.Schema;

namespace FocusLens.Domain.Common;

public abstract class Entity
{
    private readonly List<DomainEvent> _domainEvents = [];

    protected Entity()
    {
    }

    protected Entity(Guid id)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
    }

    public Guid Id { get; private set; }

    [NotMapped] public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void RemoveDomainEvent(DomainEvent domainEvent) => _domainEvents.Remove(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}