using System;
using System.Collections.Generic;
using System.IO;
using Pathfinder.Game.Content;
using UnityEditor;
using UnityEngine;

namespace Pathfinder.Editor.Setup
{
    public static partial class ContentBuilder
    {
        const string IconsDir = Root + "/Art/Icons";

        // Playtest 7: fewer words, more pictures. Icons from game-icons.net by Lorc and Delapouite, CC BY 3.0 (see CREDITS.md),
        // white on transparent so the UI can tint them. Content gets its icon only while it has none, so a designer's choice
        // in the Inspector is never overwritten.
        static readonly Dictionary<string, string> ContentIcons = new Dictionary<string, string>
        {
            ["equipment.adrenal-thrusters"] = "rocket-thruster",
            ["equipment.amplifier"] = "power-lightning",
            ["equipment.armor-plating"] = "armor-upgrade",
            ["equipment.capacitor"] = "energy-shield",
            ["equipment.critical-optics"] = "crosshair",
            ["equipment.extended-mag"] = "machine-gun-magazine",
            ["equipment.overclock"] = "speedometer",
            ["equipment.piercing-rounds"] = "supersonic-bullet",
            ["equipment.rapid-cycler"] = "hourglass",
            ["equipment.salvage-magnet"] = "magnet",
            ["equipment.twin-charge"] = "double-shot",
            ["equipment.blink-dash"] = "teleport",
            ["equipment.bullet-dash"] = "explosion-rays",
            ["equipment.incendiary"] = "small-fire",
            ["equipment.precision-loader"] = "stopwatch",
            ["equipment.kill-refund"] = "recycle",
            ["equipment.last-stand"] = "angel-wings",
            ["equipment.phase-dash"] = "ghost",
            ["equipment.pushback-dash"] = "sonic-boom",
            ["equipment.ricochet"] = "shield-bounces",
            ["equipment.split-shot"] = "striking-arrows",
            ["primary.photon-repeater"] = "laser-blast",
            ["primary.plasma-cutter"] = "laser-precision",
            ["primary.quantum-charger"] = "plasma-bolt",
            ["secondary.seeker-swarm"] = "missile-swarm",
            ["secondary.nova-pulse"] = "sun",
            ["meta.hull-plating"] = "breastplate",
            ["meta.shield-matrix"] = "bolt-shield",
            ["meta.salvage-protocols"] = "coins",
            ["meta.second-dash"] = "run",
            ["meta.unlock-quantum-charger"] = "plasma-bolt",
        };

        /// <summary>Imports the icons as UI sprites, gives content its icon and fills the currency and map icon set.</summary>
        static GameIcons BuildIcons(ContentDatabase database)
        {
            foreach (string file in Directory.GetFiles(IconsDir, "*.png"))
                ConfigureIcon(file.Replace('\\', '/'));

            var definitions = new List<ContentDefinition>();
            definitions.AddRange(database.Equipment);
            definitions.AddRange(database.PrimaryWeapons);
            definitions.AddRange(database.SecondaryWeapons);
            definitions.AddRange(database.MetaUpgrades);
            foreach (var definition in definitions)
            {
                if (definition != null && definition.Icon == null && ContentIcons.TryGetValue(definition.Id, out string file))
                {
                    SetFields(definition, ("icon", Icon(file)));
                    EditorUtility.SetDirty(definition);
                }
            }

            const string path = DataDir + "/UI/GameIcons.asset";
            var icons = AssetDatabase.LoadAssetAtPath<GameIcons>(path);
            if (icons == null)
            {
                EnsureFolder(DataDir + "/UI");
                icons = ScriptableObject.CreateInstance<GameIcons>();
                AssetDatabase.CreateAsset(icons, path);
            }
            SetIfEmpty(icons, ("Scraps", Icon("cog")), ("DarkMatter", Icon("crystal-cluster")), ("Cores", Icon("black-hole-bolas")),
                ("Equipment", Icon("chest")), ("Mystery", Icon("uncertainty")), ("Repair", Icon("auto-repair")), ("Shop", Icon("shop")),
                ("Boss", Icon("daemon-skull")), ("Reinforce", Icon("breastplate")));
            SetFields(database, ("Icons", icons));
            EditorUtility.SetDirty(database);
            return icons;
        }

        static Sprite Icon(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{IconsDir}/{name}.png")
            ?? throw new InvalidOperationException($"Icon '{name}' is missing from {IconsDir}.");

        /// <summary>A UI sprite: mipmapped and capped at 256 px, plenty for menus, so it stays sharp when scaled down.</summary>
        static void ConfigureIcon(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
                return;
            bool configured = importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single
                && importer.mipmapEnabled && importer.maxTextureSize == 256 && importer.alphaIsTransparency;
            if (configured)
                return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 256;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
    }
}
