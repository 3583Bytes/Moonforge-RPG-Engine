namespace Moonforge.Core.Runtime.Events
{

    public sealed class WarningEvent : DomainEvent
    {
        public WarningEvent(string code, string message)
            : base(nameof(WarningEvent))
        {
            Code = code;
            Message = message;
        }

        public string Code { get; }

        public string Message { get; }
    }
}
