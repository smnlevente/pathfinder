using Pathfinder.Core.Equipment;
using Pathfinder.Game.Content;
using UnityEditor;

namespace Pathfinder.Editor.Setup
{
    public static partial class ContentBuilder
    {
        /// <summary>
        /// Mystery events (GDD §10.5): short scenes with two or three choices, each trading one thing for another. Like
        /// every definition, an event is created only when missing; edits to existing assets are kept.
        /// </summary>
        static MysteryEventDefinition[] BuildMysteryEvents() => new[]
        {
            Event("Event_DerelictFreighter", "event.derelict-freighter", "Derelict Freighter",
                "A gutted freighter drifts in silence, its cargo bay half open. Your scanners pick up sealed crates inside, and a faint hum from its security grid.",
                Choice("Strip the cargo bay", "The turrets wake up as you cut the last crate loose. You get out with the salvage, and a few new scorch marks.",
                    Scraps(45), Hull(-15)),
                Choice("Scan it from a safe distance", "You tag a few loose containers and tow them clear.", Scraps(15)),
                Choice("Leave it", "Whatever it was guarding, it can keep.")),

            Event("Event_DistressBeacon", "event.distress-beacon", "Distress Beacon",
                "An old Pathfinder beacon is looping a distress call. The signal looks genuine, but the Remnant have used lures like this before.",
                Gamble("Answer the call", 0.5f,
                    new[] { Item(Rarity.Rare) }, "You find a scout's escape pod, empty, its cargo still sealed. The crew left you something good.",
                    new[] { Ambush() }, "The beacon goes dark. Remnant fighters drop out of the static around you."),
                Choice("Log it and move on", "You mark the coordinates for whoever comes next.")),

            Event("Event_Singularity", "event.singularity", "Unstable Singularity",
                "A pocket of collapsed space hums ahead. Its pull could tear the hull apart, or fold Dark Matter straight out of the void.",
                Choice("Skim the event horizon", "The hull groans and buckles, but the collectors come back full.", DarkMatter(30), MaxHull(-10)),
                Choice("Steer clear", "Some risks aren't worth taking.")),

            Event("Event_Engineer", "event.engineer", "Wandering Engineer",
                "A lone engineer in a patched-up shuttle hails you. \"Nice ship. I could make her sing, for the right price.\"",
                Require(Choice("Tune up an item", "She works fast and talks faster. Your ship has never felt this sharp.", Scraps(-40), Upgrade()),
                    EventRequirement.UpgradeableItem, 40),
                Require(Choice("Patch the hull", "A few welds and a lot of sealant later, the hull is whole again.", Scraps(-25), Hull(40)),
                    EventRequirement.None, 25),
                Choice("Decline", "\"Your loss,\" she says, and drifts off.")),

            Event("Event_SporeBloom", "event.spore-bloom", "Spore Bloom",
                "Glowing spores drift through the nebula and settle on the hull. Wherever they land, damaged plating knits itself back together.",
                Choice("Let them grow", "The hull is whole again, if a little heavier. The growth isn't coming off.",
                    HullPercent(35), Stat(StatId.MoveSpeed, -8)),
                Choice("Burn them off", "The spores crisp into a fine, glittering ash. Traders pay well for it.", Scraps(20))),

            Event("Event_RemnantToll", "event.remnant-toll", "Remnant Toll Gate",
                "A Remnant checkpoint blocks the jump lane. \"This sector is ours, Pathfinder. Pay the toll, or turn around.\"",
                Require(Choice("Pay the toll", "They take the Scraps and wave you through. For now.", Scraps(-35)), EventRequirement.None, 35),
                Choice("Run the blockade", "You punch the engines. The gate's guns light up behind you.", Ambush())),

            Event("Event_WeaponsCache", "event.weapons-cache", "Sealed Weapons Cache",
                "A military cache, still locked with an old access code. You could try to crack it, but the failsafe is explosive.",
                Gamble("Crack the lock", 0.6f,
                    new[] { Item(Rarity.Common) }, "The lock clicks open. Whoever stashed this had good taste.",
                    new[] { Hull(-25) }, "The failsafe goes off in your face."),
                Require(Choice("Cut it open carefully", "Slow and steady. The cache opens without a spark.", Scraps(-20), Item(Rarity.Common)),
                    EventRequirement.None, 20),
                Choice("Leave it", "You leave the cache to the next brave fool.")),

            Event("Event_EarthEcho", "event.earth-echo", "Echo of Earth",
                "A fragment of an old Earth broadcast loops in the static: music, laughter, a weather report for a city that no longer exists. The cockpit goes quiet.",
                Choice("Just listen", "For a few minutes, you remember what you're fighting for.", Hull(25)),
                Choice("Record it for the archive", "The Lab will want this. Every scrap of home matters.", DarkMatter(15))),

            Event("Event_ScrapTrader", "event.scrap-trader", "Scrap Trader",
                "A trader's barge, bristling with salvage, pulls alongside. \"I deal in tech. One of yours for one of mine. Or I'll just buy yours.\"",
                Require(Choice("Trade an item", "The trader picks something from your racks and hands over a crate in return.", LoseItem(), Item(Rarity.Rare)),
                    EventRequirement.AnyItem, 0),
                Require(Choice("Sell an item", "\"Pleasure doing business.\"", LoseItem(), Scraps(60)), EventRequirement.AnyItem, 0),
                Choice("Move on", "The barge drifts back into the dark.")),

            Event("Event_GravityWell", "event.gravity-well", "Gravity Well",
                "The jump drops you into a gravity well around a dead star. Breaking free will cost you something.",
                Choice("Burn the engines hard", "The hull shrieks, but you tear free.", Hull(-20)),
                Choice("Jettison salvage", "Lighter, you slip out of the well.", Scraps(-30)),
                Gamble("Slingshot around the core", 0.5f,
                    new[] { DarkMatter(20) }, "You whip around the core and come out trailing Dark Matter.",
                    new[] { Hull(-35) }, "The core nearly swallows you. You claw your way out, barely.")),
        };

