using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Tests;

public sealed class DomainResultTests
{
    [Fact]
    public void Success_Result_Has_No_Error()
    {
        DomainResult result = DomainResult.Success();

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_Result_Contains_Error()
    {
        DomainError error = new(DomainErrorCode.ValidationFailed, "Invalid command input.");
        DomainResult result = DomainResult.Fail(error);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(DomainErrorCode.ValidationFailed, result.Error!.Code);
    }
}
