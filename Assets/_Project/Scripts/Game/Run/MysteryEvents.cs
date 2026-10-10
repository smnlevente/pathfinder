using System.Collections.Generic;
using System.Linq;
using Pathfinder.Core.Equipment;
using Pathfinder.Core.Random;
using Pathfinder.Core.Run;
using Pathfinder.Game.Content;
using Pathfinder.Game.Equipment;
using Pathfinder.Game.Player;
using Pathfinder.Game.Presentation.Ui;

namespace Pathfinder.Game.Run
{
    /// <summary>What a Mystery event choice did, and what it still needs the level to do (an equipment offer, a fight).</summary>
    public sealed class EventResult
    {
        public bool Succeeded = true;
        public string Text = string.Empty;
        public readonly List<string> Lines = new List<string>();
        public Rarity? EquipmentOffer;
        public bool Ambush;
    }

    /// <summary>Mystery event rules (GDD §10.5): requirements, outcome summaries and applying outcomes to a pilot.</summary>
    public static class MysteryEvents
    {
        /// <summary>Picks an event the run hasn't met yet. Once all have been met, any event can come back.</summary>
        public static MysteryEventDefinition Pick(IReadOnlyList<MysteryEventDefinition> events, RunState run, IRandom random)
        {
            if (events == null || events.Count == 0)
                return null;
            var fresh = events.Where(e => e != null && (run == null || !run.HasSeenEvent(e.Id))).ToList();
            var pool = fresh.Count > 0 ? fresh : events.Where(e => e != null).ToList();
            return pool[random.Range(0, pool.Count)];
        }

        /// <summary>Whether the pilot can take the choice. A Scraps cost is always checked, on top of the requirement.</summary>
        public static bool IsAvailable(EventChoice choice, PlayerShip pilot)
        {
            if (pilot.Scraps < choice.RequiredScraps)
                return false;
            return choice.Requirement switch
            {
                EventRequirement.AnyItem => pilot.Equipment.All.Any(),
                EventRequirement.UpgradeableItem => pilot.Equipment.All.Any(e => e.Level < EquipmentRules.MaxLevel),
                _ => true,
            };
        }

        /// <summary>What a choice needs, e.g. "NEEDS 40 SCRAPS". Shown when it's unavailable.</summary>
        public static string RequirementText(EventChoice choice) => choice.Requirement switch
        {
            EventRequirement.AnyItem => "NEEDS AN ITEM",
            EventRequirement.UpgradeableItem when choice.RequiredScraps > 0 => $"NEEDS {choice.RequiredScraps} SCRAPS + AN UPGRADABLE ITEM",
            EventRequirement.UpgradeableItem => "NEEDS AN UPGRADABLE ITEM",
            _ => choice.RequiredScraps > 0 ? $"NEEDS {choice.RequiredScraps} SCRAPS" : string.Empty,
        };

        /// <summary>The choice's effects, gains in green and costs in red, e.g. "+40 Scraps · −15 hull".</summary>
        public static string Describe(EventChoice choice)
        {
            string success = Describe(choice.Outcomes);
            if (choice.Chance >= 1f)
                return success.Length > 0 ? success : UiKit.Colored("Nothing happens.", UiKit.TextDim);
            string fail = Describe(choice.FailOutcomes);
            return $"{choice.Chance:P0}: {success}     Otherwise: {(fail.Length > 0 ? fail : "nothing")}";
        }

        static string Describe(IEnumerable<EventOutcome> outcomes) =>
            string.Join("  ·  ", outcomes.Select(Describe).Where(text => text.Length > 0));

        public static string Describe(EventOutcome outcome)
        {
            int amount = UnityEngine.Mathf.RoundToInt(outcome.Amount);
            string Signed(string what, bool goodWhenPositive = true)
            {
                bool good = amount >= 0 == goodWhenPositive;
                string text = amount >= 0 ? $"+{amount} {what}" : $"−{-amount} {what}";
                return UiKit.Colored(text, good ? UiKit.Good : UiKit.Bad);
            }

            return outcome.Kind switch
            {
                EventOutcomeKind.Scraps => Signed("Scraps"),
                EventOutcomeKind.Hull => Signed("hull"),
                EventOutcomeKind.HullPercent => UiKit.Colored($"Repair {amount}% hull", UiKit.Good),
                EventOutcomeKind.MaxHull => Signed("max hull"),
                EventOutcomeKind.DarkMatter => UiKit.Colored($"+{amount} Dark Matter", UiKit.Good),
                EventOutcomeKind.Equipment => UiKit.Colored(outcome.Rarity == Rarity.Common ? "Equipment choice" : $"{outcome.Rarity}+ equipment choice", UiKit.Good),
                EventOutcomeKind.UpgradeRandomItem => UiKit.Colored("Upgrade a random item", UiKit.Good),
                EventOutcomeKind.LoseRandomItem => UiKit.Colored("Lose a random item", UiKit.Bad),
                EventOutcomeKind.Ambush => UiKit.Colored("A fight (double rewards)", UiKit.Gold),
                EventOutcomeKind.StatPercent => Signed($"% {StatNames.Name(outcome.Stat)}", goodWhenPositive: outcome.Stat != StatId.DamageTaken),
                _ => string.Empty,
            };
        }

