namespace Moonforge.Core.Encounters
{

    public enum EncounterRollMode
    {
        /// <summary>Weighted random pick of exactly one entry. <c>ChancePercent</c> is ignored.</summary>
        PickOne = 0,

        /// <summary>Independent roll per entry; <c>ChancePercent</c> (0-100) gates each. <c>Weight</c> is ignored.</summary>
        RollEach = 1
    }
}
