/*
PluginColorExtract - Rainmeter plugin to extract interface colors from an image
Copyright (C) 2015 Bryan "icesoldier" Mitchell

This program is free software; you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation; either version 2 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License along
with this program; if not, write to the Free Software Foundation, Inc.,
51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.
*/

using System;
using System.Collections.Generic;
using System.Drawing;

/*
 * Class for extracting color themes from bitmaps
 * Algorithm pulled from a rainmeter plugin and ported to C#
 * Source github https://github.com/QuietMisdreavus/PluginColorExtract
 */
public class ColorExtractor
{
    internal enum ColorType
    {
        Background,
        Accent1,
        Accent2
    }

    public struct ColorSet
    {
        public Color Background;
        public Color Accent1;
        public Color Accent2;
    }

    public static ColorSet SelectColors(Bitmap pImage)
    {
        ColorSet ret = new ColorSet();

        const double CandidateDiffBackground = 0.3;
        const double MinDiffBackground = 0.4;
        const double TrackDistance = 0.25;

        ret.Background = DominantColors(InsideBorder(pImage))[0];
        List<Color> CandidateColors = DominantColors(Pixels(pImage));

        bool FirstFound = false;
        bool SecondFound = false;
        double BackgroundBrightness = ColorBrightness(ret.Background);

        for (int i = 0; i < CandidateColors.Count; i++)
        {
            if (Math.Abs(ColorBrightness(CandidateColors[i]) - BackgroundBrightness) < CandidateDiffBackground)
                continue;

            if (Math.Abs(ColorBrightness(CandidateColors[i]) - BackgroundBrightness) < MinDiffBackground)
            {
                CandidateColors[i] = YUV.BrightenFromBackground(CandidateColors[i], ret.Background, (MinDiffBackground - CandidateDiffBackground));
                if (Math.Abs(ColorBrightness(CandidateColors[i]) - BackgroundBrightness) < MinDiffBackground)
                    // If the background is close enough to black (maybe white, but black was what I hit first),
                    // we can't push the brightness any closer to our threshold just by pushing the Y value. Toss it.
                    continue;
            }

            if (!FirstFound)
            {
                ret.Accent1 = CandidateColors[i];
                FirstFound = true;
                continue;
            }

            if (YUV.ColorDistance(ret.Accent1, CandidateColors[i]) > TrackDistance)
            {
                SecondFound = true;
                ret.Accent2 = CandidateColors[i];
                break;
            }
        }

        if (!SecondFound | !FirstFound)
        {
            foreach (var backup in new List<Color>{ Color.Black, Color.White})
            {
                if (Math.Abs(ColorBrightness(backup) - BackgroundBrightness) >= CandidateDiffBackground)
                {
                    if (!FirstFound)
                    {
                        ret.Accent1 = backup;
                        FirstFound = true;
                        break;
                    }
                    else if (!SecondFound && YUV.ColorDistance(ret.Accent1, backup) > TrackDistance)
                    {
                        SecondFound = true;
                        ret.Accent2 = backup;
                        break;
                    }
                }
            }
        }

        if (!SecondFound)
            ret.Accent2 = YUV.FadeIntoBackground(ret.Accent1, ret.Background, 0.1);

        return ret;
    }

    private static IEnumerable<Color> InsideBorder(Bitmap pImage)
    {
        for (var x = 2; x <= pImage.Width - 3; x++)
        {
            yield return pImage.GetPixel(x, 2);
            yield return pImage.GetPixel(x, pImage.Height - 3);
        }

        for (var y = 2; y <= pImage.Height - 3; y++)
        {
            yield return pImage.GetPixel(2, y);
            yield return pImage.GetPixel(pImage.Width - 3, y);
        }
    }

    private static IEnumerable<Color> Pixels(Bitmap pImage)
    {
        for (var x = 0; x <= pImage.Width - 1; x++)
        {
            for (var y = 0; y <= pImage.Height - 1; y++)
                yield return pImage.GetPixel(x, y);
        }
    }

    private static List<Color> DominantColors(IEnumerable<Color> pColors)
    {
        List<List<Color>> buckets = ColorBuckets(pColors);
        buckets.Sort((left, right) => left.Count.CompareTo(right.Count) * -1);

        List<Color> ColorReductions = new List<Color>();

        foreach (var b in buckets)
            ColorReductions.Add(MeanColor(b));

        return ColorReductions;
    }

