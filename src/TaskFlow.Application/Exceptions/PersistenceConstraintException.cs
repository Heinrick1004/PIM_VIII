namespace TaskFlow.Application.Exceptions;

public enum PersistenceConstraintKind
{
    Unique,
    ForeignKeyOrCheck
}

public sealed class PersistenceConstraintException : Exception
{
    public PersistenceConstraintException(PersistenceConstraintKind kind, Exception innerException)
        : base("A persistência violou uma restrição de integridade.", innerException)
    {
        Kind = kind;
    }

    public PersistenceConstraintKind Kind { get; }
}
