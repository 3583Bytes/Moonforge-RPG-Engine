namespace Moonforge.Sample.MonsterCatcher.Content;

/// <summary>
/// The Champion's roster — a five-mon team you fight on the Goal tile after clearing all
/// eight gyms. Modeled as a <see cref="GymLeader"/> with gym number 9 for sub-battle reuse,
/// even though it's not a true gym.
/// </summary>
internal static class Champion
{
    public static GymLeader Encounter { get; } = new GymLeader(
        gymNumber: 9,
        displayName: "Auriel",
        title: "Reigning Champion",
        badgeId: "champion.crest",
        badgeName: "Champion's Crest",
        roster: new[]
        {
            (SpeciesIds.Pebblet, 34),
            (SpeciesIds.Wisplet, 35),
            (SpeciesIds.Cinderfox, 36),
            (SpeciesIds.Hydrofin, 36),
            (SpeciesIds.Leafwing, 38)
        },
        goldReward: 10000,
        introLine: "Auriel: \"You've come a long way. The road wants to see what you've become. So do I.\"",
        victoryLine: "Auriel: \"The road bends to you now. You are the Champion.\"",
        defeatLine: "Auriel: \"Not yet. Train. Catch. Return.\"");
}
