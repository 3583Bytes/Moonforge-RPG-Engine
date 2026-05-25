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
        Npc
    }
}
