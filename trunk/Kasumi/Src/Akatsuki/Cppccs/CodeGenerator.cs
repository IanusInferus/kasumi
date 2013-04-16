//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Kasumi.Akatsuki <Visual C#>
//  Description: Kasumi界面结构Cocos2dx界面代码生成器
//  Version:     2013.04.16.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.Mapping.MetaSchema;
using Firefly.TextEncoding;
using Yuki.ObjectSchema;
using Kasumi.UISchema;

namespace Kasumi.Akatsuki
{
    public static class CodeGenerator
    {
        public static String CompileToCppccs(this Window ksm, String ClassName, String NamespaceName, String[] Imports)
        {
            Writer w = new Writer(ksm, ClassName, NamespaceName, Imports);
            var a = w.GetSchema();
            return String.Join("\r\n", a);
        }

        public class Writer
        {
            private static ObjectSchemaTemplateInfo TemplateInfo;

            private Window ksm;
            private String ClassName;
            private String NamespaceName;
            private String[] Imports;

            static Writer()
            {
                var OriginalTemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Yuki.ObjectSchema.Properties.Resources.Cpp);
                TemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.Cppccs);
                TemplateInfo.Keywords = OriginalTemplateInfo.Keywords;
                TemplateInfo.PrimitiveMappings = OriginalTemplateInfo.PrimitiveMappings;
            }

            public Writer(Window ksm, String ClassName, String NamespaceName, String[] Imports)
            {
                this.ksm = ksm;
                this.ClassName = ClassName;
                this.NamespaceName = NamespaceName;
                this.Imports = Imports;
            }

            public String[] GetSchema()
            {
                var Header = GetHeader();
                var Includes = this.Imports.Where(i => IsInclude(i)).ToArray();
                var Imports = this.Imports.Where(i => !IsInclude(i)).ToArray();
                var ComplexTypes = GetComplexTypes(ksm);
                var Contents = ComplexTypes;
                if (NamespaceName != "")
                {
                    foreach (var nn in NamespaceName.Split('.').Reverse())
                    {
                        Contents = GetTemplate("Namespace").Substitute("NamespaceName", nn).Substitute("Contents", Contents);
                    }
                }
                return EvaluateEscapedIdentifiers(GetTemplate("Main").Substitute("Header", Header).Substitute("Includes", Includes).Substitute("Imports", Imports).Substitute("Contents", Contents)).Select(Line => Line.TrimEnd(' ')).ToArray();
            }

            public Boolean IsInclude(String s)
            {
                if (s.StartsWith("<") && s.EndsWith(">")) { return true; }
                if (s.StartsWith(@"""") && s.EndsWith(@"""")) { return true; }
                return false;
            }

            public String[] GetHeader()
            {
                return GetTemplate("Header");
            }

            private Optional<String> GetName(Control c)
            {
                if (c.OnGrid)
                {
                    return c.Grid.Name;
                }
                else if (c.OnButton)
                {
                    return c.Button.Name;
                }
                else if (c.OnLabel)
                {
                    return c.Label.Name;
                }
                else if (c.OnImage)
                {
                    return c.Image.Name;
                }
                else if (c.OnTextBox)
                {
                    return c.TextBox.Name;
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            private String GetControlTypeName(Control c)
            {
                return c._Tag.ToString();
            }

            public Control[] GetAllNamedControls(Window ksm)
            {
                var l = new List<Control>();
                Action<Control> a = null;
                a = c =>
                {
                    if (GetName(c).OnHasValue)
                    {
                        l.Add(c);
                    }
                    if (c.OnGrid)
                    {
                        foreach (var cc in c.Grid.Content)
                        {
                            a(cc);
                        }
                    }
                };
                a(ksm.Content);
                return l.ToArray();
            }

            public String[] GetControlTypeDefinitions()
            {
                var ControlTypes = (new String[] { "Control" }).Concat(Enum.GetNames(typeof(ControlTag))).ToArray();
                return ControlTypes.SelectMany(ct => GetTemplate("ControlTypeDefinition").Substitute("ControlTypeName", ct)).ToArray();
            }

            public String[] GetNamedChildDefinitions(Control[] NamedControls)
            {
                return NamedControls.SelectMany(c => GetTemplate("NamedChildDefinition").Substitute("Name", GetName(c).HasValue).Substitute("ControlTypeName", GetControlTypeName(c))).ToArray();
            }

            public String[] GetNamedChildInitializations(Control[] NamedControls)
            {
                var l = NamedControls.SelectMany(c => GetTemplate("NamedChildInitialization").Substitute("Name", GetName(c).HasValue)).ToArray();
                for (int k = 0; k < l.Length - 1; k += 1)
                {
                    l[k] = l[k] + ",";
                }
                if (l.Length >= 1)
                {
                    return (new String[] { ":" }).Concat(l).ToArray();
                }
                return l.ToArray();
            }

            public String[] GetNamedChildAssignments(Control[] NamedControls)
            {
                return NamedControls.SelectMany(c => GetTemplate("NamedChildAssignment").Substitute("Name", GetName(c).HasValue).Substitute("ControlTypeName", GetControlTypeName(c))).ToArray();
            }

            public String[] GetNamedChildDestructions(Control[] NamedControls)
            {
                return NamedControls.SelectMany(c => GetTemplate("NamedChildDestruction").Substitute("Name", GetName(c).HasValue)).ToArray();
            }

            public String[] GetChildren(Window ksm)
            {
                var NamedControls = GetAllNamedControls(ksm);
                var ControlTypeDefinitions = GetControlTypeDefinitions();
                var NamedChildDefinitions = GetNamedChildDefinitions(NamedControls);
                var NamedChildInitializations = GetNamedChildInitializations(NamedControls);
                var NamedChildAssignments = GetNamedChildAssignments(NamedControls);
                var NamedChildDestructions = GetNamedChildDestructions(NamedControls);
                return GetTemplate("Children").Substitute("ClassName", ClassName).Substitute("ControlTypeDefinitions", ControlTypeDefinitions).Substitute("NamedChildDefinitions", NamedChildDefinitions).Substitute("NamedChildInitializations", NamedChildInitializations).Substitute("NamedChildAssignments", NamedChildAssignments).Substitute("NamedChildDestructions", NamedChildDestructions);
            }

            public String[] GetComplexTypes(Window ksm)
            {
                var l = new List<String>();

                l.AddRange(GetChildren(ksm));
                l.Add("");

                if (l.Count > 0)
                {
                    l = l.Take(l.Count - 1).ToList();
                }

                return l.ToArray();
            }

            public String[] GetTemplate(String Name)
            {
                return GetLines(TemplateInfo.Templates[Name].Value);
            }
            public String[] GetLines(String Value)
            {
                return Yuki.ObjectSchema.Cpp.Common.CodeGenerator.Writer.GetLines(Value);
            }
            public String GetEscapedIdentifier(String Identifier)
            {
                return Yuki.ObjectSchema.Cpp.Common.CodeGenerator.Writer.GetEscapedIdentifier(Identifier);
            }
            private String[] EvaluateEscapedIdentifiers(String[] Lines)
            {
                return Yuki.ObjectSchema.Cpp.Common.CodeGenerator.Writer.EvaluateEscapedIdentifiers(Lines);
            }
        }

        private static String[] Substitute(this String[] Lines, String Parameter, String Value)
        {
            return Yuki.ObjectSchema.Cpp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
        private static String[] Substitute(this String[] Lines, String Parameter, String[] Value)
        {
            return Yuki.ObjectSchema.Cpp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
    }
}
