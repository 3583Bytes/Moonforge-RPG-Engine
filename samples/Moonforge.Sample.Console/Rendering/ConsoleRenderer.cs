using System.Collections.Generic;
using System.Text;
using Moonforge.Core.Combat;
using Moonforge.Core.Exploration;
using Spectre.Console;
using Spectre.Console.Rendering;
using Moonforge.Sample.Roguelike.Rendering;

namespace Moonforge.Sample.ConsoleApp.Rendering;

internal static class ConsoleRenderer
{
    private const int HpBarCells = 12;

    public static void RenderMap(MapRenderModel model)
    {
        AnsiConsole.Clear();

        Panel mapPanel = new(new Markup(BuildMapBody(model)))
        {
            Header = new PanelHeader($"[bold]{Escape(model.Title)}[/]"),
            Border = BoxBorder.Rounded
        };

        Panel statusPanel = new(new Markup(BuildStatusBody(model)))
        {
            Header = new PanelHeader("[bold]Status[/]"),
            Border = BoxBorder.Rounded
        };

        Grid grid = new();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn(new GridColumn());
        grid.AddRow(mapPanel, statusPanel);
        AnsiConsole.Write(grid);

        AnsiConsole.Write(new Markup(BuildControlsLine(model.Controls)));
        AnsiConsole.WriteLine();

        if (!string.IsNullOrWhiteSpace(model.LastMessage))
        {
            AnsiConsole.Write(new Markup(BuildMessageLine(model.LastMessage, model.MessageTone)));
            AnsiConsole.WriteLine();
        }
    }

