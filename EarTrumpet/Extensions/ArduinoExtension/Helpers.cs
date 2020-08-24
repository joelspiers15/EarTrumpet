using System;
using EarTrumpet.Interop.Helpers;
using System.Drawing;
using EarTrumpet.Interop;
using System.Text;

using static Models;

public static class Helpers
{
    public static Icon iconFromPath(string path)
    {
        Icon toReturn;
        try
        {
            // Get System icon
            StringBuilder iconPath = new StringBuilder((path.Contains(",") ? path : path + ",1"));
            int iconIndex = Shlwapi.PathParseIconLocationW(iconPath);
            toReturn = IconHelper.LoadIconResource(iconPath.ToString(), iconIndex, 128, 128);
        }
        catch(Exception)
        {
            toReturn = Icon.ExtractAssociatedIcon(path);
        }
        return toReturn;
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