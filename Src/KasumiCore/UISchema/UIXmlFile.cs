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

        static UIXmlFile()
        {
            xs = new UISchemaXmlSerializer();
            xs.PutReader(StringToThickness);
            xs.PutWriter((Func<Thickness, String>)(ThicknessToString));
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
