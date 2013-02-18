﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Firefly;
using Firefly.Mapping.XmlText;
using Firefly.Texting.TreeFormat;
using Kasumi.UISchema;

namespace Kasumi
{
    public class ActionCommand : ICommand
    {
        private Action a;

        public ActionCommand(Action a)
        {
            this.a = a;
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        event EventHandler ICommand.CanExecuteChanged
        {
            add { }
            remove { }
        }

        public void Execute(object parameter)
        {
            a();
        }
    }

    public partial class MainWindow : System.Windows.Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private static String ConfigurationFilePath;
        private static Configuration c;
        private Dictionary<Resolution, Canvas> ResolutionToCanvas = new Dictionary<Resolution, Canvas>();
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ConfigurationFilePath = System.IO.Path.Combine(Environment.CurrentDirectory, Assembly.GetEntryAssembly().GetName().Name + ".exe.ini");
            if (!File.Exists(ConfigurationFilePath))
            {
                ConfigurationFilePath = Assembly.GetEntryAssembly().Location + ".ini";
            }
            if (File.Exists(ConfigurationFilePath))
            {
                var x = TreeFile.ReadFile(ConfigurationFilePath);
                c = (new XmlSerializer()).Read<Configuration>(x);
            }
            else
            {
                c = new Configuration();
                c.Resolutions = new Resolution[]
                {
                    new Resolution { Name = "iPhone3G", Width = 480, Height = 320},
                    new Resolution { Name = "HTC HD2", Width = 800, Height = 480},
                    new Resolution { Name = "iPhone4", Width = 960, Height = 640},
                    new Resolution { Name = "iPhone5", Width = 1136, Height = 640},
                    new Resolution { Name = "iPad", Width = 1024, Height = 768}
                };
            }

            foreach (var r in c.Resolutions)
            {
                var cv = new Canvas();
                cv.Width = r.Width;
                cv.Height = r.Height;
                cv.Background = Brushes.LightGray;
                cv.Margin = new System.Windows.Thickness(10);
                cv.ToolTip = String.Format("{0} {1}x{2}", r.Name, r.Width, r.Height);
                ResolutionToCanvas.Add(r, cv);
                StackPanel_Preview.Children.Add(cv);
            }

            Menu_File_Open.Click += Menu_File_Open_Click;
            Menu_File_Exit.Click += Menu_File_Exit_Click;

            AddInputBinding(new ActionCommand(() => Menu_File_Open_Click(null, null)), new KeyGesture(Key.O, ModifierKeys.Control));
            AddInputBinding(new ActionCommand(() => Menu_File_Exit_Click(null, null)), new KeyGesture(Key.F4, ModifierKeys.Alt));

            if (KasumiFilePath != null)
            {
                if (fsw == null)
                {
                    fsw = new FileSystemWatcher();
                    fsw.EnableRaisingEvents = false;
                    fsw.NotifyFilter = NotifyFilters.LastWrite;
                    fsw.Changed += fsw_Changed;
                }
                else
                {
                    fsw.EnableRaisingEvents = false;
                }
                Load(KasumiFilePath);
                fsw.Path = System.IO.Path.GetDirectoryName(KasumiFilePath);
                if (KasumiFilePath != null)
                {
                    fsw.EnableRaisingEvents = true;
                }
            }
        }

        private void AddInputBinding(ICommand c, KeyGesture g)
        {
            CommandBindings.Add(new CommandBinding(c));
            InputBindings.Add(new KeyBinding(c, g));
        }

