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
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using XerahS.Common;
using XerahS.Common.Converters;
using XerahS.Bootstrap;
using XerahS.Core;
using XerahS.Core.Cloud;
using XerahS.Core.Managers;
using XerahS.Core.Services;
using XerahS.History;
using XerahS.Media;
using XerahS.Platform.Abstractions;
using XerahS.Services.Abstractions;
using XerahS.UI.Services;
using ShareX.ImageEditor.Core.Persistence;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace XerahS.UI.ViewModels
{
    public partial class HistoryViewModel : ViewModelBase, IDisposable
    {
        private static readonly HashSet<string> CombinableImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".gif",
            ".bmp",
            ".webp",
            ".ico",
            ".tif",
            ".tiff"
        };

        // Converter to load thumbnail from file path (resource-efficient)
        public static IValueConverter ThumbnailConverter { get; } = new FuncValueConverter<string?, Bitmap?>(
            filePath =>
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    return null;

                try
                {
                    // Check if it's an image file
                    var ext = Path.GetExtension(filePath).ToLowerInvariant();
                    if (ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".gif" && ext != ".bmp" && ext != ".webp")
                        return null;

                    // Load with decode size for memory efficiency (thumbnail size)
                    using var stream = File.OpenRead(filePath);
                    return Bitmap.DecodeToWidth(stream, 180); // Decode to thumbnail width
                }
                catch
                {
                    return null;
                }
            });

        [ObservableProperty]
        private ObservableCollection<HistoryItem> _historyItems;

        [ObservableProperty]
        private bool _isGridView = true;

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private bool _isCombiningSelection = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsSearchActive))]
        private string _searchText = string.Empty;

        private int _searchVersion;
        private int _reloadRequested;

        public ObservableCollection<HistoryItem> SelectedHistoryItems { get; } = new();

        public bool CanCombineSelectedImages => GetSelectedCombinableHistoryItems().Count >= 2;

        public bool ShowCombineActions => CanCombineSelectedImages;

        public int SelectedImageCount => GetSelectedCombinableHistoryItems().Count;

        public bool IsSearchActive => !string.IsNullOrWhiteSpace(SearchText);

        public string CombineSelectionSummary => SelectedImageCount == 1
            ? "1 image selected"
            : $"{SelectedImageCount} images selected";



        // Pagination Properties
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanGoNext))]
        [NotifyPropertyChangedFor(nameof(CanGoPrevious))]
        [NotifyPropertyChangedFor(nameof(PageInfo))]
        private int _currentPage = 1;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanGoNext))]
        [NotifyPropertyChangedFor(nameof(PageInfo))]
        private int _totalPages = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PageInfo))]
        private int _totalItems = 0;

        [ObservableProperty]
        private int _pageSize = 50;

        public bool CanGoNext => CurrentPage < TotalPages;
        public bool CanGoPrevious => CurrentPage > 1;

        public string PageInfo => $"Page {CurrentPage} of {Math.Max(1, TotalPages)} ({TotalItems} items)"; // Prevent "Page 1 of 0" looking weird

        private readonly HistoryManagerSQLite _historyManager;
        private readonly IDesktopTaskManager _taskManager;
        private readonly IDialogService _coreDialogService;
        private readonly IXerahSCloudClient? _cloudClient;
        private readonly IXerahSCloudOAuthCoordinator? _cloudOAuthCoordinator;

        public HistoryViewModel(
            IDesktopTaskManager taskManager,
            IDialogService coreDialogService,
            bool autoLoadHistory = true,
            IXerahSCloudClient? cloudClient = null,
            IXerahSCloudOAuthCoordinator? cloudOAuthCoordinator = null)
        {
            _taskManager = taskManager;
            _coreDialogService = coreDialogService;
            _cloudClient = cloudClient;
            _cloudOAuthCoordinator = cloudOAuthCoordinator;
            HistoryItems = new ObservableCollection<HistoryItem>();
            SelectedHistoryItems.CollectionChanged += (_, _) => NotifySelectionStateChanged();

            // Create history manager with centralized path
            var historyPath = SettingsManager.GetHistoryFilePath();
            DebugHelper.WriteLine($"HistoryViewModel - History file path: {historyPath}");

            _historyManager = new HistoryManagerSQLite(historyPath);

            // Configure backup settings similar to JSON files
            _historyManager.BackupFolder = SettingsManager.HistoryBackupFolder;
            _historyManager.CreateBackup = true;
            _historyManager.CreateWeeklyBackup = true;

            // Start loading history asynchronously WITHOUT blocking UI
            // Use fire-and-forget to let view display immediately
            if (autoLoadHistory)
            {
                _ = BeginHistoryLoadAsync();
            }
        }

        internal string? CurrentCloudOwnerSubject => _cloudClient?.CurrentOwnerSubject;

        internal HistoryItem? GetHistoryItem(long id) => _historyManager.GetHistoryItem(id);

        /// <summary>
        /// Starts history loading asynchronously without blocking the UI thread.
        /// This allows the empty panel to display immediately.
        /// </summary>
        private async Task BeginHistoryLoadAsync()
        {
            // Small delay to allow UI to render the empty history view first
            await Task.Delay(100);
            await LoadHistoryAsync();
        }

        /// <summary>Reloads the current page, e.g. after a finished task added a history entry.</summary>
        public Task ReloadAsync()
        {
            if (SelectedHistoryItems.Count > 0)
            {
                Interlocked.Exchange(ref _selectionToRestore, SelectedHistoryItems.Select(item => item.Id).ToHashSet());
            }

            return LoadHistoryAsync();
        }

        private HashSet<long>? _selectionToRestore;

        /// <summary>Raised after a reload that kept the user's selection; carries the re-matched items.</summary>
        public event Action<IReadOnlyList<HistoryItem>>? SelectionRestored;

        [RelayCommand]
        private async Task LoadHistoryAsync()
        {
            if (IsLoading)
            {
                Interlocked.Exchange(ref _reloadRequested, 1);
                return;
            }

            IsLoading = true;
            try
            {
                var historyPath = SettingsManager.GetHistoryFilePath();
                DebugHelper.WriteLine($"History.xml location: {historyPath} (exists={File.Exists(historyPath)})");

                List<HistoryItem> items;
                string query = SearchText.Trim();
                if (string.IsNullOrWhiteSpace(query))
                {
                    TotalItems = await _historyManager.GetTotalCountAsync();
                    TotalPages = (int)Math.Ceiling((double)TotalItems / PageSize);
                    if (TotalPages == 0) TotalPages = 1; // Ensure at least 1 page even if empty

                    // Adjust CurrentPage if out of bounds (e.g. after deletion)
                    if (CurrentPage > TotalPages) CurrentPage = TotalPages;
                    if (CurrentPage < 1) CurrentPage = 1;

                    int offset = (CurrentPage - 1) * PageSize;
                    items = await _historyManager.GetHistoryItemsAsync(offset, PageSize);
                }
                else
                {
                    int requestedPage = Math.Max(CurrentPage, 1);
                    int offset = (requestedPage - 1) * PageSize;
                    (items, TotalItems) = await SearchHistoryItemsAsync(query, offset, PageSize);
                    TotalPages = (int)Math.Ceiling((double)TotalItems / PageSize);
                    if (TotalPages == 0) TotalPages = 1;

                    if (requestedPage > TotalPages)
                    {
                        CurrentPage = TotalPages;
                        offset = (CurrentPage - 1) * PageSize;
                        (items, TotalItems) = await SearchHistoryItemsAsync(query, offset, PageSize);
                    }
                    if (CurrentPage < 1) CurrentPage = 1;
                }

                if (!string.Equals(query, SearchText.Trim(), StringComparison.Ordinal))
                {
                    Interlocked.Exchange(ref _reloadRequested, 1);
                }
                else
                {
                    HashSet<long>? restoreIds = Interlocked.Exchange(ref _selectionToRestore, null);
                    ClearSelectedHistoryItems();
                    HistoryItems = new ObservableCollection<HistoryItem>(items);

                    if (restoreIds != null)
                    {
                        List<HistoryItem> restored = items.Where(item => restoreIds.Contains(item.Id)).ToList();

                        if (restored.Count > 0)
                        {
                            SetSelectedHistoryItems(restored);
                            SelectionRestored?.Invoke(restored);
                        }
                    }

                    DebugHelper.WriteLine($"History loaded: {items.Count} items (Page {CurrentPage}/{TotalPages})");

                }
            }

            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to load history");
            }
            finally
            {
                IsLoading = false;
            }

            if (Interlocked.Exchange(ref _reloadRequested, 0) != 0)
            {
                await LoadHistoryAsync();
            }
        }

        [RelayCommand]
        private async Task SearchHistoryAsync()
        {
            Interlocked.Increment(ref _searchVersion);
            CurrentPage = 1;
            await LoadHistoryAsync();
        }

        [RelayCommand]
        private async Task ClearSearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return;
            }

            SearchText = string.Empty;
            Interlocked.Increment(ref _searchVersion);
            CurrentPage = 1;
            await LoadHistoryAsync();
        }

        partial void OnSearchTextChanged(string value)
        {
            CurrentPage = 1;
            int version = Interlocked.Increment(ref _searchVersion);
            _ = DebouncedSearchAsync(version);
        }

        private async Task DebouncedSearchAsync(int version)
        {
            await Task.Delay(300);
            if (version == _searchVersion)
            {
                await LoadHistoryAsync();
            }
        }

        private Task<(List<HistoryItem> Items, int TotalCount)> SearchHistoryItemsAsync(string query, int offset, int limit)
        {
            return Task.Run(() => _historyManager.SearchHistoryItems(query, offset, limit));
        }

        [RelayCommand]
        private async Task NextPage()
        {
            if (CanGoNext)
            {
                CurrentPage++;
                await LoadHistoryAsync();
            }
        }

        [RelayCommand]
        private async Task PreviousPage()
        {
            if (CanGoPrevious)
            {
                CurrentPage--;
                await LoadHistoryAsync();
            }
        }

        [RelayCommand]
        private void ToggleView()
        {
            IsGridView = !IsGridView;
        }

        [RelayCommand]
        private async Task RefreshHistory()
        {
            await LoadHistoryAsync();
        }

        [RelayCommand]
        private Task CombineHorizontalAsync()
        {
            return CombineSelectedImagesAsync(ImageCombinerOrientation.Horizontal);
        }

        [RelayCommand]
        private Task CombineVerticalAsync()
        {
            return CombineSelectedImagesAsync(ImageCombinerOrientation.Vertical);
        }

        [RelayCommand]
        private async Task EditImage(HistoryItem? item)
        {
            await OpenImageInEditorAsync(item, preferAnnotations: false);
        }

        [RelayCommand]
        private async Task EditAnnotations(HistoryItem? item)
        {
            await OpenImageInEditorAsync(item, preferAnnotations: true);
        }

        private async Task OpenImageInEditorAsync(HistoryItem? item, bool preferAnnotations)
        {
            if (item == null || string.IsNullOrEmpty(item.FilePath)) return;
            if (!File.Exists(item.FilePath)) return;

            try
            {
                FileContentSnapshot originalImageSnapshot = FileContentSnapshot.Create(item.FilePath);
                FileContentSnapshot originalSidecarSnapshot = FileContentSnapshot.Create(ResolveAnnotationSidecarPath(item));

                if (preferAnnotations)
                {
                    string? sidecarPath = ResolveAnnotationSidecarPath(item);
                    if (!string.IsNullOrWhiteSpace(sidecarPath))
                    {
                        try
                        {
                            var project = await XannProjectFileService.LoadAsync(sidecarPath, item.FilePath);
                            SKBitmap sessionImage = project.SourceImage;

                            try
                            {
                                if (!project.ImageHashMatches)
                                {
                                    DebugHelper.WriteLine($"Annotation sidecar hash mismatch for '{item.FilePath}'. Loading current file image for the editor session.");
                                    sessionImage = DecodeImageFile(item.FilePath)
                                        ?? throw new InvalidOperationException($"Failed to decode current image file '{item.FilePath}'.");
                                }

                                var sessionResult = await XerahS.Platform.Abstractions.PlatformServices.UI.ShowEditorSessionAsync(
                                    sessionImage,
                                    item.FilePath,
                                    annotations: project.Project.Annotations,
                                    restoredAnnotations: true);

                                try
                                {
                                    await PersistAnnotationSessionResultAsync(item.FilePath, sessionResult);
                                }
                                finally
                                {
                                    sessionResult?.RenderedImage.Dispose();
                                    sessionResult?.SourceImage?.Dispose();
                                }
                            }
                            finally
                            {
                                if (!ReferenceEquals(sessionImage, project.SourceImage))
                                {
                                    sessionImage.Dispose();
                                }

                                project.SourceImage.Dispose();
                            }

                            await RefreshHistoryItemAfterEditorSessionAsync(item, originalImageSnapshot, originalSidecarSnapshot);
                            return;
                        }
                        catch (Exception ex)
                        {
                            DebugHelper.WriteLine($"Failed to load annotation sidecar '{sidecarPath}': {ex.Message}");
                            DebugHelper.WriteException(ex);
                        }
                    }
                }

                // Load the image from file directly as SKBitmap and close the file before launching the editor.
                // The editor may save back to the same source path, which would fail on platforms that enforce file-share locks.
                using var skBitmap = DecodeImageFile(item.FilePath);
                if (skBitmap == null) return;

                // Open in Editor using the platform service
                var rendered = await XerahS.Platform.Abstractions.PlatformServices.UI.ShowEditorAsync(skBitmap, item.FilePath);
                if (rendered != null && !ReferenceEquals(rendered, skBitmap))
                {
                    rendered.Dispose();
                }

                await RefreshHistoryItemAfterEditorSessionAsync(item, originalImageSnapshot, originalSidecarSnapshot);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Failed to open image in editor: {ex.Message}");
            }
        }

        private static SKBitmap? DecodeImageFile(string filePath)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return SKBitmap.Decode(stream);
        }

        private static string? ResolveAnnotationSidecarPath(HistoryItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.AnnotationSidecarPath) && File.Exists(item.AnnotationSidecarPath))
            {
                return item.AnnotationSidecarPath;
            }

            string defaultSidecarPath = XannProjectFileService.GetDefaultSidecarPath(item.FilePath);
            return File.Exists(defaultSidecarPath) ? defaultSidecarPath : null;
        }

        private static async Task PersistAnnotationSessionResultAsync(string imagePath, ShareX.ImageEditor.Hosting.ImageEditorSessionResult? sessionResult)
        {
            if (sessionResult == null || string.IsNullOrWhiteSpace(imagePath))
            {
                return;
            }

            if (sessionResult.Annotations.Count == 0)
            {
                bool deleted = XannProjectFileService.TryDeleteSidecar(imagePath);
                if (deleted)
                {
                    DebugHelper.WriteLine($"Deleted annotation sidecar for '{imagePath}' because the session returned no annotations.");
                }

                return;
            }

            if (sessionResult.SourceImage == null)
            {
                DebugHelper.WriteLine($"Skipped saving annotation sidecar for '{imagePath}' because the editor returned annotations without a source image.");
                return;
            }

            string? sidecarPath = await XannProjectFileService.SaveAsync(
                imagePath,
                sessionResult.SourceImage,
                sessionResult.Annotations);
            DebugHelper.WriteLine($"Saved annotation sidecar after editor continue: {sidecarPath}");
        }

        private async Task RefreshHistoryItemAfterEditorSessionAsync(
            HistoryItem item,
            FileContentSnapshot originalImageSnapshot,
            FileContentSnapshot originalSidecarSnapshot)
        {
            if (string.IsNullOrWhiteSpace(item.FilePath) || !File.Exists(item.FilePath))
            {
                return;
            }

            bool imageFileChanged = !FileContentSnapshot.Create(item.FilePath).Equals(originalImageSnapshot);
            bool sidecarPathChanged = SynchronizeAnnotationSidecarPath(item);
            bool sidecarContentChanged = !FileContentSnapshot.Create(item.AnnotationSidecarPath).Equals(originalSidecarSnapshot);

            if (!imageFileChanged && !sidecarPathChanged && !sidecarContentChanged)
            {
                return;
            }

            if (item.Id > 0)
            {
                await Task.Run(() => _historyManager.Edit(item));
            }

            int index = HistoryItems.IndexOf(item);
            if (index < 0)
            {
                return;
            }

            bool wasSelected = SelectedHistoryItems.Contains(item);
            var refreshedItem = CloneHistoryItem(item);
            HistoryItems[index] = refreshedItem;

            if (wasSelected)
            {
                SelectedHistoryItems.Remove(item);
                SelectedHistoryItems.Add(refreshedItem);
                NotifySelectionStateChanged();
            }
        }

        private static bool SynchronizeAnnotationSidecarPath(HistoryItem item)
        {
            string? originalSidecarPath = item.AnnotationSidecarPath;
            string? refreshedSidecarPath = null;

            if (!string.IsNullOrWhiteSpace(originalSidecarPath) && File.Exists(originalSidecarPath))
            {
                refreshedSidecarPath = originalSidecarPath;
            }
            else if (!string.IsNullOrWhiteSpace(item.FilePath))
            {
                string defaultSidecarPath = XannProjectFileService.GetDefaultSidecarPath(item.FilePath);
                if (File.Exists(defaultSidecarPath))
                {
                    refreshedSidecarPath = defaultSidecarPath;
                }
            }

            if (string.Equals(originalSidecarPath, refreshedSidecarPath, StringComparison.Ordinal))
            {
                return false;
            }

            item.AnnotationSidecarPath = refreshedSidecarPath;
            return true;
        }

        private static HistoryItem CloneHistoryItem(HistoryItem item)
        {
            return new HistoryItem
            {
                Id = item.Id,
                FileName = item.FileName,
                FilePath = item.FilePath,
                DateTime = item.DateTime,
                Type = item.Type,
                Host = item.Host,
                URL = item.URL,
                ThumbnailURL = item.ThumbnailURL,
                DeletionURL = item.DeletionURL,
                ShortenedURL = item.ShortenedURL,
                AnnotationSidecarPath = item.AnnotationSidecarPath,
                Tags = item.Tags != null
                    ? new Dictionary<string, string?>(item.Tags, StringComparer.Ordinal)
                    : new Dictionary<string, string?>()
            };
        }

        private readonly record struct FileContentSnapshot(bool Exists, long Length, DateTime LastWriteTimeUtc)
        {
            public static FileContentSnapshot Create(string? path)
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return default;
                }

                var fileInfo = new FileInfo(path);
                return new FileContentSnapshot(true, fileInfo.Length, fileInfo.LastWriteTimeUtc);
            }
        }

        [RelayCommand]
        private void OpenFile(HistoryItem? item)
        {
            if (item == null || string.IsNullOrEmpty(item.FilePath)) return;
            if (!File.Exists(item.FilePath)) return;

            try
            {
                XerahS.Platform.Abstractions.PlatformServices.System.OpenFile(item.FilePath);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Failed to open file: {ex.Message}");
            }
        }

        [RelayCommand]
        private void OpenFolder(HistoryItem? item)
        {
            if (item == null || string.IsNullOrEmpty(item.FilePath)) return;

            XerahS.Platform.Abstractions.PlatformServices.System.ShowFileInExplorer(item.FilePath);
        }

        [RelayCommand]
        private async Task UploadItem(HistoryItem? item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.FilePath))
            {
                return;
            }

            if (!File.Exists(item.FilePath))
            {
                DebugHelper.WriteLine($"HistoryViewModel - Upload skipped, file not found: {item.FilePath}");
                return;
            }

            try
            {
                var settings = GetUploadTaskSettings();
                settings.Job = WorkflowType.FileUpload;
                await _taskManager.StartFileTask(settings, item.FilePath);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "HistoryViewModel - UploadItem failed");
            }
        }

        /// <summary>
        /// The entries a copy action applies to: the whole selection when the clicked entry is part of a
        /// multi-selection, otherwise just the clicked entry.
        /// </summary>
        internal IReadOnlyList<HistoryItem> GetCopyTargets(HistoryItem item)
        {
            return SelectedHistoryItems.Count > 1 && SelectedHistoryItems.Contains(item)
                ? SelectedHistoryItems.ToList()
                : new List<HistoryItem> { item };
        }

        internal bool IsInMultiSelection(HistoryItem item) => SelectedHistoryItems.Count > 1 && SelectedHistoryItems.Contains(item);

        /// <summary>One value per line, in the order shown in History; blank values are skipped.</summary>
        internal static string JoinLines(IEnumerable<string?> values) =>
            string.Join(Environment.NewLine, values.Where(value => !string.IsNullOrEmpty(value)));

        public bool HasMultipleSelection => SelectedHistoryItems.Count > 1;
        public int SelectedPathCount => SelectedHistoryItems.Count(item => !string.IsNullOrEmpty(item.FilePath));
        public int SelectedUrlCount => SelectedHistoryItems.Count(item => !string.IsNullOrEmpty(item.URL));
        public bool CanCopySelectedPaths => SelectedPathCount > 0;
        public bool CanCopySelectedUrls => SelectedUrlCount > 0;
        public string SelectionSummary => $"{SelectedHistoryItems.Count} selected";

        private static async Task CopyTextAsync(string text, string what)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            try
            {
                if (PlatformServices.IsInitialized)
                {
                    await PlatformServices.Clipboard.SetTextAsync(text);
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Failed to copy {what}: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task CopyFilePath(HistoryItem? item)
        {
            if (item == null) return;

            await CopyTextAsync(JoinLines(GetCopyTargets(item).Select(target => target.FilePath)), "file path");
        }

        [RelayCommand]
        private async Task CopyURL(HistoryItem? item)
        {
            if (item == null) return;

            await CopyTextAsync(JoinLines(GetCopyTargets(item).Select(target => target.URL)), "URL");
        }

        [RelayCommand]
        private Task CopySelectedPaths() => CopyTextAsync(JoinLines(SelectedHistoryItems.Select(item => item.FilePath)), "file paths");

        [RelayCommand]
        private Task CopySelectedUrls() => CopyTextAsync(JoinLines(SelectedHistoryItems.Select(item => item.URL)), "URLs");

        [RelayCommand]
        private async Task CopyMarkdownImage(HistoryItem? item)
        {
            if (item == null || string.IsNullOrEmpty(item.URL)) return;

            var markdownImage = ToastViewModel.BuildMarkdownImage(item.URL, item.FileName);
            try
            {
                if (PlatformServices.IsInitialized)
                {
                    await PlatformServices.Clipboard.SetTextAsync(markdownImage);
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Failed to copy markdown image: {ex.Message}");
            }
        }

        [RelayCommand]
        private void CopyImageToClipboard(HistoryItem? item)
        {
            if (item == null || string.IsNullOrEmpty(item.FilePath) || !File.Exists(item.FilePath)) return;

            try
            {
                using var bitmap = SKBitmap.Decode(item.FilePath);
                if (bitmap != null)
                {
                    PlatformServices.Clipboard.SetImage(bitmap);
                    DebugHelper.WriteLine($"Copied image to clipboard: {item.FilePath}");
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "Failed to copy image to clipboard from history");
            }
        }

        [RelayCommand]
        private async Task CopyErrors(HistoryItem? item)
        {
            if (item == null || string.IsNullOrEmpty(item.Errors)) return;

            try
            {
                if (PlatformServices.IsInitialized)
                {
                    await PlatformServices.Clipboard.SetTextAsync(item.Errors);
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Failed to copy errors: {ex.Message}");
            }
        }

        [RelayCommand]
        private void OpenURL(HistoryItem? item)
        {
            if (item == null || string.IsNullOrEmpty(item.URL)) return;

            try
            {
                XerahS.Platform.Abstractions.PlatformServices.System.OpenUrl(item.URL);
            }
            catch (Exception ex)
            {
                DebugHelper.WriteLine($"Failed to open URL: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task PublishItemAsync(HistoryItem? item)
        {
            if (item == null || !HistoryPublishMetadata.CanPublish(item, _cloudClient?.CurrentOwnerSubject))
            {
                return;
            }

            string clientId = HistoryPublishMetadata.EnsureClientId(item);
            await PersistHistoryItemAsync(item);

            if (_cloudClient == null || !_cloudClient.IsConfigured)
            {
                ShowCloudToast(
                    "XerahS Cloud is not enabled",
                    "Desktop publishing remains launch-gated until the OAuth public client and secure token validation are configured.");
                return;
            }

            try
            {
                if (!await EnsureCloudSessionAsync())
                {
                    return;
                }

                if (!HistoryPublishMetadata.CanPublish(item, _cloudClient.CurrentOwnerSubject))
                {
                    ShowCloudToast(
                        "Account changed",
                        "This history item is bound to a different XerahS Cloud account. Sign back in to that account to publish it.");
                    return;
                }

                var request = new XerahSCloudPublishRequest(
                    clientId,
                    item.URL,
                    string.IsNullOrWhiteSpace(item.ThumbnailURL) ? null : item.ThumbnailURL,
                    IsVideoHistoryItem(item) ? "screencast" : "screenshot",
                    item.FileName,
                    new DateTimeOffset(item.DateTime).ToUniversalTime(),
                    string.IsNullOrWhiteSpace(item.Host) ? null : item.Host,
                    null);
                XerahSCloudPublishResponse response = await _cloudClient.PublishAsync(request);

                HistoryPublishMetadata.MarkPublished(
                    item,
                    response.Id,
                    response.OwnerSubject,
                    response.PublishedAt);
                await PersistHistoryItemAsync(item);
                ShowCloudToast("Published", "Published to your XerahS profile.");
            }
            catch (Exception ex) when (ex is XerahSCloudException or HttpRequestException)
            {
                DebugHelper.WriteException(ex, "XerahS Cloud publish failed");
                ShowCloudToast("Publish failed", ex.Message);
            }
        }

        private async Task<bool> EnsureCloudSessionAsync()
        {
            if (_cloudClient == null)
            {
                return false;
            }

            if (_cloudClient.HasSessionCredential)
            {
                if (await _cloudClient.RestoreSessionAsync())
                {
                    return true;
                }
            }

            if (_cloudOAuthCoordinator == null)
            {
                ShowCloudToast("Sign-in unavailable", "XerahS Cloud sign-in is not available in this application host.");
                return false;
            }

            XerahSCloudOAuthAttempt attempt = _cloudOAuthCoordinator.Begin();
            if (!PlatformServices.System.OpenUrl(attempt.AuthorizationUri.AbsoluteUri))
            {
                ShowCloudToast("Sign-in failed", "The system browser could not be opened.");
                return false;
            }

            ShowCloudToast("Complete sign-in", "Complete XerahS Cloud sign-in and strong authentication in your browser.");
            XerahSCloudOAuthCompletion completion = await _cloudOAuthCoordinator
                .WaitForCompletionAsync(attempt.State)
                .ConfigureAwait(true);
            if (completion == XerahSCloudOAuthCompletion.Accepted)
            {
                await _cloudClient.GetAccountAsync().ConfigureAwait(true);
                return true;
            }

            string message = completion switch
            {
                XerahSCloudOAuthCompletion.Expired => "The sign-in request expired. Try Publish again.",
                XerahSCloudOAuthCompletion.Denied => "XerahS Cloud authorization was denied.",
                XerahSCloudOAuthCompletion.TokenRejected => "The returned session did not pass XerahS Cloud security checks.",
                XerahSCloudOAuthCompletion.InvalidCallback => "The sign-in callback was invalid.",
                _ => "The sign-in callback was unknown or had already been used."
            };
            ShowCloudToast("Sign-in failed", message);
            return false;
        }

        [RelayCommand]
        private async Task UnpublishItemAsync(HistoryItem? item)
        {
            if (item == null || !HistoryPublishMetadata.CanUnpublish(item, _cloudClient?.CurrentOwnerSubject))
            {
                return;
            }

            string clientId = HistoryPublishMetadata.EnsureClientId(item);
            string? ownerSubject = HistoryPublishMetadata.GetOwnerSubject(item) ?? _cloudClient?.CurrentOwnerSubject;
            if (_cloudClient == null || !_cloudClient.IsConfigured ||
                string.IsNullOrWhiteSpace(ownerSubject))
            {
                ShowCloudToast(
                    "XerahS Cloud is not enabled",
                    "Sign in to the account that published this item before removing it.");
                return;
            }

            bool confirmed = await _coreDialogService.ShowConfirmationAsync(
                "Confirm Unpublish",
                $"Remove '{HistoryPublishMetadata.CreateTitle(item)}' from your XerahS profile?\n\nThe local file and destination URL will not be deleted.");
            if (!confirmed)
            {
                return;
            }

            try
            {
                await _cloudClient.UnpublishAsync(clientId, ownerSubject);
                HistoryPublishMetadata.MarkUnpublished(item);
                await PersistHistoryItemAsync(item);
                ShowCloudToast("Unpublished", "Removed from your XerahS profile.");
            }
            catch (Exception ex) when (ex is XerahSCloudException or HttpRequestException)
            {
                DebugHelper.WriteException(ex, "XerahS Cloud unpublish failed");
                ShowCloudToast("Unpublish failed", ex.Message);
            }
        }

        private Task PersistHistoryItemAsync(HistoryItem item) =>
            item.Id > 0 ? Task.Run(() => _historyManager.Edit(item)) : Task.CompletedTask;

        private static bool IsVideoHistoryItem(HistoryItem item)
        {
            string candidate = !string.IsNullOrWhiteSpace(item.FilePath) ? item.FilePath : item.FileName;
            return FileHelpers.IsVideoFile(candidate) ||
                item.Type.Equals("Video", StringComparison.OrdinalIgnoreCase) ||
                item.Type.Equals("Screencast", StringComparison.OrdinalIgnoreCase);
        }

        private static void ShowCloudToast(string title, string text)
        {
            try
            {
                PlatformServices.Toast?.ShowToast(new ToastConfig
                {
                    Title = title,
                    Text = text,
                    Duration = 6f,
                    AutoHide = true,
                    LeftClickAction = ToastClickAction.CloseNotification
                });
            }
            catch
            {
                // Headless and early-startup hosts may not have a toast surface.
            }
        }

        [RelayCommand]
        private async Task DeleteItem(HistoryItem? item)
        {
            if (item == null) return;

            // Show confirmation dialog
            var confirmDelete = await ShowDeleteConfirmationDialog(item.FileName);
            if (!confirmDelete) return;

            // Remove from the observable collection (UI update)
            HistoryItems.Remove(item);

            // Persist deletion to database
            _historyManager.Delete(item);
            new HistoryOcrIndexStore(SettingsManager.GetHistoryFilePath()).Delete(item.Id);
            DebugHelper.WriteLine($"Deleted history item: {item.FileName}");
        }

        [RelayCommand]
        private void ResizeImage(HistoryItem? item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.FilePath) || !File.Exists(item.FilePath)) return;
            MediaToolsToolService.OpenImageBatchTool(XerahS.Media.ImageBatchOperation.Resize, [item.FilePath], owner: null);
        }

        [RelayCommand]
        private void TrimVideo(HistoryItem? item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.FilePath) || !File.Exists(item.FilePath)) return;
            MediaToolsToolService.OpenVideoTrimmer(item.FilePath, owner: null);
        }

        [RelayCommand]
        private async Task DeleteRemoteItem(HistoryItem? item)
        {
            if (item == null || !UploadRemoteDeletionService.CanDelete(item)) return;

            bool confirmed = await _coreDialogService.ShowConfirmationAsync(
                "Delete from host",
                $"Delete '{item.FileName}' from {(string.IsNullOrWhiteSpace(item.Host) ? "the upload destination" : item.Host)}?\n\n{item.URL} will stop working. The local file and history entry are kept.");
            if (!confirmed) return;

            bool deleted = await UploadRemoteDeletionService.DeleteAsync(item);
            if (deleted)
            {
                await PersistHistoryItemAsync(item);
            }

            ShowCloudToast(
                deleted ? "Deleted from host" : "Delete from host failed",
                deleted ? item.FileName : "See the XerahS log for details.");
        }

        private Task<bool> ShowDeleteConfirmationDialog(string fileName)
        {
            return _coreDialogService.ShowConfirmationAsync(
                "Confirm Delete",
                $"Are you sure you want to delete '{fileName}' from history?\n\nThis action cannot be undone.");
        }

        private TaskSettings GetUploadTaskSettings()
        {
            var uploadWorkflow = SettingsManager.GetFirstWorkflow(WorkflowType.FileUpload);
            if (uploadWorkflow?.TaskSettings != null)
            {
                var settings = CloneTaskSettings(uploadWorkflow.TaskSettings);
                settings.WorkflowId = uploadWorkflow.Id;
                return settings;
            }

            return CloneTaskSettings(SettingsManager.DefaultTaskSettings ?? new TaskSettings());
        }

        private static TaskSettings CloneTaskSettings(TaskSettings source)
        {
            var jsonSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                Converters = new List<JsonConverter>
                {
                    new StringEnumConverter(),
                    new SkColorJsonConverter()
                }
            };

            var json = JsonConvert.SerializeObject(source, jsonSettings);
            return JsonConvert.DeserializeObject<TaskSettings>(json, jsonSettings) ?? new TaskSettings();
        }

        private async Task CombineSelectedImagesAsync(ImageCombinerOrientation orientation)
        {
            if (IsCombiningSelection)
            {
                return;
            }

            var selectedItems = GetSelectedCombinableHistoryItems();
            if (selectedItems.Count < 2)
            {
                return;
            }

            IsCombiningSelection = true;

            try
            {
                var combinedHistoryItem = await Task.Run(() => CreateCombinedHistoryItem(selectedItems, orientation));
                if (combinedHistoryItem == null)
                {
                    return;
                }

                if (!_historyManager.AppendHistoryItem(combinedHistoryItem))
                {
                    DebugHelper.WriteLine($"HistoryViewModel - Failed to append combined history item: {combinedHistoryItem.FilePath}");
                    ShowHistoryBackupFailureToastIfPresent(_historyManager.LastBackupFailureReason);
                    return;
                }

                XerahS.Core.Services.OcrIndexingService.QueueIndexHistoryItem(combinedHistoryItem);

                ClearSelectedHistoryItems();
                CurrentPage = 1;
                await LoadHistoryAsync();
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex, "HistoryViewModel - CombineSelectedImages failed");
            }
            finally
            {
                IsCombiningSelection = false;
            }
        }

        private static HistoryItem? CreateCombinedHistoryItem(IReadOnlyList<HistoryItem> selectedItems, ImageCombinerOrientation orientation)
        {
            var filePaths = selectedItems
                .Select(item => item.FilePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToList();

            if (filePaths.Count < 2)
            {
                return null;
            }

            var bitmaps = new List<SKBitmap>();

            try
            {
                foreach (var filePath in filePaths)
                {
                    var bitmap = ImageHelpers.LoadBitmap(filePath);
                    if (bitmap != null)
                    {
                        bitmaps.Add(bitmap);
                    }
                }

                if (bitmaps.Count < 2)
                {
                    return null;
                }

                var combiner = new ImageCombiner();
                using var combinedBitmap = combiner.Combine(bitmaps, orientation);
                if (combinedBitmap == null)
                {
                    return null;
                }

                var outputFolder = TaskHelpers.GetScreenshotsFolder();
                FileHelpers.CreateDirectory(outputFolder);

                var outputPath = FileHelpers.GetUniqueFilePath(
                    Path.Combine(outputFolder, $"Combined_{DateTime.Now:yyyyMMdd_HHmmss}.png"));

                ImageHelpers.SaveBitmap(combinedBitmap, outputPath);

                return new HistoryItem
                {
                    FilePath = outputPath,
                    FileName = Path.GetFileName(outputPath),
                    DateTime = DateTime.Now,
                    Type = "Image"
                };
            }
            finally
            {
                foreach (var bitmap in bitmaps)
                {
                    bitmap.Dispose();
                }
            }
        }

        public void SetSelectedHistoryItems(IEnumerable<HistoryItem> items)
        {
            ArgumentNullException.ThrowIfNull(items);

            var selectedSet = new HashSet<HistoryItem>(items);

            SelectedHistoryItems.Clear();

            foreach (var item in HistoryItems)
            {
                if (selectedSet.Contains(item))
                {
                    SelectedHistoryItems.Add(item);
                }
            }
        }

        public void ClearSelectedHistoryItems()
        {
            if (SelectedHistoryItems.Count == 0)
            {
                return;
            }

            SelectedHistoryItems.Clear();
        }

        private void NotifySelectionStateChanged()
        {
            OnPropertyChanged(nameof(CanCombineSelectedImages));
            OnPropertyChanged(nameof(ShowCombineActions));
            OnPropertyChanged(nameof(SelectedImageCount));
            OnPropertyChanged(nameof(CombineSelectionSummary));
            OnPropertyChanged(nameof(HasMultipleSelection));
            OnPropertyChanged(nameof(SelectedPathCount));
            OnPropertyChanged(nameof(SelectedUrlCount));
            OnPropertyChanged(nameof(CanCopySelectedPaths));
            OnPropertyChanged(nameof(CanCopySelectedUrls));
            OnPropertyChanged(nameof(SelectionSummary));
        }

        private List<HistoryItem> GetSelectedCombinableHistoryItems()
        {
            return SelectedHistoryItems
                .Where(CanCombineHistoryItem)
                .ToList();
        }

        private static bool CanCombineHistoryItem(HistoryItem? item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.FilePath) || !File.Exists(item.FilePath))
            {
                return false;
            }

            var extension = Path.GetExtension(item.FilePath);
            return !string.IsNullOrWhiteSpace(extension) && CombinableImageExtensions.Contains(extension);
        }

        /// <summary>
        /// Surfaces a user-visible toast when a history append failure was caused by the
        /// backup step rather than the data write itself. The history file write can succeed
        /// even when the backup step fails, so this helper gives the user actionable context
        /// (folder path, free space, permissions) instead of a silent DebugHelper line.
        /// </summary>
        /// <param name="reason">User-friendly description set by HistoryManager.LastBackupFailureReason,
        /// or null when the failure was unrelated to backup.</param>
        internal static void ShowHistoryBackupFailureToastIfPresent(string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return;
            }

            try
            {
                PlatformServices.Toast?.ShowToast(new ToastConfig
                {
                    Title = "History Backup Failed",
                    Text = reason,
                    Duration = 6f,
                    Size = new SizeI(420, 140),
                    AutoHide = true,
                    LeftClickAction = ToastClickAction.CloseNotification
                });
            }
            catch
            {
                // Ignore toast errors (platform not ready, headless mode, etc.)
            }
        }

        public void Dispose()
        {
            _historyManager?.Dispose();
        }
    }
}
