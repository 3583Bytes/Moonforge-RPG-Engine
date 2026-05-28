namespace Moonforge.Core.Combat
{

    public enum StatusStackPolicy
    {
        /// <summary>If already active, replace the remaining duration with the newly-applied value.</summary>
        RefreshDuration = 0,

        /// <summary>If already active, do nothing — the new application is silently ignored.</summary>
        IgnoreIfPresent = 1
    }
}
