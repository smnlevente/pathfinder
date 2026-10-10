using System.Collections;
using System.IO;
using NUnit.Framework;
using Pathfinder.Core.Combat;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Player;
using Pathfinder.Game.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>The HUD's ship readout and its layout on wide screens (playtest 6).</summary>
    public class HudTests : SandboxTestBase
    {
        [UnityTest]
        public IEnumerator ShipReadout_ShowsAmmoAndHull_OnTheShip()
        {
            yield return LoadRun();
            var hud = Object.FindAnyObjectByType<CombatHud>();
            yield return null;
            Assert.IsTrue(hud.ShipReadoutShown, "A readout follows the ship.");
            Assert.AreEqual(1f, hud.ShipAmmoFill, 0.001f, "A full magazine: a full ring.");

            Script(_ => new ShipCommand { FirePrimary = true });
            yield return WaitTicks(30);
            yield return null;
            Assert.Less(hud.ShipAmmoFill, 1f, "The ring around the ship drains as it fires.");

            Player.ApplyDamage(new DamageInfo(Player.Vitals.MaxShield + 30f, Team.Enemy, 0, Player.Position, Vector2.up));
            yield return null;
            Assert.Less(hud.ShipHullFill, 1f, "The hull bar under the ship drops.");
        }

        [UnityTest]
        public IEnumerator ShipReadout_ShowsThePrecisionLoaderWindow()
        {
            yield return LoadRun();
            var hud = Object.FindAnyObjectByType<CombatHud>();
            Player.Equip(Item("equipment.precision-loader"));
            var magazine = ((AutomaticPrimary)Player.Primary).Magazine;
            Assert.IsFalse(Player.ReloadWindow.HasValue);

            Script(_ => new ShipCommand { FirePrimary = true });
            yield return WaitUntilTicks(() => magazine.IsReloading, 400);
            Script(_ => default);
            yield return null;
            Assert.IsTrue(Player.ReloadWindow.HasValue && hud.SweetSpotShown, "While reloading, the window shows on the ship's ring.");
        }

        /// <summary>The HUD at 16:9 and at 21:9: on the wide screen the corner panels stay inside a centred 16:9 area.</summary>
        [UnityTest, Category("Screenshot")]
        public IEnumerator CaptureHud()
        {
            string directory = Screenshots.Directory();
            yield return LoadRun();
            Player.Equip(Item("equipment.precision-loader"));
            Spawn("enemy.gunner", new Vector2(4f, 7f));
            var magazine = ((AutomaticPrimary)Player.Primary).Magazine;
            Script(_ => new ShipCommand { FirePrimary = true, Aim = Vector2.up });
            yield return WaitUntilTicks(() => magazine.IsReloading, 400);
            Script(_ => default);
            yield return WaitTicks(20);
            yield return null;
            Screenshots.CaptureWithUi(Path.Combine(directory, "hud_16x9.png"));

            int width = Screenshots.Width, height = Screenshots.Height;
            try
            {
                Screenshots.Width = 2520;
                Screenshots.Height = 1080;
                Screenshots.CaptureWithUi(Path.Combine(directory, "hud_21x9.png"));
            }
            finally
            {
                Screenshots.Width = width;
                Screenshots.Height = height;
            }
        }
    }
}
