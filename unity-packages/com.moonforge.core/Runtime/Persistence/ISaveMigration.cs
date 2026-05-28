namespace Moonforge.Core.Persistence
{

    /// <summary>
    /// A single, ordered transformation applied to a save payload to bring it from
    /// <see cref="FromVersion"/> to the next schema version. Migrations operate on the
    /// raw JSON string so that fields removed between versions can still be read.
    /// </summary>
    public interface ISaveMigration
    {
        int FromVersion { get; }

        string Migrate(string payload);
    }
}
