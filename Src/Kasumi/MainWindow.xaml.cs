using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

                    DrawCanvas(Canvas_Displayer, ksm, ksm.Width, ksm.Height, KasumiFilePath);

                    foreach (var p in ResolutionToCanvas)
                    {
                        DrawCanvas(p.Value, ksm, p.Key.Width, p.Key.Height, KasumiFilePath);
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

        private static void DrawCanvas(Canvas c, UISchema.Window ksm, double Width, double Height, String KasumiFilePath)
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
            var nl = InitNode(ksm.Content, VirtualWidth, VirtualHeight, KasumiFilePath);
            foreach (var n in nl)
            {
                Root.Children.Add(n);
            }
            c.InvalidateVisual();
        }

        private static Regex rSumImagePathExpr = new Regex(@"(?<FilePath>.*?)@(\[(?<Rectangle>\d+,\s*\d+,\s*\d+,\s*\d+)\])?\$(\[(?<Padding>\d+,\s*\d+,\s*\d+,\s*\d+)\])?", RegexOptions.ExplicitCapture);
        private static FrameworkElement GetImage(String ImagePathExpr, String KasumiFilePath, double BoxWidth, double BoxHeight)
        {
            var n = new Canvas();

            var SumImagePathExprs = ImagePathExpr.Split('+');
            foreach (var SumImagePathExpr in SumImagePathExprs)
            {
                var m = rSumImagePathExpr.Match(SumImagePathExpr);
                if (!m.Success)
                {
                    throw new InvalidOperationException("NotInvalidImagePath: {0}".Formats(SumImagePathExpr));
                }
                var FilePath = m.Result("${FilePath}");
                var RectangleExpr = m.Result("${Rectangle}");
                var PaddingExpr = m.Result("${Padding}");

                var Bitmap = BitmapFrame.Create(new Uri(FileNameHandling.GetAbsolutePath(FilePath, FileNameHandling.GetFileDirectory(KasumiFilePath))));
                Int32Rect Rect;
                if (RectangleExpr != "")
                {
                    var RectValues = RectangleExpr.Split(',').Select(v => int.Parse(v.Trim(' '))).ToArray();
                    Rect = new Int32Rect(RectValues[0], RectValues[1], RectValues[2], RectValues[3]);
                }
                else
                {
                    Rect = new Int32Rect(0, 0, Bitmap.PixelWidth, Bitmap.PixelHeight);
                }
                System.Windows.Thickness Padding;
                if (PaddingExpr != "")
                {
                    var PaddingValues = PaddingExpr.Split(',').Select(v => int.Parse(v.Trim(' '))).ToArray();
                    Padding = new System.Windows.Thickness(PaddingValues[0], PaddingValues[1], PaddingValues[2], PaddingValues[3]);
                }
                else
                {
                    Padding = new System.Windows.Thickness(0);
                }

                Func<double, double, double, double, Rect> CreateRelativeRect =
                    (x, y, w, h) => new Rect(x / (double)(Bitmap.PixelWidth), y / (double)(Bitmap.PixelHeight), w / (double)(Bitmap.PixelWidth), h / (double)(Bitmap.PixelHeight));

                Action<Rectangle, double, double> AddRectangle =
                    (r, x, y) =>
                    {
                        r.SetValue(Canvas.LeftProperty, x - BoxWidth * 0.5);
                        r.SetValue(Canvas.TopProperty, y - BoxHeight * 0.5);
                        n.Children.Add(r);
                    };

                var CenterWidth = Math.Max(Rect.Width - Padding.Left - Padding.Right, 0);
                var BoxCenterWidth = Math.Max(BoxWidth - Padding.Left - Padding.Right + 0.5, 0);
                var CenterHeight = Math.Max(Rect.Height - Padding.Top - Padding.Bottom, 0);
                var BoxCenterHeight = Math.Max(BoxHeight - Padding.Top - Padding.Bottom + 0.5, 0);
                AddRectangle
                (
                    new Rectangle
                    {
                        Fill = new ImageBrush
                        {
                            ImageSource = Bitmap,
                            Viewbox = CreateRelativeRect(Rect.X, Rect.Y, Padding.Left, Padding.Top)
                        },
                        Width = Padding.Left,
                        Height = Padding.Top
                    },
                    0,
                    0
                );
                AddRectangle
                (
                    new Rectangle
                    {
                        Fill = new ImageBrush
                        {
                            ImageSource = Bitmap,
                            Viewbox = CreateRelativeRect(Rect.X + Padding.Left, Rect.Y, CenterWidth, Padding.Top)
                        },
                        Width = BoxCenterWidth,
                        Height = Padding.Top
                    },
                    Padding.Left,
                    0
                );
                AddRectangle
                (
                    new Rectangle
                    {
                        Fill = new ImageBrush
                        {
                            ImageSource = Bitmap,
                            Viewbox = CreateRelativeRect(Rect.X + Rect.Width - Padding.Right, Rect.Y, Padding.Right, Padding.Top)
                        },
                        Width = Padding.Right,
                        Height = Padding.Top
                    },
                    BoxWidth - Padding.Right,
                    0
                );
                AddRectangle
                (
                    new Rectangle
                    {
                        Fill = new ImageBrush
                        {
                            ImageSource = Bitmap,
                            Viewbox = CreateRelativeRect(Rect.X, Rect.Y + Padding.Top, Padding.Left, CenterHeight)
                        },
                        Width = Padding.Left,
                        Height = BoxCenterHeight
                    },
                    0,
                    Padding.Top
                );
                AddRectangle
                (
                    new Rectangle
                    {
                        Fill = new ImageBrush
                        {
                            ImageSource = Bitmap,
                            Viewbox = CreateRelativeRect(Rect.X + Padding.Left, Rect.Y + Padding.Top, CenterWidth, CenterHeight)
                        },
                        Width = BoxCenterWidth,
                        Height = BoxCenterHeight
                    },
                    Padding.Left,
                    Padding.Top
                );
                AddRectangle
                (
                    new Rectangle
                    {
                        Fill = new ImageBrush
                        {
                            ImageSource = Bitmap,
                            Viewbox = CreateRelativeRect(Rect.X + Rect.Width - Padding.Right, Rect.Y + Padding.Top, Padding.Right, CenterHeight)
                        },
                        Width = Padding.Right,
                        Height = BoxCenterHeight
                    },
                    BoxWidth - Padding.Right,
                    Padding.Top
                );
                AddRectangle
                (
                    new Rectangle
                    {
                        Fill = new ImageBrush
                        {
                            ImageSource = Bitmap,
                            Viewbox = CreateRelativeRect(Rect.X, Rect.Y + Rect.Height - Padding.Bottom, Padding.Left, Padding.Bottom)
                        },
                        Width = Padding.Left,
                        Height = Padding.Bottom
                    },
                    0,
                    BoxHeight - Padding.Bottom
                );
                AddRectangle
                (
                    new Rectangle
                    {
                        Fill = new ImageBrush
                        {
                            ImageSource = Bitmap,
                            Viewbox = CreateRelativeRect(Rect.X + Padding.Left, Rect.Y + Rect.Height - Padding.Bottom, CenterWidth, Padding.Bottom)
                        },
                        Width = BoxCenterWidth,
                        Height = Padding.Bottom
                    },
                    Padding.Left,
                    BoxHeight - Padding.Bottom
                );
                AddRectangle
                (
                    new Rectangle
                    {
                        Fill = new ImageBrush
                        {
                            ImageSource = Bitmap,
                            Viewbox = CreateRelativeRect(Rect.X + Rect.Width - Padding.Right, Rect.Y + Rect.Height - Padding.Bottom, Padding.Right, Padding.Bottom)
                        },
                        Width = Padding.Right,
                        Height = Padding.Bottom
                    },
                    BoxWidth - Padding.Right,
                    BoxHeight - Padding.Bottom
                );
            }

            return n;
        }

        private static FrameworkElement[] InitNode(UISchema.Control c, double ParentWidth, double ParentHeight, String KasumiFilePath)
        {
            if (c.OnGrid)
            {
                var na = c.Grid;
                var nc = new Canvas();
                var n = new Rectangle { Stroke = Brushes.DodgerBlue, StrokeThickness = 3 };
                var ActualSize = SetChildLayout(nc, na.HorizontalAlignment, na.VerticalAlignment, na.Margin, na.Width, na.Height, ParentWidth, ParentHeight);
                n.Width = ActualSize.Width;
                n.Height = ActualSize.Height;
                n.SetValue(Canvas.LeftProperty, 0.0);
                n.SetValue(Canvas.TopProperty, 0.0);
                n.RenderTransform = new TranslateTransform(-n.RenderSize.Width * 0.5, -n.RenderSize.Height * 0.5);
                n.SizeChanged += (sender, eArgs) => { n.RenderTransform = new TranslateTransform(-eArgs.NewSize.Width * 0.5, -eArgs.NewSize.Height * 0.5); };
                nc.Children.Add(n);
                foreach (var Child in na.Content)
                {
                    var cl = InitNode(Child, ActualSize.Width, ActualSize.Height, KasumiFilePath);
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
                FrameworkElement n1 = new Rectangle { Stroke = Brushes.DodgerBlue, StrokeThickness = 2 };
                SetChildLayout(n1, na.HorizontalAlignment, na.VerticalAlignment, na.Margin, na.Width, na.Height, ParentWidth, ParentHeight);
                if (na.NormalImage != null)
                {
                    var ni = GetImage(na.NormalImage.HasValue, KasumiFilePath, n1.Width, n1.Height);
                    ni.SetValue(Canvas.LeftProperty, n1.GetValue(Canvas.LeftProperty));
                    ni.SetValue(Canvas.TopProperty, n1.GetValue(Canvas.TopProperty));
                    n1 = ni;
                }
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
                if (na.FontColor != null)
                {
                    var ARGB = na.FontColor.HasValue.Value;
                    var Color = System.Windows.Media.Color.FromArgb((Byte)(ARGB.Bits(31, 24)), (Byte)(ARGB.Bits(23, 16)), (Byte)(ARGB.Bits(15, 8)), (Byte)(ARGB.Bits(7, 0)));
                    n2.Foreground = new SolidColorBrush(Color);
                }
                else
                {
                    n2.Foreground = Brushes.Black;
                }
                SetChildLayout(n2, na.HorizontalAlignment, na.VerticalAlignment, na.Margin, na.Width, na.Height, ParentWidth, ParentHeight);
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
                if (na.FontColor != null)
                {
                    var ARGB = na.FontColor.HasValue.Value;
                    var Color = System.Windows.Media.Color.FromArgb((Byte)(ARGB.Bits(31, 24)), (Byte)(ARGB.Bits(23, 16)), (Byte)(ARGB.Bits(15, 8)), (Byte)(ARGB.Bits(7, 0)));
                    n.Foreground = new SolidColorBrush(Color);
                }
                else
                {
                    n.Foreground = Brushes.Black;
                }
                SetChildLayout(n, na.HorizontalAlignment, na.VerticalAlignment, na.Margin, na.Width, na.Height, ParentWidth, ParentHeight);
                return new FrameworkElement[] { n };
            }
            else if (c.OnImage)
            {
                var na = c.Image;
                FrameworkElement n = new Rectangle { Stroke = Brushes.DodgerBlue, StrokeThickness = 2 };
                SetChildLayout(n, na.HorizontalAlignment, na.VerticalAlignment, na.Margin, na.Width, na.Height, ParentWidth, ParentHeight);
                var ni = GetImage(na.Content, KasumiFilePath, n.Width, n.Height);
                ni.SetValue(Canvas.LeftProperty, n.GetValue(Canvas.LeftProperty));
                ni.SetValue(Canvas.TopProperty, n.GetValue(Canvas.TopProperty));
                n = ni;
                return new FrameworkElement[] { n };
            }
            else if (c.OnTextBox)
            {
                var na = c.TextBox;
                var n = new Rectangle { Stroke = Brushes.DodgerBlue, StrokeThickness = 2 };
                SetChildLayout(n, na.HorizontalAlignment, na.VerticalAlignment, na.Margin, na.Width, na.Height, ParentWidth, ParentHeight);
                return new FrameworkElement[] { n };
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static Size SetChildLayout(FrameworkElement n, Optional<UISchema.HorizontalAlignment> HorizontalAlignment, Optional<UISchema.VerticalAlignment> VerticalAlignment, Optional<UISchema.Thickness> Margin, Optional<int> Width, Optional<int> Height, double ParentWidth, double ParentHeight)
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

            if (!(n is Panel))
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
