using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace LilAgentsWindows
{
    internal static class ImageFrameCache
    {
        private static readonly Dictionary<string, Image?> Cache = new(StringComparer.OrdinalIgnoreCase);

        public static Image? Get(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            if (Cache.TryGetValue(path, out var cached))
            {
                return cached;
            }

            try
            {
                using var stream = File.OpenRead(path);
                cached = Image.FromStream(stream);
                Cache[path] = (Image)cached.Clone();
                return Cache[path];
            }
            catch
            {
                Cache[path] = null;
                return null;
            }
        }

        public static void Clear()
        {
            foreach (var image in Cache.Values)
            {
                image?.Dispose();
            }

            Cache.Clear();
        }
    }
}
