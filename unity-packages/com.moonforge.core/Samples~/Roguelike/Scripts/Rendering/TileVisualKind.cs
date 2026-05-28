namespace Moonforge.Sample.Roguelike.Rendering
{
    public enum TileVisualKind
    {
        Empty,

        DungeonFloor,
        DungeonWall,
        DungeonPillar,
        DungeonStairsDown,
        DungeonStairsUp,

        TownFloor,
        TownWall,
        TownDoor,
        TownShopMarker,
        TownHealerMarker,
        TownAlchemistMarker,
        TownGuardMarker,
        TownCacheMarker,
        TownFountainMarker,
        TownQuestBoardMarker,
        TownShrineMarker,

        Hero,
        Enemy,
        EnemyElite,
        EnemyBoss,
        Npc,

        // Inventory-screen item-slot icons. Procedural-only — there are no
        // PNGs bundled for these; the catalog draws a sword/shield/ring
        // silhouette in the equipment's tier colour.
        WeaponIcon,
        ArmorIcon,
        AccessoryIcon
    }
}
