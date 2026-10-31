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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShareX.ImageEditor.Presentation.Views;
using XerahS.Core;
using XerahS.Core.Hotkeys;
using XerahS.Core.Managers;
using XerahS.UI.Helpers;
using XerahS.UI.Services.SettingsSearch;
using XerahS.UI.Theming;
using XerahS.UI.ViewModels;

namespace XerahS.UI.Views
{
    public partial class MainWindow
    {
        private NavigationNode? _captureNavigationNode;
        private StatisticsView? _statisticsWindow;
        private string _navigationSearchText = string.Empty;
        private bool _navigationFilterUpdateQueued;
        private bool _navigationSearchIndexEnriched;

        private void OnNavigationSearchTextChanged(object? sender, TextChangedEventArgs e)
        {
            _navigationSearchText = (sender as TextBox)?.Text ?? string.Empty;
            QueueNavigationFilterUpdate();
        }

        private void QueueNavigationFilterUpdate()
        {
            if (_navigationFilterUpdateQueued)
            {
                return;
            }

            _navigationFilterUpdateQueued = true;
            Dispatcher.UIThread.Post(() =>
            {
                _navigationFilterUpdateQueued = false;
                ApplyNavigationFilter();
            }, DispatcherPriority.Loaded);
        }

        private void ApplyNavigationFilter()
        {
            foreach (NavigationNode node in NavigationNodes)
            {
                node.ApplyFilter(_navigationSearchText);
            }

            TreeView? navigationTree = this.FindControl<TreeView>("NavigationTree");
            if (navigationTree?.SelectedItem is NavigationNode selected && !selected.IsVisible)
            {
                NavigationNode? firstVisiblePage = FlattenNavigationNodes(NavigationNodes)
                    .FirstOrDefault(x => x.IsVisible && x.Kind == NavigationNodeKind.Page);

                navigationTree.SelectedItem = firstVisiblePage;
            }

            ApplyApplicationSettingsPanelFilter();
        }

        private void ApplyApplicationSettingsPanelFilter()
        {
            ContentControl? contentFrame = this.FindControl<ContentControl>("ContentFrame");
            if (contentFrame?.Content is ApplicationSettingsView appSettings)
            {
                XerahS.UI.Controls.SettingsSearch.Apply(appSettings, _navigationSearchText);
            }
        }

        private static IEnumerable<NavigationNode> FlattenNavigationNodes(IEnumerable<NavigationNode> nodes)
        {
            foreach (NavigationNode node in nodes)
            {
                yield return node;

                foreach (NavigationNode child in FlattenNavigationNodes(node.Children))
                {
                    yield return child;
                }
            }
        }

        private void OnNavSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            ContentControl? contentFrame = this.FindControl<ContentControl>("ContentFrame");
            NavigationNode? selectedItem = (sender as TreeView)?.SelectedItem as NavigationNode;

            if (contentFrame == null || selectedItem == null || selectedItem.Kind != NavigationNodeKind.Page)
            {
                return;
            }

            HandleNavigationTag(selectedItem.Tag, contentFrame, out _);
            QueueNavigationFilterUpdate();
        }

        private void OnNavigationNodeTapped(object? sender, TappedEventArgs e)
        {
            if (sender is not Control control || control.DataContext is not NavigationNode node)
            {
                return;
            }

            if (InvokeNavigationNode(node, toggleGroups: true))
            {
                e.Handled = true;
            }
        }

