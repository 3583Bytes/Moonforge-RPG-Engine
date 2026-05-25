namespace Moonforge.Core.Runtime.Results;

public readonly struct DomainResult
{
    private DomainResult(bool isSuccess, DomainError? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public DomainError? Error { get; }

    public static DomainResult Success() => new(true, null);

    public static DomainResult Fail(DomainError error) => new(false, error);
}