        static MysteryEventDefinition Event(string asset, string id, string title, string body, params EventChoice[] choices) =>
            Definition<MysteryEventDefinition>($"Events/{asset}", id, title, so =>
            {
                so.FindProperty("<Body>k__BackingField").stringValue = body;
                var array = so.FindProperty("<Choices>k__BackingField");
                array.arraySize = choices.Length;
                for (int i = 0; i < choices.Length; i++)
                {
                    var choice = choices[i];
                    var element = array.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("Label").stringValue = choice.Label;
                    element.FindPropertyRelative("Requirement").intValue = (int)choice.Requirement;
                    element.FindPropertyRelative("RequiredScraps").intValue = choice.RequiredScraps;
                    element.FindPropertyRelative("Chance").floatValue = choice.Chance;
                    element.FindPropertyRelative("ResultText").stringValue = choice.ResultText ?? string.Empty;
                    element.FindPropertyRelative("FailText").stringValue = choice.FailText ?? string.Empty;
                    SetOutcomes(element.FindPropertyRelative("Outcomes"), choice.Outcomes);
                    SetOutcomes(element.FindPropertyRelative("FailOutcomes"), choice.FailOutcomes);
                }
            });

        static void SetOutcomes(SerializedProperty array, EventOutcome[] outcomes)
        {
            array.arraySize = outcomes.Length;
            for (int i = 0; i < outcomes.Length; i++)
            {
                var element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Kind").intValue = (int)outcomes[i].Kind;
                element.FindPropertyRelative("Amount").floatValue = outcomes[i].Amount;
                element.FindPropertyRelative("Rarity").intValue = (int)outcomes[i].Rarity;
                element.FindPropertyRelative("Stat").intValue = (int)outcomes[i].Stat;
            }
        }

        static EventChoice Choice(string label, string result, params EventOutcome[] outcomes) =>
            new EventChoice { Label = label, ResultText = result, Outcomes = outcomes };

        static EventChoice Gamble(string label, float chance, EventOutcome[] success, string successText, EventOutcome[] fail, string failText) =>
            new EventChoice { Label = label, Chance = chance, Outcomes = success, ResultText = successText, FailOutcomes = fail, FailText = failText };

        static EventChoice Require(EventChoice choice, EventRequirement requirement, int scraps)
        {
            choice.Requirement = requirement;
            choice.RequiredScraps = scraps;
            return choice;
        }

        static EventOutcome Scraps(int amount) => new EventOutcome(EventOutcomeKind.Scraps, amount);
        static EventOutcome Hull(int amount) => new EventOutcome(EventOutcomeKind.Hull, amount);
        static EventOutcome HullPercent(int percent) => new EventOutcome(EventOutcomeKind.HullPercent, percent);
        static EventOutcome MaxHull(int amount) => new EventOutcome(EventOutcomeKind.MaxHull, amount);
        static EventOutcome DarkMatter(int amount) => new EventOutcome(EventOutcomeKind.DarkMatter, amount);
        static EventOutcome Item(Rarity minimum) => new EventOutcome(EventOutcomeKind.Equipment, rarity: minimum);
        static EventOutcome Upgrade() => new EventOutcome(EventOutcomeKind.UpgradeRandomItem);
        static EventOutcome LoseItem() => new EventOutcome(EventOutcomeKind.LoseRandomItem);
        static EventOutcome Ambush() => new EventOutcome(EventOutcomeKind.Ambush);
        static EventOutcome Stat(StatId stat, int percent) => new EventOutcome(EventOutcomeKind.StatPercent, percent, stat: stat);
    }
}
