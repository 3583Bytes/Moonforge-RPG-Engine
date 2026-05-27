namespace Moonforge.Core.Runtime.Results
{

    public sealed class DomainError
    {
        public DomainError(DomainErrorCode code, string message)
        {
            Code = code;
            Message = message;
        }

        public DomainErrorCode Code { get; }

        public string Message { get; }
    }
}