        /// <summary>Applies the choice. A gamble rolls with <paramref name="random"/>. Authority only.</summary>
        public static EventResult Apply(EventChoice choice, PlayerShip pilot, RunState run, IRandom random)
        {
            var result = new EventResult();
            if (choice.Chance < 1f)
                result.Succeeded = random.Chance(choice.Chance);

            var outcomes = result.Succeeded ? choice.Outcomes : choice.FailOutcomes;
            result.Text = result.Succeeded ? choice.ResultText : choice.FailText;
            foreach (var outcome in outcomes)
                Apply(outcome, pilot, run, random, result);
            return result;
        }

        static void Apply(EventOutcome outcome, PlayerShip pilot, RunState run, IRandom random, EventResult result)
        {
            int amount = UnityEngine.Mathf.RoundToInt(outcome.Amount);
            switch (outcome.Kind)
            {
                case EventOutcomeKind.Scraps:
                    if (amount >= 0)
                    {
                        pilot.AddScraps(amount);
                        result.Lines.Add(UiKit.Colored($"+{amount} Scraps", UiKit.Good));
                    }
                    else
                    {
                        result.Lines.Add(UiKit.Colored($"−{pilot.LoseScraps(-amount)} Scraps", UiKit.Bad));
                    }
                    break;

                case EventOutcomeKind.Hull:
                    if (amount >= 0)
                        result.Lines.Add(UiKit.Colored($"+{UnityEngine.Mathf.RoundToInt(pilot.Heal(amount))} hull", UiKit.Good));
                    else
                        result.Lines.Add(UiKit.Colored($"−{UnityEngine.Mathf.RoundToInt(pilot.DrainHull(-amount))} hull", UiKit.Bad));
                    break;

                case EventOutcomeKind.HullPercent:
                    result.Lines.Add(UiKit.Colored($"+{UnityEngine.Mathf.RoundToInt(pilot.Heal(pilot.Vitals.MaxHull * outcome.Amount / 100f))} hull", UiKit.Good));
                    break;

                case EventOutcomeKind.MaxHull:
                    pilot.AddRunModifier(StatId.MaxHull, amount, 0f);
                    result.Lines.Add(Describe(outcome));
                    break;

                case EventOutcomeKind.DarkMatter:
                    run?.AddDarkMatter(amount);
                    result.Lines.Add(Describe(outcome));
                    break;

                case EventOutcomeKind.Equipment:
                    result.EquipmentOffer = outcome.Rarity;
                    break;

                case EventOutcomeKind.UpgradeRandomItem:
                {
                    var candidates = pilot.Equipment.All.Where(e => e.Level < EquipmentRules.MaxLevel).ToList();
                    if (candidates.Count == 0)
                        break;
                    var entry = candidates[random.Range(0, candidates.Count)];
                    var item = entry.Item;
                    pilot.UpgradeEquipment(item);
                    result.Lines.Add(UiKit.Colored($"{item.DisplayName} upgraded to {Presentation.EquipmentChoiceScreen.Roman(pilot.Equipment.Find(item).Level)}", UiKit.Good));
                    break;
                }

                case EventOutcomeKind.LoseRandomItem:
                {
                    var candidates = pilot.Equipment.All.ToList();
                    if (candidates.Count == 0)
                        break;
                    var item = candidates[random.Range(0, candidates.Count)].Item;
                    pilot.RemoveEquipment(item);
                    result.Lines.Add(UiKit.Colored($"Lost {item.DisplayName}", UiKit.Bad));
                    break;
                }

                case EventOutcomeKind.Ambush:
                    result.Ambush = true;
                    break;

                case EventOutcomeKind.StatPercent:
                    pilot.AddRunModifier(outcome.Stat, 0f, outcome.Amount / 100f);
                    result.Lines.Add(Describe(outcome));
                    break;
            }
        }
    }
}
