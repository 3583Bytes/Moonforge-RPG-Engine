namespace Moonforge.Sample.Roguelike.Session
{

    /// <summary>
    /// Modal notice surfaced when a contract starts, completes, or is turned in.
    /// </summary>
    public sealed record ContractNoticeSnapshot(
        string Title,
        string Body);
}
