using System;

namespace Moonforge.Core.Runtime.Results
{

    public sealed class DomainError
    {
        public DomainError(DomainErrorCode code, string message)
            : this(code, message, exception: null)
        {
        }

        public DomainError(DomainErrorCode code, string message, Exception? exception)
        {
            Code = code;
            Message = message;
            Exception = exception;
        }

        public DomainErrorCode Code { get; }

        public string Message { get; }

        /// <summary>
        /// The exception that caused this error, when the failure came from a thrown exception
        /// rather than an expected domain failure (set by the dispatcher's rollback path).
        /// Carries the full stack trace and inner-exception chain for logging — null for
        /// ordinary domain failures.
        /// </summary>
        public Exception? Exception { get; }
    }
}
