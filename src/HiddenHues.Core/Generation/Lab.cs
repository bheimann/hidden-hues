namespace HiddenHues.Core.Generation;

/// <summary>
/// A color in CIELAB space (D65 white). Distances here roughly match how different two colors look,
/// which is what color reduction needs.
/// </summary>
public readonly record struct Lab(double L, double A, double B)
{
    private const double Xn = 0.95047, Yn = 1.0, Zn = 1.08883;

    public static Lab FromRgb(Rgb c)
    {
        var r = ToLinear(c.R);
        var g = ToLinear(c.G);
        var b = ToLinear(c.B);

        var x = (0.4124 * r + 0.3576 * g + 0.1805 * b) / Xn;
        var y = (0.2126 * r + 0.7152 * g + 0.0722 * b) / Yn;
        var z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / Zn;

        var fx = F(x);
        var fy = F(y);
        var fz = F(z);
        return new Lab(116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz));

        static double F(double t) => t > 216.0 / 24389 ? Math.Cbrt(t) : (24389.0 / 27 * t + 16) / 116;
    }

    public Rgb ToRgb()
    {
        var fy = (L + 16) / 116;
        var fx = fy + A / 500;
        var fz = fy - B / 200;

        var x = FInv(fx) * Xn;
        var y = FInv(fy) * Yn;
        var z = FInv(fz) * Zn;

        var r = 3.2406 * x - 1.5372 * y - 0.4986 * z;
        var g = -0.9689 * x + 1.8758 * y + 0.0415 * z;
        var b = 0.0557 * x - 0.2040 * y + 1.0570 * z;
        return new Rgb(ToByte(r), ToByte(g), ToByte(b));

        static double FInv(double t) => t * t * t > 216.0 / 24389 ? t * t * t : (116 * t - 16) / (24389.0 / 27);
    }

    public double DistanceSquared(Lab other)
    {
        var dl = L - other.L;
        var da = A - other.A;
        var db = B - other.B;
        return dl * dl + da * da + db * db;
    }

    private static double ToLinear(byte channel)
    {
        var c = channel / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static byte ToByte(double linear)
    {
        linear = Math.Clamp(linear, 0, 1);
        var c = linear <= 0.0031308 ? 12.92 * linear : 1.055 * Math.Pow(linear, 1 / 2.4) - 0.055;
        return (byte)Math.Round(c * 255);
    }
}
