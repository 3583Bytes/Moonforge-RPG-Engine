namespace Moonforge.Sample.Roguelike.Input
{
    public enum PlayerAction
    {
        None,

        MoveNorth,
        MoveSouth,
        MoveEast,
        MoveWest,

        Interact,
        UseStairs,
        TownPortal,
        OpenJournal,
        OpenGearInventory,
        OpenMenu,
        BuyPotion,
        SellHerb,

        Attack,
        ClassSkill1,
        ClassSkill2,
        UsePotion,
        Retreat,

        Confirm,
        Cancel,

        Digit1,
        Digit2,
        Digit3,
        Digit4,
        Digit5,
        Digit6,

        NewRun,
        ContinueRun,
        DeleteSave,
        Quit,

        // Gear-inventory specific. UnequipAll replaces the previous overload of
        // PlayerAction.Attack as "unequip all from gear screen" — same key (U)
        // now maps to a dedicated action so the meaning is obvious at the call
        // site. FilterCycle rotates through All -> Weapon -> Armor -> Accessory.
        UnequipAll,
        FilterCycle
    }
}
