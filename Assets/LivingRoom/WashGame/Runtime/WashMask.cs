using UnityEngine;

// R = soap coverage, G = towel coverage. Counts distinct pixels, never distance travelled.
public sealed class WashMask : System.IDisposable
{
    public const int Width = 256, Height = 144;
    public Texture2D Texture { get; }
    readonly Color32[] pixels = new Color32[Width * Height];
    int soapPixels, towelPixels;
    public float SoapCoverage => (float)soapPixels / pixels.Length;
    public float TowelCoverage => (float)towelPixels / pixels.Length;

    public WashMask()
    {
        Texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, true);
        Texture.name = "WashGame coverage";
        Texture.wrapMode = TextureWrapMode.Clamp;
        Texture.filterMode = FilterMode.Bilinear;
        Upload();
    }

    public void Stroke(Vector2 from, Vector2 to, float radius, float aspect, bool towel)
    {
        radius = Mathf.Max(.005f, radius);
        aspect = Mathf.Max(.1f, aspect);
        Vector2 delta = to - from;
        float distance = new Vector2(delta.x * aspect, delta.y).magnitude;
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / (radius * .35f)));
        for (int i = 0; i <= steps; i++) Stamp(Vector2.Lerp(from, to, (float)i / steps), radius, aspect, towel);
        Upload();
    }

    void Stamp(Vector2 uv, float radius, float aspect, bool towel)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt((uv.x - radius / aspect) * Width));
        int x1 = Mathf.Min(Width - 1, Mathf.CeilToInt((uv.x + radius / aspect) * Width));
        int y0 = Mathf.Max(0, Mathf.FloorToInt((uv.y - radius) * Height));
        int y1 = Mathf.Min(Height - 1, Mathf.CeilToInt((uv.y + radius) * Height));
        for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
        {
            float dx = ((x + .5f) / Width - uv.x) * aspect;
            float dy = (y + .5f) / Height - uv.y;
            float distance = Mathf.Sqrt(dx * dx + dy * dy) / radius;
            if (distance >= 1) continue;
            byte value = (byte)Mathf.RoundToInt(255 * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.8f, 1, distance))));
            int index = y * Width + x;
            byte old = towel ? pixels[index].g : pixels[index].r;
            if (value <= old) continue;
            if (old < 128 && value >= 128) { if (towel) towelPixels++; else soapPixels++; }
            if (towel) pixels[index].g = value; else pixels[index].r = value;
        }
    }

    void Upload() { Texture.SetPixels32(pixels); Texture.Apply(false, false); }
    public void Dispose() { if (Texture) Object.Destroy(Texture); }
}
