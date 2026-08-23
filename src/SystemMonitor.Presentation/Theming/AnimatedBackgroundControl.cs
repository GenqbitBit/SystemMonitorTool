using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImageSharpImage = SixLabors.ImageSharp.Image;
using AvaloniaImage = Avalonia.Controls.Image;

namespace SystemMonitor.Presentation.Theming;

public sealed class AnimatedBackgroundControl : UserControl, IDisposable
{
    private const int CacheCapacity = 1;
    private const int MaxBackgroundWidth = 800;
    private const int MaxBackgroundHeight = 500;
    private const int MinimumFrameDelayMilliseconds = 80;
    private const int MaximumFrameDelayMilliseconds = 250;

    private readonly AvaloniaImage _image = new();
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Render);
    private readonly Dictionary<string, FrameSet> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _loadTasksLock = new();
    private readonly List<Task> _loadTasks = new();
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private FrameSet? _currentFrameSet;
    private FrameSet? _displayedFrameSet;
    private string? _currentAssetName;
    private CancellationTokenSource? _loadCancellation;
    private Task? _loadTask;
    private int _frameIndex;
    private bool _disposed;

    public static readonly StyledProperty<string?> AssetNameProperty =
        AvaloniaProperty.Register<AnimatedBackgroundControl, string?>(nameof(AssetName));

    public string? AssetName
    {
        get => GetValue(AssetNameProperty);
        set => SetValue(AssetNameProperty, value);
    }

    static AnimatedBackgroundControl()
    {
        AssetNameProperty.Changed.AddClassHandler<AnimatedBackgroundControl>((control, change) =>
        {
            if (change.NewValue is string assetName && !string.IsNullOrWhiteSpace(assetName))
                control.SetAsset(assetName);
        });
    }

    public AnimatedBackgroundControl()
    {
        Content = _image;
        _image.Stretch = Avalonia.Media.Stretch.UniformToFill;
        _timer.Tick += OnTimerTick;
    }

    public void SetAsset(string assetName)
    {
        if (_disposed)
            return;

        var cancellation = new CancellationTokenSource();
        var previousCancellation = Interlocked.Exchange(ref _loadCancellation, cancellation);
        try
        {
            previousCancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        // 1. Instant cache hit check
        if (_cache.TryGetValue(assetName, out var cached))
        {
            DisposeCancellationSource(cancellation);
            ApplyFrameSet(cached, assetName);
            TrimCache();
            return;
        }

        // 2. Create an empty frame set container and switch to it IMMEDIATELY
        var newFrameSet = new FrameSet();
        _cache[assetName] = newFrameSet;
        ApplyFrameSet(newFrameSet, assetName);

        // 3. Stream frames progressively in background thread
        var loadTask = StreamFramesAsync(assetName, newFrameSet, cancellation);
        lock (_loadTasksLock)
            _loadTasks.Add(loadTask);

        _loadTask = loadTask;
        _ = loadTask.ContinueWith(
            completedTask =>
            {
                lock (_loadTasksLock)
                    _loadTasks.Remove(completedTask);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        TrimCache();
    }

    private async Task StreamFramesAsync(
        string assetName,
        FrameSet frameSet,
        CancellationTokenSource cancellationSource)
    {
        var cancellation = cancellationSource.Token;
        var enteredLoadGate = false;

        try
        {
            await _loadGate.WaitAsync(cancellation).ConfigureAwait(false);
            enteredLoadGate = true;

            await Task.Run(async () =>
            {
                cancellation.ThrowIfCancellationRequested();

                var assetPath = string.Join(
                    "/",
                    assetName.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries)
                        .Select(Uri.EscapeDataString));
                var uri = new Uri($"avares://SystemMonitor.Presentation/Assets/{assetPath}");
                using Stream stream = AssetLoader.Open(uri);
                using var source = await ImageSharpImage.LoadAsync<Bgra32>(stream, cancellation);
                cancellation.ThrowIfCancellationRequested();
                var isGif = Path.GetExtension(assetName).Equals(".gif", StringComparison.OrdinalIgnoreCase);

                var scale = Math.Min(1.0, Math.Min(
                    (double)MaxBackgroundWidth / source.Width,
                    (double)MaxBackgroundHeight / source.Height));

                var targetWidth = (int)Math.Max(1, source.Width * scale);
                var targetHeight = (int)Math.Max(1, source.Height * scale);

                using var canvas = new SixLabors.ImageSharp.Image<Bgra32>(targetWidth, targetHeight);
                var pixels = new byte[targetWidth * targetHeight * 4];

                var index = 0;
                var firstFramePosted = false;
                while (index < source.Frames.Count)
                {
                    cancellation.ThrowIfCancellationRequested();

                    var delay = isGif
                        ? source.Frames[index].Metadata.GetGifMetadata().FrameDelay
                        : 0;
                    using var frame = source.Frames.CloneFrame(index);
                    if (source.Frames.Count > 1)
                        source.Frames.RemoveFrame(index);
                    else
                        index++;

                    cancellation.ThrowIfCancellationRequested();
                    frame.Mutate(ctx => ctx.Resize(targetWidth, targetHeight));
                    cancellation.ThrowIfCancellationRequested();
                    canvas.Mutate(ctx => ctx.DrawImage(frame, 1.0f));
                    cancellation.ThrowIfCancellationRequested();

                    var writeableBitmap = new WriteableBitmap(
                        new PixelSize(canvas.Width, canvas.Height),
                        new Vector(96, 96),
                        PixelFormat.Bgra8888,
                        AlphaFormat.Premul);

                    var ownsBitmap = true;
                    try
                    {
                        using (var buffer = writeableBitmap.Lock())
                        {
                            cancellation.ThrowIfCancellationRequested();
                            canvas.CopyPixelDataTo(pixels);
                            Marshal.Copy(pixels, 0, buffer.Address, pixels.Length);
                        }

                        var delayMs = Math.Clamp(
                            isGif && delay > 0 ? delay * 10 : 100,
                            MinimumFrameDelayMilliseconds,
                            MaximumFrameDelayMilliseconds);

                        if (!frameSet.TryAddFrame(writeableBitmap, TimeSpan.FromMilliseconds(delayMs), cancellation))
                        {
                            cancellation.ThrowIfCancellationRequested();
                            continue;
                        }

                        ownsBitmap = false;

                        if (!firstFramePosted)
                        {
                            firstFramePosted = true;
                            Dispatcher.UIThread.Post(() =>
                            {
                                if (!_disposed && ReferenceEquals(_currentFrameSet, frameSet))
                                {
                                    _image.Source = frameSet.GetFrame(0);
                                    _displayedFrameSet = frameSet;
                                    TrimCache();
                                    if (!_timer.IsEnabled)
                                    {
                                        _timer.Interval = TimeSpan.FromMilliseconds(delayMs);
                                        _timer.Start();
                                    }
                                }
                            });
                        }
                    }
                    finally
                    {
                        if (ownsBitmap)
                            writeableBitmap.Dispose();
                    }
                }

                frameSet.IsFullyLoaded = true;
            }, cancellation).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Background theme '{assetName}' streaming failed: {ex}");
        }
        finally
        {
            if (enteredLoadGate)
                _loadGate.Release();

            frameSet.DisposeIfIncomplete(_displayedFrameSet);
            DisposeCancellationSource(cancellationSource);
        }
    }

    private void DisposeCancellationSource(CancellationTokenSource cancellationSource)
    {
        Interlocked.CompareExchange(ref _loadCancellation, null, cancellationSource);
        cancellationSource.Dispose();
    }

    private void ApplyFrameSet(FrameSet frameSet, string assetName)
    {
        _timer.Stop();
        _currentFrameSet = frameSet;
        _currentAssetName = assetName;
        _frameIndex = 0;
        _timer.Interval = TimeSpan.FromMilliseconds(MinimumFrameDelayMilliseconds);

        var frames = frameSet.GetFramesSnapshot();
        if (frames.Count > 0)
        {
            _image.Source = frames[0];
            _displayedFrameSet = frameSet;
            _timer.Interval = frameSet.DelaysSnapshot[0];
            _timer.Start();
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_currentFrameSet is null)
            return;

        var frames = _currentFrameSet.GetFramesSnapshot();
        var delays = _currentFrameSet.DelaysSnapshot;

        if (frames.Count == 0)
        {
            _timer.Interval = TimeSpan.FromMilliseconds(MinimumFrameDelayMilliseconds);
            return;
        }

        _frameIndex = (_frameIndex + 1) % frames.Count;

        _image.Source = frames[_frameIndex];
        _timer.Interval = delays[_frameIndex];
    }

    private void TrimCache()
    {
        while (_cache.Count > CacheCapacity)
        {
            string? keyToEvict = null;
            foreach (var entry in _cache)
            {
                if (string.Equals(entry.Key, _currentAssetName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (ReferenceEquals(entry.Value, _displayedFrameSet))
                    continue;

                keyToEvict = entry.Key;
                break;
            }

            if (keyToEvict is null)
                break;

            var frameSet = _cache[keyToEvict];
            _cache.Remove(keyToEvict);
            frameSet.Dispose();
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _image.Source = null;
        _loadCancellation?.Cancel();
        _timer.Stop();

        try
        {
            _loadTask?.GetAwaiter().GetResult();

            Task[] remainingTasks;
            lock (_loadTasksLock)
                remainingTasks = _loadTasks.ToArray();

            Task.WaitAll(remainingTasks);
        }
        catch (OperationCanceledException)
        {
        }

        foreach (var frameSet in _cache.Values)
            frameSet.Dispose();

        _cache.Clear();
        _currentFrameSet = null;
        _displayedFrameSet = null;
        _currentAssetName = null;
        _loadCancellation = null;
        _loadTask = null;
        _loadGate.Dispose();
    }

    private sealed class FrameSet : IDisposable
    {
        private readonly object _lock = new();
        private readonly List<Bitmap> _frames = new();
        private readonly List<TimeSpan> _delays = new();

        public bool IsFullyLoaded { get; set; }

        public bool TryAddFrame(Bitmap frame, TimeSpan delay, CancellationToken cancellation)
        {
            lock (_lock)
            {
                if (_disposed || cancellation.IsCancellationRequested)
                    return false;

                _frames.Add(frame);
                _delays.Add(delay);
                return true;
            }
        }

        public Bitmap GetFrame(int index)
        {
            lock (_lock)
            {
                return _frames[index];
            }
        }

        public List<Bitmap> GetFramesSnapshot()
        {
            lock (_lock)
            {
                return new List<Bitmap>(_frames);
            }
        }

        public List<TimeSpan> DelaysSnapshot
        {
            get
            {
                lock (_lock)
                {
                    return new List<TimeSpan>(_delays);
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                    return;

                _disposed = true;
                foreach (var frame in _frames)
                    frame.Dispose();

                _frames.Clear();
                _delays.Clear();
            }
        }

        public void DisposeIfIncomplete(FrameSet? displayedFrameSet)
        {
            if (!IsFullyLoaded && !ReferenceEquals(this, displayedFrameSet))
                Dispose();
        }

        private bool _disposed;
    }
}