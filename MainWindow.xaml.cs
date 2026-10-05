using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace BCCScreenShot
{
    public class ArrowData
    {
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }
    }

    public partial class MainWindow : Window
    {
        private string _currentTool = "select";
        private System.Windows.Media.Color _currentColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A62639");
        private System.Windows.Media.Color _currentCalloutBgColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E293B");
        private double _currentStrokeWidth = 4;
        private double _currentFontSize = 18;
        private int _currentStepNumber = 1;
        private double _zoomLevel = 1.0;

        private bool _isFillEnabled = false;
        private double _fillOpacity = 30; // 0-100%
        private bool _isSyncingInspector = false;

        private BitmapSource? _bgImage;
        private readonly List<UIElement> _annotations = new();
        private readonly Stack<List<UIElement>> _undoStack = new();
        private readonly Stack<List<UIElement>> _redoStack = new();

        private bool _isDrawing;
        private bool _isTwoStageActive;
        private bool _isDraggingElement;
        private System.Windows.Point _startPoint;
        private System.Windows.Point _dragOffset;
        private System.Windows.Point _dragLastPoint;
        private UIElement? _activePreviewElement;
        private Polyline? _activePolyline;
        private UIElement? _selectedElement;

        private string? _activeHandleName;

        public MainWindow()
        {
            InitializeComponent();
            UpdateScreenCaptureMenu();
            ConfigureHandleZIndices();
            Loaded += (s, e) => LoadDemoCanvas();
        }

        private void ConfigureHandleZIndices()
        {
            Panel.SetZIndex(SelectionBoxBorder, 99990);
            Panel.SetZIndex(HandleTL, 99999);
            Panel.SetZIndex(HandleTR, 99999);
            Panel.SetZIndex(HandleBL, 99999);
            Panel.SetZIndex(HandleBR, 99999);
            Panel.SetZIndex(HandleP1, 99999);
            Panel.SetZIndex(HandleP2, 99999);
        }

        private void UpdateScreenCaptureMenu()
        {
            var monitors = ScreenCaptureService.GetMonitors();
            var menu = new ContextMenu();

            foreach (var mon in monitors)
            {
                var item = new MenuItem
                {
                    Header = $"🖥️ {mon.FriendlyName} ({mon.Bounds.Width} × {mon.Bounds.Height} px)"
                };
                var currentMon = mon;
                item.Click += (s, e) => CaptureSelectedMonitor(currentMon);
                menu.Items.Add(item);
            }

            if (monitors.Count > 1)
            {
                menu.Items.Add(new Separator());
                var allItem = new MenuItem
                {
                    Header = "🌐 Все экраны (Полный рабочий стол)"
                };
                allItem.Click += (s, e) => CaptureVirtualScreen();
                menu.Items.Add(allItem);
            }

            BtnCaptureScreen.ContextMenu = menu;
        }

        private void LoadDemoCanvas()
        {
            int w = 1200, h = 720;
            var drawingVisual = new DrawingVisual();
            using (var dc = drawingVisual.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42)), null, new Rect(0, 0, w, h));
                dc.DrawRectangle(new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59)), null, new Rect(0, 0, w, 60));
                dc.DrawText(
                    new FormattedText("Аналитический Отчет компании — Рабочий Стол",
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                        18, Brushes.White, 1.25),
                    new System.Windows.Point(24, 18));

                // Cards
                var cardBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59));
                dc.DrawRoundedRectangle(cardBg, null, new Rect(40, 100, 340, 120), 8, 8);
                dc.DrawText(new FormattedText("Выручка за месяц", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 13, Brushes.Gray, 1.25), new System.Windows.Point(60, 120));
                dc.DrawText(new FormattedText("2 450 000 ₽", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), 26, Brushes.LightGreen, 1.25), new System.Windows.Point(60, 155));

                dc.DrawRoundedRectangle(cardBg, null, new Rect(420, 100, 340, 120), 8, 8);
                dc.DrawText(new FormattedText("Новые пользователи", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 13, Brushes.Gray, 1.25), new System.Windows.Point(440, 120));
                dc.DrawText(new FormattedText("+184 аккаунта", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), 26, Brushes.Cyan, 1.25), new System.Windows.Point(440, 155));

                dc.DrawRoundedRectangle(cardBg, null, new Rect(800, 100, 340, 120), 8, 8);
                dc.DrawText(new FormattedText("Конверсия в оплату", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 13, Brushes.Gray, 1.25), new System.Windows.Point(820, 120));
                dc.DrawText(new FormattedText("94.2%", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), 26, Brushes.MediumPurple, 1.25), new System.Windows.Point(820, 155));
            }

            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(drawingVisual);
            SetBackgroundImage(rtb);

            AddDefaultDemoAnnotations();
        }

        private void SetBackgroundImage(BitmapSource imgSource)
        {
            _bgImage = imgSource;
            DrawingCanvas.Width = imgSource.PixelWidth;
            DrawingCanvas.Height = imgSource.PixelHeight;
            DrawingCanvas.Background = new ImageBrush(imgSource);
            TxtCanvasSize.Text = $"{imgSource.PixelWidth} × {imgSource.PixelHeight} px";

            FitZoomToViewport();
        }

        private void ResetCanvasAnnotations()
        {
            DrawingCanvas.Children.Clear();
            DrawingCanvas.Children.Add(SelectionBoxBorder);
            DrawingCanvas.Children.Add(HandleTL);
            DrawingCanvas.Children.Add(HandleTR);
            DrawingCanvas.Children.Add(HandleBL);
            DrawingCanvas.Children.Add(HandleBR);
            DrawingCanvas.Children.Add(HandleP1);
            DrawingCanvas.Children.Add(HandleP2);

            _annotations.Clear();
            _undoStack.Clear();
            _currentStepNumber = 1;
            UpdateNextStepUI();
            ClearSelection();
        }

        private void AddDefaultDemoAnnotations()
        {
            ResetCanvasAnnotations();

            // Demo Arrow
            var arrow = CreateArrow(800, 280, 620, 170, _currentColor, 4);
            AddAnnotation(arrow);

            // Demo Rect with Shaded Fill
            var fillBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, _currentColor.R, _currentColor.G, _currentColor.B));
            var rect = new Rectangle
            {
                Width = 340, Height = 120,
                Stroke = new SolidColorBrush(_currentColor),
                StrokeThickness = 3,
                Fill = fillBrush,
                Cursor = Cursors.SizeAll
            };
            Canvas.SetLeft(rect, 800);
            Canvas.SetTop(rect, 100);
            AddAnnotation(rect);

            // Demo Callout with pointer and background
            var callout = CreateCalloutElement(800, 100, 820, 260, _currentColor, "Проверьте конверсию здесь!\nЦвет и размер теперь можно редактировать!", 15, _currentCalloutBgColor);
            AddAnnotation(callout);

            // Demo Step Badge
            var step = CreateStepBadge(780, 100, _currentColor, _currentStepNumber++);
            UpdateNextStepUI();
            AddAnnotation(step);

            // Select the rectangle by default
            SelectElement(rect);
        }

        private async Task PrepareForCaptureAsync()
        {
            Opacity = 0;
            Hide();
            // Process any pending render/layout messages so the OS updates the window state
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Render);
            // Allow Windows DWM to complete any window hide/fade animation cleanly
            await Task.Delay(250);
        }

        private void RestoreAfterCapture()
        {
            Show();
            Activate();
            Opacity = 1;
        }

        // Screen Capture Region Snippet Window
        private async void BtnCaptureArea_Click(object sender, RoutedEventArgs e)
        {
            await PrepareForCaptureAsync();

            using var bmp = ScreenCaptureService.CaptureVirtualScreen();
            var bs = ScreenCaptureService.BitmapToBitmapSource(bmp);

            var snippetWin = new ScreenSnippetWindow(bs);
            bool? result = snippetWin.ShowDialog();

            RestoreAfterCapture();

            if (result == true && snippetWin.CapturedBitmap != null)
            {
                SetBackgroundImage(snippetWin.CapturedBitmap);
                ResetCanvasAnnotations();
                TxtStatus.Text = $"Выделенная область ({snippetWin.CapturedBitmap.PixelWidth} × {snippetWin.CapturedBitmap.PixelHeight} px) загружена на холст!";
            }
        }

        // Screen Capture Button
        private void BtnCaptureScreen_Click(object sender, RoutedEventArgs e)
        {
            var monitors = ScreenCaptureService.GetMonitors();
            if (monitors.Count == 1)
            {
                CaptureSelectedMonitor(monitors[0]);
            }
            else
            {
                UpdateScreenCaptureMenu();
                if (BtnCaptureScreen.ContextMenu != null)
                {
                    BtnCaptureScreen.ContextMenu.PlacementTarget = BtnCaptureScreen;
                    BtnCaptureScreen.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                    BtnCaptureScreen.ContextMenu.IsOpen = true;
                }
            }
        }

        private async void CaptureSelectedMonitor(MonitorItem mon)
        {
            await PrepareForCaptureAsync();

            using var bmp = ScreenCaptureService.CaptureMonitor(mon);
            var bs = ScreenCaptureService.BitmapToBitmapSource(bmp);

            RestoreAfterCapture();

            SetBackgroundImage(bs);
            ResetCanvasAnnotations();
            TxtStatus.Text = $"Снимок дисплея «{mon.FriendlyName}» ({bs.PixelWidth} × {bs.PixelHeight} px) загружен на холст!";
        }

        private async void CaptureVirtualScreen()
        {
            await PrepareForCaptureAsync();

            using var bmp = ScreenCaptureService.CaptureVirtualScreen();
            var bs = ScreenCaptureService.BitmapToBitmapSource(bmp);

            RestoreAfterCapture();

            SetBackgroundImage(bs);
            ResetCanvasAnnotations();
            TxtStatus.Text = $"Снимок всех экранов ({bs.PixelWidth} × {bs.PixelHeight} px) загружен на холст!";
        }

        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp" };
            if (dlg.ShowDialog() == true)
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(dlg.FileName);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                bi.Freeze();

                SetBackgroundImage(bi);
                ResetCanvasAnnotations();
                TxtStatus.Text = $"Файл загружен: {System.IO.Path.GetFileName(dlg.FileName)} ({bi.PixelWidth} × {bi.PixelHeight} px)";
            }
        }

        private void BtnDemo_Click(object sender, RoutedEventArgs e)
        {
            LoadDemoCanvas();
            TxtStatus.Text = "Демо-холст загружен.";
        }

        // Tools Selection
        private void Tool_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag != null)
            {
                CancelActiveDrawing();
                _currentTool = rb.Tag.ToString()!;
                if (_currentTool != "select")
                {
                    ClearSelection();
                }

                switch (_currentTool)
                {
                    case "select": TxtStatus.Text = "Режим выбора (Кликните по фигуре, Drag для перемещения, маркеры для изменения размера)."; break;
                    case "arrow": TxtStatus.Text = "Инструмент: Стрелка (Кликните и потяните)."; break;
                    case "callout": TxtStatus.Text = "Инструмент: Выноска (Кликните на цель, затем место для текста)."; break;
                    case "step": TxtStatus.Text = "Инструмент: Порядковая метка (Кликните для размещения круга с номером)."; break;
                    case "rect": TxtStatus.Text = "Инструмент: Рамка / Прямоугольник (Кликните и потяните)."; break;
                    case "ellipse": TxtStatus.Text = "Инструмент: Овал / Круг (Кликните и потяните)."; break;
                    case "line": TxtStatus.Text = "Инструмент: Прямая линия (Кликните и потяните)."; break;
                    case "text": TxtStatus.Text = "Инструмент: Текстовый блок (Кликните для добавления текста)."; break;
                    case "pencil": TxtStatus.Text = "Инструмент: Карандаш (Зажмите ЛКМ и рисуйте)."; break;
                    case "highlighter": TxtStatus.Text = "Инструмент: Маркер-выделитель (Зажмите ЛКМ и выделяйте)."; break;
                }
            }
        }

        // Color Swatches
        private void ColorSwatch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex)
            {
                _currentColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
                ApplyStyleToSelectedElement();
                TxtStatus.Text = $"Цвет изменен: {hex}";
            }
        }

        // Callout Background Swatches & Presets
        private void CalloutBgPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                if (tag == "accent")
                {
                    _currentCalloutBgColor = System.Windows.Media.Color.FromArgb(200, _currentColor.R, _currentColor.G, _currentColor.B);
                }
                else if (tag == "transparent")
                {
                    _currentCalloutBgColor = Colors.Transparent;
                }
                else
                {
                    _currentCalloutBgColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(tag);
                }

                ApplyCalloutBackgroundToSelected();
                TxtStatus.Text = $"Фон выноски установлен: {btn.Content}";
            }
        }

        private void CalloutBgColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex)
            {
                _currentCalloutBgColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
                ApplyCalloutBackgroundToSelected();
                TxtStatus.Text = $"Фон выноски выбран: {hex}";
            }
        }

        private void ApplyCalloutBackgroundToSelected()
        {
            if (_selectedElement is Canvas calloutCanvas && calloutCanvas.Children.Count >= 3)
            {
                if (calloutCanvas.Children[2] is Border card)
                {
                    card.Background = new SolidColorBrush(_currentCalloutBgColor);
                    card.Tag = _currentCalloutBgColor;
                    if (card.Child is TextBlock cardTb)
                    {
                        var strokeColor = (card.BorderBrush as SolidColorBrush)?.Color ?? _currentColor;
                        cardTb.Foreground = GetContrastTextBrush(_currentCalloutBgColor, strokeColor);
                    }
                }
            }
        }

        private Brush GetContrastTextBrush(System.Windows.Media.Color bgColor, System.Windows.Media.Color strokeColor)
        {
            if (bgColor.A < 30)
            {
                return new SolidColorBrush(strokeColor);
            }
            double luminance = (0.299 * bgColor.R + 0.587 * bgColor.G + 0.114 * bgColor.B) / 255.0;
            if (luminance > 0.6)
            {
                return new SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42));
            }
            return Brushes.White;
        }

        // Fill & Shading
        private void ChkEnableFill_Changed(object sender, RoutedEventArgs e)
        {
            if (_isSyncingInspector) return;
            _isFillEnabled = ChkEnableFill.IsChecked == true;
            ApplyStyleToSelectedElement();
        }

        private void SliderFillOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _fillOpacity = e.NewValue;
            if (TxtFillOpacityVal != null) TxtFillOpacityVal.Text = $"{(int)_fillOpacity}%";
            if (_isSyncingInspector) return;
            ApplyStyleToSelectedElement();
        }

        private void SliderStroke_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _currentStrokeWidth = e.NewValue;
            if (TxtStrokeVal != null) TxtStrokeVal.Text = $"{(int)_currentStrokeWidth} px";
            if (_isSyncingInspector) return;
            ApplyStyleToSelectedElement();
        }

        private void SliderFont_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _currentFontSize = e.NewValue;
            if (TxtFontVal != null) TxtFontVal.Text = $"{(int)_currentFontSize} px";
            if (_isSyncingInspector) return;
            ApplyStyleToSelectedElement();
        }

        private void TxtNextStepVal_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(TxtNextStepVal.Text, out int nextStep) && nextStep > 0)
            {
                _currentStepNumber = nextStep;
            }
        }

        private void UpdateNextStepUI()
        {
            if (TxtNextStepVal != null)
            {
                TxtNextStepVal.Text = _currentStepNumber.ToString();
            }
        }

        private Brush? GetCurrentFillBrush()
        {
            if (!_isFillEnabled) return null;
            byte alpha = (byte)(_fillOpacity * 2.55);
            return new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, _currentColor.R, _currentColor.G, _currentColor.B));
        }

        private void ApplyStyleToSelectedElement()
        {
            if (_isSyncingInspector || _selectedElement == null) return;

            if (_selectedElement is Canvas calloutCanvas && calloutCanvas.Children.Count >= 3)
            {
                if (calloutCanvas.Children[0] is Line line)
                {
                    line.Stroke = new SolidColorBrush(_currentColor);
                    line.StrokeThickness = Math.Max(2, _currentStrokeWidth / 2.0);
                }
                if (calloutCanvas.Children[1] is Ellipse dot)
                {
                    dot.Fill = new SolidColorBrush(_currentColor);
                }
                if (calloutCanvas.Children[2] is Border card)
                {
                    card.BorderBrush = new SolidColorBrush(_currentColor);
                    card.Background = new SolidColorBrush(_currentCalloutBgColor);
                    card.Tag = _currentCalloutBgColor;
                    if (card.Child is TextBlock cardTb)
                    {
                        cardTb.FontSize = _currentFontSize;
                        cardTb.Foreground = GetContrastTextBrush(_currentCalloutBgColor, _currentColor);
                    }
                }
                UpdateSelectionHighlight(_selectedElement);
            }
            else if (_selectedElement is System.Windows.Shapes.Path path && path.Tag is ArrowData arrowData)
            {
                path.Stroke = new SolidColorBrush(_currentColor);
                path.StrokeThickness = _currentStrokeWidth;
                path.Data = BuildArrowGeometry(arrowData.X1, arrowData.Y1, arrowData.X2, arrowData.Y2, _currentStrokeWidth);
                UpdateSelectionHighlight(_selectedElement);
            }
            else if (_selectedElement is Line ln)
            {
                ln.Stroke = new SolidColorBrush(_currentColor);
                ln.StrokeThickness = _currentStrokeWidth;
                UpdateSelectionHighlight(_selectedElement);
            }
            else if (_selectedElement is Border b && b.Child is TextBlock tb)
            {
                tb.Foreground = new SolidColorBrush(_currentColor);
                tb.FontSize = _currentFontSize;
                b.BorderBrush = new SolidColorBrush(_currentColor);
                UpdateSelectionHighlight(_selectedElement);
            }
            else if (_selectedElement is Shape shape)
            {
                shape.Stroke = new SolidColorBrush(_currentColor);
                shape.StrokeThickness = _currentStrokeWidth;
                if (shape is Rectangle || shape is Ellipse)
                {
                    shape.Fill = GetCurrentFillBrush();
                }
                UpdateSelectionHighlight(_selectedElement);
            }
            else if (_selectedElement is Grid grid && grid.Children.Count >= 2)
            {
                if (grid.Children[0] is Ellipse ellipse)
                {
                    ellipse.Fill = new SolidColorBrush(_currentColor);
                }
                if (grid.Children[1] is TextBlock stepTb)
                {
                    stepTb.FontSize = _currentFontSize;
                }
                UpdateSelectionHighlight(_selectedElement);
            }
            else if (_selectedElement is Polyline poly)
            {
                poly.Stroke = new SolidColorBrush(_currentColor);
                poly.StrokeThickness = poly.Opacity < 0.9 ? _currentStrokeWidth * 3.5 : _currentStrokeWidth;
                UpdateSelectionHighlight(_selectedElement);
            }
        }

        private void SyncInspectorWithElement(UIElement elem)
        {
            _isSyncingInspector = true;
            try
            {
                if (elem is Canvas calloutCanvas && calloutCanvas.Children.Count >= 3)
                {
                    if (calloutCanvas.Children[0] is Line line && line.Stroke is SolidColorBrush sb)
                    {
                        _currentColor = sb.Color;
                        _currentStrokeWidth = Math.Max(1, line.StrokeThickness * 2.0);
                        SliderStroke.Value = _currentStrokeWidth;
                    }
                    if (calloutCanvas.Children[2] is Border card)
                    {
                        if (card.Background is SolidColorBrush bgBrush)
                        {
                            _currentCalloutBgColor = bgBrush.Color;
                        }
                        if (card.Child is TextBlock tb)
                        {
                            _currentFontSize = tb.FontSize;
                            SliderFont.Value = _currentFontSize;
                        }
                    }
                }
                else if (elem is System.Windows.Shapes.Path path)
                {
                    if (path.Stroke is SolidColorBrush sb)
                    {
                        _currentColor = sb.Color;
                    }
                    _currentStrokeWidth = path.StrokeThickness;
                    SliderStroke.Value = _currentStrokeWidth;
                }
                else if (elem is Line ln)
                {
                    if (ln.Stroke is SolidColorBrush sb)
                    {
                        _currentColor = sb.Color;
                    }
                    _currentStrokeWidth = ln.StrokeThickness;
                    SliderStroke.Value = _currentStrokeWidth;
                }
                else if (elem is Shape shape)
                {
                    if (shape.Stroke is SolidColorBrush sb)
                    {
                        _currentColor = sb.Color;
                    }
                    _currentStrokeWidth = shape.StrokeThickness;
                    SliderStroke.Value = _currentStrokeWidth;

                    if (shape.Fill is SolidColorBrush fillBrush)
                    {
                        ChkEnableFill.IsChecked = true;
                        _isFillEnabled = true;
                        _fillOpacity = Math.Round(fillBrush.Color.A / 2.55);
                        SliderFillOpacity.Value = _fillOpacity;
                    }
                    else
                    {
                        ChkEnableFill.IsChecked = false;
                        _isFillEnabled = false;
                    }
                }
                else if (elem is Border border && border.Child is TextBlock tb)
                {
                    if (tb.Foreground is SolidColorBrush sb)
                    {
                        _currentColor = sb.Color;
                    }
                    _currentFontSize = tb.FontSize;
                    SliderFont.Value = _currentFontSize;
                }
                else if (elem is Grid grid && grid.Children.Count >= 2)
                {
                    if (grid.Children[0] is Ellipse el && el.Fill is SolidColorBrush sb)
                    {
                        _currentColor = sb.Color;
                    }
                    if (grid.Children[1] is TextBlock stepTb)
                    {
                        _currentFontSize = stepTb.FontSize;
                        SliderFont.Value = _currentFontSize;
                    }
                }
            }
            finally
            {
                _isSyncingInspector = false;
            }
        }

        // Canvas Zooming
        private void CanvasScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                if (e.Delta > 0)
                {
                    _zoomLevel = Math.Min(4.0, _zoomLevel + 0.1);
                }
                else
                {
                    _zoomLevel = Math.Max(0.2, _zoomLevel - 0.1);
                }

                ApplyZoomLevel();
            }
        }

        private void FitZoomToViewport()
        {
            if (_bgImage == null) return;

            double availableW = CanvasScrollViewer.ActualWidth > 150 ? CanvasScrollViewer.ActualWidth - 60 : 800;
            double availableH = CanvasScrollViewer.ActualHeight > 150 ? CanvasScrollViewer.ActualHeight - 60 : 500;

            if (DrawingCanvas.Width > availableW || DrawingCanvas.Height > availableH)
            {
                double scaleX = availableW / DrawingCanvas.Width;
                double scaleY = availableH / DrawingCanvas.Height;
                _zoomLevel = Math.Round(Math.Min(scaleX, scaleY), 2);
                if (_zoomLevel < 0.1) _zoomLevel = 0.1;
                if (_zoomLevel > 1.0) _zoomLevel = 1.0;
            }
            else
            {
                _zoomLevel = 1.0;
            }

            ApplyZoomLevel();
        }

        private void ApplyZoomLevel()
        {
            CanvasScaleTransform.ScaleX = _zoomLevel;
            CanvasScaleTransform.ScaleY = _zoomLevel;
            BtnResetZoom.Content = $"🔍 {(int)(_zoomLevel * 100)}%";
            TxtStatus.Text = $"Масштаб: {(int)(_zoomLevel * 100)}% (Ctrl + 0 — 100%)";
        }

        private void BtnFitZoom_Click(object sender, RoutedEventArgs e)
        {
            FitZoomToViewport();
        }

        private void BtnResetZoom_Click(object sender, RoutedEventArgs e)
        {
            ResetZoom();
        }

        private void ResetZoom()
        {
            _zoomLevel = 1.0;
            CanvasScaleTransform.ScaleX = 1.0;
            CanvasScaleTransform.ScaleY = 1.0;
            BtnResetZoom.Content = "🔍 100%";
            TxtStatus.Text = "Масштаб сброшен на 100%.";
        }

        // Selection & Handle Management
        private void SelectElement(UIElement? elem)
        {
            _selectedElement = elem;
            UpdateSelectionHighlight(elem);

            if (elem != null)
            {
                SyncInspectorWithElement(elem);
                TxtStatus.Text = "Элемент выделен. Измените цвет, толщину или потяните за маркеры для изменения размера.";
            }
        }

        private void ClearSelection()
        {
            _selectedElement = null;
            HideAllHandles();
        }

        private void HideAllHandles()
        {
            SelectionBoxBorder.Visibility = Visibility.Collapsed;
            HandleTL.Visibility = Visibility.Collapsed;
            HandleTR.Visibility = Visibility.Collapsed;
            HandleBL.Visibility = Visibility.Collapsed;
            HandleBR.Visibility = Visibility.Collapsed;
            HandleP1.Visibility = Visibility.Collapsed;
            HandleP2.Visibility = Visibility.Collapsed;
        }

        private void UpdateSelectionHighlight(UIElement? elem)
        {
            if (elem == null || elem == DrawingCanvas || elem == SelectionBoxBorder)
            {
                HideAllHandles();
                return;
            }

            // Case A: Line
            if (elem is Line line)
            {
                SelectionBoxBorder.Visibility = Visibility.Collapsed;
                HandleTL.Visibility = Visibility.Collapsed;
                HandleTR.Visibility = Visibility.Collapsed;
                HandleBL.Visibility = Visibility.Collapsed;
                HandleBR.Visibility = Visibility.Collapsed;

                Canvas.SetLeft(HandleP1, line.X1 - 6);
                Canvas.SetTop(HandleP1, line.Y1 - 6);
                HandleP1.Visibility = Visibility.Visible;

                Canvas.SetLeft(HandleP2, line.X2 - 6);
                Canvas.SetTop(HandleP2, line.Y2 - 6);
                HandleP2.Visibility = Visibility.Visible;
                return;
            }

            // Case B: Arrow
            if (elem is System.Windows.Shapes.Path path && path.Tag is ArrowData arrowData)
            {
                SelectionBoxBorder.Visibility = Visibility.Collapsed;
                HandleTL.Visibility = Visibility.Collapsed;
                HandleTR.Visibility = Visibility.Collapsed;
                HandleBL.Visibility = Visibility.Collapsed;
                HandleBR.Visibility = Visibility.Collapsed;

                Canvas.SetLeft(HandleP1, arrowData.X1 - 6);
                Canvas.SetTop(HandleP1, arrowData.Y1 - 6);
                HandleP1.Visibility = Visibility.Visible;

                Canvas.SetLeft(HandleP2, arrowData.X2 - 6);
                Canvas.SetTop(HandleP2, arrowData.Y2 - 6);
                HandleP2.Visibility = Visibility.Visible;
                return;
            }

            // Case C: Box-based elements (Rectangle, Ellipse, Border Text, Step Grid, Callout)
            HandleP1.Visibility = Visibility.Collapsed;
            HandleP2.Visibility = Visibility.Collapsed;

            double left = Canvas.GetLeft(elem);
            double top = Canvas.GetTop(elem);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;

            double w = 0, h = 0;
            if (elem is FrameworkElement fe)
            {
                w = fe.Width;
                h = fe.Height;
                if (double.IsNaN(w) || w <= 0) w = fe.ActualWidth;
                if (double.IsNaN(h) || h <= 0) h = fe.ActualHeight;
            }

            if (w <= 0 || h <= 0)
            {
                var bounds = VisualTreeHelper.GetDescendantBounds(elem);
                w = Math.Max(10, bounds.Width);
                h = Math.Max(10, bounds.Height);
            }

            Canvas.SetLeft(SelectionBoxBorder, left - 4);
            Canvas.SetTop(SelectionBoxBorder, top - 4);
            SelectionBoxBorder.Width = w + 8;
            SelectionBoxBorder.Height = h + 8;
            SelectionBoxBorder.Visibility = Visibility.Visible;

            // Show corner handles for editable resizable shapes
            if (elem is Rectangle || elem is Ellipse || (elem is Border border && !(border.Parent is Canvas)))
            {
                Canvas.SetLeft(HandleTL, left - 5);
                Canvas.SetTop(HandleTL, top - 5);
                HandleTL.Visibility = Visibility.Visible;

                Canvas.SetLeft(HandleTR, left + w - 5);
                Canvas.SetTop(HandleTR, top - 5);
                HandleTR.Visibility = Visibility.Visible;

                Canvas.SetLeft(HandleBL, left - 5);
                Canvas.SetTop(HandleBL, top + h - 5);
                HandleBL.Visibility = Visibility.Visible;

                Canvas.SetLeft(HandleBR, left + w - 5);
                Canvas.SetTop(HandleBR, top + h - 5);
                HandleBR.Visibility = Visibility.Visible;
            }
            else
            {
                HandleTL.Visibility = Visibility.Collapsed;
                HandleTR.Visibility = Visibility.Collapsed;
                HandleBL.Visibility = Visibility.Collapsed;
                HandleBR.Visibility = Visibility.Collapsed;
            }
        }

        // Handle Mouse Events for Resizing
        private void Handle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe)
            {
                e.Handled = true;
                _activeHandleName = fe.Name;
                fe.CaptureMouse();
                TxtStatus.Text = "Изменение размера / положения аннотации...";
            }
        }

        private void Handle_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_activeHandleName != null)
            {
                if (sender is FrameworkElement fe)
                {
                    fe.ReleaseMouseCapture();
                }
                _activeHandleName = null;
                UpdateSelectionHighlight(_selectedElement);
                TxtStatus.Text = "Размер аннотации изменен.";
            }
        }

        private void DeleteSelectedElement()
        {
            if (_selectedElement != null)
            {
                _undoStack.Push(new List<UIElement>(_annotations));
                _redoStack.Clear();
                DrawingCanvas.Children.Remove(_selectedElement);
                _annotations.Remove(_selectedElement);
                ClearSelection();
                TxtStatus.Text = "Выделенный элемент удален.";
            }
        }

        private void BtnDeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedElement();
        }

        private void CancelActiveDrawing()
        {
            _isTwoStageActive = false;
            _isDrawing = false;
            if (_activePreviewElement != null)
            {
                DrawingCanvas.Children.Remove(_activePreviewElement);
                _activePreviewElement = null;
            }
            if (_activePolyline != null)
            {
                DrawingCanvas.Children.Remove(_activePolyline);
                _activePolyline = null;
            }
        }

        // Element Duplication Helper (Ctrl + Drag)
        private UIElement? CloneUIElement(UIElement original)
        {
            if (original is Border border && border.Child is TextBlock tb)
            {
                double left = Canvas.GetLeft(border);
                double top = Canvas.GetTop(border);
                var color = (border.BorderBrush as SolidColorBrush)?.Color ?? _currentColor;
                return CreateEditableTextBlock(tb.Text, left + 15, top + 15, color, tb.FontSize);
            }
            else if (original is Grid grid && grid.Children.Count >= 2 && grid.Children[1] is TextBlock stepTb)
            {
                double left = Canvas.GetLeft(grid) + 17;
                double top = Canvas.GetTop(grid) + 17;
                var color = ((grid.Children[0] as Ellipse)?.Fill as SolidColorBrush)?.Color ?? _currentColor;
                int.TryParse(stepTb.Text, out int num);
                var badge = CreateStepBadge(left + 20, top + 20, color, num > 0 ? num : _currentStepNumber);
                if (badge is Grid newGrid && newGrid.Children[1] is TextBlock newTb)
                {
                    newTb.Text = stepTb.Text;
                }
                return badge;
            }
            else if (original is Canvas calloutCanvas && calloutCanvas.Children.Count >= 3)
            {
                Line? line = calloutCanvas.Children[0] as Line;
                Border? card = calloutCanvas.Children[2] as Border;
                TextBlock? cardTb = card?.Child as TextBlock;

                if (line != null && card != null && cardTb != null)
                {
                    double x1 = line.X1 + 20; double y1 = line.Y1 + 20;
                    double x2 = Canvas.GetLeft(card) + 20; double y2 = Canvas.GetTop(card) + 20;
                    var color = (card.BorderBrush as SolidColorBrush)?.Color ?? _currentColor;
                    var bg = (card.Background as SolidColorBrush)?.Color ?? _currentCalloutBgColor;
                    return CreateCalloutElement(x1, y1, x2, y2, color, cardTb.Text, cardTb.FontSize, bg);
                }
            }
            else if (original is System.Windows.Shapes.Path path && path.Tag is ArrowData arrowData)
            {
                var stroke = (path.Stroke as SolidColorBrush)?.Color ?? _currentColor;
                return CreateArrow(arrowData.X1 + 20, arrowData.Y1 + 20, arrowData.X2 + 20, arrowData.Y2 + 20, stroke, path.StrokeThickness);
            }
            else if (original is Rectangle rect)
            {
                double left = Canvas.GetLeft(rect);
                double top = Canvas.GetTop(rect);
                var stroke = rect.Stroke as SolidColorBrush;
                var cloneRect = new Rectangle
                {
                    Width = rect.Width, Height = rect.Height,
                    Stroke = stroke != null ? new SolidColorBrush(stroke.Color) : new SolidColorBrush(_currentColor),
                    Fill = rect.Fill != null ? new SolidColorBrush(((SolidColorBrush)rect.Fill).Color) : null,
                    StrokeThickness = rect.StrokeThickness,
                    Cursor = Cursors.SizeAll
                };
                Canvas.SetLeft(cloneRect, left + 15);
                Canvas.SetTop(cloneRect, top + 15);
                return cloneRect;
            }
            else if (original is Ellipse ellipse)
            {
                double left = Canvas.GetLeft(ellipse);
                double top = Canvas.GetTop(ellipse);
                var stroke = ellipse.Stroke as SolidColorBrush;
                var cloneEllipse = new Ellipse
                {
                    Width = ellipse.Width, Height = ellipse.Height,
                    Stroke = stroke != null ? new SolidColorBrush(stroke.Color) : new SolidColorBrush(_currentColor),
                    Fill = ellipse.Fill != null ? new SolidColorBrush(((SolidColorBrush)ellipse.Fill).Color) : null,
                    StrokeThickness = ellipse.StrokeThickness,
                    Cursor = Cursors.SizeAll
                };
                Canvas.SetLeft(cloneEllipse, left + 15);
                Canvas.SetTop(cloneEllipse, top + 15);
                return cloneEllipse;
            }
            else if (original is Line line)
            {
                var stroke = line.Stroke as SolidColorBrush;
                return new Line
                {
                    X1 = line.X1 + 20, Y1 = line.Y1 + 20, X2 = line.X2 + 20, Y2 = line.Y2 + 20,
                    Stroke = stroke != null ? new SolidColorBrush(stroke.Color) : new SolidColorBrush(_currentColor),
                    StrokeThickness = line.StrokeThickness,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Cursor = Cursors.SizeAll
                };
            }

            return null;
        }

        // Canvas Mouse Events
        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // If clicking a resize handle, let Handle_MouseDown take it
            if (e.Source == HandleTL || e.Source == HandleTR || e.Source == HandleBL || e.Source == HandleBR || e.Source == HandleP1 || e.Source == HandleP2)
            {
                return;
            }

            System.Windows.Point current = e.GetPosition(DrawingCanvas);

            // Check if user clicked directly on an existing annotation
            var hit = e.Source as UIElement;
            if (hit != null && hit != DrawingCanvas && hit != SelectionBoxBorder)
            {
                DependencyObject target = hit;
                while (target != null && VisualTreeHelper.GetParent(target) != null && VisualTreeHelper.GetParent(target) != DrawingCanvas)
                {
                    target = VisualTreeHelper.GetParent(target);
                }

                var targetElem = target as UIElement;
                if (targetElem != null && _annotations.Contains(targetElem))
                {
                    // Existing annotation clicked! Select it immediately!
                    SelectElement(targetElem);
                    ToolSelect.IsChecked = true;

                    // Ctrl + Drag to Duplicate
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        var clone = CloneUIElement(targetElem);
                        if (clone != null)
                        {
                            AddAnnotation(clone);
                            targetElem = clone;
                            SelectElement(targetElem);
                            TxtStatus.Text = "Элемент скопирован и перемещается (Ctrl + Drag).";
                        }
                    }

                    _isDraggingElement = true;
                    _startPoint = current;
                    _dragLastPoint = current;

                    double elemLeft = Canvas.GetLeft(targetElem);
                    double elemTop = Canvas.GetTop(targetElem);
                    if (double.IsNaN(elemLeft)) elemLeft = 0;
                    if (double.IsNaN(elemTop)) elemTop = 0;

                    _dragOffset = new System.Windows.Point(current.X - elemLeft, current.Y - elemTop);
                    return;
                }
            }

            // Clicked on empty canvas in Select mode -> clear selection
            if (_currentTool == "select")
            {
                ClearSelection();
                return;
            }

            // Clicked on empty canvas in Drawing mode
            if (_isTwoStageActive)
            {
                FinalizeActiveDrawing(current);
                return;
            }

            ClearSelection();

            _startPoint = current;
            _isTwoStageActive = true;
            _isDrawing = true;

            if (_currentTool == "step")
            {
                var badge = CreateStepBadge(_startPoint.X, _startPoint.Y, _currentColor, _currentStepNumber++);
                UpdateNextStepUI();
                AddAnnotation(badge);
                SelectElement(badge);
                ToolSelect.IsChecked = true;
                _isTwoStageActive = false;
                _isDrawing = false;
                return;
            }

            if (_currentTool == "text")
            {
                var txtElem = CreateEditableTextBlock("Введите текст\n(Ctrl+Enter — сохранить)", _startPoint.X, _startPoint.Y, _currentColor, _currentFontSize);
                AddAnnotation(txtElem);
                SelectElement(txtElem);
                _isTwoStageActive = false;
                _isDrawing = false;

                if (txtElem is Border border && border.Child is TextBlock tb)
                {
                    StartInlineTextEdit(border, tb);
                }
                ToolSelect.IsChecked = true;
                return;
            }

            if (_currentTool == "pencil" || _currentTool == "highlighter")
            {
                _activePolyline = new Polyline
                {
                    Stroke = new SolidColorBrush(_currentColor),
                    StrokeThickness = _currentTool == "highlighter" ? _currentStrokeWidth * 3.5 : _currentStrokeWidth,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Opacity = _currentTool == "highlighter" ? 0.40 : 1.0,
                    IsHitTestVisible = false,
                    Cursor = Cursors.SizeAll
                };
                _activePolyline.Points.Add(_startPoint);
                DrawingCanvas.Children.Add(_activePolyline);
                return;
            }

            TxtStatus.Text = "Начальная точка задана. Двигайте мышь и кликните повторно для фиксации (Esc — отмена).";
        }

        private void Canvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            System.Windows.Point current = e.GetPosition(DrawingCanvas);

            // Handle active handle resizing
            if (_activeHandleName != null && _selectedElement != null)
            {
                if (_selectedElement is Line line)
                {
                    if (_activeHandleName == "HandleP1")
                    {
                        line.X1 = current.X;
                        line.Y1 = current.Y;
                    }
                    else if (_activeHandleName == "HandleP2")
                    {
                        line.X2 = current.X;
                        line.Y2 = current.Y;
                    }
                    UpdateSelectionHighlight(line);
                    return;
                }
                else if (_selectedElement is System.Windows.Shapes.Path path && path.Tag is ArrowData arrowData)
                {
                    if (_activeHandleName == "HandleP1")
                    {
                        arrowData.X1 = current.X;
                        arrowData.Y1 = current.Y;
                    }
                    else if (_activeHandleName == "HandleP2")
                    {
                        arrowData.X2 = current.X;
                        arrowData.Y2 = current.Y;
                    }
                    path.Data = BuildArrowGeometry(arrowData.X1, arrowData.Y1, arrowData.X2, arrowData.Y2, path.StrokeThickness);
                    UpdateSelectionHighlight(path);
                    return;
                }
                else if (_selectedElement is FrameworkElement boxElem)
                {
                    double left = Canvas.GetLeft(boxElem);
                    double top = Canvas.GetTop(boxElem);
                    if (double.IsNaN(left)) left = 0;
                    if (double.IsNaN(top)) top = 0;
                    double w = boxElem.Width;
                    double h = boxElem.Height;
                    if (double.IsNaN(w) || w <= 0) w = boxElem.ActualWidth;
                    if (double.IsNaN(h) || h <= 0) h = boxElem.ActualHeight;

                    double right = left + w;
                    double bottom = top + h;

                    switch (_activeHandleName)
                    {
                        case "HandleBR":
                            boxElem.Width = Math.Max(10, current.X - left);
                            boxElem.Height = Math.Max(10, current.Y - top);
                            break;
                        case "HandleTL":
                            double newL = Math.Min(right - 10, current.X);
                            double newT = Math.Min(bottom - 10, current.Y);
                            Canvas.SetLeft(boxElem, newL);
                            Canvas.SetTop(boxElem, newT);
                            boxElem.Width = right - newL;
                            boxElem.Height = bottom - newT;
                            break;
                        case "HandleTR":
                            double newTop = Math.Min(bottom - 10, current.Y);
                            Canvas.SetTop(boxElem, newTop);
                            boxElem.Width = Math.Max(10, current.X - left);
                            boxElem.Height = bottom - newTop;
                            break;
                        case "HandleBL":
                            double newLeft = Math.Min(right - 10, current.X);
                            Canvas.SetLeft(boxElem, newLeft);
                            boxElem.Width = right - newLeft;
                            boxElem.Height = Math.Max(10, current.Y - top);
                            break;
                    }
                    UpdateSelectionHighlight(boxElem);
                    return;
                }
            }

            // Handle moving element
            if (_isDraggingElement && _selectedElement != null)
            {
                if (_selectedElement is Line line)
                {
                    double dx = current.X - _dragLastPoint.X;
                    double dy = current.Y - _dragLastPoint.Y;
                    line.X1 += dx; line.Y1 += dy;
                    line.X2 += dx; line.Y2 += dy;
                    _dragLastPoint = current;
                    UpdateSelectionHighlight(line);
                    return;
                }
                else if (_selectedElement is System.Windows.Shapes.Path path && path.Tag is ArrowData arrowData)
                {
                    double dx = current.X - _dragLastPoint.X;
                    double dy = current.Y - _dragLastPoint.Y;
                    arrowData.X1 += dx; arrowData.Y1 += dy;
                    arrowData.X2 += dx; arrowData.Y2 += dy;
                    path.Data = BuildArrowGeometry(arrowData.X1, arrowData.Y1, arrowData.X2, arrowData.Y2, path.StrokeThickness);
                    _dragLastPoint = current;
                    UpdateSelectionHighlight(path);
                    return;
                }
                else
                {
                    double newL = current.X - _dragOffset.X;
                    double newT = current.Y - _dragOffset.Y;
                    Canvas.SetLeft(_selectedElement, newL);
                    Canvas.SetTop(_selectedElement, newT);
                    UpdateSelectionHighlight(_selectedElement);
                    return;
                }
            }

            if (!_isDrawing && !_isTwoStageActive) return;

            if (_currentTool == "pencil" || _currentTool == "highlighter")
            {
                _activePolyline?.Points.Add(current);
                return;
            }

            UpdatePreviewElement(current);
        }

        private void UpdatePreviewElement(System.Windows.Point current)
        {
            if (_activePreviewElement != null)
            {
                DrawingCanvas.Children.Remove(_activePreviewElement);
            }

            double width = Math.Abs(current.X - _startPoint.X);
            double height = Math.Abs(current.Y - _startPoint.Y);
            double left = Math.Min(_startPoint.X, current.X);
            double top = Math.Min(_startPoint.Y, current.Y);

            switch (_currentTool)
            {
                case "arrow":
                    _activePreviewElement = CreateArrow(_startPoint.X, _startPoint.Y, current.X, current.Y, _currentColor, _currentStrokeWidth);
                    break;
                case "line":
                    _activePreviewElement = new Line
                    {
                        X1 = _startPoint.X, Y1 = _startPoint.Y, X2 = current.X, Y2 = current.Y,
                        Stroke = new SolidColorBrush(_currentColor),
                        StrokeThickness = _currentStrokeWidth,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round,
                        Cursor = Cursors.SizeAll
                    };
                    break;
                case "callout":
                    _activePreviewElement = CreateCalloutElement(_startPoint.X, _startPoint.Y, current.X, current.Y, _currentColor, "Введите выноску\n(Ctrl+Enter)", _currentFontSize, _currentCalloutBgColor);
                    break;
                case "rect":
                    var rect = new Rectangle
                    {
                        Width = width, Height = height,
                        Stroke = new SolidColorBrush(_currentColor),
                        StrokeThickness = _currentStrokeWidth,
                        Fill = GetCurrentFillBrush(),
                        Cursor = Cursors.SizeAll
                    };
                    Canvas.SetLeft(rect, left); Canvas.SetTop(rect, top);
                    _activePreviewElement = rect;
                    break;
                case "ellipse":
                    var ellipse = new Ellipse
                    {
                        Width = width, Height = height,
                        Stroke = new SolidColorBrush(_currentColor),
                        StrokeThickness = _currentStrokeWidth,
                        Fill = GetCurrentFillBrush(),
                        Cursor = Cursors.SizeAll
                    };
                    Canvas.SetLeft(ellipse, left); Canvas.SetTop(ellipse, top);
                    _activePreviewElement = ellipse;
                    break;
            }

            if (_activePreviewElement != null)
            {
                _activePreviewElement.IsHitTestVisible = false;
                DrawingCanvas.Children.Add(_activePreviewElement);
            }
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_activeHandleName != null)
            {
                HandleTL.ReleaseMouseCapture();
                HandleTR.ReleaseMouseCapture();
                HandleBL.ReleaseMouseCapture();
                HandleBR.ReleaseMouseCapture();
                HandleP1.ReleaseMouseCapture();
                HandleP2.ReleaseMouseCapture();
                _activeHandleName = null;
                UpdateSelectionHighlight(_selectedElement);
            }

            _isDraggingElement = false;

            if (_activePolyline != null)
            {
                _activePolyline.IsHitTestVisible = true;
                AddAnnotation(_activePolyline);
                SelectElement(_activePolyline);
                ToolSelect.IsChecked = true;
                _activePolyline = null;
                _isDrawing = false;
                _isTwoStageActive = false;
                return;
            }

            System.Windows.Point current = e.GetPosition(DrawingCanvas);
            double dist = Math.Sqrt(Math.Pow(current.X - _startPoint.X, 2) + Math.Pow(current.Y - _startPoint.Y, 2));

            if (dist > 14 && _isTwoStageActive)
            {
                FinalizeActiveDrawing(current);
            }
        }

        private void FinalizeActiveDrawing(System.Windows.Point endPoint)
        {
            if (!_isTwoStageActive && !_isDrawing) return;

            UpdatePreviewElement(endPoint);

            if (_activePreviewElement != null)
            {
                var finalElem = _activePreviewElement;
                DrawingCanvas.Children.Remove(_activePreviewElement);
                _activePreviewElement = null;

                finalElem.IsHitTestVisible = true;
                AddAnnotation(finalElem);

                // Auto-select newly created element and switch to Select tool
                SelectElement(finalElem);
                ToolSelect.IsChecked = true;

                if (_currentTool == "callout")
                {
                    if (finalElem is Canvas c)
                    {
                        foreach (var child in c.Children)
                        {
                            if (child is Border border && border.Child is TextBlock tb)
                            {
                                StartInlineTextEdit(border, tb);
                                break;
                            }
                        }
                    }
                }
            }

            _isTwoStageActive = false;
            _isDrawing = false;
            TxtStatus.Text = "Элемент размещен и выделен. Вы можете изменить его цвет, размер или стиль в правой панели.";
        }

        private void AddAnnotation(UIElement elem)
        {
            _undoStack.Push(new List<UIElement>(_annotations));
            _redoStack.Clear();
            _annotations.Add(elem);
            if (!DrawingCanvas.Children.Contains(elem))
            {
                DrawingCanvas.Children.Add(elem);
            }
        }

        // Factory: Vector Arrow with dynamic geometry
        private static GeometryGroup BuildArrowGeometry(double x1, double y1, double x2, double y2, double thickness)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1) len = 1;

            double angle = Math.Atan2(dy, dx);
            double headLen = Math.Max(14, thickness * 3.5);
            double arrowAngle = 0.42;

            System.Windows.Point pArrow1 = new System.Windows.Point(x2 - headLen * Math.Cos(angle - arrowAngle), y2 - headLen * Math.Sin(angle - arrowAngle));
            System.Windows.Point pArrow2 = new System.Windows.Point(x2 - headLen * Math.Cos(angle + arrowAngle), y2 - headLen * Math.Sin(angle + arrowAngle));

            var geom = new GeometryGroup();
            geom.Children.Add(new LineGeometry(new System.Windows.Point(x1, y1), new System.Windows.Point(x2, y2)));
            geom.Children.Add(new LineGeometry(new System.Windows.Point(x2, y2), pArrow1));
            geom.Children.Add(new LineGeometry(new System.Windows.Point(x2, y2), pArrow2));
            return geom;
        }

        private UIElement CreateArrow(double x1, double y1, double x2, double y2, System.Windows.Media.Color color, double thickness)
        {
            var geom = BuildArrowGeometry(x1, y1, x2, y2, thickness);
            var path = new System.Windows.Shapes.Path
            {
                Data = geom,
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Cursor = Cursors.SizeAll,
                Tag = new ArrowData { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2 }
            };
            return path;
        }

        // Factory: Callout
        private UIElement CreateCalloutElement(double x1, double y1, double x2, double y2, System.Windows.Media.Color color, string text, double fontSize, System.Windows.Media.Color? bgColor = null)
        {
            var calloutBg = bgColor ?? _currentCalloutBgColor;
            var calloutCanvas = new Canvas();

            var leaderLine = new Line
            {
                X1 = x1, Y1 = y1, X2 = x2 + 30, Y2 = y2 + 15,
                Stroke = new SolidColorBrush(color),
                StrokeThickness = Math.Max(2, _currentStrokeWidth / 2.0),
                StrokeDashArray = new DoubleCollection { 4, 3 }
            };

            var targetDot = new Ellipse
            {
                Width = 12, Height = 12,
                Fill = new SolidColorBrush(color),
                Stroke = Brushes.White,
                StrokeThickness = 2,
                Cursor = Cursors.Hand
            };
            Canvas.SetLeft(targetDot, x1 - 6);
            Canvas.SetTop(targetDot, y1 - 6);

            var border = new Border
            {
                Background = new SolidColorBrush(calloutBg),
                BorderBrush = new SolidColorBrush(color),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 6, 10, 6),
                Cursor = Cursors.SizeAll,
                MinWidth = 110,
                Tag = calloutBg
            };

            var tb = new TextBlock
            {
                Text = text,
                Foreground = GetContrastTextBrush(calloutBg, color),
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap
            };

            border.Child = tb;
            Canvas.SetLeft(border, x2);
            Canvas.SetTop(border, y2);

            bool isDraggingDot = false;

            targetDot.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                isDraggingDot = true;
                targetDot.CaptureMouse();
                SelectElement(calloutCanvas);
            };

            targetDot.MouseMove += (s, e) =>
            {
                if (isDraggingDot)
                {
                    System.Windows.Point cur = e.GetPosition(DrawingCanvas);
                    Canvas.SetLeft(targetDot, cur.X - 6);
                    Canvas.SetTop(targetDot, cur.Y - 6);
                    leaderLine.X1 = cur.X;
                    leaderLine.Y1 = cur.Y;
                    UpdateSelectionHighlight(calloutCanvas);
                }
            };

            targetDot.MouseLeftButtonUp += (s, e) =>
            {
                if (isDraggingDot)
                {
                    isDraggingDot = false;
                    targetDot.ReleaseMouseCapture();
                }
            };

            border.MouseLeftButtonDown += (s, e) =>
            {
                SelectElement(calloutCanvas);
                if (e.ClickCount == 2)
                {
                    e.Handled = true;
                    StartInlineTextEdit(border, tb);
                }
            };

            calloutCanvas.Children.Add(leaderLine);
            calloutCanvas.Children.Add(targetDot);
            calloutCanvas.Children.Add(border);

            return calloutCanvas;
        }

        // Factory: Step Badge
        private UIElement CreateStepBadge(double x, double y, System.Windows.Media.Color color, int stepNum)
        {
            var grid = new Grid { Width = 34, Height = 34, Cursor = Cursors.SizeAll };
            var ellipse = new Ellipse
            {
                Width = 34, Height = 34,
                Fill = new SolidColorBrush(color),
                Stroke = Brushes.White,
                StrokeThickness = 2
            };

            var txt = new TextBlock
            {
                Text = stepNum.ToString(),
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            grid.Children.Add(ellipse);
            grid.Children.Add(txt);

            grid.MouseLeftButtonDown += (s, e) =>
            {
                SelectElement(grid);
                if (e.ClickCount == 2)
                {
                    e.Handled = true;
                    StartInlineStepEdit(grid, txt);
                }
            };

            Canvas.SetLeft(grid, x - 17);
            Canvas.SetTop(grid, y - 17);
            return grid;
        }

        private void StartInlineStepEdit(Grid grid, TextBlock txt)
        {
            var editBox = new TextBox
            {
                Text = txt.Text,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42)),
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Cyan,
                Padding = new Thickness(2),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Width = 34, Height = 34
            };

            grid.Children.Add(editBox);
            editBox.Focus();
            editBox.SelectAll();

            Action commitStep = () =>
            {
                string newLabel = editBox.Text.Trim();
                if (string.IsNullOrEmpty(newLabel)) newLabel = "1";
                txt.Text = newLabel;
                grid.Children.Remove(editBox);
                UpdateSelectionHighlight(grid);
            };

            editBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    commitStep();
                }
                else if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    grid.Children.Remove(editBox);
                }
            };

            editBox.LostFocus += (s, e) => commitStep();
        }

        // Factory: Editable Text Block
        private UIElement CreateEditableTextBlock(string text, double x, double y, System.Windows.Media.Color color, double fontSize)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(210, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(color),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 4, 8, 4),
                Cursor = Cursors.SizeAll
            };

            var tb = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(color),
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap
            };

            border.Child = tb;

            border.MouseLeftButtonDown += (s, e) =>
            {
                SelectElement(border);
                if (e.ClickCount == 2)
                {
                    e.Handled = true;
                    StartInlineTextEdit(border, tb);
                }
            };

            Canvas.SetLeft(border, x);
            Canvas.SetTop(border, y);
            return border;
        }

        private void StartInlineTextEdit(Border border, TextBlock tb)
        {
            var editBox = new TextBox
            {
                Text = tb.Text,
                FontSize = tb.FontSize,
                FontWeight = FontWeights.Bold,
                Foreground = tb.Foreground,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42)),
                BorderThickness = new Thickness(1.5),
                BorderBrush = Brushes.Cyan,
                Padding = new Thickness(6),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinWidth = 140,
                MinHeight = 45
            };

            border.Child = editBox;
            editBox.Focus();
            editBox.SelectAll();

            Action commitEdit = () =>
            {
                string newText = editBox.Text.Trim();
                if (string.IsNullOrEmpty(newText)) newText = "Текст";
                tb.Text = newText;
                border.Child = tb;
                UpdateSelectionHighlight(border);
            };

            editBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    e.Handled = true;
                    commitEdit();
                }
                else if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    border.Child = tb;
                }
            };

            editBox.LostFocus += (s, e) => commitEdit();
        }

        // Actions
        private void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            if (_undoStack.Count > 0)
            {
                _redoStack.Push(new List<UIElement>(_annotations));
                var lastState = _undoStack.Pop();
                ResetCanvasAnnotations();
                foreach (var item in lastState)
                {
                    _annotations.Add(item);
                    DrawingCanvas.Children.Add(item);
                }
                ClearSelection();
                TxtStatus.Text = "Действие отменено (Ctrl+Z).";
            }
        }

        private void BtnRedo_Click(object sender, RoutedEventArgs e)
        {
            if (_redoStack.Count > 0)
            {
                _undoStack.Push(new List<UIElement>(_annotations));
                var nextState = _redoStack.Pop();
                ResetCanvasAnnotations();
                foreach (var item in nextState)
                {
                    _annotations.Add(item);
                    DrawingCanvas.Children.Add(item);
                }
                ClearSelection();
                TxtStatus.Text = "Действие возвращено (Ctrl+Y).";
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            _undoStack.Push(new List<UIElement>(_annotations));
            _redoStack.Clear();
            ResetCanvasAnnotations();
            TxtStatus.Text = "Все аннотации очищены.";
        }

        private RenderTargetBitmap RenderCanvasToBitmap()
        {
            HideAllHandles();

            int w = (int)DrawingCanvas.Width;
            int h = (int)DrawingCanvas.Height;
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(DrawingCanvas);

            if (_selectedElement != null)
            {
                UpdateSelectionHighlight(_selectedElement);
            }

            return rtb;
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            var rtb = RenderCanvasToBitmap();
            Clipboard.SetImage(rtb);
            TxtStatus.Text = $"Изображение скопировано в буфер ({rtb.PixelWidth} × {rtb.PixelHeight} px)!";
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "PNG Image|*.png|JPEG Image|*.jpg", FileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png" };
            if (dlg.ShowDialog() == true)
            {
                var rtb = RenderCanvasToBitmap();
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                using (var fs = File.Create(dlg.FileName))
                {
                    encoder.Save(fs);
                }
                TxtStatus.Text = $"Файл сохранен в исходном разрешении ({rtb.PixelWidth} × {rtb.PixelHeight} px): {System.IO.Path.GetFileName(dlg.FileName)}";
            }
        }

        // Global KeyDown
        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CancelActiveDrawing();
                ClearSelection();
                ToolSelect.IsChecked = true;
                TxtStatus.Text = "Инструмент сброшен на режим выбора (Esc).";
            }
            else if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                DeleteSelectedElement();
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C)
            {
                BtnCopy_Click(sender, e);
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
            {
                BtnSave_Click(sender, e);
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z)
            {
                BtnUndo_Click(sender, e);
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y)
            {
                BtnRedo_Click(sender, e);
            }
            else if ((Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) && e.Key == Key.Z)
            {
                BtnRedo_Click(sender, e);
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && (e.Key == Key.D0 || e.Key == Key.NumPad0))
            {
                ResetZoom();
            }
            else if (Keyboard.Modifiers == ModifierKeys.None)
            {
                switch (e.Key)
                {
                    case Key.A: ToolArrow.IsChecked = true; break;
                    case Key.L: ToolLine.IsChecked = true; break;
                    case Key.C: ToolCallout.IsChecked = true; break;
                    case Key.T: ToolText.IsChecked = true; break;
                    case Key.R: ToolRect.IsChecked = true; break;
                    case Key.E: ToolEllipse.IsChecked = true; break;
                    case Key.N: ToolStep.IsChecked = true; break;
                    case Key.P: ToolPencil.IsChecked = true; break;
                    case Key.H: ToolHighlighter.IsChecked = true; break;
                }
            }
        }
    }
}