    private static List<List<Color>> ColorBuckets(IEnumerable<Color> pColors)
    {
        List<List<Color>> subsets = new List<List<Color>>();

        foreach (var c in pColors)
        {
            List<Color> bucket = null;

            foreach (var check in subsets)
            {
                if (YUV.ColorDistance(c, check[0]) < 0.1)
                {
                    bucket = check;
                    break;
                }
            }

            if (bucket == null)
            {
                bucket = new List<Color>();
                subsets.Add(bucket);
            }

            bucket.Add(c);
        }

        return subsets;
    }

    private static double ColorBrightness(Color pColor)
    {
        double accum = 0.0;

        accum += Math.Pow(pColor.R, 2) * 0.299;
        accum += Math.Pow(pColor.G, 2) * 0.587;
        accum += Math.Pow(pColor.B, 2) * 0.114;

        return Math.Sqrt(accum) / 255;
    }

    private static Color MeanColor(List<Color> pColors)
    {
        int RAccum = 0;
        int GAccum = 0;
        int BAccum = 0;

        foreach (var c in pColors)
        {
            RAccum += c.R;
            GAccum += c.G;
            BAccum += c.B;
        }

        return Color.FromArgb(System.Convert.ToInt32(RAccum / (double)pColors.Count), System.Convert.ToInt32(GAccum / (double)pColors.Count), System.Convert.ToInt32(BAccum / (double)pColors.Count));
    }

    public struct YUV
    {
        public double Y;
        public double U;
        public double V;

        private const double RWeight = 0.299;
        private const double GWeight = 0.587;
        private const double BWeight = 0.114;
        private const double UMax = 0.436;
        private const double VMax = 0.615;

        public Color ToRGB()
        {
            int R = (int)Math.Round(Y + (1.14 * V));
            int G = (int)Math.Round(Y - (0.395 * U) - (0.581 * V));
            int B = (int)Math.Round(Y + (2.033 * U));

            if (R > 255)
                R = 255;
            else if (R < 0)
                R = 0;

            if (G > 255)
                G = 255;
            else if (G < 0)
                G = 0;

            if (B > 255)
                B = 255;
            else if (B < 0)
                B = 0;

            return Color.FromArgb(R, G, B);
        }

        public static YUV FromRGB(Color pRGB)
        {
            YUV ret;

            ret.Y = (RWeight * pRGB.R) + (GWeight * pRGB.G) + (BWeight * pRGB.B);
            ret.U = UMax * ((pRGB.B - ret.Y) / (1 - BWeight));
            ret.V = VMax * ((pRGB.R - ret.Y) / (1 - RWeight));

            return ret;
        }

        public static Color FadeIntoBackground(Color pRGB, Color pBackground, double pAmount)
        {
            YUV Back = YUV.FromRGB(pBackground);
            YUV ret = YUV.FromRGB(pRGB);

            if ((Back.Y - ret.Y > 0))
            {
                ret.Y += pAmount * 255;
                ret.Y = Math.Min(ret.Y, 255);
            }
            else
            {
                ret.Y -= pAmount * 255;
                ret.Y = Math.Max(ret.Y, 0);
            }

            return ret.ToRGB();
        }

        public static Color BrightenFromBackground(Color pRGB, Color pBackground, double pAmount)
        {
            YUV Back = YUV.FromRGB(pBackground);
            YUV ret = YUV.FromRGB(pRGB);

            if ((Back.Y - ret.Y < 0))
            {
                ret.Y += pAmount * 255;
                ret.Y = Math.Min(ret.Y, 255);
            }
            else
            {
                ret.Y -= pAmount * 255;
                ret.Y = Math.Max(ret.Y, 0);
            }

            return ret.ToRGB();
        }

        public static double Distance(YUV pLeft, YUV pRight)
        {
            double accum = 0.0;
            double term;

            term = pLeft.Y - pRight.Y;
            accum += term * term;

            term = pLeft.U - pRight.U;
            accum += term * term;

            term = pLeft.V - pRight.V;
            accum += term * term;

            return Math.Sqrt(accum) / 255;
        }

        public static double ColorDistance(Color pLeft, Color pRight)
        {
            return Distance(FromRGB(pLeft), FromRGB(pRight));
        }
    }
}

