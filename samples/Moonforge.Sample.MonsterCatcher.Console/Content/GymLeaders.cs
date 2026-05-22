using System.Collections.Generic;

namespace Moonforge.Sample.MonsterCatcher.Content;

/// <summary>
/// One gym leader. Their roster is sent out one mon at a time as a chain of sub-battles —
/// player HP and PP persist across sub-battles but the trainer's mons are individual
/// fights. Defeating the leader grants the badge, gold, and unlocks the east passage on
/// the gym screen.
/// </summary>
internal sealed class GymLeader
{
    public GymLeader(
        int gymNumber,
        string displayName,
        string title,
        string badgeId,
        string badgeName,
        IReadOnlyList<(string speciesId, int level)> roster,
        long goldReward,
        string introLine,
        string victoryLine,
        string defeatLine)
    {
        GymNumber = gymNumber;
        DisplayName = displayName;
        Title = title;
        BadgeId = badgeId;
        BadgeName = badgeName;
        Roster = roster;
        GoldReward = goldReward;
        IntroLine = introLine;
        VictoryLine = victoryLine;
        DefeatLine = defeatLine;
    }

    public int GymNumber { get; }

    public string DisplayName { get; }

    /// <summary>Short descriptor shown under the leader's name (e.g. "Rock Warden").</summary>
    public string Title { get; }

    public string BadgeId { get; }

    public string BadgeName { get; }

    public IReadOnlyList<(string speciesId, int level)> Roster { get; }

    public long GoldReward { get; }

    /// <summary>Spoken when the gym battle starts.</summary>
    public string IntroLine { get; }

    /// <summary>Spoken when the player wins.</summary>
    public string VictoryLine { get; }

    /// <summary>Spoken when the player loses.</summary>
    public string DefeatLine { get; }
}

