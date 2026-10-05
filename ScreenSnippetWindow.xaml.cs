using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace BCCScreenShot
{
    public partial class ScreenSnippetWindow : Window
    {
        public BitmapSource? CapturedBitmap { get; private set; }

        private readonly BitmapSource _frozenBitmap;
        private bool _isSelecting;
        private Point _startPoint;

        public ScreenSnippetWindow(BitmapSource frozenBitmap)
        {
            InitializeComponent();
            _frozenBitmap = frozenBitmap;
            FrozenScreenImage.Source = _frozenBitmap;

            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            Loaded += (s, e) =>
            {
                FullMaskGeometry.Rect = new Rect(0, 0, ActualWidth, ActualHeight);
                Canvas.SetLeft(InfoBanner, Math.Max(20, (ActualWidth - 360) / 2));
            };
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(OverlayCanvas);
            _isSelecting = true;

            GuideLineH.Visibility = Visibility.Collapsed;
            GuideLineV.Visibility = Visibility.Collapsed;

            SelectionBorder.Visibility = Visibility.Visible;
            HandleTL.Visibility = Visibility.Visible;
            HandleTR.Visibility = Visibility.Visible;
            HandleBL.Visibility = Visibility.Visible;
            HandleBR.Visibility = Visibility.Visible;
            DimensionPill.Visibility = Visibility.Visible;

            UpdateSelection(0, 0, _startPoint.X, _startPoint.Y);
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            Point current = e.GetPosition(OverlayCanvas);

            if (!_isSelecting)
            {
                // Update Crosshair Guidelines
                GuideLineH.Visibility = Visibility.Visible;
                GuideLineV.Visibility = Visibility.Visible;

                GuideLineH.X1 = 0;
                GuideLineH.Y1 = current.Y;
                GuideLineH.X2 = ActualWidth;
                GuideLineH.Y2 = current.Y;

                GuideLineV.X1 = current.X;
                GuideLineV.Y1 = 0;
                GuideLineV.X2 = current.X;
                GuideLineV.Y2 = ActualHeight;
                return;
            }

            double left = Math.Min(_startPoint.X, current.X);
            double top = Math.Min(_startPoint.Y, current.Y);
            double w = Math.Abs(current.X - _startPoint.X);
            double h = Math.Abs(current.Y - _startPoint.Y);

            UpdateSelection(w, h, left, top);
        }

        private void UpdateSelection(double w, double h, double left, double top)
        {
            // Update Cutout Geometry
            CutoutGeometry.Rect = new Rect(left, top, w, h);

            // Update Selection Border
            Canvas.SetLeft(SelectionBorder, left);
            Canvas.SetTop(SelectionBorder, top);
            SelectionBorder.Width = w;
            SelectionBorder.Height = h;

            // Corner Handles
            Canvas.SetLeft(HandleTL, left - 4);
            Canvas.SetTop(HandleTL, top - 4);

            Canvas.SetLeft(HandleTR, left + w - 4);
            Canvas.SetTop(HandleTR, top - 4);

            Canvas.SetLeft(HandleBL, left - 4);
            Canvas.SetTop(HandleBL, top + h - 4);

            Canvas.SetLeft(HandleBR, left + w - 4);
            Canvas.SetTop(HandleBR, top + h - 4);

            // Calculate pixel dimensions
            double scaleX = (double)_frozenBitmap.PixelWidth / ActualWidth;
            double scaleY = (double)_frozenBitmap.PixelHeight / ActualHeight;
            int pxW = (int)Math.Round(w * scaleX);
            int pxH = (int)Math.Round(h * scaleY);

            TxtDimensions.Text = $"{pxW} × {pxH} px";

            // Position Dimension Pill neatly
            double pillTop = (top - 32 < 10) ? (top + h + 8) : (top - 32);
            Canvas.SetLeft(DimensionPill, Math.Max(10, Math.Min(ActualWidth - 110, left)));
            Canvas.SetTop(DimensionPill, pillTop);
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSelecting) return;
            _isSelecting = false;

            double left = Canvas.GetLeft(SelectionBorder);
            double top = Canvas.GetTop(SelectionBorder);
            double w = SelectionBorder.Width;
            double h = SelectionBorder.Height;

            double scaleX = (double)_frozenBitmap.PixelWidth / ActualWidth;
            double scaleY = (double)_frozenBitmap.PixelHeight / ActualHeight;

            int cropX = (int)Math.Round(left * scaleX);
            int cropY = (int)Math.Round(top * scaleY);
            int cropW = (int)Math.Round(w * scaleX);
            int cropH = (int)Math.Round(h * scaleY);

            // Clamp safely
            cropX = Math.Max(0, Math.Min(_frozenBitmap.PixelWidth - 1, cropX));
            cropY = Math.Max(0, Math.Min(_frozenBitmap.PixelHeight - 1, cropY));
            cropW = Math.Max(1, Math.Min(_frozenBitmap.PixelWidth - cropX, cropW));
            cropH = Math.Max(1, Math.Min(_frozenBitmap.PixelHeight - cropY, cropH));

            if (cropW >= 5 && cropH >= 5)
            {
                try
                {
                    var rect = new Int32Rect(cropX, cropY, cropW, cropH);
                    var cropped = new CroppedBitmap(_frozenBitmap, rect);
                    cropped.Freeze();
                    CapturedBitmap = cropped;
                }
                catch
                {
                    // Fallback full bitmap if crop failed
                    CapturedBitmap = _frozenBitmap;
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
