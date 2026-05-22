using System.Collections.Generic;
using Moonforge.Core.Runtime.Random;

namespace Moonforge.Sample.MonsterCatcher.WorldGen;

/// <summary>
/// Holds the screen cache and the biome schedule. Screens are generated lazily on first
/// access and stay cached for the lifetime of the world so backtracking returns the same
/// layout and remembers per-screen mutable state (rested heal pads, cleared gyms).
/// </summary>
internal sealed class World
{
    /// <summary>Total screens in a run: gyms at indices 4, 9, 14, …, 39 (every 5th) and the Champion's Hall at index 44.</summary>
    public const int GymCount = 8;

    /// <summary>Screens between gyms — index = (gymNumber * GymInterval) - 1.</summary>
    public const int GymInterval = 5;

    /// <summary>Champion's Hall sits 5 screens after the 8th gym.</summary>
    public const int ChampionScreenIndex = (GymCount * GymInterval) + 4;   // = 44

    private readonly ulong _seed;
    private readonly Dictionary<int, WorldScreen> _cache = new();

    public World(ulong seed)
    {
        _seed = seed;
    }

    /// <summary>The deepest screen index that is part of a normal run (0 .. <see cref="ChampionScreenIndex"/>).</summary>
    public int LastScreenIndex => ChampionScreenIndex;

    /// <summary>Returns the cached screen, generating it on first access.</summary>
    public WorldScreen GetOrGenerate(int screenIndex)
    {
        if (_cache.TryGetValue(screenIndex, out WorldScreen? cached)) return cached;

        BiomeKind biome = BiomeFor(screenIndex);
        WorldScreen screen = WorldScreenGenerator.Generate(_seed, screenIndex, biome);
        _cache[screenIndex] = screen;
        return screen;
    }

    /// <summary>
    /// Deterministically picks the biome for a given screen index. Gym screens (every
    /// <see cref="GymInterval"/> screens) are always <see cref="BiomeKind.Town"/>; the final
    /// screen is <see cref="BiomeKind.Champion"/>; everything else is a natural biome rolled
    /// from a per-band weighted table.
    /// </summary>
    public BiomeKind BiomeFor(int screenIndex)
    {
        if (screenIndex >= ChampionScreenIndex) return BiomeKind.Champion;
        // Gyms at indices 4, 9, 14, 19, 24, 29, 34, 39 (every (GymInterval)th, zero-indexed
        // so the player walks 4 wild screens then hits a town on the 5th).
        if (screenIndex > 0 && ((screenIndex + 1) % GymInterval) == 0) return BiomeKind.Town;

        // Natural biome roll: deterministic per-screen via a separate stream so changing the
        // wild-screen layout never shifts which screens are gyms.
        IRandomSource biomeRng = new Pcg32RandomSource(_seed, (ulong)(0xB10E + screenIndex));
        int band = screenIndex / GymInterval;   // 0..7 — which gym band we're in

        // Bands progress through tougher biome mixes as the player walks east. Each band's
        // weight table favors the band's "feature" biome but keeps Plains common throughout
        // so the world feels mixed rather than a strict biome-per-band wall.
        (BiomeKind kind, int weight)[] table = band switch
        {
            0 => new[] { (BiomeKind.Plains, 70), (BiomeKind.Forest, 30) },
            1 => new[] { (BiomeKind.Plains, 40), (BiomeKind.Forest, 50), (BiomeKind.Beach, 10) },
            2 => new[] { (BiomeKind.Forest, 45), (BiomeKind.Plains, 25), (BiomeKind.Cave, 30) },
            3 => new[] { (BiomeKind.Cave, 40), (BiomeKind.Marsh, 30), (BiomeKind.Forest, 30) },
            4 => new[] { (BiomeKind.Highlands, 45), (BiomeKind.Plains, 25), (BiomeKind.Marsh, 30) },
            5 => new[] { (BiomeKind.Highlands, 35), (BiomeKind.Cave, 35), (BiomeKind.Forest, 30) },
            6 => new[] { (BiomeKind.Beach, 35), (BiomeKind.Marsh, 30), (BiomeKind.Highlands, 35) },
            _ => new[] { (BiomeKind.Cave, 30), (BiomeKind.Highlands, 35), (BiomeKind.Marsh, 35) }
        };

        int total = 0;
        foreach (var (_, w) in table) total += w;
        int roll = biomeRng.NextInt(total);
        int acc = 0;
        foreach (var (kind, w) in table)
        {
            acc += w;
            if (roll < acc) return kind;
        }

        return BiomeKind.Plains;
    }

    /// <summary>
    /// Returns the gym number for the given screen index (1..<see cref="GymCount"/>), or 0
    /// if the screen is not a gym screen. Useful for picking the right gym leader content.
    /// </summary>
    public int GymNumberFor(int screenIndex)
    {
        if (screenIndex <= 0 || screenIndex >= ChampionScreenIndex) return 0;
        if (((screenIndex + 1) % GymInterval) != 0) return 0;
        return (screenIndex + 1) / GymInterval;
    }
}