internal static class GymLeaders
{
    /// <summary>Indexed by gym number (1..8). Use <see cref="ForGymNumber"/> for safe access.</summary>
    public static IReadOnlyList<GymLeader> All { get; } = new List<GymLeader>
    {
        // Gym 1 — Rock. Wild encounter levels at screen 4 are ~5; leader at +3.
        new GymLeader(
            gymNumber: 1,
            displayName: "Brell",
            title: "Stone Warden",
            badgeId: BadgeIds.Stone,
            badgeName: "Stone Badge",
            roster: new[] { (SpeciesIds.Pebblet, 8), (SpeciesIds.Pebblet, 10) },
            goldReward: 200,
            introLine: "Brell: \"Mountains don't move for the timid. Show me your resolve.\"",
            victoryLine: "Brell: \"Hmph. The Stone Badge is yours. Go — the road wants you.\"",
            defeatLine: "Brell: \"Come back when stone runs in your veins.\""),

        // Gym 2 — Water. Screen 9, wild ~7; leader at +3.
        new GymLeader(
            gymNumber: 2,
            displayName: "Naia",
            title: "Tide Caller",
            badgeId: BadgeIds.Tide,
            badgeName: "Tide Badge",
            roster: new[] { (SpeciesIds.Bubbleling, 12), (SpeciesIds.Mudling, 13), (SpeciesIds.Bubbleling, 14) },
            goldReward: 350,
            introLine: "Naia: \"The tide takes everything in time. Try to outlast it.\"",
            victoryLine: "Naia: \"You weather the surf. Take the Tide Badge and go.\"",
            defeatLine: "Naia: \"Pulled under. Train, then return.\""),

        // Gym 3 — Ghost. Screen 14, wild ~10; leader at +5 (ghost types are harder).
        new GymLeader(
            gymNumber: 3,
            displayName: "Mara",
            title: "Whisper-Speaker",
            badgeId: BadgeIds.Whisper,
            badgeName: "Whisper Badge",
            roster: new[] { (SpeciesIds.Wisplet, 15), (SpeciesIds.Wisplet, 16), (SpeciesIds.Featherling, 17) },
            goldReward: 500,
            introLine: "Mara: \"Listen. You can hear them already. Will you flinch?\"",
            victoryLine: "Mara: \"The whispers approve. Take the badge.\"",
            defeatLine: "Mara: \"They wait. So do I.\""),

        // Gym 4 — Electric. Screen 19.
        new GymLeader(
            gymNumber: 4,
            displayName: "Volt",
            title: "Stormbringer",
            badgeId: BadgeIds.Spark,
            badgeName: "Spark Badge",
            roster: new[] { (SpeciesIds.Sparkmite, 18), (SpeciesIds.Sparkmite, 19), (SpeciesIds.Featherling, 20) },
            goldReward: 750,
            introLine: "Volt: \"Lightning fast or lightning struck. Pick.\"",
            victoryLine: "Volt: \"You danced with the storm. The Spark Badge is yours.\"",
            defeatLine: "Volt: \"Grounded. Try again when you've got the charge.\""),

        // Gym 5 — Ground/earth. Screen 24.
        new GymLeader(
            gymNumber: 5,
            displayName: "Garth",
            title: "Quake Speaker",
            badgeId: BadgeIds.Quake,
            badgeName: "Quake Badge",
            roster: new[] { (SpeciesIds.Mudling, 21), (SpeciesIds.Pebblet, 22), (SpeciesIds.Mudling, 23) },
            goldReward: 1000,
            introLine: "Garth: \"The earth has memory. So do I. Show me what you've learned.\"",
            victoryLine: "Garth: \"The ground accepts you. Take the Quake Badge.\"",
            defeatLine: "Garth: \"The earth remembers. Return when you can stand on it.\""),

        // Gym 6 — Flying/sky. Screen 29.
        new GymLeader(
            gymNumber: 6,
            displayName: "Cael",
            title: "Sky-Rider",
            badgeId: BadgeIds.Sky,
            badgeName: "Sky Badge",
            roster: new[] { (SpeciesIds.Featherling, 24), (SpeciesIds.Featherling, 25), (SpeciesIds.Featherling, 26) },
            goldReward: 1350,
            introLine: "Cael: \"Look up. That's where this fight ends.\"",
            victoryLine: "Cael: \"You met me in the air. Take the Sky Badge.\"",
            defeatLine: "Cael: \"You stayed grounded. Come back with wings.\""),

        // Gym 7 — Fire. Screen 34.
        new GymLeader(
            gymNumber: 7,
            displayName: "Sera",
            title: "Ember Keeper",
            badgeId: BadgeIds.Ember,
            badgeName: "Ember Badge",
            roster: new[] { (SpeciesIds.Charpup, 27), (SpeciesIds.Charpup, 28), (SpeciesIds.Cinderfox, 29) },
            goldReward: 1750,
            introLine: "Sera: \"Most who reach me burn. Be the exception.\"",
            victoryLine: "Sera: \"You held against the heat. The Ember Badge is yours.\"",
            defeatLine: "Sera: \"Ash. Heal. Return when you stop melting.\""),

        // Gym 8 — Mixed/dark. Screen 39. Final test before the Champion.
        new GymLeader(
            gymNumber: 8,
            displayName: "Vexar",
            title: "Shadow Champion",
            badgeId: BadgeIds.Shadow,
            badgeName: "Shadow Badge",
            roster: new[] { (SpeciesIds.Cinderfox, 30), (SpeciesIds.Hydrofin, 31), (SpeciesIds.Leafwing, 32) },
            goldReward: 2500,
            introLine: "Vexar: \"You're at the gate. Past me is the Champion's Hall. Earn it.\"",
            victoryLine: "Vexar: \"The Shadow Badge — and the road to the Champion. Go finish this.\"",
            defeatLine: "Vexar: \"Not yet. The Champion would have unmade you.\"")
    };

    public static GymLeader ForGymNumber(int gymNumber) => All[gymNumber - 1];
}
