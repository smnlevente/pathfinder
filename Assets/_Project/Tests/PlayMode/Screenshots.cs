using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>Screenshot helpers for visual review. Active only when PATHFINDER_SCREENSHOT_DIR is set.</summary>
    static class Screenshots
    {
        /// <summary>
        /// Draws a galaxy map graph (nodes coloured by type, edges, visited path) straight into a PNG. The in-game map is
        /// IMGUI, which batch mode cannot capture.
        /// </summary>
        public static void SaveMapPreview(Pathfinder.Core.Run.RunState run, string path)
        {
            const int size = 900;
            var image = new Texture2D(size, size, TextureFormat.RGB24, mipChain: false);
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(6, 8, 16, 255);

            var map = run.Map;
            Vector2 Position(Pathfinder.Core.Run.MapNode node) =>
                new Vector2(80f + node.X * (size - 160f), 80f + node.Row / (float)(map.RowCount - 1) * (size - 160f));

            foreach (var node in map.Nodes)
            {
                foreach (var next in node.Next)
                    DrawLine(pixels, size, Position(node), Position(next), new Color32(70, 90, 120, 255));
            }
            foreach (var node in map.Nodes)
                DrawDisc(pixels, size, Position(node), node.Type == Pathfinder.Core.Run.MapNodeType.Boss ? 28 : 20, TypeColor(node.Type));

            image.SetPixels32(pixels);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.Destroy(image);
        }

        static Color32 TypeColor(Pathfinder.Core.Run.MapNodeType type) => type switch
        {
            Pathfinder.Core.Run.MapNodeType.Scraps => new Color32(255, 200, 64, 255),
            Pathfinder.Core.Run.MapNodeType.Equipment => new Color32(90, 215, 255, 255),
            Pathfinder.Core.Run.MapNodeType.DarkMatter => new Color32(140, 115, 255, 255),
            Pathfinder.Core.Run.MapNodeType.Repair => new Color32(90, 255, 140, 255),
            Pathfinder.Core.Run.MapNodeType.Mystery => new Color32(215, 215, 215, 255),
            Pathfinder.Core.Run.MapNodeType.Shop => new Color32(255, 230, 128, 255),
            _ => new Color32(255, 76, 64, 255),
        };

        static void DrawLine(Color32[] pixels, int size, Vector2 a, Vector2 b, Color32 color)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(a, b));
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)Mathf.Max(1, steps));
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                        Plot(pixels, size, (int)p.x + dx, (int)p.y + dy, color);
                }
            }
        }

        static void DrawDisc(Color32[] pixels, int size, Vector2 center, int radius, Color32 color)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy <= radius * radius)
                        Plot(pixels, size, (int)center.x + dx, (int)center.y + dy, color);
                }
            }
        }

        static void Plot(Color32[] pixels, int size, int x, int y, Color32 color)
        {
            if (x >= 0 && y >= 0 && x < size && y < size)
                pixels[y * size + x] = color;
        }

        const int Width = 1600, Height = 900;

        /// <summary>The output directory, or an ignored test when screenshots are not requested.</summary>
        public static string Directory()
        {
            string directory = Environment.GetEnvironmentVariable("PATHFINDER_SCREENSHOT_DIR");
            if (string.IsNullOrEmpty(directory))
                Assert.Ignore("Set PATHFINDER_SCREENSHOT_DIR to capture screenshots.");
            System.IO.Directory.CreateDirectory(directory);
            return directory;
        }

        /// <summary>Renders the main camera, including post-processing but not IMGUI or overlay UI. Works in batch mode with a GPU.</summary>
        public static void CaptureCamera(string path)
        {
            var image = Render(Camera.main, TextureFormat.RGB24);
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.Destroy(image);
        }

        /// <summary>
        /// Renders the main camera, then the screen-space overlay canvases on top, as the player sees them. Overlay UI
        /// isn't drawn by cameras, so the canvases are briefly moved onto a transparent UI camera without post-processing
        /// and composited over the scene.
        /// </summary>
        public static void CaptureWithUi(string path)
        {
            var world = Render(Camera.main, TextureFormat.RGB24);

            var canvases = new System.Collections.Generic.List<Canvas>();
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas.isRootCanvas && canvas.isActiveAndEnabled && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    canvases.Add(canvas);
            }

            var uiCamera = new GameObject("UI Capture Camera").AddComponent<Camera>();
            uiCamera.clearFlags = CameraClearFlags.SolidColor;
            uiCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            uiCamera.cullingMask = 1 << 5;
            uiCamera.orthographic = true;
            uiCamera.allowHDR = false;
            uiCamera.allowMSAA = false;
            uiCamera.depth = -100;
            // Canvases size themselves to their camera's target, so give it one the size of the capture.
            var uiTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            uiCamera.targetTexture = uiTarget;

            foreach (var canvas in canvases)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = uiCamera;
                canvas.planeDistance = 1f;
            }
            Canvas.ForceUpdateCanvases();
            var ui = Render(uiCamera, TextureFormat.RGBA32);
            foreach (var canvas in canvases)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
            }
            uiCamera.targetTexture = null;
            Object.Destroy(uiCamera.gameObject);
            uiTarget.Release();
            Object.Destroy(uiTarget);

            var worldPixels = world.GetPixels32();
            var uiPixels = ui.GetPixels32();
            for (int i = 0; i < worldPixels.Length; i++)
            {
                // The UI was blended onto transparent black, so its colour is already weighted by its coverage.
                var u = uiPixels[i];
                float keep = 1f - u.a / 255f;
                worldPixels[i] = new Color32(
                    (byte)Mathf.Min(255, u.r + worldPixels[i].r * keep),
                    (byte)Mathf.Min(255, u.g + worldPixels[i].g * keep),
                    (byte)Mathf.Min(255, u.b + worldPixels[i].b * keep), 255);
            }
            world.SetPixels32(worldPixels);
            world.Apply();
            File.WriteAllBytes(path, world.EncodeToPNG());
            Object.Destroy(world);
            Object.Destroy(ui);
        }

        static Texture2D Render(Camera camera, TextureFormat format)
        {
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
            {
                RenderPipeline.SubmitRenderRequest(camera, request);
            }
            else
            {
                camera.targetTexture = target;
                camera.Render();
                camera.targetTexture = null;
            }

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, format, mipChain: false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            target.Release();
            Object.Destroy(target);
            return image;
        }
    }
}
