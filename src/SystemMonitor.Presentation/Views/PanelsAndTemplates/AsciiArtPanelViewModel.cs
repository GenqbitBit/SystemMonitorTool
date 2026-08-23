using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SystemMonitor.Domain.AsciiArt;

namespace SystemMonitor.Presentation.Views.PanelsAndTemplates;

public partial class AsciiArtPanelViewModel : ObservableObject, IDisposable
{
    private const int Columns = 52;
    private const int Rows = 28;

    private static readonly Uri FixedArtSourceUri =
        new("avares://SystemMonitor.Presentation/Assets/AsciiArt/source.png");

    private readonly IAsciiArtConverter _converter;
    private readonly IStorageProvider _storageProvider;
    private CancellationTokenSource _loadCts = new();
    private bool _disposed;

    [ObservableProperty] private AsciiCell[,]? currentArt;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private bool isPlaying = true;

    public AsciiArtPanelViewModel(IAsciiArtConverter converter, IStorageProvider storageProvider)
    {
        _converter = converter;
        _storageProvider = storageProvider;
        _ = LoadFixedArtAsync();
    }

    private async Task LoadFixedArtAsync()
    {
        var cancellationToken = BeginLoad();
        ErrorMessage = null;
        IsLoading = true;
        try
        {
            await using var stream = AssetLoader.Open(FixedArtSourceUri);
            await LoadFromStreamAsync(stream, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
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
        if (_disposed) return;

        ErrorMessage = null;
        CancellationToken cancellationToken = default;
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
            cancellationToken = BeginLoad();
            await using var stream = await files[0].OpenReadAsync();
            await LoadFromStreamAsync(stream, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
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

    private CancellationToken BeginLoad()
    {
        _loadCts.Cancel();
        _loadCts.Dispose();
        _loadCts = new CancellationTokenSource();
        return _loadCts.Token;
    }

    private async Task LoadFromStreamAsync(System.IO.Stream stream, CancellationToken cancellationToken)
    {
        using var bitmap = WriteableBitmap.Decode(stream);
        using var pixelSource = new WriteableBitmapPixelSource(bitmap);

        var art = await Task.Run(
            () => _converter.Convert(pixelSource, Columns, Rows),
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        if (_disposed) return;

        CurrentArt = art;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _loadCts.Cancel();
        _loadCts.Dispose();
    }
}