        private String KasumiFilePath;
        private OpenFileDialog fd;
        private FileSystemWatcher fsw;
        private void Menu_File_Open_Click(object sender, RoutedEventArgs e)
        {
            if (fd == null)
            {
                fd = new OpenFileDialog();
                fd.Filter = "Kasumi文件(*.ksm)|*.ksm";
            }
            if (fsw == null)
            {
                fsw = new FileSystemWatcher();
                fsw.EnableRaisingEvents = false;
                fsw.NotifyFilter = NotifyFilters.LastWrite;
                fsw.Changed += fsw_Changed;
            }
            else
            {
                fsw.EnableRaisingEvents = false;
            }
            if (fd.ShowDialog() == true)
            {
                KasumiFilePath = fd.FileName;
                Load(KasumiFilePath);
                fsw.Path = System.IO.Path.GetDirectoryName(KasumiFilePath);
            }
            if (KasumiFilePath != null)
            {
                fsw.EnableRaisingEvents = true;
            }
        }

        public void LoadFile(String KasumiFilePath)
        {
            this.KasumiFilePath = KasumiFilePath;
        }

        private Object LoadLockee = new Object();
        private void fsw_Changed(object sender, FileSystemEventArgs e)
        {
            if (e.FullPath == KasumiFilePath)
            {
                Action a = () => Load(KasumiFilePath);
                Dispatcher.Invoke(a);
            }
        }

        private void Load(String KasumiFilePath)
        {
            if (Monitor.TryEnter(LoadLockee))
            {
                try
                {
                    var ksm = UISchema.UIXmlFile.ReadFile(KasumiFilePath);
                    if (Double.IsNaN(Canvas_Displayer.Width))
                    {
                        Canvas_Displayer.Width = ksm.Width;
                    }
                    if (Double.IsNaN(Canvas_Displayer.Height))
                    {
                        Canvas_Displayer.Height = ksm.Height;
                    }
                    Canvas_Displayer.Background = Brushes.LightGray;
                    TextBox_Output.Text = "装载完成: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    DrawCanvas(Canvas_Displayer, ksm, ksm.Width, ksm.Height);

                    foreach (var p in ResolutionToCanvas)
                    {
                        DrawCanvas(p.Value, ksm, p.Key.Width, p.Key.Height);
                    }
                }
                catch (Exception ex)
                {
                    TextBox_Output.Text = ExceptionInfo.GetExceptionInfo(ex);
                }
                finally
                {
                    Monitor.Exit(LoadLockee);
                }
            }
        }

        private static void DrawCanvas(Canvas c, UISchema.Window ksm, double Width, double Height)
        {
            c.Children.Clear();
            var ScaleFactor = Math.Min(Width / (double)(ksm.Width), Height / (double)(ksm.Height));
            var VirtualWidth = Width / ScaleFactor;
            var VirtualHeight = Height / ScaleFactor;
            var Root = new Canvas();
            var tg = new TransformGroup();
            tg.Children.Add(new ScaleTransform(ScaleFactor, ScaleFactor));
            tg.Children.Add(new TranslateTransform(Width * 0.5, Height * 0.5));
            Root.RenderTransform = tg;
            Root.SizeChanged += (sender, eArgs) => { Root.RenderTransform = new TranslateTransform(eArgs.NewSize.Width * 0.5, eArgs.NewSize.Height * 0.5); };
            Root.SetValue(Canvas.LeftProperty, 0.0);
            Root.SetValue(Canvas.TopProperty, 0.0);
            c.Children.Add(Root);
            var nl = InitNode(ksm.Content, VirtualWidth, VirtualHeight);
            foreach (var n in nl)
            {
                Root.Children.Add(n);
            }
            c.InvalidateVisual();
        }

