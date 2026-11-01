using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Original synthesized placeholder effects; replace clips on MealGame at any time.
public static class MealSoundAssets
{
    public static AudioClip Make(string name, float seconds, int kind)
    {
        string folder = MealGameSetup.Folder + "/Audio";
        Directory.CreateDirectory(folder);
        string path = folder + "/" + name + ".wav";
        if (!File.Exists(path))
        {
            const int rate = 22050;
            int samples = (int)(rate * seconds);
            var random = new System.Random(42 + kind);
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
                writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
                double low = 0;
                for (int i = 0; i < samples; i++)
                {
                    double t = (double)i / rate, noise = random.NextDouble() * 2 - 1;
                    low = .88 * low + .12 * noise;
                    double value;
                    if (kind == 0)
                        value = (Math.Sin(2 * Math.PI * 1480 * t) + .4 * Math.Sin(2 * Math.PI * 2310 * t)) * Math.Exp(-5 * t) * .45;
                    else if (kind == 1)
                        value = (low * 1.5 + .1 * Math.Sin(2 * Math.PI * (380 * t + 18 * Math.Sin(9 * t)))) *
                            Math.Min(1, t * 12) * Math.Min(1, (seconds - t) * 8) * (.65 + .25 * Math.Sin(26 * t));
                    else
                    {
                        double cycle = t % .47, envelope = Math.Exp(-cycle * 19) * Math.Min(1, cycle * 200);
                        value = (noise * .48 + low * 1.1) * envelope * (.8 + .2 * Math.Sin(140 * t));
                    }
                    writer.Write((short)(Math.Max(-1, Math.Min(1, value)) * 24000));
                }
            }
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
