using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;

namespace BCCScreenShot
{
    public partial class ScreenSnippetWindow : Window
    {
        public BitmapSource? CapturedBitmap { get; private set; }

        private bool _isSelecting;
        private System.Windows.Point _startPoint;
        private Drawing.Bitmap? _fullScreenBmp;

        public ScreenSnippetWindow()
        {
            InitializeComponent();
            SetupFullScreenBounds();
            CaptureFullScreen();
        }

        private void SetupFullScreenBounds()
        {
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
        }

        private void CaptureFullScreen()
        {
            int w = (int)Width;
            int h = (int)Height;
            _fullScreenBmp = new Drawing.Bitmap(w, h);

            using (var g = Drawing.Graphics.FromImage(_fullScreenBmp))
            {
                g.CopyFromScreen((int)Left, (int)Top, 0, 0, new Drawing.Size(w, h));
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(OverlayCanvas);
            _isSelecting = true;
            SelectionRect.Visibility = Visibility.Visible;
            Canvas.SetLeft(SelectionRect, _startPoint.X);
            Canvas.SetTop(SelectionRect, _startPoint.Y);
            SelectionRect.Width = 0;
            SelectionRect.Height = 0;
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isSelecting) return;
            System.Windows.Point current = e.GetPosition(OverlayCanvas);

            double left = Math.Min(_startPoint.X, current.X);
            double top = Math.Min(_startPoint.Y, current.Y);
            double w = Math.Abs(current.X - _startPoint.X);
            double h = Math.Abs(current.Y - _startPoint.Y);

            Canvas.SetLeft(SelectionRect, left);
            Canvas.SetTop(SelectionRect, top);
            SelectionRect.Width = w;
            SelectionRect.Height = h;

            TxtDimensions.Text = $"{(int)w} x {(int)h} px";
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSelecting) return;
            _isSelecting = false;

            double left = Canvas.GetLeft(SelectionRect);
            double top = Canvas.GetTop(SelectionRect);
            double w = SelectionRect.Width;
            double h = SelectionRect.Height;

            if (w >= 10 && h >= 10 && _fullScreenBmp != null)
            {
                try
                {
                    var rect = new Drawing.Rectangle((int)left, (int)top, (int)w, (int)h);
                    using (var croppedBmp = _fullScreenBmp.Clone(rect, _fullScreenBmp.PixelFormat))
                    {
                        using (var ms = new MemoryStream())
                        {
                            croppedBmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                            ms.Position = 0;

                            var bi = new BitmapImage();
                            bi.BeginInit();
                            bi.CacheOption = BitmapCacheOption.OnLoad;
                            bi.StreamSource = ms;
                            bi.EndInit();
                            bi.Freeze();

                            CapturedBitmap = bi;
                        }
                    }
                }
                catch
                {
                    // Fallback full bitmap if crop out of bounds
                }
            }

            DialogResult = CapturedBitmap != null;
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }
    }
}
