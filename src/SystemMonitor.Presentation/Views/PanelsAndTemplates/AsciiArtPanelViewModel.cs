using System;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using SystemMonitor.Domain.AsciiArt;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

public partial class AsciiArtPanelViewModel : ObservableObject
{
    private const int Columns = 33;
    private const int Rows = 18;
    private const double CellWidth = 3.0;
    private const double CellHeight = 5.0;

    // Fixed, dev-provided image — bundled as an Avalonia resource, not user-uploadable.
    private static readonly Uri FixedArtSourceUri =
        new("avares://SystemMonitor.Presentation/Assets/AsciiArt/source.png");

    private readonly IAsciiArtConverter _converter;

    [ObservableProperty] private AsciiCell[,]? currentArt;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private double rotationCenterX;
    [ObservableProperty] private double rotationCenterY;

    public AsciiArtPanelViewModel(IAsciiArtConverter converter)
    {
        _converter = converter;
        _ = LoadFixedArtAsync();
    }

    private async Task LoadFixedArtAsync()
    {
        ErrorMessage = null;
        IsLoading = true;
        try
        {
            await using var stream = AssetLoader.Open(FixedArtSourceUri);
            using var bitmap = WriteableBitmap.Decode(stream);
            using var pixelSource = new WriteableBitmapPixelSource(bitmap);

            CurrentArt = await Task.Run(() => _converter.Convert(pixelSource, Columns, Rows));

            _ = Dispatcher.UIThread.InvokeAsync(
                () =>
                {
                    var (centerX, centerY) = CalculateAsciiContentCenter(CurrentArt);
                    RotationCenterX = centerX;
                    RotationCenterY = centerY;
                },
                DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load image: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Calculates the center point of the actual ASCII content (non-space characters).
    /// This center is used as the pivot point for rotation.
    /// </summary>
    private (double centerX, double centerY) CalculateAsciiContentCenter(AsciiCell[,]? art)
    {
        if (art == null || art.Length == 0)
            return (0, 0);

        int rows = art.GetLength(0);
        int cols = art.GetLength(1);

        int minRow = rows, maxRow = -1;
        int minCol = cols, maxCol = -1;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                if (art[row, col].Glyph != ' ')
                {
                    minRow = Math.Min(minRow, row);
                    maxRow = Math.Max(maxRow, row);
                    minCol = Math.Min(minCol, col);
                    maxCol = Math.Max(maxCol, col);
                }
            }
        }

        if (maxRow < 0)
            return (Columns * CellWidth / 2.0, Rows * CellHeight / 2.0);

        double centerCol = (minCol + maxCol) / 2.0;
        double centerRow = (minRow + maxRow) / 2.0;

        double centerX = centerCol * CellWidth;
        double centerY = centerRow * CellHeight;

        return (centerX, centerY);
    }
}