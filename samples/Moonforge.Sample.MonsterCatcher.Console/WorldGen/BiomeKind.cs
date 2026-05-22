namespace Moonforge.Sample.MonsterCatcher.WorldGen;

/// <summary>
/// One screen's biome. Drives tile palette, density, encounter chance, and the wild
/// monster pool. <see cref="Town"/> and <see cref="Champion"/> are special-cased
/// generators rather than rolls from the natural-biome pool.
/// </summary>
internal enum BiomeKind
{
    Plains,
    Forest,
    Cave,
    Highlands,
    Beach,
    Marsh,
    Town,
    Champion
}