        private static FrameworkElement[] InitNode(UISchema.Control c, double ParentWidth, double ParentHeight)
        {
            if (c.OnGrid)
            {
                var na = c.Grid;
                var nc = new Canvas();
                var n = new Rectangle { Stroke = Brushes.DodgerBlue, StrokeThickness = 3 };
                var ActualSize = SetChildLayout(c, nc, na.HorizontalAlignment, na.VerticalAlignment, na.Margin, na.Width, na.Height, ParentWidth, ParentHeight);
                n.Width = ActualSize.Width;
                n.Height = ActualSize.Height;
                n.SetValue(Canvas.LeftProperty, 0.0);
                n.SetValue(Canvas.TopProperty, 0.0);
                n.RenderTransform = new TranslateTransform(-n.RenderSize.Width * 0.5, -n.RenderSize.Height * 0.5);
                n.SizeChanged += (sender, eArgs) => { n.RenderTransform = new TranslateTransform(-eArgs.NewSize.Width * 0.5, -eArgs.NewSize.Height * 0.5); };
                nc.Children.Add(n);
                foreach (var Child in na.Content)
                {
                    var cl = InitNode(Child, ActualSize.Width, ActualSize.Height);
                    foreach (var cc in cl)
                    {
                        nc.Children.Add(cc);
                    }
                }
                return new FrameworkElement[] { nc };
            }
            else if (c.OnButton)
            {
                var na = c.Button;
                var n1 = new Rectangle { Stroke = Brushes.DodgerBlue, StrokeThickness = 2 };
                SetChildLayout(c, n1, na.HorizontalAlignment, na.VerticalAlignment, na.Margin, na.Width, na.Height, ParentWidth, ParentHeight);
                var n2 = new System.Windows.Controls.Label { Content = na.Content };
                n2.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center;
                n2.VerticalContentAlignment = System.Windows.VerticalAlignment.Center;
                if (na.FontFamily != null)
                {
                    n2.FontFamily = new FontFamily(na.FontFamily.HasValue);
                }
                if (na.FontSize != null)
                {
                    n2.FontSize = na.FontSize.HasValue;
                }
                SetChildLayout(c, n2, na.HorizontalAlignment, na.VerticalAlignment, na.Margin, na.Width, na.Height, ParentWidth, ParentHeight);
                return new FrameworkElement[] { n1, n2 };
            }
            else if (c.OnLabel)
            {
                var na = c.Label;
                var n = new System.Windows.Controls.Label { Content = na.Content };
                n.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center;
                n.VerticalContentAlignment = System.Windows.VerticalAlignment.Center;
                if (na.FontFamily != null)
                {
                    n.FontFamily = new FontFamily(na.FontFamily.HasValue);
                }
                if (na.FontSize != null)
                {
                    n.FontSize = na.FontSize.HasValue;
                }
                SetChildLayout(c, n, na.HorizontalAlignment, na.VerticalAlignment, na.Margin, na.Width, na.Height, ParentWidth, ParentHeight);
                return new FrameworkElement[] { n };
            }
            else if (c.OnImage)
            {
                var na = c.Image;
                var n = new Rectangle { Stroke = Brushes.DodgerBlue, StrokeThickness = 2 };
                SetChildLayout(c, n, na.HorizontalAlignment, na.VerticalAlignment, na.Margin, na.Width, na.Height, ParentWidth, ParentHeight);
                return new FrameworkElement[] { n };
            }
            else if (c.OnTextBox)
            {
                var na = c.TextBox;
                var n = new Rectangle { Stroke = Brushes.DodgerBlue, StrokeThickness = 2 };
                SetChildLayout(c, n, na.HorizontalAlignment, na.VerticalAlignment, na.Margin, na.Width, na.Height, ParentWidth, ParentHeight);
                return new FrameworkElement[] { n };
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static Size SetChildLayout(UISchema.Control c, FrameworkElement n, Optional<UISchema.HorizontalAlignment> HorizontalAlignment, Optional<UISchema.VerticalAlignment> VerticalAlignment, Optional<UISchema.Thickness> Margin, Optional<int> Width, Optional<int> Height, double ParentWidth, double ParentHeight)
        {
            double PreferredWidth = 0;
            double PreferredHeight = 0;
            if (Width != null)
            {
                PreferredWidth = Width.HasValue;
            }
            if (Height != null)
            {
                PreferredHeight = Height.HasValue;
            }

            if (Margin == null)
            {
                Margin = new UISchema.Thickness { };
            }
            if (HorizontalAlignment == null)
            {
                HorizontalAlignment = UISchema.HorizontalAlignment.Stretch;
            }
            if (VerticalAlignment == null)
            {
                VerticalAlignment = UISchema.VerticalAlignment.Stretch;
            }

            if (HorizontalAlignment == UISchema.HorizontalAlignment.Stretch)
            {
                PreferredWidth = ParentWidth - Margin.HasValue.Left - Margin.HasValue.Right;
                HorizontalAlignment = UISchema.HorizontalAlignment.Center;
            }
            if (VerticalAlignment == UISchema.VerticalAlignment.Stretch)
            {
                PreferredHeight = ParentHeight - Margin.HasValue.Top - Margin.HasValue.Bottom;
                VerticalAlignment = UISchema.VerticalAlignment.Center;
            }
            if (PreferredWidth != 0)
            {
                n.Width = PreferredWidth;
            }
            if (PreferredHeight != 0)
            {
                n.Height = PreferredHeight;
            }

            Action<double, double> SetPosition = (ActualWidth, ActualHeight) =>
            {
                if (HorizontalAlignment == UISchema.HorizontalAlignment.Left)
                {
                    var x = Margin.HasValue.Left + ActualWidth * 0.5;
                    n.SetValue(Canvas.LeftProperty, x - ParentWidth * 0.5);
                }
                else if (HorizontalAlignment == UISchema.HorizontalAlignment.Center)
                {
                    var w = ParentWidth - Margin.HasValue.Left - Margin.HasValue.Right;
                    var x = Margin.HasValue.Left + w * 0.5;
                    n.SetValue(Canvas.LeftProperty, x - ParentWidth * 0.5);
                }
                else if (HorizontalAlignment == UISchema.HorizontalAlignment.Right)
                {
                    var x = ParentWidth - Margin.HasValue.Right - ActualWidth * 0.5;
                    n.SetValue(Canvas.LeftProperty, x - ParentWidth * 0.5);
                }
                else
                {
                    throw new InvalidOperationException();
                }

                if (VerticalAlignment == UISchema.VerticalAlignment.Top)
                {
                    var y = Margin.HasValue.Top + ActualHeight * 0.5;
                    n.SetValue(Canvas.TopProperty, y - ParentHeight * 0.5);
                }
                else if (VerticalAlignment == UISchema.VerticalAlignment.Center)
                {
                    var h = ParentHeight - Margin.HasValue.Top - Margin.HasValue.Bottom;
                    var y = Margin.HasValue.Top + h * 0.5;
                    n.SetValue(Canvas.TopProperty, y - ParentHeight * 0.5);
                }
                else if (VerticalAlignment == UISchema.VerticalAlignment.Bottom)
                {
                    var y = ParentHeight - Margin.HasValue.Bottom - ActualHeight * 0.5;
                    n.SetValue(Canvas.TopProperty, y - ParentHeight * 0.5);
                }
                else
                {
                    throw new InvalidOperationException();
                }
            };

            SetPosition(PreferredWidth, PreferredHeight);

            if (!c.OnGrid)
            {
                n.RenderTransform = new TranslateTransform(-PreferredWidth * 0.5, -PreferredHeight * 0.5);
                n.SizeChanged += (sender, eArgs) =>
                {
                    n.RenderTransform = new TranslateTransform(-eArgs.NewSize.Width * 0.5, -eArgs.NewSize.Height * 0.5);
                    SetPosition(eArgs.NewSize.Width, eArgs.NewSize.Height);
                };
            }

            return new Size(PreferredWidth, PreferredHeight);
        }

        private void Menu_File_Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var x = (new XmlSerializer()).Write(c);
            try
            {
                TreeFile.WriteFile(ConfigurationFilePath, x);
            }
            catch
            {
            }
        }
    }
}