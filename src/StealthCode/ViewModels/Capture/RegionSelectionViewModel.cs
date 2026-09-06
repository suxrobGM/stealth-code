namespace StealthCode.ViewModels.Capture;

public sealed partial class RegionSelectionViewModel : ViewModelBase
{
    private readonly TaskCompletionSource<(int X, int Y, int Width, int Height)?> tcs = new();

    public Task<(int X, int Y, int Width, int Height)?> ResultTask => tcs.Task;

    public void Complete(int x, int y, int width, int height)
    {
        tcs.TrySetResult((x, y, width, height));
    }

    public void Cancel()
    {
        tcs.TrySetResult(null);
    }
}
