//==========================================================================
//
//  File:        Program.cs
//  Location:    Kasumi.Akatsuki <Visual C#>
//  Description: Cocos2dx界面生成工具
//  Version:     2013.02.16.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Firefly;
using Firefly.Mapping.Binary;
using Firefly.Streaming;
using Firefly.TextEncoding;
using Firefly.Texting;
using Kasumi.UISchema;

namespace Kasumi.Akatsuki
{
    public static class Program
    {
        public static int Main()
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                return MainInner();
            }
            else
            {
                try
                {
                    return MainInner();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ExceptionInfo.GetExceptionInfo(ex));
                    return -1;
                }
            }
        }

        public static int MainInner()
        {
            TextEncoding.WritingDefault = TextEncoding.UTF8;

            var CmdLine = CommandLine.GetCmdLine();
            var argv = CmdLine.Arguments;

            if (CmdLine.Arguments.Length != 0)
            {
                DisplayInfo();
                return -1;
            }

            if (CmdLine.Options.Length == 0)
            {
                DisplayInfo();
                return 0;
            }

            Window ksm = null;
            String ClassName = "";
            var Imports = new List<String>();
            foreach (var opt in CmdLine.Options)
            {
                if ((opt.Name.ToLower() == "?") || (opt.Name.ToLower() == "help"))
                {
                    DisplayInfo();
                    return 0;
                }
                else if (opt.Name.ToLower() == "loadksm")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        var KasumiFilePath = args[0];
                        ClassName = FileNameHandling.GetMainFileName(KasumiFilePath).Replace(".", "_");
                        ksm = UIXmlFile.ReadFile(KasumiFilePath);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "import")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        Imports.Add(args[0]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "b")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        var BinaryFilePath = args[0];
                        var Bytes = BinaryTranslator.Serialize(ksm);
                        using (var fs = Streams.CreateWritable(BinaryFilePath))
                        {
                            fs.Write(Bytes);
                        }
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "cppccs")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        GenerateCppCode(ksm, ClassName, args[0], "", Imports.ToArray());
                    }
                    else if (args.Length == 2)
                    {
                        GenerateCppCode(ksm, ClassName, args[0], args[1], Imports.ToArray());
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else
                {
                    throw (new ArgumentException(opt.Name));
                }
            }
            return 0;
        }

        public static void DisplayInfo()
        {
            Console.WriteLine(@"Kasumi界面结构Cocos2dx界面生成工具");
            Console.WriteLine(@"Kasumi.Akatsuki，按BSD许可证分发");
            Console.WriteLine(@"F.R.C.");
            Console.WriteLine(@"");
            Console.WriteLine(@"本工具用于从Kasumi界面结构生成代码。");
            Console.WriteLine(@"");
            Console.WriteLine(@"用法:");
            Console.WriteLine(@"Akatsuki (/<Command>)*");
            Console.WriteLine(@"装载Kasumi节目结构");
            Console.WriteLine(@"/loadksm:<KasumiFile>");
            Console.WriteLine(@"增加命名空间引用");
            Console.WriteLine(@"/import:<NamespaceName>");
            Console.WriteLine(@"将Kasumi XML格式数据转化为二进制数据");
            Console.WriteLine(@"/b:<BinaryFile>");
            Console.WriteLine(@"生成C++2011 Cocos2dx代码");
            Console.WriteLine(@"/cppccs:<CppCodePath>[,<NamespaceName>]");
            Console.WriteLine(@"KasumiFile Kasumi XML文件路径。");
            Console.WriteLine(@"NamespaceName C#文件中的命名空间名称。");
            Console.WriteLine(@"BinaryFile 二进制文件路径。");
            Console.WriteLine(@"CppCodePath C++代码文件路径。");
            Console.WriteLine(@"");
            Console.WriteLine(@"示例:");
            Console.WriteLine(@"Akatsuki /loadksm:Login.ksm /b:Login.ksb /cppccs:Login.h,UI");
        }

        public static void GenerateCppCode(Window ksm, String ClassName, String CppCodePath, String NamespaceName, String[] Imports)
        {
            var Compiled = ksm.CompileToCppccs(ClassName, NamespaceName, Imports);
            if (File.Exists(CppCodePath))
            {
                var Original = Txt.ReadFile(CppCodePath);
                if (String.Equals(Compiled, Original, StringComparison.Ordinal))
                {
                    return;
                }
            }
            var Dir = FileNameHandling.GetFileDirectory(CppCodePath);
            if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
            Txt.WriteFile(CppCodePath, Compiled);
        }
    }
}
