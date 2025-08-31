using System.IO;
using System.Reflection;
using UnityEngine;
using BigGustave;

namespace AssetBundleLoader
{
    public class Texture2DLoader
    {
        public static Texture2D CreateTexture2DFromPNG(Assembly executingAssembly, string fileName)
        {
            Texture2D tex = null;
            string filePath = Path.Combine(Path.GetDirectoryName(executingAssembly.Location),  fileName);

            if (!System.IO.File.Exists(filePath))
                filePath = Path.Combine(Path.GetDirectoryName(executingAssembly.Location), "plugins", fileName);

            if (System.IO.File.Exists(filePath))
            {
                using (var stream = System.IO.File.OpenRead(filePath))
                {
                    Png image = Png.Open(stream);
                    tex = new Texture2D(image.Width, image.Height);

                    Pixel pixel;

                    for (int x = 0; x < image.Width; x++)
                    {
                        for (int y = 0; y < image.Height; y++)
                        {
                            pixel = image.GetPixel(x, y);
                            tex.SetPixel(x, (image.Height - 1) - y, new Color(pixel.R / 255f, pixel.G / 255f, pixel.B / 255f, pixel.A / 255f));
                        }
                    }
                }

                tex.Apply();
            }
            return tex;
        }

        public static Texture2D CombineTexture2D(Texture2D baseTexture, Texture2D overlayTexture, Color overrideColour, bool overwrite = true)
        {
            var combinedTexture2D = new Texture2D(baseTexture.width, baseTexture.height);

            if (baseTexture.width != overlayTexture.width || baseTexture.height != overlayTexture.height)
            {
                BundleLoader.PluginLogger.LogError("Base and overlay textures must be the same size.");
                return null;
            }

            for (int x = 0; x < baseTexture.width; x++)
            {
                for (int y = 0; y < baseTexture.height; y++)
                {
                    Color overlayPixel = overlayTexture.GetPixel(x, y);
                    if (overlayPixel.a > 0.01f)
                    {
                        if (overwrite)
                        {
                            combinedTexture2D.SetPixel(x, y, overrideColour.a > 0.01f ? overrideColour : overlayPixel);
                        }
                        else
                        {
                            combinedTexture2D.SetPixel(x, y, overlayPixel);
                        }
                    }
                    else
                    {
                        combinedTexture2D.SetPixel(x, y, baseTexture.GetPixel(x, y));
                    }
                }
            }

            combinedTexture2D.Apply();

            return combinedTexture2D;
        }
    }
}
