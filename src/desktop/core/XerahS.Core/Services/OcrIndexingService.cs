#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using XerahS.Common;
using XerahS.History;
using XerahS.Platform.Abstractions;
using SkiaSharp;
using System.Collections.Concurrent;

namespace XerahS.Core.Services;

public static class OcrIndexingService
{
    private const string TagName = "OcrText";
    private const int MaximumQueuedItems = 256;
    private static readonly SemaphoreSlim IndexGate = new(1, 1);
    private static readonly ConcurrentQueue<HistoryItem> IndexQueue = new();
    private static readonly ConcurrentDictionary<long, byte> QueuedItemIds = new();
    private static int _queuedItemCount;
    private static int _queueWorkerRunning;

    public static bool IsEnabled => SettingsManager.Settings.ScreenshotContentSearchEnabled;

    public static HistoryOcrIndexStore CreateStore()
    {
        return new HistoryOcrIndexStore(SettingsManager.GetHistoryFilePath());
    }

    public static void QueueIndexHistoryItem(HistoryItem item)
    {
        if (!IsEnabled || item.Id <= 0 || !IsSearchableImage(item))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(GetOcrTag(item)) && PlatformServices.Ocr?.IsSupported != true)
        {
            return;
        }

        if (!QueuedItemIds.TryAdd(item.Id, 0))
        {
            return;
        }

        if (Interlocked.Increment(ref _queuedItemCount) > MaximumQueuedItems)
        {
            Interlocked.Decrement(ref _queuedItemCount);
            QueuedItemIds.TryRemove(item.Id, out _);
            DebugHelper.WriteLine($"OcrIndexingService queue is full; skipped history item {item.Id}.");
            return;
        }