        private void OnNavigationTreeKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter && e.Key != Key.Space)
            {
                return;
            }

            if ((sender as TreeView)?.SelectedItem is not NavigationNode node)
            {
                return;
            }

            if (InvokeNavigationNode(node, toggleGroups: true))
            {
                e.Handled = true;
            }
        }

        private bool HandleNavigationTag(string? tag, ContentControl contentFrame, out bool openedExternalWindow)
        {
            openedExternalWindow = false;

            if (string.IsNullOrEmpty(tag))
            {
                return false;
            }

            if (tag.StartsWith("Capture_", StringComparison.Ordinal))
            {
                string workflowId = tag.Replace("Capture_", "", StringComparison.Ordinal);
                if (!string.IsNullOrEmpty(workflowId))
                {
                    WorkflowSettings? workflow = null;

                    if (Application.Current is App app && app.WorkflowManager != null)
                    {
                        workflow = app.WorkflowManager.GetWorkflowById(workflowId);
                    }

                    if (workflow == null)
                    {
                        workflow = SettingsManager.WorkflowsConfig.Hotkeys.FirstOrDefault(w => w.Id == workflowId);
                    }

                    if (workflow != null)
                    {
                        _ = ExecuteCaptureAsync(workflow.Job, workflow.Id);
                        NavigateToEditor();
                        openedExternalWindow = true;
                        return true;
                    }
                }

                return false;
            }

            if (tag.StartsWith("Workflow_", StringComparison.Ordinal))
            {
                string workflowId = tag.Replace("Workflow_", "", StringComparison.Ordinal);
                if (!string.IsNullOrEmpty(workflowId))
                {
                    WorkflowSettings? workflow = SettingsManager.WorkflowsConfig?.Hotkeys?.FirstOrDefault(w => w.Id == workflowId);
                    if (workflow != null)
                    {
                        _ = ExecuteCaptureAsync(workflow.Job, workflow.Id);
                        openedExternalWindow = true;
                        return true;
                    }
                }

                return false;
            }

            if (ToolNavigationHelper.TryHandleToolsTag(
                    tag,
                    this,
                    contentFrame,
                    _taskManager ?? throw new InvalidOperationException("Task manager is required for tool navigation."),
                    ExecuteWorkflowFromNavigationAsync,
                    out openedExternalWindow))
            {
                return true;
            }

            switch (tag)
            {
                case "Editor":
                    _editorView ??= CreateEditorView();
                    contentFrame.Content = _editorView;
                    return true;
                case "Recording":
                    contentFrame.Content = new RecordingView();
                    return true;
                case "History":
                    contentFrame.Content = new HistoryView();
                    return true;
                case "Statistics":
                    if (_statisticsWindow == null)
                    {
                        _statisticsWindow = new StatisticsView();
                        _statisticsWindow.Closed += (_, _) => _statisticsWindow = null;
                    }
                    if (!_statisticsWindow.IsVisible) _statisticsWindow.Show(this);
                    if (_statisticsWindow.WindowState == WindowState.Minimized) _statisticsWindow.WindowState = WindowState.Normal;
                    _statisticsWindow.Activate();
                    openedExternalWindow = true;
                    return true;
                case "Workflows":
                    contentFrame.Content = new WorkflowsView();
                    return true;
                case "Upload_ClipboardUploadWithContentViewer":
                    _ = ExecuteWorkflowFromNavigationAsync(WorkflowType.ClipboardUploadWithContentViewer);
                    openedExternalWindow = true;
                    return true;
                case "Upload_FileUpload":
                    _ = ExecuteWorkflowFromNavigationAsync(WorkflowType.FileUpload);
                    openedExternalWindow = true;
                    return true;
                case "Settings":
                case "Settings_App":
                    _applicationSettingsView ??= CreateApplicationSettingsView();
                    contentFrame.Content = _applicationSettingsView;
                    return true;
                case "Settings_Dest":
                    _destinationSettingsView ??= CreateDestinationSettingsView();
                    contentFrame.Content = _destinationSettingsView;
                    return true;
                case "Debug":
                    contentFrame.Content = new DebugView();
                    return true;
                case "About":
                    contentFrame.Content = new AboutView();
                    return true;
                default:
                    return false;
            }
        }

        private EditorView CreateEditorView()
        {
            return new EditorView();
        }

        private ApplicationSettingsView CreateApplicationSettingsView()
        {
            return new ApplicationSettingsView();
        }

        private DestinationSettingsView CreateDestinationSettingsView()
        {
            return new DestinationSettingsView();
        }

        private void BuildNavigationNodes()
        {
            NavigationNodes.Clear();

            _captureNavigationNode = CreateNode("Capture", "Capture", HostIcons.NavigationCapture, NavigationNodeKind.Group, isExpanded: true);
            NavigationNodes.Add(_captureNavigationNode);
            NavigationNodes.Add(CreateNode("Recording", "Recording", HostIcons.NavigationRecording, NavigationNodeKind.Page));
            NavigationNodes.Add(CreateNode("Editor", "Editor", HostIcons.NavigationEditor, NavigationNodeKind.Page));
            NavigationNodes.Add(CreateNode("History", "History", HostIcons.NavigationHistory, NavigationNodeKind.Page));
            NavigationNodes.Add(CreateNode("Statistics", "Statistics", HostIcons.NavigationStatistics, NavigationNodeKind.Action));
            NavigationNodes.Add(CreateNode("Workflows", "Workflows", HostIcons.NavigationWorkflows, NavigationNodeKind.Page));
            NavigationNodes.Add(CreateUploadNode());
            NavigationNodes.Add(CreateToolsNode());
            NavigationNodes.Add(CreateSettingsNode());
            NavigationNodes.Add(CreateNode("Debug", "Debug", HostIcons.NavigationDebug, NavigationNodeKind.Page));
            NavigationNodes.Add(CreateNode("About", "About", HostIcons.NavigationAbout, NavigationNodeKind.Page));

            UpdateNavigationItems();
            QueueNavigationFilterUpdate();
        }

        internal void EnrichNavigationSearchTextFromSettingsIndex()
        {
            if (_navigationSearchIndexEnriched)
            {
                return;
            }

            IReadOnlyList<SettingsSearchEntry> entries = SettingsSearchService.Instance.GetAllEntries();
            Dictionary<string, List<string>> byTag = new(StringComparer.Ordinal);

            foreach (SettingsSearchEntry entry in entries)
            {
                if (!byTag.TryGetValue(entry.NavigationTag, out List<string>? list))
                {
                    list = [];
                    byTag[entry.NavigationTag] = list;
                }

                list.Add(entry.Title);
                list.AddRange(entry.Keywords);
            }

            foreach (NavigationNode node in FlattenNavigationNodes(NavigationNodes))
            {
                if (node.Tag != null && byTag.TryGetValue(node.Tag, out List<string>? extras))
                {
                    node.AppendSearchText(string.Join(' ', extras));
                }

                if (string.Equals(node.Tag, "Settings", StringComparison.Ordinal))
                {
                    node.AppendSearchText(string.Join(' ',
                        entries.Select(entry => entry.Title).Concat(entries.SelectMany(entry => entry.Keywords))));
                }
            }

            _navigationSearchIndexEnriched = true;
            QueueNavigationFilterUpdate();
        }

        public void NavigateToEditor()
        {
            NavigateTo("Editor");
        }

        public void NavigateToSettings()
        {
            NavigateTo("Settings_App");
        }

        public void NavigateToHistory()
        {
            NavigateTo("History");
        }

        public void NavigateToStatistics() => NavigateTo("Statistics");

        public void NavigateToAbout()
        {
            NavigateTo("About");
        }

        private void NavigateTo(string navTag)
        {
            bool handled = false;
            bool openedExternalWindow = false;
            ContentControl? contentFrame = this.FindControl<ContentControl>("ContentFrame");
            TreeView? navigationTree = this.FindControl<TreeView>("NavigationTree");

            if (navigationTree != null)
            {
                NavigationNode? navNode = FindNavigationNodeByTag(NavigationNodes, navTag);
                if (navNode != null)
                {
                    navNode.ExpandPath();

                    if (navNode.Kind == NavigationNodeKind.Action)
                    {
                        if (contentFrame != null)
                        {
                            handled = HandleNavigationTag(navTag, contentFrame, out openedExternalWindow);
                        }
                    }
                    else if (navNode.Kind == NavigationNodeKind.Page && !ReferenceEquals(navigationTree.SelectedItem, navNode))
                    {
                        navigationTree.SelectedItem = navNode;
                        handled = true;
                    }
                    else if (navNode.Kind == NavigationNodeKind.Page && contentFrame != null)
                    {
                        handled = HandleNavigationTag(navTag, contentFrame, out openedExternalWindow);
                    }
                }
            }

            if (!handled && contentFrame != null)
            {
                HandleNavigationTag(navTag, contentFrame, out openedExternalWindow);
            }

            if (!SilentRunStartupPolicy.ShouldActivateWindowOnNavigate(_suppressWindowActivation))
            {
                return;
            }

            if (!this.IsVisible)
            {
                this.Show();
            }

            if (this.WindowState == Avalonia.Controls.WindowState.Minimized)
            {
                this.WindowState = Avalonia.Controls.WindowState.Normal;
            }

            if (!openedExternalWindow)
            {
                this.Activate();
                this.Focus();
            }
        }

        private bool InvokeNavigationNode(NavigationNode node, bool toggleGroups)
        {
            TreeView? navigationTree = this.FindControl<TreeView>("NavigationTree");
            ContentControl? contentFrame = this.FindControl<ContentControl>("ContentFrame");

            if (node.Kind == NavigationNodeKind.Group)
            {
                if (toggleGroups)
                {
                    node.IsExpanded = !node.IsExpanded;
                    return true;
                }

                return false;
            }

            if (contentFrame == null)
            {
                return false;
            }

            node.ExpandPath();

            if (node.Kind == NavigationNodeKind.Action)
            {
                return HandleNavigationTag(node.Tag, contentFrame, out _);
            }

            if (navigationTree != null && !ReferenceEquals(navigationTree.SelectedItem, node))
            {
                navigationTree.SelectedItem = node;
                return true;
            }

            return HandleNavigationTag(node.Tag, contentFrame, out _);
        }

        private static NavigationNode? FindNavigationNodeByTag(IEnumerable? menuItems, string navTag)
        {
            if (menuItems == null)
            {
                return null;
            }

            foreach (object? item in menuItems)
            {
                if (item is not NavigationNode navItem)
                {
                    continue;
                }

                if (string.Equals(navItem.Tag, navTag, StringComparison.Ordinal))
                {
                    return navItem;
                }

                NavigationNode? child = FindNavigationNodeByTag(navItem.Children, navTag);
                if (child != null)
                {
                    return child;
                }
            }

            return null;
        }

        private void UpdateNavigationItems()
        {
            if (_captureNavigationNode == null)
            {
                return;
            }

            _captureNavigationNode.ReplaceChildren(NavigationItemsHelper.CreateCaptureNavigationNodes());
            QueueNavigationFilterUpdate();
        }

        private static NavigationNode CreateNode(string text, string? tag, string? glyph, NavigationNodeKind kind, bool isExpanded = false)
        {
            return new NavigationNode(text, tag, glyph, kind, NavigationSearchKeywords.ForTag(tag))
            {
                IsExpanded = isExpanded
            };
        }

        private static NavigationNode CreateUploadNode()
        {
            NavigationNode uploadNode = CreateNode("Upload", "Upload", HostIcons.NavigationUpload, NavigationNodeKind.Group);
            uploadNode.AddChild(CreateNode("Upload File...", "Upload_FileUpload", null, NavigationNodeKind.Action));
            uploadNode.AddChild(CreateNode("Upload Content...", "Upload_ClipboardUploadWithContentViewer", null, NavigationNodeKind.Action));
            return uploadNode;
        }

        private static NavigationNode CreateToolsNode()
        {
            NavigationNode toolsNode = CreateNode("Tools", "Tools", HostIcons.NavigationTools, NavigationNodeKind.Page);
            toolsNode.AddChild(CreateNode("Color Picker...", "Tools_ColorPicker", null, NavigationNodeKind.Action));
            toolsNode.AddChild(CreateNode("Ruler", "Tools_Ruler", null, NavigationNodeKind.Action));
            toolsNode.AddChild(CreateNode("Index Folder...", "Tools_IndexFolder", null, NavigationNodeKind.Action));

            NavigationNode qrCodeNode = CreateNode("QR Code", null, null, NavigationNodeKind.Group);
            qrCodeNode.AddChild(CreateNode("Generator...", "Tools_QrGenerator", null, NavigationNodeKind.Action));
            qrCodeNode.AddChild(CreateNode("Scan from screen", "Tools_QrScanScreen", null, NavigationNodeKind.Action));
            qrCodeNode.AddChild(CreateNode("Scan from region", "Tools_QrScanRegion", null, NavigationNodeKind.Action));
            toolsNode.AddChild(qrCodeNode);

            toolsNode.AddChild(CreateNode("Image Combiner...", "Tools_ImageCombiner", null, NavigationNodeKind.Action));
            toolsNode.AddChild(CreateNode("Image Splitter...", "Tools_ImageSplitter", null, NavigationNodeKind.Action));
            toolsNode.AddChild(CreateNode("Image Thumbnailer...", "Tools_ImageThumbnailer", null, NavigationNodeKind.Action));
            toolsNode.AddChild(CreateNode("Image Resizer...", "Tools_ImageResizer", null, NavigationNodeKind.Action));
            toolsNode.AddChild(CreateNode("Image Converter...", "Tools_ImageConverter", null, NavigationNodeKind.Action));
            toolsNode.AddChild(CreateNode("Image Watermark...", "Tools_ImageWatermark", null, NavigationNodeKind.Action));
            toolsNode.AddChild(CreateNode("Animated GIF Maker...", "Tools_AnimatedGifMaker", null, NavigationNodeKind.Action));
#if DEBUG
            toolsNode.AddChild(CreateNode("Video Editor...", "Tools_VideoEditor", null, NavigationNodeKind.Action));
#endif
            toolsNode.AddChild(CreateNode("Video Converter...", "Tools_VideoConverter", null, NavigationNodeKind.Action));
            toolsNode.AddChild(CreateNode("Video Trimmer...", "Tools_VideoTrimmer", null, NavigationNodeKind.Action));
            toolsNode.AddChild(CreateNode("Video Thumbnailer...", "Tools_VideoThumbnailer", null, NavigationNodeKind.Action));
            toolsNode.AddChild(CreateNode("Analyze Image...", "Tools_AnalyzeImage", null, NavigationNodeKind.Action));
            toolsNode.AddChild(CreateNode("Monitor Test", "Tools_MonitorTest", null, NavigationNodeKind.Action));
            toolsNode.AddChild(CreateNode("Network Monitor...", "Tools_NetworkMonitor", null, NavigationNodeKind.Action));

            return toolsNode;
        }

        private static NavigationNode CreateSettingsNode()
        {
            NavigationNode settingsNode = CreateNode("Settings", "Settings", HostIcons.NavigationSettings, NavigationNodeKind.Group, isExpanded: true);
            settingsNode.AddChild(CreateNode("Application Settings", "Settings_App", null, NavigationNodeKind.Page));
            settingsNode.AddChild(CreateNode("Destination Settings", "Settings_Dest", null, NavigationNodeKind.Page));
            return settingsNode;
        }
    }
}