    public static void RenderBattle(BattleRenderModel model)
    {
        AnsiConsole.Clear();

        Panel partyPanel = new(new Markup(BuildPartyBody(model)))
        {
            Header = new PanelHeader($"[{Theme.Party}]Party[/]"),
            Border = BoxBorder.Rounded
        };

        Panel enemyPanel = new(new Markup(BuildEnemyBody(model)))
        {
            Header = new PanelHeader($"[{Theme.Enemy}]Enemies[/]"),
            Border = BoxBorder.Rounded
        };

        Grid top = new();
        top.AddColumn(new GridColumn());
        top.AddColumn(new GridColumn());
        top.AddRow(partyPanel, enemyPanel);

        AnsiConsole.Write(new Rule($"[bold yellow1]{Escape(model.Title)}[/] [grey58]Round {model.Battle.Round}[/]").LeftJustified());
        AnsiConsole.Write(top);

        Panel logPanel = new(new Markup(BuildBattleLogBody(model.RecentLog)))
        {
            Header = new PanelHeader("[bold]Log[/]"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(logPanel);

        AnsiConsole.Write(new Markup(BuildControlsLine(model.Controls)));
        AnsiConsole.WriteLine();

        if (!string.IsNullOrWhiteSpace(model.LastMessage))
        {
            AnsiConsole.Write(new Markup(BuildMessageLine(model.LastMessage, model.MessageTone)));
            AnsiConsole.WriteLine();
        }
    }

    public static void RenderMainMenu(string subtitle, bool canContinue)
    {
        AnsiConsole.Clear();
        StringBuilder sb = new();
        sb.AppendLine("[bold green1]Moonforge RPG Engine — Roguelike Sample[/]");
        sb.AppendLine();
        if (canContinue)
        {
            sb.AppendLine($"[{Theme.Hero}]C[/]  Continue");
            sb.AppendLine($"[{Theme.Warning}]D[/]  Delete save");
        }

        sb.AppendLine($"[{Theme.Hero}]N[/]  New run");
        sb.AppendLine($"[{Theme.Muted}]Q[/]  Exit");
        sb.AppendLine();
        sb.Append($"[{Theme.Muted}]{Escape(subtitle)}[/]");

        Panel panel = new(new Markup(sb.ToString()))
        {
            Header = new PanelHeader("[bold]Main Menu[/]"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(panel);
    }

    public static void RenderClassSelection(IReadOnlyList<ClassSelectionOption> options, string controls)
    {
        AnsiConsole.Clear();
        StringBuilder sb = new();
        sb.AppendLine("[bold]Choose your class:[/]");
        sb.AppendLine();
        for (int i = 0; i < options.Count; i++)
        {
            ClassSelectionOption option = options[i];
            sb.AppendLine($"[{Theme.Hero}]{Escape(option.Hotkey)}[/]  [bold]{Escape(option.Name)}[/] [grey58]— {Escape(option.Summary)}[/]");
        }

        Panel panel = new(new Markup(sb.ToString()))
        {
            Header = new PanelHeader("[bold]Class Selection[/]"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(panel);
        AnsiConsole.Write(new Markup(BuildControlsLine(controls)));
        AnsiConsole.WriteLine();
    }

    public static void RenderBattleSummary(BattleSummaryRenderModel model)
    {
        AnsiConsole.Clear();
        string outcomeColor = model.Outcome.StartsWith("Victory", StringComparison.OrdinalIgnoreCase)
            ? Theme.Success
            : Theme.Error;

        StringBuilder sb = new();
        sb.AppendLine($"[bold {outcomeColor}]{Escape(model.Outcome)}[/] [grey58]— {Escape(model.EncounterTitle)}[/]");
        sb.AppendLine();
        sb.AppendLine("[bold]Rewards[/]");
        sb.AppendLine(FormatChange("Gold   ", Theme.Gold, model.GoldBefore, model.GoldAfter, model.GoldDelta));
        sb.AppendLine(FormatChange("Tokens ", Theme.Tokens, model.TokensBefore, model.TokensAfter, model.TokensDelta));
        sb.AppendLine(FormatChange("Potions", Theme.Potions, model.PotionsBefore, model.PotionsAfter, model.PotionsDelta));
        sb.AppendLine(FormatChange("Herbs  ", Theme.Potions, model.HerbsBefore, model.HerbsAfter, model.HerbsDelta));

        if (model.BossRewardOptions.Count > 0 || !string.IsNullOrWhiteSpace(model.BossRewardChosen))
        {
            sb.AppendLine();
            sb.AppendLine($"[bold {Theme.ContractReady}]Boss Reward[/]");
            if (!string.IsNullOrWhiteSpace(model.BossRewardChosen))
            {
                sb.AppendLine($"  [{Theme.Success}]✓[/] {Escape(model.BossRewardChosen!)}");
            }
            else
            {
                for (int i = 0; i < model.BossRewardOptions.Count; i++)
                {
                    sb.AppendLine($"  [{Theme.Hero}]{i + 1}[/] {Escape(model.BossRewardOptions[i])}");
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("[bold]Battle Highlights[/]");
        if (model.RecentLog.Count == 0)
        {
            sb.AppendLine($"  [{Theme.Muted}]— none —[/]");
        }
        else
        {
            foreach (BattleLogEntry entry in model.RecentLog)
            {
                sb.AppendLine($"  {FormatLogEntry(entry)}");
            }
        }

        Panel panel = new(new Markup(sb.ToString()))
        {
            Header = new PanelHeader("[bold]Battle Summary[/]"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(panel);
        AnsiConsole.Write(new Markup(BuildControlsLine(model.Controls)));
        AnsiConsole.WriteLine();
    }

    public static void RenderDialogue(DialogueRenderModel model)
    {
        AnsiConsole.Clear();
        StringBuilder sb = new();
        sb.AppendLine($"[bold {Theme.Guard}]{Escape(model.NpcName)}[/]");
        sb.AppendLine();
        sb.AppendLine($"[{Theme.Info}]\"{Escape(model.BodyText)}\"[/]");

        if (model.Choices.Count > 0)
        {
            sb.AppendLine();
            for (int i = 0; i < model.Choices.Count; i++)
            {
                DialogueChoiceView choice = model.Choices[i];
                sb.AppendLine($"  [{Theme.Hero}]{Escape(choice.Hotkey)}[/] {Escape(choice.Text)}");
            }
        }

        Panel panel = new(new Markup(sb.ToString()))
        {
            Header = new PanelHeader($"[bold]{Escape(model.NpcName)}[/]"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(panel);
        AnsiConsole.Write(new Markup(BuildControlsLine(model.Controls)));
        AnsiConsole.WriteLine();
    }

    public static void RenderContractNotice(string title, string body, string controls)
    {
        AnsiConsole.Clear();
        StringBuilder sb = new();
        sb.Append($"[{Theme.Info}]{Escape(body)}[/]");

        Panel panel = new(new Markup(sb.ToString()))
        {
            Header = new PanelHeader($"[bold {Theme.ContractActive}]{Escape(title)}[/]"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(panel);
        AnsiConsole.Write(new Markup(BuildControlsLine(controls)));
        AnsiConsole.WriteLine();
    }

    public static void RenderContractJournal(string title, IReadOnlyList<string> lines, string controls)
    {
        AnsiConsole.Clear();
        StringBuilder sb = new();
        if (lines.Count == 0)
        {
            sb.Append($"[{Theme.Muted}]No data available.[/]");
        }
        else
        {
            for (int i = 0; i < lines.Count; i++)
            {
                sb.AppendLine(Escape(lines[i]));
            }
        }

        Panel panel = new(new Markup(sb.ToString()))
        {
            Header = new PanelHeader($"[bold]{Escape(title)}[/]"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(panel);
        AnsiConsole.Write(new Markup(BuildControlsLine(controls)));
        AnsiConsole.WriteLine();
    }

    public static void RenderGearInventory(GearInventoryRenderModel model)
    {
        AnsiConsole.Clear();

        // Loadout panel (top): three-slot summary grid showing the equipped item
        // in each slot with its stat block.
        Grid loadoutGrid = new();
        loadoutGrid.AddColumn(new GridColumn().NoWrap());
        loadoutGrid.AddColumn(new GridColumn().NoWrap());
        loadoutGrid.AddColumn(new GridColumn().NoWrap());
        loadoutGrid.AddRow(
            BuildSlotCardMarkup(model.WeaponSlot),
            BuildSlotCardMarkup(model.ArmorSlot),
            BuildSlotCardMarkup(model.AccessorySlot));

        Panel loadoutPanel = new(loadoutGrid)
        {
            Header = new PanelHeader($"[bold {Theme.Hero}]Equipped Loadout — {Escape(model.ClassName)}[/]"),
            Border = BoxBorder.Rounded
        };

        // Filter chip strip.
        string filterStrip = BuildFilterStrip(model.Filter);

        // Inventory table, grouped by slot section header.
        Table inv = new();
        inv.AddColumn(new TableColumn("[bold]#[/]").NoWrap());
        inv.AddColumn(new TableColumn("[bold]Item[/]"));
        inv.AddColumn(new TableColumn("[bold]Tier[/]").NoWrap());
        inv.AddColumn(new TableColumn("[bold]Stats[/]"));
        inv.AddColumn(new TableColumn("[bold]vs Equipped[/]"));
        inv.Border = TableBorder.Minimal;

        if (model.Entries.Count == 0)
        {
            inv.AddRow(
                new Markup(""),
                new Markup($"[{Theme.Muted}](nothing matches the current filter — press Tab to switch)[/]"),
                new Markup(""),
                new Markup(""),
                new Markup(""));
        }
        else
        {
            string? lastSlot = null;
            foreach (GearInventoryEntry entry in model.Entries)
            {
                if (lastSlot != entry.SlotId)
                {
                    inv.AddRow(
                        new Markup(""),
                        new Markup($"[bold {Theme.Info}]── {Escape(entry.SlotLabel)} ──[/]"),
                        new Markup(""), new Markup(""), new Markup(""));
                    lastSlot = entry.SlotId;
                }
                string hotkey = entry.HotkeyIndex > 0 ? $"[{Theme.Info}]{entry.HotkeyIndex}[/]" : "";
                string name = $"[{TierConsoleColor(entry.Tier)}]{Escape(entry.Name)}[/]";
                if (entry.IsEquipped) name += $"  [{Theme.Success}][[EQUIPPED]][/]";
                else if (entry.Quantity > 1) name += $"  [{Theme.Muted}]x{entry.Quantity}[/]";
                string tier = $"[{TierConsoleColor(entry.Tier)}]{entry.Tier}[/]";
                string stats = FormatStatsConsole(entry.Stats);
                string deltas = entry.Deltas.Count == 0 ? "" : FormatDeltasConsole(entry.Deltas);
                inv.AddRow(new Markup(hotkey), new Markup(name), new Markup(tier), new Markup(stats), new Markup(deltas));
            }
        }

        Panel invPanel = new(inv)
        {
            Header = new PanelHeader($"[bold]Inventory  [{Theme.Muted}]{filterStrip}[/][/]"),
            Border = BoxBorder.Rounded
        };

        AnsiConsole.Write(loadoutPanel);
        AnsiConsole.Write(invPanel);
        AnsiConsole.Write(new Markup(BuildControlsLine(model.Controls)));
        AnsiConsole.WriteLine();
        if (!string.IsNullOrWhiteSpace(model.LastMessage))
        {
            AnsiConsole.Write(new Markup(BuildMessageLine(model.LastMessage, model.MessageTone)));
            AnsiConsole.WriteLine();
        }
    }

    private static IRenderable BuildSlotCardMarkup(GearSlotView slot)
    {
        StringBuilder sb = new();
        sb.Append($"[bold]{Escape(slot.SlotLabel)}[/]\n");
        if (slot.Equipped is null)
        {
            sb.Append($"[{Theme.Muted}](empty)[/]");
        }
        else
        {
            GearInventoryEntry e = slot.Equipped;
            sb.Append($"[{TierConsoleColor(e.Tier)}]{Escape(e.Name)}[/]  [{Theme.Muted}]{e.Tier}[/]\n");
            sb.Append(FormatStatsConsole(e.Stats));
            if (e.GrantedSkillIds.Count > 0)
            {
                sb.Append($"\n[{Theme.Info}]grants {Escape(string.Join(", ", e.GrantedSkillIds))}[/]");
            }
        }
        return new Panel(new Markup(sb.ToString())) { Border = BoxBorder.Square, Padding = new Padding(1, 0, 1, 0) };
    }

    private static string BuildFilterStrip(GearFilter active)
    {
        string Chip(string label, GearFilter f) => f == active
            ? $"[bold {Theme.ContractActive}][[{label}]][/]"
            : $"[{Theme.Muted}]{label}[/]";

        return $"{Chip("All", GearFilter.All)} {Chip("Weapons", GearFilter.Weapon)} {Chip("Armor", GearFilter.Armor)} {Chip("Accessories", GearFilter.Accessory)}";
    }

    private static string FormatStatsConsole(IReadOnlyList<GearStatLine> stats)
    {
        if (stats.Count == 0) return $"[{Theme.Muted}](no stats)[/]";
        StringBuilder sb = new();
        for (int i = 0; i < stats.Count; i++)
        {
            if (i > 0) sb.Append("  ");
            int v = stats[i].Value;
            string sign = v > 0 ? "+" : "";
            sb.Append($"{Escape(stats[i].Label)} {sign}{v}");
        }
        return sb.ToString();
    }

    private static string FormatDeltasConsole(IReadOnlyList<GearStatDelta> deltas)
    {
        StringBuilder sb = new();
        for (int i = 0; i < deltas.Count; i++)
        {
            if (i > 0) sb.Append("  ");
            int v = deltas[i].Delta;
            string color = v > 0 ? Theme.Heal : Theme.Damage;
            string sign = v > 0 ? "+" : "";
            string arrow = v > 0 ? "^" : "v";
            sb.Append($"[{color}]{Escape(deltas[i].Label)} {sign}{v}{arrow}[/]");
        }
        return sb.ToString();
    }

    private static string TierConsoleColor(GearTier tier) => tier switch
    {
        GearTier.Common => "grey85",
        GearTier.Uncommon => "green1",
        GearTier.Rare => "deepskyblue1",
        GearTier.Epic => "mediumorchid1",
        _ => "white"
    };

    // ---------- map screen body builders ----------

    private static string BuildMapBody(MapRenderModel model)
    {
        StringBuilder sb = new();
        for (int y = 0; y < model.Map.Height; y++)
        {
            for (int x = 0; x < model.Map.Width; x++)
            {
                GridPosition position = new(x, y);
                sb.Append(RenderCell(position, model));
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string RenderCell(GridPosition position, MapRenderModel model)
    {
        if (model.HeroPosition.HasValue && IsAt(model.HeroPosition.Value, position))
        {
            return $"[bold {Theme.Hero}]@[/]";
        }

        if (model.GuardPosition.HasValue && IsAt(model.GuardPosition.Value, position))
        {
            return $"[{Theme.Guard}]G[/]";
        }

        for (int i = 0; i < model.Markers.Count; i++)
        {
            MapMarker marker = model.Markers[i];
            if (IsAt(marker.Position, position))
            {
                string color = Theme.MarkerColors.TryGetValue(marker.Symbol, out string? markerColor) ? markerColor : "white";
                return $"[bold {color}]{marker.Symbol}[/]";
            }
        }

        if (!model.Map.TryGetTileFlags(position, out ExplorationTileFlags flags))
        {
            return " ";
        }

        if ((flags & ExplorationTileFlags.Walkable) == ExplorationTileFlags.Walkable)
        {
            if (model.FloorDecorations is not null
                && model.FloorDecorations.TryGetValue(position, out char floorDeco))
            {
                return $"[{Theme.Floor}]{floorDeco}[/]";
            }

            return $"[{Theme.Floor}].[/]";
        }

        if (model.WallDecorations is not null
            && model.WallDecorations.TryGetValue(position, out char wallDeco))
        {
            return $"[{Theme.Wall}]{wallDeco}[/]";
        }

        return $"[{Theme.Wall}]#[/]";
    }

    private static string BuildStatusBody(MapRenderModel model)
    {
        StringBuilder sb = new();
        sb.AppendLine($"[{Theme.Gold}]◆ Gold    [/] [bold]{model.Gold}[/]");
        sb.AppendLine($"[{Theme.Tokens}]◆ Tokens  [/] [bold]{model.Tokens}[/]");
        sb.AppendLine($"[{Theme.Potions}]◆ Potions [/] [bold]{model.Potions}[/]");
        sb.AppendLine($"[{Theme.Depth}]◆ Floor   [/] [bold]{model.Depth}[/]");

        sb.AppendLine();
        sb.AppendLine("[bold]Contract[/]");
        if (string.IsNullOrWhiteSpace(model.ContractInfo) || model.ContractInfo == "None")
        {
            sb.AppendLine($"[{Theme.Muted}]— none —[/]");
        }
        else
        {
            sb.AppendLine($"[{Theme.ContractActive}]{Escape(model.ContractInfo)}[/]");
        }

        string? currentLocation = ResolveCurrentLocation(model);
        if (!string.IsNullOrWhiteSpace(currentLocation))
        {
            sb.AppendLine();
            sb.AppendLine($"[bold]Here[/]");
            sb.AppendLine($"[{Theme.Hero}]{Escape(currentLocation!)}[/]");
        }

        string legend = BuildLandmarkLegend(model);
        if (!string.IsNullOrWhiteSpace(legend))
        {
            sb.AppendLine();
            sb.AppendLine("[bold]Landmarks[/]");
            sb.Append(legend);
        }

        return sb.ToString();
    }

    private static string BuildLandmarkLegend(MapRenderModel model)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        StringBuilder sb = new();
        if (model.GuardPosition.HasValue)
        {
            sb.AppendLine($"  [{Theme.Guard}]G[/] Guard");
            seen.Add("G:Guard");
        }

        for (int i = 0; i < model.Markers.Count; i++)
        {
            MapMarker marker = model.Markers[i];
            string key = $"{marker.Symbol}:{marker.Label}";
            if (!seen.Add(key))
            {
                continue;
            }

            string color = Theme.MarkerColors.TryGetValue(marker.Symbol, out string? markerColor) ? markerColor : "white";
            sb.AppendLine($"  [bold {color}]{marker.Symbol}[/] {Escape(marker.Label)}");
        }

        return sb.ToString().TrimEnd();
    }

    // ---------- battle screen body builders ----------

    private static string BuildPartyBody(BattleRenderModel model)
    {
        StringBuilder sb = new();
        IEnumerable<BattleActorState> party = model.Battle.Actors.Values
            .Where(a => a.Faction == CombatFaction.Party)
            .OrderBy(a => a.ActorId, StringComparer.Ordinal);

        foreach (BattleActorState actor in party)
        {
            string turnMarker = actor.ActorId == model.CurrentTurnActorId ? $"[{Theme.Turn}]▶[/] " : "  ";
            string nameColor = actor.IsDowned ? Theme.Muted : Theme.Party;
            sb.AppendLine($"{turnMarker}[bold {nameColor}]{Escape(actor.DisplayName)}[/]");
            sb.AppendLine($"  HP {BuildHpBar(actor.Hp, actor.MaxHp)} [{HpColor(actor.Hp, actor.MaxHp)}]{actor.Hp}/{actor.MaxHp}[/]");
            string statusLine = BuildStatusBadges(actor);
            if (!string.IsNullOrEmpty(statusLine))
            {
                sb.AppendLine($"  {statusLine}");
            }

            if (actor.Resources.Count > 0)
            {
                foreach (KeyValuePair<string, int> resource in actor.Resources)
                {
                    int max = actor.ResourceMaxes.TryGetValue(resource.Key, out int maxValue) ? maxValue : resource.Value;
                    sb.AppendLine($"  {Capitalize(resource.Key)} {BuildPips(resource.Value, max)} [{Theme.FocusFilled}]{resource.Value}/{max}[/]");
                }
            }

            if (actor.Cooldowns.Count > 0)
            {
                List<string> cdParts = new();
                foreach (KeyValuePair<string, int> cd in actor.Cooldowns)
                {
                    cdParts.Add($"[{Theme.Muted}]{ShortenSkillId(cd.Key)} CD {cd.Value}[/]");
                }

                sb.AppendLine($"  {string.Join(" ", cdParts)}");
            }
        }

        if (!string.IsNullOrWhiteSpace(model.ClassActionInfo))
        {
            sb.AppendLine();
            sb.AppendLine($"[{Theme.Muted}]{Escape(model.ClassActionInfo)}[/]");
        }

        return sb.ToString().TrimEnd();
    }

    private static string BuildEnemyBody(BattleRenderModel model)
    {
        StringBuilder sb = new();
        IEnumerable<BattleActorState> enemies = model.Battle.Actors.Values
            .Where(a => a.Faction == CombatFaction.Enemy)
            .OrderBy(a => a.ActorId, StringComparer.Ordinal);

        foreach (BattleActorState actor in enemies)
        {
            string turnMarker = actor.ActorId == model.CurrentTurnActorId ? $"[{Theme.Turn}]▶[/] " : "  ";
            string nameColor = actor.IsDowned ? Theme.Muted : Theme.Enemy;
            string slainTag = actor.IsDowned ? $" [{Theme.Muted}](down)[/]" : string.Empty;
            sb.AppendLine($"{turnMarker}[bold {nameColor}]{Escape(actor.DisplayName)}[/]{slainTag}");
            sb.AppendLine($"  HP {BuildHpBar(actor.Hp, actor.MaxHp)} [{HpColor(actor.Hp, actor.MaxHp)}]{actor.Hp}/{actor.MaxHp}[/]");
            string statusLine = BuildStatusBadges(actor);
            if (!string.IsNullOrEmpty(statusLine))
            {
                sb.AppendLine($"  {statusLine}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string BuildBattleLogBody(IReadOnlyList<BattleLogEntry> log)
    {
        if (log.Count == 0)
        {
            return $"[{Theme.Muted}]— nothing yet —[/]";
        }

        StringBuilder sb = new();
        foreach (BattleLogEntry entry in log)
        {
            sb.AppendLine($"  {FormatLogEntry(entry)}");
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatLogEntry(BattleLogEntry entry)
    {
        string color = entry.Kind switch
        {
            BattleLogKind.Damage => Theme.Damage,
            BattleLogKind.Heal => Theme.Heal,
            BattleLogKind.Victory => Theme.Success,
            BattleLogKind.Defeat => Theme.Error,
            BattleLogKind.Intro => Theme.ContractActive,
            _ => Theme.Info
        };

        return $"[{color}]{Escape(entry.Text)}[/]";
    }

    // ---------- HP bar / pip helpers ----------

    private static string BuildHpBar(int current, int max)
    {
        if (max <= 0)
        {
            return $"[{Theme.HpEmpty}]{new string('░', HpBarCells)}[/]";
        }

        int filled = (int)System.Math.Round((double)current / max * HpBarCells);
        if (filled < 0) filled = 0;
        if (filled > HpBarCells) filled = HpBarCells;
        string fillColor = HpColor(current, max);
        string filledPart = new string('█', filled);
        string emptyPart = new string('░', HpBarCells - filled);
        return $"[{fillColor}]{filledPart}[/][{Theme.HpEmpty}]{emptyPart}[/]";
    }

    private static string HpColor(int current, int max)
    {
        if (max <= 0) return Theme.HpLow;
        double ratio = (double)current / max;
        if (ratio <= 0) return Theme.HpEmpty;
        if (ratio < 0.34) return Theme.HpLow;
        if (ratio < 0.67) return Theme.HpMid;
        return Theme.HpHigh;
    }

    private static string BuildPips(int filledCount, int maxCount)
    {
        if (maxCount <= 0)
        {
            return string.Empty;
        }

        if (filledCount < 0) filledCount = 0;
        if (filledCount > maxCount) filledCount = maxCount;
        StringBuilder sb = new();
        for (int i = 0; i < maxCount; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(i < filledCount
                ? $"[{Theme.FocusFilled}]●[/]"
                : $"[{Theme.FocusEmpty}]○[/]");
        }

        return sb.ToString();
    }

    // ---------- shared helpers ----------

    private static string BuildControlsLine(string controls)
    {
        if (string.IsNullOrWhiteSpace(controls))
        {
            return string.Empty;
        }

        string[] parts = controls.Split('|');
        StringBuilder sb = new();
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i].Trim();
            if (string.IsNullOrWhiteSpace(part))
            {
                continue;
            }

            if (i > 0)
            {
                sb.Append($" [{Theme.Muted}]│[/] ");
            }

            sb.Append(HighlightHotkeys(part));
        }

        return sb.ToString();
    }

    private static string HighlightHotkeys(string segment)
    {
        // Crude: highlight the first colon-delimited key, or the first run of capital letters.
        int colonIdx = segment.IndexOf(':');
        if (colonIdx > 0)
        {
            string key = segment.Substring(0, colonIdx);
            string rest = segment.Substring(colonIdx + 1);
            return $"[{Theme.Hero}]{Escape(key.Trim())}[/] [{Theme.Muted}]{Escape(rest.Trim())}[/]";
        }

        return $"[{Theme.Muted}]{Escape(segment)}[/]";
    }

    private static string BuildMessageLine(string message, MessageTone tone)
    {
        string color = tone switch
        {
            MessageTone.Success => Theme.Success,
            MessageTone.Warning => Theme.Warning,
            MessageTone.Error => Theme.Error,
            MessageTone.Muted => Theme.Muted,
            _ => Theme.Info
        };

        return $"  [{color}]→ {Escape(message)}[/]";
    }

    private static string FormatChange(string label, string valueColor, long before, long after, long delta)
    {
        string deltaPart;
        if (delta > 0)
        {
            deltaPart = $"[{Theme.Success}]+{delta}[/]";
        }
        else if (delta < 0)
        {
            deltaPart = $"[{Theme.Error}]{delta}[/]";
        }
        else
        {
            deltaPart = $"[{Theme.Muted}]±0[/]";
        }

        return $"  [{Theme.Muted}]{label}[/]  [{valueColor}]{before}[/] [{Theme.Muted}]→[/] [bold {valueColor}]{after}[/]  ({deltaPart})";
    }

    private static bool IsAt(GridPosition left, GridPosition right)
    {
        return left.X == right.X && left.Y == right.Y;
    }

    private static string? ResolveCurrentLocation(MapRenderModel model)
    {
        if (!model.HeroPosition.HasValue)
        {
            return null;
        }

        GridPosition hero = model.HeroPosition.Value;
        if (model.GuardPosition.HasValue && IsAt(model.GuardPosition.Value, hero))
        {
            return "Guard (G)";
        }

        for (int i = 0; i < model.Markers.Count; i++)
        {
            MapMarker marker = model.Markers[i];
            if (IsAt(marker.Position, hero))
            {
                return $"{marker.Label} ({marker.Symbol})";
            }
        }

        return null;
    }

    private static string Capitalize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return char.ToUpperInvariant(value[0]) + value.Substring(1);
    }

    private static string BuildStatusBadges(BattleActorState actor)
    {
        if (actor.ActiveStatusEffects.Count == 0)
        {
            return string.Empty;
        }

        List<string> parts = new();
        foreach (KeyValuePair<string, ActiveStatusEffect> entry in actor.ActiveStatusEffects)
        {
            string shortName = ShortenSkillId(entry.Key);
            parts.Add($"[{Theme.Warning}]✦ {Escape(shortName)}({entry.Value.RemainingTurns})[/]");
        }

        return string.Join(" ", parts);
    }

    private static string ShortenSkillId(string skillId)
    {
        int lastDot = skillId.LastIndexOf('.');
        return lastDot >= 0 && lastDot < skillId.Length - 1 ? skillId.Substring(lastDot + 1) : skillId;
    }

    private static string Escape(string value)
    {
        return Markup.Escape(value ?? string.Empty);
    }
}

