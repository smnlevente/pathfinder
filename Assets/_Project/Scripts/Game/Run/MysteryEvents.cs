using System.Collections.Generic;
using System.Linq;
using Pathfinder.Core.Equipment;
using Pathfinder.Core.Random;
using Pathfinder.Core.Run;
using Pathfinder.Game.Content;
using Pathfinder.Game.Equipment;
using Pathfinder.Game.Localization;
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
            EventRequirement.AnyItem => Loc.T("need.item"),
            EventRequirement.UpgradeableItem when choice.RequiredScraps > 0 => Loc.T("need.scraps_upgradable", ("amount", choice.RequiredScraps)),
            EventRequirement.UpgradeableItem => Loc.T("need.upgradable"),
            _ => choice.RequiredScraps > 0 ? Loc.T("need.scraps", ("amount", choice.RequiredScraps)) : string.Empty,
        };

        /// <summary>The choice's effects, gains in green and costs in red, e.g. "+40 Scraps · −15 hull".</summary>
        public static string Describe(EventChoice choice)
        {
            string success = Describe(choice.Outcomes);
            if (choice.Chance >= 1f)
                return success.Length > 0 ? success : UiKit.Colored(Loc.T("effect.nothing"), UiKit.TextDim);
            string fail = Describe(choice.FailOutcomes);
            return Loc.T("effect.gamble", ("chance", choice.Chance * 100f), ("success", success), ("fail", fail.Length > 0 ? fail : Loc.T("effect.gamble.nothing")));
        }

        static string Describe(IEnumerable<EventOutcome> outcomes) =>
            string.Join(Loc.T("effect.separator"), outcomes.Select(Describe).Where(text => text.Length > 0));

        public static string Describe(EventOutcome outcome)
        {
            int amount = UnityEngine.Mathf.RoundToInt(outcome.Amount);
            string signed = StatEquipmentDefinition.Signed(amount, "0");
            string Colored(string key, bool good, params (string, object)[] args) => UiKit.Colored(Loc.T(key, args), good ? UiKit.Good : UiKit.Bad);

            return outcome.Kind switch
            {
                EventOutcomeKind.Scraps => Colored("effect.scraps", amount >= 0, ("signed", signed)),
                EventOutcomeKind.Hull => Colored("effect.hull", amount >= 0, ("signed", signed)),
                EventOutcomeKind.HullPercent => Colored("effect.hull_percent", true, ("amount", amount)),
                EventOutcomeKind.MaxHull => Colored("effect.max_hull", amount >= 0, ("signed", signed)),
                EventOutcomeKind.DarkMatter => Colored("effect.darkmatter", true, ("amount", amount)),
                EventOutcomeKind.Equipment => outcome.Rarity == Rarity.Common
                    ? Colored("effect.equipment", true)
                    : Colored("effect.equipment_rarity", true, ("rarity", Presentation.EquipmentChoiceScreen.RarityName(outcome.Rarity))),
                EventOutcomeKind.UpgradeRandomItem => Colored("effect.upgrade", true),
                EventOutcomeKind.LoseRandomItem => Colored("effect.lose_item", false),
                EventOutcomeKind.Ambush => UiKit.Colored(Loc.T("effect.ambush"), UiKit.Gold),
                EventOutcomeKind.StatPercent => Colored("effect.stat", amount >= 0 == (outcome.Stat != StatId.DamageTaken), ("signed", signed), ("stat", StatNames.Name(outcome.Stat))),
                _ => string.Empty,
            };
        }

        /// <summary>Applies choice <paramref name="index"/> of the event. A gamble rolls with <paramref name="random"/>. Authority only.</summary>
        public static EventResult Apply(MysteryEventDefinition definition, int index, PlayerShip pilot, RunState run, IRandom random)
        {
            var choice = definition.Choices[index];
            var result = new EventResult();
            if (choice.Chance < 1f)
                result.Succeeded = random.Chance(choice.Chance);

            var outcomes = result.Succeeded ? choice.Outcomes : choice.FailOutcomes;
            result.Text = definition.ResultText(index, result.Succeeded);
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
                        result.Lines.Add(Line("effect.scraps", amount));
                    }
                    else
                    {
                        result.Lines.Add(Line("effect.scraps", -pilot.LoseScraps(-amount)));
                    }
                    break;

                case EventOutcomeKind.Hull:
                    if (amount >= 0)
                        result.Lines.Add(Line("effect.hull", UnityEngine.Mathf.RoundToInt(pilot.Heal(amount))));
                    else
                        result.Lines.Add(Line("effect.hull", -UnityEngine.Mathf.RoundToInt(pilot.DrainHull(-amount))));
                    break;

                case EventOutcomeKind.HullPercent:
                    result.Lines.Add(Line("effect.hull", UnityEngine.Mathf.RoundToInt(pilot.Heal(pilot.Vitals.MaxHull * outcome.Amount / 100f))));
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
                    result.Lines.Add(UiKit.Colored(Loc.T("effect.upgraded", ("item", item.DisplayName),
                        ("level", Presentation.EquipmentChoiceScreen.Roman(pilot.Equipment.Find(item).Level))), UiKit.Good));
                    break;
                }

                case EventOutcomeKind.LoseRandomItem:
                {
                    var candidates = pilot.Equipment.All.ToList();
                    if (candidates.Count == 0)
                        break;
                    var item = candidates[random.Range(0, candidates.Count)].Item;
                    pilot.RemoveEquipment(item);
                    result.Lines.Add(UiKit.Colored(Loc.T("effect.lost", ("item", item.DisplayName)), UiKit.Bad));
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

        /// <summary>A result line such as "+40 Scraps" or "−15 hull", green for gains and red for losses.</summary>
        static string Line(string key, int amount) =>
            UiKit.Colored(Loc.T(key, ("signed", StatEquipmentDefinition.Signed(amount, "0"))), amount >= 0 ? UiKit.Good : UiKit.Bad);
    }
}
