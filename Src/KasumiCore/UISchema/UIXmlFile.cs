using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Firefly;
using Firefly.Texting;

namespace Kasumi.UISchema
{
    public static class UIXmlFile
    {
        private static UISchemaXmlSerializer xs;

        private static Thickness StringToThickness(String s)
        {
            var v = s.Split(',').Select(i => int.Parse(i)).ToArray();
            if (v.Length != 4) { throw new InvalidOperationException(); }
            return new Thickness { Left = v[0], Top = v[1], Right = v[2], Bottom = v[3] };
        }
        private static String ThicknessToString(Thickness t)
        {
            return String.Join(",", (new int[] { t.Left, t.Top, t.Right, t.Bottom }).Select(v => v.ToInvariantString()).ToArray());
        }
        private static Color StringToColor(String s)
        {
            if (!s.StartsWith("#")) { throw new InvalidOperationException(); }
            if (s.Length == 7)
            {
                var i = 0xFF000000U | UInt32.Parse(s.Substring(1), System.Globalization.NumberStyles.HexNumber);
                return new Color { Value = i };
            }
            else if (s.Length == 9)
            {
                var i = UInt32.Parse(s.Substring(1), System.Globalization.NumberStyles.HexNumber);
                return new Color { Value = i };
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        private static String ColorToString(Color c)
        {
            if ((c.Value & 0xFF000000U) == 0xFF000000U)
            {
                return "#" + (c.Value & 0x00FFFFFFU).ToString("X6", System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                return "#" + c.Value.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        static UIXmlFile()
        {
            xs = new UISchemaXmlSerializer();
            xs.PutReader(StringToThickness);
            xs.PutWriter((Func<Thickness, String>)(ThicknessToString));
            xs.PutReader(StringToColor);
            xs.PutWriter((Func<Color, String>)(ColorToString));
        }

        public static Window ReadFile(String Path)
        {
            var x = XmlFile.ReadFile(Path);
            var v = xs.Read<Window>(x);
            return v;
        }

        public static Window ReadFile(StreamReader sr)
        {
            var x = XmlFile.ReadFile(sr);
            var v = xs.Read<Window>(x);
            return v;
        }

        public static void WriteFile(String Path, Window Value)
        {
            var x = xs.Write<Window>(Value);
            XmlFile.WriteFile(Path, x);
        }

        public static void WriteFile(StreamWriter sw, Window Value)
        {
            var x = xs.Write<Window>(Value);
            XmlFile.WriteFile(sw, x);
        }
    }
}
