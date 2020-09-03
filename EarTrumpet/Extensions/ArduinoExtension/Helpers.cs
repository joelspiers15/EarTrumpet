using System;
using EarTrumpet.Interop.Helpers;
using System.Drawing;
using EarTrumpet.Interop;
using System.Text;

using static Models;
using System.Runtime.InteropServices;
using TsudaKageyu;
using System.Collections.Generic;

public static class Helpers
{
    /*
     * Attempt to get an icon from an application path
     */
    public static Bitmap iconBmapFromPath(string path)
    {
        Icon toReturn = null;
        try
        {
            // Get System icon
            StringBuilder iconPath = new StringBuilder((path.Contains(",") ? path : path + ",1"));
            int iconIndex = Shlwapi.PathParseIconLocationW(iconPath);
            toReturn = IconHelper.LoadIconResource(iconPath.ToString(), iconIndex, 128, 128);
        }
        catch (Exception)
        {
            // Use IconExtractor library to try to get a high res icon
            IconExtractor ie = new IconExtractor(path);
            Icon icon = ie.GetIcon(0);
            Icon[] allIcons = IconUtil.Split(icon);

            // Pick the highest resolution icon with a bit depth >16
            int bestIcon = 0;
            for (int i = 0; i < allIcons.Length; i++)
            {
                int curSize = allIcons[i].Size.Height;
                if (curSize > allIcons[bestIcon].Size.Height && IconUtil.GetBitCount(allIcons[i]) >= 16)
                {
                    bestIcon = i;
                }
            }

            toReturn = allIcons[bestIcon];
        }
        return toReturn.ToBitmap();
    }

    /*
    * Converts a Color object into a 16 bit R5G6B5 int
    */
    public static UInt16 colorTo16bit(Color color)
    {
        byte[] colorBytes = colorTo16bitByteArray(color);

        // Need to swap endianness since ToUint16 uses system default (little endian in my case)
        return BitConverter.ToUInt16(new byte[] { colorBytes[1], colorBytes[0]}, 0);
    }

    /*
     * Converts a Color object into a 16 bit R5G6B5 byte array
     */
    public static byte[] colorTo16bitByteArray(Color color)
    {
        byte[] toReturn = new byte[2];

        byte r = Convert.ToByte(map(color.R, 0, 255, 0, 31));
        byte g = Convert.ToByte(map(color.G, 0, 255, 0, 63));
        byte b = Convert.ToByte(map(color.B, 0, 255, 0, 31));

        toReturn[0] = Convert.ToByte((r << 3) | (g >> 3));
        toReturn[1] = Convert.ToByte(255 & ((g << 5) | b));

        return toReturn;
    }

    public static Color colorFromBitmap(Bitmap source)
    {
        ColorExtractor.ColorSet set = ColorExtractor.SelectColors(source);
        Color toReturn = set.Accent1;

        // If accent1 is gray and accent2 isn't use accent 2
        if(isGray(set.Accent1) && !isGray(set.Accent2))
        {
            toReturn = set.Accent2;
        }
        return toReturn;
    }

    /*
     * Checks if a given color is on the grayscale
     */
    public static bool isGray(Color color, int tolerance = 10)
    {
        return closeTo(color.R, color.G, tolerance) &&
            closeTo(color.G, color.B, tolerance) &&
            closeTo(color.B, color.R, tolerance);
    }

    public static bool closeTo(int source, int target, int tolerance)
    {
        int min = target - tolerance;
        int max = target + tolerance;
        return (source >= min && source <= max);
    }

    public static double map(int value, int min, int max, int minScale, int maxScale)
    {
        double scaled = minScale + (double)(value - min) / (max - min) * (maxScale - minScale);
        return scaled;
    }

    public static int SortAppsByPriority(AppData obj1, AppData obj2)
    {
        return obj1.getPriority().CompareTo(obj2.getPriority());
    }
}