        IndexQueue.Enqueue(item);
        StartQueueWorker();
    }

    private static void StartQueueWorker()
    {
        if (Interlocked.CompareExchange(ref _queueWorkerRunning, 1, 0) == 0)
        {
            _ = Task.Run(ProcessQueueAsync);
        }
    }

    private static async Task ProcessQueueAsync()
    {
        while (true)
        {
            while (IndexQueue.TryDequeue(out HistoryItem? item))
            {
                Interlocked.Decrement(ref _queuedItemCount);
                try
                {
                    await IndexHistoryItemAsync(item, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    DebugHelper.WriteException(ex, "OcrIndexingService.QueueIndexHistoryItem");
                }
                finally
                {
                    QueuedItemIds.TryRemove(item.Id, out _);
                }
            }

            Interlocked.Exchange(ref _queueWorkerRunning, 0);
            if (IndexQueue.IsEmpty || Interlocked.CompareExchange(ref _queueWorkerRunning, 1, 0) != 0)
            {
                return;
            }
        }
    }

    public static async Task IndexHistoryItemAsync(HistoryItem item, CancellationToken cancellationToken)
    {
        if (!IsEnabled || item.Id <= 0 || !IsSearchableImage(item))
        {
            return;
        }

        string? cachedText = GetOcrTag(item);
        if (!string.IsNullOrWhiteSpace(cachedText))
        {
            await PersistRecognizedTextAsync(item, cachedText, "history-tag", GetConfiguredLanguage(), cancellationToken);
            return;
        }

        await IndexGate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(item.FilePath))
            {
                CreateStore().MarkStatus(item.Id, item.FilePath, "missing_file");
                return;
            }

            IOcrService? ocr = PlatformServices.Ocr;
            if (ocr?.IsSupported != true)
            {
                CreateStore().MarkStatus(item.Id, item.FilePath, "ocr_unavailable");
                return;
            }

            using SKBitmap? bitmap = SKBitmap.Decode(item.FilePath);
            if (bitmap == null)
            {
                CreateStore().MarkStatus(item.Id, item.FilePath, "decode_failed");
                return;
            }

            OcrResult result = await ocr.RecognizeAsync(bitmap, CreateOptions());
            if (result.Success && !string.IsNullOrWhiteSpace(result.Text))
            {
                await PersistRecognizedTextAsync(item, result.Text, "native", GetConfiguredLanguage(), cancellationToken);
                return;
            }

            CreateStore().MarkStatus(item.Id, item.FilePath, result.Success ? "no_text" : "ocr_failed");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "OcrIndexingService.IndexHistoryItemAsync");
            CreateStore().MarkStatus(item.Id, item.FilePath, "index_failed");
        }
        finally
        {
            IndexGate.Release();
        }
    }

    public static Task PersistRecognizedTextAsync(
        HistoryItem item,
        string ocrText,
        string? engine,
        string? language,
        CancellationToken cancellationToken)
    {
        if (item.Id <= 0 || string.IsNullOrWhiteSpace(item.FilePath) || string.IsNullOrWhiteSpace(ocrText))
        {
            return Task.CompletedTask;
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            string normalizedText = NormalizeText(ocrText);
            if (string.IsNullOrWhiteSpace(normalizedText))
            {
                return;
            }

            string historyPath = SettingsManager.GetHistoryFilePath();
            var store = new HistoryOcrIndexStore(historyPath);
            store.UpsertText(item.Id, item.FilePath, null, normalizedText, engine, language);

            if (!string.Equals(GetOcrTag(item), normalizedText, StringComparison.Ordinal))
            {
                item.Tags ??= new Dictionary<string, string?>();
                item.Tags[TagName] = normalizedText;
                item.Tags.Remove("OCRText");

                using var manager = new HistoryManagerSQLite(historyPath);
                manager.Edit(item);
            }
        }, cancellationToken);
    }

    public static string? GetCachedText(HistoryItem item)
    {
        if (item.Id <= 0)
        {
            return GetOcrTag(item);
        }

        return CreateStore().GetText(item.Id) ?? GetOcrTag(item);
    }

    private static bool IsSearchableImage(HistoryItem item)
    {
        return string.Equals(item.Type, "Image", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(item.FilePath) &&
            FileHelpers.IsImageFile(item.FilePath);
    }

    private static OcrOptions CreateOptions()
    {
        var taskOptions = SettingsManager.DefaultTaskSettings?.CaptureSettings?.OCROptions;
        return new OcrOptions
        {
            Language = NormalizeLanguage(taskOptions?.Language),
            Engine = taskOptions?.Engine,
            // Indexing reads every image in the history; none of them goes to an AI provider.
            LocalOnly = true,
            ScaleFactor = NormalizeScaleFactor(taskOptions?.ScaleFactor ?? 2f),
            SingleLine = taskOptions?.SingleLine ?? false
        };
    }

    private static string GetConfiguredLanguage()
    {
        return NormalizeLanguage(SettingsManager.DefaultTaskSettings?.CaptureSettings?.OCROptions?.Language);
    }

    private static string NormalizeLanguage(string? language)
    {
        string? trimmed = language?.Trim();
        return string.IsNullOrEmpty(trimmed) ? "en" : trimmed;
    }

    private static float NormalizeScaleFactor(float scaleFactor)
    {
        return float.IsFinite(scaleFactor) ? Math.Max(scaleFactor, 1f) : 1f;
    }

    private static string? GetOcrTag(HistoryItem item)
    {
        if (item.Tags == null)
        {
            return null;
        }

        return item.Tags.TryGetValue(TagName, out string? ocrText) && !string.IsNullOrWhiteSpace(ocrText)
            ? ocrText
            : item.Tags.TryGetValue("OCRText", out string? legacyText) && !string.IsNullOrWhiteSpace(legacyText)
                ? legacyText
                : null;
    }

    private static string NormalizeText(string text)
    {
        return string.Join(
            Environment.NewLine,
            text.Replace("\r\n", "\n").Replace('\r', '\n')
                .Split('\n')
                .Select(line => string.Join(' ', line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)))
                .Where(line => !string.IsNullOrWhiteSpace(line)));
    }
}
