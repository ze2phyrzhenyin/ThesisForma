namespace ThesisDocx.Core.Utilities;

public static class UnitConverter
{
    private const double TwipsPerInch = 1440;
    private const double EmuPerInch = 914400;
    private const double CmPerInch = 2.54;
    private const double MmPerInch = 25.4;

    public static int CentimetersToTwips(double centimeters)
    {
        return (int)Math.Round(centimeters / CmPerInch * TwipsPerInch, MidpointRounding.AwayFromZero);
    }

    public static int MillimetersToTwips(double millimeters)
    {
        return (int)Math.Round(millimeters / MmPerInch * TwipsPerInch, MidpointRounding.AwayFromZero);
    }

    public static int InchesToTwips(double inches)
    {
        return (int)Math.Round(inches * TwipsPerInch, MidpointRounding.AwayFromZero);
    }

    public static int PointsToTwips(double points)
    {
        return (int)Math.Round(points * 20, MidpointRounding.AwayFromZero);
    }

    public static double TwipsToPoints(int twips)
    {
        return twips / 20.0;
    }

    public static double TwipsToCentimeters(int twips)
    {
        return twips / TwipsPerInch * CmPerInch;
    }

    public static int PointsToHalfPoints(double points)
    {
        return (int)Math.Round(points * 2, MidpointRounding.AwayFromZero);
    }

    public static long CentimetersToEmu(double centimeters)
    {
        return (long)Math.Round(centimeters / CmPerInch * EmuPerInch, MidpointRounding.AwayFromZero);
    }

    public static long InchesToEmu(double inches)
    {
        return (long)Math.Round(inches * EmuPerInch, MidpointRounding.AwayFromZero);
    }
}
