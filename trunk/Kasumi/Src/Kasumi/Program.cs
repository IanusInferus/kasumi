//==========================================================================
//
//  File:        Program.cs
//  Location:    Kasumi <Visual C#>
//  Description: Kasumi界面设计器
//  Version:     2013.02.18.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Firefly;

namespace Kasumi
{
    public class Program
    {
        private static MainWindow MainWindow;

        public static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (MainWindow == null)
            {
                MessageBox.Show(ExceptionInfo.GetExceptionInfo(e.Exception, new StackTrace(3, true)), "Error");
            }
            else
            {
                MessageBox.Show(MainWindow, ExceptionInfo.GetExceptionInfo(e.Exception, new StackTrace(3, true)), "Error");
            }
            e.Handled = true;
        }

        [STAThread]
        public static int Main(String[] args)
        {
            if (!Debugger.IsAttached)
            {
                try
                {
                    var a = new App();
                    MainWindow = new MainWindow();
                    a.DispatcherUnhandledException += App_DispatcherUnhandledException;
                    if (args.Length == 1)
                    {
                        MainWindow.LoadFile(args[0]);
                    }
                    a.Run(MainWindow);
                    Environment.Exit(0);
                    return 0;
                }
                catch (Exception ex)
                {
                    if (MainWindow == null)
                    {
                        MessageBox.Show(ExceptionInfo.GetExceptionInfo(ex), "Error");
                    }
                    else
                    {
                        MessageBox.Show(MainWindow, ExceptionInfo.GetExceptionInfo(ex), "Error");
                    }
                    Environment.Exit(-1);
                    return -1;
                }
            }
            else
            {
                var Success = false;
                try
                {
                    var a = new App();
                    MainWindow = new MainWindow();
                    if (args.Length == 1)
                    {
                        MainWindow.LoadFile(args[0]);
                    }
                    a.Run(MainWindow);
                    Success = true;
                    Environment.Exit(0);
                    return 0;
                }
                finally
                {
                    if (!Success)
                    {
                        Environment.Exit(-1);
                    }
                }
            }
        }
    }
}
