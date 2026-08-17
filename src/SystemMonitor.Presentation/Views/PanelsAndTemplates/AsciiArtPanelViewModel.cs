using System;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SystemMonitor.Domain.AsciiArt;
using Avalonia;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

public partial class AsciiArtPanelViewModel : ObservableObject
{
    private const int Columns = 33;
    private const int Rows = 18;
    private const double CellWidth = 4.0;
    private const double CellHeight = 6.5;

    private static readonly Uri FixedArtSourceUri =
        new("avares://SystemMonitor.Presentation/Assets/AsciiArt/source.png");

    private readonly IAsciiArtConverter _converter;
    private readonly IStorageProvider _storageProvider;

    [ObservableProperty] private AsciiCell[,]? currentArt;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private double rotationCenterX;
    [ObservableProperty] private double rotationCenterY;
    [ObservableProperty] private bool isPlaying = true; 
    [ObservableProperty] private RelativePoint rotationOrigin = new(0.5, 0.5, RelativeUnit.Relative);

    public AsciiArtPanelViewModel(IAsciiArtConverter converter, IStorageProvider storageProvider)
    {
        _converter = converter;
        _storageProvider = storageProvider;
        _ = LoadFixedArtAsync();
    }

    private async Task LoadFixedArtAsync()
    {
        ErrorMessage = null;
        IsLoading = true;
        try
        {
            await using var stream = AssetLoader.Open(FixedArtSourceUri);
            await LoadFromStreamAsync(stream);
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

    [RelayCommand]
    private async Task Upload()
    {
        ErrorMessage = null;
        try
        {
            var files = await _storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select a PNG image",
                AllowMultiple = false,
                FileTypeFilter = new[] { FilePickerFileTypes.ImagePng }
            });

            if (files.Count == 0) return;

            IsLoading = true;
            await using var stream = await files[0].OpenReadAsync();
            await LoadFromStreamAsync(stream);
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

    [RelayCommand]
    private void TogglePlayback() => IsPlaying = !IsPlaying;

    private async Task LoadFromStreamAsync(System.IO.Stream stream)
    {
        using var bitmap = WriteableBitmap.Decode(stream);
        using var pixelSource = new WriteableBitmapPixelSource(bitmap);

        CurrentArt = await Task.Run(() => _converter.Convert(pixelSource, Columns, Rows));

        _ = Dispatcher.UIThread.InvokeAsync(
        () =>
        {
            var (centerX, centerY) = CalculateAsciiContentCenter(CurrentArt);
            RotationOrigin = new RelativePoint(centerX, centerY, RelativeUnit.Absolute);
        },
        DispatcherPriority.Background);
    }

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

        return (centerCol * CellWidth, centerRow * CellHeight);
    }
}