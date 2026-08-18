using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using Drawing = System.Drawing;

namespace BCCScreenShot
{
    public partial class MainWindow : Window
    {
        private string _currentTool = "select";
        private System.Windows.Media.Color _currentColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EF4444");
        private double _currentStrokeWidth = 4;
        private double _currentFontSize = 20;
        private int _currentStepNumber = 1;

        private ImageSource? _bgImage;
        private readonly List<UIElement> _annotations = new();
        private readonly Stack<List<UIElement>> _undoStack = new();

        private bool _isDrawing;
        private bool _isDraggingElement;
        private System.Windows.Point _startPoint;
        private System.Windows.Point _dragOffset;
        private UIElement? _activePreviewElement;
        private Polyline? _activePolyline;
        private UIElement? _selectedElement;

        public MainWindow()
        {
            InitializeComponent();
            LoadDemoCanvas();
        }

        private void LoadDemoCanvas()
        {
            int w = 920, h = 560;
            var drawingVisual = new DrawingVisual();
            using (var dc = drawingVisual.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42)), null, new Rect(0, 0, w, h));
                dc.DrawRectangle(new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59)), null, new Rect(0, 0, w, 50));
                dc.DrawText(
                    new FormattedText("Аналитический Отчет компании — Рабочий Стол",
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"), 16, Brushes.White, 1.25),
                    new System.Windows.Point(20, 14));

                // Cards
                var cardBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59));
                dc.DrawRoundedRectangle(cardBg, null, new Rect(40, 80, 260, 100), 8, 8);
                dc.DrawText(new FormattedText("Выручка", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Brushes.Gray, 1.25), new System.Windows.Point(60, 95));
                dc.DrawText(new FormattedText("2 450 000 ₽", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 22, Brushes.LightGreen, 1.25), new System.Windows.Point(60, 125));

                dc.DrawRoundedRectangle(cardBg, null, new Rect(330, 80, 260, 100), 8, 8);
                dc.DrawText(new FormattedText("Пользователи", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Brushes.Gray, 1.25), new System.Windows.Point(350, 95));
                dc.DrawText(new FormattedText("+184 аккаунта", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 22, Brushes.Cyan, 1.25), new System.Windows.Point(350, 125));

                dc.DrawRoundedRectangle(cardBg, null, new Rect(620, 80, 260, 100), 8, 8);
                dc.DrawText(new FormattedText("Конверсия", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Brushes.Gray, 1.25), new System.Windows.Point(640, 95));
                dc.DrawText(new FormattedText("94.2%", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 22, Brushes.MediumPurple, 1.25), new System.Windows.Point(640, 125));
            }

            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(drawingVisual);
            SetBackgroundImage(rtb);

            AddDefaultDemoAnnotations();
        }

        private void SetBackgroundImage(ImageSource imgSource)
        {
            _bgImage = imgSource;
            DrawingCanvas.Width = imgSource.Width;
            DrawingCanvas.Height = imgSource.Height;
            DrawingCanvas.Background = new ImageBrush(imgSource);
            TxtCanvasSize.Text = $"{(int)imgSource.Width} x {(int)imgSource.Height} px";
        }

        private void AddDefaultDemoAnnotations()
        {
            DrawingCanvas.Children.Clear();
            DrawingCanvas.Children.Add(SelectionBoxBorder);
            _annotations.Clear();
            _currentStepNumber = 1;

            // Demo Arrow
            var arrow = CreateArrow(620, 220, 480, 140, _currentColor, 4);
            AddAnnotation(arrow);

            // Demo Rect
            var rect = new Rectangle
            {
                Width = 260, Height = 100,
                Stroke = new SolidColorBrush(_currentColor), StrokeThickness = 3
            };
            Canvas.SetLeft(rect, 620);
            Canvas.SetTop(rect, 80);
            AddAnnotation(rect);

            // Demo Editable Text
            var txt = CreateEditableTextBlock("Проверьте конверсию здесь! (двойной клик)", 620, 45, (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F97316"), 18);
            AddAnnotation(txt);

            // Demo Step Badge
            var step = CreateStepBadge(600, 80, _currentColor, _currentStepNumber++);
            AddAnnotation(step);
        }

        // Screen Capture Region Snippet Window
        private void BtnCaptureArea_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            System.Threading.Thread.Sleep(200);

            var snippetWin = new ScreenSnippetWindow();
            bool? result = snippetWin.ShowDialog();

            Show();

            if (result == true && snippetWin.CapturedBitmap != null)
            {
                SetBackgroundImage(snippetWin.CapturedBitmap);
                DrawingCanvas.Children.Clear();
                DrawingCanvas.Children.Add(SelectionBoxBorder);
                _annotations.Clear();
                _currentStepNumber = 1;
                TxtStatus.Text = "Выделенный скриншот загружен на холст!";
            }
        }

        // Screen Capture Full Screen
        private void BtnCaptureScreen_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            System.Threading.Thread.Sleep(300);

            int width = (int)SystemParameters.PrimaryScreenWidth;
            int height = (int)SystemParameters.PrimaryScreenHeight;

            using (var bmp = new Drawing.Bitmap(width, height))
            {
                using (var g = Drawing.Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(0, 0, 0, 0, new Drawing.Size(width, height));
                }

                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Position = 0;

                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.StreamSource = ms;
                    bi.EndInit();
                    bi.Freeze();

                    SetBackgroundImage(bi);
                    DrawingCanvas.Children.Clear();
                    DrawingCanvas.Children.Add(SelectionBoxBorder);
                    _annotations.Clear();
                    _currentStepNumber = 1;
                }
            }

            Show();
            TxtStatus.Text = "Скриншот всего экрана загружен на холст!";
        }

        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp" };
            if (dlg.ShowDialog() == true)
            {
                var bi = new BitmapImage(new Uri(dlg.FileName));
                SetBackgroundImage(bi);
                DrawingCanvas.Children.Clear();
                DrawingCanvas.Children.Add(SelectionBoxBorder);
                _annotations.Clear();
                _currentStepNumber = 1;
                TxtStatus.Text = "Файл открыт успешно!";
            }
        }

        private void BtnDemo_Click(object sender, RoutedEventArgs e)
        {
            LoadDemoCanvas();
            TxtStatus.Text = "Загружен демо-снимок!";
        }

        // Tool Selection
        private void Tool_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string tool)
            {
                _currentTool = tool;
                if (_currentTool != "select")
                {
                    ClearSelection();
                }
                if (TxtStatus != null)
                    TxtStatus.Text = $"Выбран инструмент: {rb.Content} (Esc — скинуть на выбор)";
            }
        }

        private void ColorSwatch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex)
            {
                _currentColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
                ApplyStyleToSelectedElement();
            }
        }

        private void SliderStroke_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _currentStrokeWidth = e.NewValue;
            if (TxtStrokeVal != null) TxtStrokeVal.Text = $"{ (int)_currentStrokeWidth } px";
            ApplyStyleToSelectedElement();
        }

        private void SliderFont_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _currentFontSize = e.NewValue;
            if (TxtFontVal != null) TxtFontVal.Text = $"{ (int)_currentFontSize } px";
            ApplyStyleToSelectedElement();
        }

        private void ApplyStyleToSelectedElement()
        {
            if (_selectedElement is Border b && b.Child is TextBlock tb)
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
                UpdateSelectionHighlight(_selectedElement);
            }
        }

        // Visual Selection Bounding Box Highlight
        private void UpdateSelectionHighlight(UIElement? elem)
        {
            if (elem == null || elem == DrawingCanvas || elem == SelectionBoxBorder)
            {
                SelectionBoxBorder.Visibility = Visibility.Collapsed;
                return;
            }

            double left = Canvas.GetLeft(elem);
            double top = Canvas.GetTop(elem);
            double w = 0, h = 0;

            if (elem is FrameworkElement fe)
            {
                w = fe.ActualWidth > 0 ? fe.ActualWidth : fe.Width;
                h = fe.ActualHeight > 0 ? fe.ActualHeight : fe.Height;
            }

            if (elem is Shape shape)
            {
                if (double.IsNaN(w) || w <= 0) w = shape.Width;
                if (double.IsNaN(h) || h <= 0) h = shape.Height;
                if (w <= 0 || h <= 0)
                {
                    var bounds = VisualTreeHelper.GetDescendantBounds(shape);
                    left = bounds.Left;
                    top = bounds.Top;
                    w = bounds.Width;
                    h = bounds.Height;
                }
            }

            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;

            if (w > 0 && h > 0)
            {
                Canvas.SetLeft(SelectionBoxBorder, left - 4);
                Canvas.SetTop(SelectionBoxBorder, top - 4);
                SelectionBoxBorder.Width = w + 8;
                SelectionBoxBorder.Height = h + 8;
                SelectionBoxBorder.Visibility = Visibility.Visible;
            }
            else
            {
                SelectionBoxBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void ClearSelection()
        {
            _selectedElement = null;
            SelectionBoxBorder.Visibility = Visibility.Collapsed;
        }

        private void DeleteSelectedElement()
        {
            if (_selectedElement != null)
            {
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

        // Canvas Mouse Events
        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(DrawingCanvas);
            _isDrawing = true;

            if (_currentTool == "select")
            {
                var hit = e.Source as UIElement;
                if (hit != null && hit != DrawingCanvas && hit != SelectionBoxBorder)
                {
                    // Find top-level child under canvas
                    DependencyObject target = hit;
                    while (target != null && VisualTreeHelper.GetParent(target) != null && VisualTreeHelper.GetParent(target) != DrawingCanvas)
                    {
                        target = VisualTreeHelper.GetParent(target);
                    }

                    _selectedElement = target as UIElement;
                    UpdateSelectionHighlight(_selectedElement);

                    _isDraggingElement = true;
                    double elemLeft = Canvas.GetLeft(_selectedElement);
                    double elemTop = Canvas.GetTop(_selectedElement);
                    if (double.IsNaN(elemLeft)) elemLeft = 0;
                    if (double.IsNaN(elemTop)) elemTop = 0;

                    _dragOffset = new System.Windows.Point(_startPoint.X - elemLeft, _startPoint.Y - elemTop);
                    TxtStatus.Text = "Элемент выделен. (Delete для удаления)";
                }
                else
                {
                    ClearSelection();
                }
                return;
            }

            ClearSelection();

            if (_currentTool == "step")
            {
                var badge = CreateStepBadge(_startPoint.X, _startPoint.Y, _currentColor, _currentStepNumber++);
                AddAnnotation(badge);
                _isDrawing = false;
                return;
            }

            if (_currentTool == "text")
            {
                var txtElem = CreateEditableTextBlock("Введите текст (двойной клик)", _startPoint.X, _startPoint.Y, _currentColor, _currentFontSize);
                AddAnnotation(txtElem);
                _isDrawing = false;
                TxtStatus.Text = "Текст добавлен! Двойной клик для редактирования.";
                return;
            }

            if (_currentTool == "pencil" || _currentTool == "highlighter")
            {
                _activePolyline = new Polyline
                {
                    Stroke = new SolidColorBrush(_currentColor),
                    StrokeThickness = _currentTool == "highlighter" ? _currentStrokeWidth * 3 : _currentStrokeWidth,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Opacity = _currentTool == "highlighter" ? 0.45 : 1.0
                };
                _activePolyline.Points.Add(_startPoint);
                DrawingCanvas.Children.Add(_activePolyline);
            }
        }

        private void Canvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            System.Windows.Point current = e.GetPosition(DrawingCanvas);

            if (_isDraggingElement && _selectedElement != null)
            {
                double newL = current.X - _dragOffset.X;
                double newT = current.Y - _dragOffset.Y;
                Canvas.SetLeft(_selectedElement, newL);
                Canvas.SetTop(_selectedElement, newT);
                UpdateSelectionHighlight(_selectedElement);
                return;
            }

            if (!_isDrawing) return;

            if (_currentTool == "pencil" || _currentTool == "highlighter")
            {
                _activePolyline?.Points.Add(current);
                return;
            }

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
                case "rect":
                    var rect = new Rectangle
                    {
                        Width = width, Height = height,
                        Stroke = new SolidColorBrush(_currentColor), StrokeThickness = _currentStrokeWidth
                    };
                    Canvas.SetLeft(rect, left); Canvas.SetTop(rect, top);
                    _activePreviewElement = rect;
                    break;
                case "ellipse":
                    var ellipse = new Ellipse
                    {
                        Width = width, Height = height,
                        Stroke = new SolidColorBrush(_currentColor), StrokeThickness = _currentStrokeWidth
                    };
                    Canvas.SetLeft(ellipse, left); Canvas.SetTop(ellipse, top);
                    _activePreviewElement = ellipse;
                    break;
                case "line":
                    _activePreviewElement = new Line
                    {
                        X1 = _startPoint.X, Y1 = _startPoint.Y, X2 = current.X, Y2 = current.Y,
                        Stroke = new SolidColorBrush(_currentColor), StrokeThickness = _currentStrokeWidth
                    };
                    break;
                case "blur":
                    var blurRect = new Rectangle
                    {
                        Width = width, Height = height,
                        Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(220, 30, 41, 59)),
                        Stroke = new SolidColorBrush(_currentColor), StrokeThickness = 1
                    };
                    Canvas.SetLeft(blurRect, left); Canvas.SetTop(blurRect, top);
                    _activePreviewElement = blurRect;
                    break;
            }

            if (_activePreviewElement != null)
            {
                DrawingCanvas.Children.Add(_activePreviewElement);
            }
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingElement = false;

            if (!_isDrawing) return;
            _isDrawing = false;

            if (_activePolyline != null)
            {
                AddAnnotation(_activePolyline);
                _activePolyline = null;
            }
            else if (_activePreviewElement != null)
            {
                DrawingCanvas.Children.Remove(_activePreviewElement);
                AddAnnotation(_activePreviewElement);
                _activePreviewElement = null;
            }
        }

        private void AddAnnotation(UIElement elem)
        {
            _undoStack.Push(new List<UIElement>(_annotations));
            _annotations.Add(elem);
            if (!DrawingCanvas.Children.Contains(elem))
            {
                DrawingCanvas.Children.Add(elem);
            }
        }

        // Factory Methods
        private System.Windows.Shapes.Path CreateArrow(double x1, double y1, double x2, double y2, System.Windows.Media.Color color, double thickness)
        {
            var geom = new PathGeometry();
            var figure = new PathFigure { StartPoint = new System.Windows.Point(x1, y1) };
            figure.Segments.Add(new LineSegment(new System.Windows.Point(x2, y2), true));

            double headLen = Math.Max(12, thickness * 3);
            double angle = Math.Atan2(y2 - y1, x2 - x1);

            System.Windows.Point p1 = new System.Windows.Point(x2 - headLen * Math.Cos(angle - Math.PI / 6), y2 - headLen * Math.Sin(angle - Math.PI / 6));
            System.Windows.Point p2 = new System.Windows.Point(x2 - headLen * Math.Cos(angle + Math.PI / 6), y2 - headLen * Math.Sin(angle + Math.PI / 6));

            figure.Segments.Add(new LineSegment(p1, true));
            figure.Segments.Add(new LineSegment(new System.Windows.Point(x2, y2), true));
            figure.Segments.Add(new LineSegment(p2, true));

            geom.Figures.Add(figure);

            return new System.Windows.Shapes.Path
            {
                Data = geom,
                Stroke = new SolidColorBrush(color),
                Fill = new SolidColorBrush(color),
                StrokeThickness = thickness,
                StrokeLineJoin = PenLineJoin.Round
            };
        }

        private UIElement CreateStepBadge(double x, double y, System.Windows.Media.Color color, int stepNum)
        {
            var grid = new Grid { Width = 32, Height = 32, Cursor = Cursors.SizeAll };
            var ellipse = new Ellipse
            {
                Fill = new SolidColorBrush(color),
                Stroke = Brushes.White, StrokeThickness = 2
            };
            var txt = new TextBlock
            {
                Text = stepNum.ToString(),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold, FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(ellipse);
            grid.Children.Add(txt);

            Canvas.SetLeft(grid, x - 16);
            Canvas.SetTop(grid, y - 16);
            return grid;
        }

        // Editable Text Block Control with Double-Click Inline Editor
        private UIElement CreateEditableTextBlock(string text, double x, double y, System.Windows.Media.Color color, double fontSize)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(210, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(color),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 4, 8, 4),
                Cursor = Cursors.SizeAll
            };

            var tb = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(color),
                FontSize = fontSize,
                FontWeight = FontWeights.Bold
            };

            border.Child = tb;

            border.MouseLeftButtonDown += (s, e) =>
            {
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
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Cyan,
                Padding = new Thickness(4)
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
                if (e.Key == Key.Enter)
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
                var lastState = _undoStack.Pop();
                DrawingCanvas.Children.Clear();
                DrawingCanvas.Children.Add(SelectionBoxBorder);
                _annotations.Clear();
                foreach (var item in lastState)
                {
                    _annotations.Add(item);
                    DrawingCanvas.Children.Add(item);
                }
                ClearSelection();
                TxtStatus.Text = "Действие отменено.";
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            _undoStack.Push(new List<UIElement>(_annotations));
            _annotations.Clear();
            DrawingCanvas.Children.Clear();
            DrawingCanvas.Children.Add(SelectionBoxBorder);
            _currentStepNumber = 1;
            ClearSelection();
            TxtStatus.Text = "Все аннотации очищены.";
        }

        private RenderTargetBitmap RenderCanvasToBitmap()
        {
            SelectionBoxBorder.Visibility = Visibility.Collapsed;

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
            TxtStatus.Text = "Изображение скопировано в системный буфер обмена Windows!";
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
                TxtStatus.Text = $"Файл сохранен: {dlg.FileName}";
            }
        }

        // Global KeyDown (Escape for Reset Tool, Delete / Backspace for Instant Delete)
        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                _isDrawing = false;
                if (_activePreviewElement != null)
                {
                    DrawingCanvas.Children.Remove(_activePreviewElement);
                    _activePreviewElement = null;
                }
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
        }
    }
}
