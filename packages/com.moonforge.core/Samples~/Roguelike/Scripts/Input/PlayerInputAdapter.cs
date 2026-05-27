using UnityEngine;

namespace Moonforge.Sample.Roguelike.Input
{
    public sealed class PlayerInputAdapter
    {
        public PlayerAction PollScene(SceneId scene)
        {
            PlayerAction action = PollGlobal();
            if (action != PlayerAction.None)
            {
                return action;
            }

            switch (scene)
            {
                case SceneId.MainMenu:
                    return PollMainMenu();
                case SceneId.ClassSelect:
                    return PollDigits();
                case SceneId.Town:
                    return PollTown();
                case SceneId.Dungeon:
                    return PollDungeon();
                case SceneId.Battle:
                    return PollBattle();
                case SceneId.BattleSummary:
                case SceneId.ContractNotice:
                    return PollConfirm();
                case SceneId.ContractJournal:
                    return PollJournal();
                case SceneId.GearInventory:
                    return PollGearInventory();
                case SceneId.MetaShrine:
                    return PollMetaShrine();
                case SceneId.BossReward:
                    return PollDigits();
                case SceneId.Dialogue:
                    return PollDialogue();
                default:
                    return PlayerAction.None;
            }
        }

        private static PlayerAction PollGlobal()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                return PlayerAction.Cancel;
            }
            return PlayerAction.None;
        }

        private static PlayerAction PollMainMenu()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.N))
            {
                return PlayerAction.NewRun;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.C))
            {
                return PlayerAction.ContinueRun;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.D))
            {
                return PlayerAction.DeleteSave;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.Q))
            {
                return PlayerAction.Quit;
            }
            return PlayerAction.None;
        }

        private static PlayerAction PollTown()
        {
            // Digit keys take precedence so a pending landmark-interaction menu
            // (rendered by the session) can be resolved by 1/2/3/... key presses.
            // Otherwise pressing 'S' for "Sell herb" while the menu is open would
            // override the menu choice.
            PlayerAction digit = PollDigits();
            if (digit != PlayerAction.None)
            {
                return digit;
            }

            PlayerAction move = PollMovement();
            if (move != PlayerAction.None)
            {
                return move;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.E))
            {
                return PlayerAction.Interact;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.J))
            {
                return PlayerAction.OpenJournal;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.I))
            {
                return PlayerAction.OpenGearInventory;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.B))
            {
                return PlayerAction.BuyPotion;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.S))
            {
                return PlayerAction.SellHerb;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.M))
            {
                return PlayerAction.OpenMenu;
            }
            return PlayerAction.None;
        }

        private static PlayerAction PollDungeon()
        {
            PlayerAction move = PollMovement();
            if (move != PlayerAction.None)
            {
                return move;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.E))
            {
                return PlayerAction.UseStairs;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.J))
            {
                return PlayerAction.OpenJournal;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.I))
            {
                return PlayerAction.OpenGearInventory;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.T))
            {
                return PlayerAction.TownPortal;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.M))
            {
                return PlayerAction.OpenMenu;
            }
            return PlayerAction.None;
        }

        private static PlayerAction PollBattle()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.A))
            {
                return PlayerAction.Attack;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad1))
            {
                return PlayerAction.ClassSkill1;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad2))
            {
                return PlayerAction.ClassSkill2;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.P))
            {
                return PlayerAction.UsePotion;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.Q))
            {
                return PlayerAction.Retreat;
            }
            return PlayerAction.None;
        }

        private static PlayerAction PollJournal()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.A))
            {
                return PlayerAction.Attack;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.J))
            {
                return PlayerAction.Confirm;
            }
            return PollConfirm();
        }

        private static PlayerAction PollGearInventory()
        {
            PlayerAction digit = PollDigits();
            if (digit != PlayerAction.None)
            {
                return digit;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.U))
            {
                return PlayerAction.Attack;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.I))
            {
                return PlayerAction.Confirm;
            }
            return PollConfirm();
        }

        private static PlayerAction PollMetaShrine()
        {
            PlayerAction digit = PollDigits();
            if (digit != PlayerAction.None)
            {
                return digit;
            }
            return PollConfirm();
        }

        private static PlayerAction PollDialogue()
        {
            return PollDigits();
        }

        private static PlayerAction PollMovement()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.W) || UnityEngine.Input.GetKeyDown(KeyCode.UpArrow))
            {
                return PlayerAction.MoveNorth;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.S) || UnityEngine.Input.GetKeyDown(KeyCode.DownArrow))
            {
                return PlayerAction.MoveSouth;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.A) || UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow))
            {
                return PlayerAction.MoveWest;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.D) || UnityEngine.Input.GetKeyDown(KeyCode.RightArrow))
            {
                return PlayerAction.MoveEast;
            }
            return PlayerAction.None;
        }

        private static PlayerAction PollConfirm()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.Space))
            {
                return PlayerAction.Confirm;
            }
            return PlayerAction.None;
        }

        private static PlayerAction PollDigits()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad1))
            {
                return PlayerAction.Digit1;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad2))
            {
                return PlayerAction.Digit2;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad3))
            {
                return PlayerAction.Digit3;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha4) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad4))
            {
                return PlayerAction.Digit4;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha5) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad5))
            {
                return PlayerAction.Digit5;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha6) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad6))
            {
                return PlayerAction.Digit6;
            }
            return PlayerAction.None;
        }
    }
}
