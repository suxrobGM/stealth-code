using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using StealthCode.ViewModels;

namespace StealthCode.Views;

// ReSharper disable once PartialTypeWithSinglePart
public partial class RegionSelectionWindow : Window
{
    private readonly RegionSelectionViewModel viewModel = new();
    private Point startPoint;
    private Rectangle? selectionRect;
    private bool isDragging;

    public RegionSelectionWindow()
    {
        DataContext = viewModel;
        InitializeComponent();
        Closed += (_, _) => viewModel.Cancel();
    }

    public Task<(int X, int Y, int Width, int Height)?> GetSelectionAsync() => viewModel.ResultTask;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            viewModel.Cancel();
            Close();
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        startPoint = e.GetPosition(SelectionCanvas);
        isDragging = true;

        if (selectionRect != null)
        {
            SelectionCanvas.Children.Remove(selectionRect);
        }

        selectionRect = new Rectangle
        {
            Stroke = new SolidColorBrush(Color.Parse("#10B981")),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.Parse("#2010B981"))
        };

        Canvas.SetLeft(selectionRect, startPoint.X);
        Canvas.SetTop(selectionRect, startPoint.Y);
        SelectionCanvas.Children.Add(selectionRect);

        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!isDragging || selectionRect == null)
        {
            return;
        }

        var currentPoint = e.GetPosition(SelectionCanvas);

        var x = Math.Min(startPoint.X, currentPoint.X);
        var y = Math.Min(startPoint.Y, currentPoint.Y);
        var w = Math.Abs(currentPoint.X - startPoint.X);
        var h = Math.Abs(currentPoint.Y - startPoint.Y);

        Canvas.SetLeft(selectionRect, x);
        Canvas.SetTop(selectionRect, y);
        selectionRect.Width = w;
        selectionRect.Height = h;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!isDragging || selectionRect == null)
        {
            return;
        }

        isDragging = false;

        var currentPoint = e.GetPosition(SelectionCanvas);
        var x = Math.Min(startPoint.X, currentPoint.X);
        var y = Math.Min(startPoint.Y, currentPoint.Y);
        var w = Math.Abs(currentPoint.X - startPoint.X);
        var h = Math.Abs(currentPoint.Y - startPoint.Y);

        if (w < 10 || h < 10)
        {
            return;
        }

        // v12 removed IRenderRoot, so VisualRoot no longer exposes RenderScaling.
        // This window is itself the TopLevel, so read it directly.
        var scaling = RenderScaling;
        viewModel.Complete(
            (int)(x * scaling),
            (int)(y * scaling),
            (int)(w * scaling),
            (int)(h * scaling));
        Close();
    }
}
