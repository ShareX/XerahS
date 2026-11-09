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

using NUnit.Framework;
using ShareX.ImageEditor.Core.Annotations;
using ShareX.ImageEditor.Core.Editor;
using ShareX.ImageEditor.Core.Persistence;
using ShareX.ImageEditor.Presentation.ViewModels;
using SkiaSharp;
using System.Reflection;
using XerahS.UI.Services;

namespace XerahS.Tests.Editor;

[TestFixture]
public class MainViewModelHelperSaveTests
{
    [Test]
    public async Task SaveToPathAsync_TruncatesExistingDestinationFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(path, Enumerable.Repeat((byte)'x', 8192).ToArray());

        try
        {
            var viewModel = new MainViewModel
            {
                ImageFilePath = path,
                IsDirty = true
            };

            await InvokeSaveToPathAsync(viewModel, () =>
            {
                var bitmap = new SKBitmap(1, 1);
                bitmap.Erase(SKColors.CadetBlue);
                return bitmap;
            }, path);

            var savedBytes = await File.ReadAllBytesAsync(path);
            Assert.That(savedBytes.Length, Is.LessThan(8192));
            Assert.That(viewModel.ImageFilePath, Is.EqualTo(path));
            Assert.That(viewModel.IsDirty, Is.False);

            using var decoded = SKBitmap.Decode(savedBytes);
            Assert.That(decoded, Is.Not.Null);
            Assert.That(decoded.Width, Is.EqualTo(1));
            Assert.That(decoded.Height, Is.EqualTo(1));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Test]
    public async Task SaveToPathAsync_KeepsEditorDirty_WhenAnnotationSidecarSaveFails()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        string sidecarTempPath = XannProjectFileService.GetDefaultSidecarPath(path) + ".tmp";
        Directory.CreateDirectory(sidecarTempPath);

        try
        {
            using var source = new SKBitmap(4, 4);
            source.Erase(SKColors.White);

            var editorCore = new EditorCore();
            editorCore.LoadImage(source.Copy());
            editorCore.AddAnnotation(new RectangleAnnotation
            {
                StartPoint = new SKPoint(0, 0),
                EndPoint = new SKPoint(2, 2)
            });

            var viewModel = new MainViewModel
            {
                IsDirty = true
            };
            viewModel.AttachEditorCore(editorCore);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            {
                await InvokeSaveToPathAsync(viewModel, () =>
                {
                    var bitmap = new SKBitmap(1, 1);
                    bitmap.Erase(SKColors.CadetBlue);
                    return bitmap;
                }, path);
            });

            Assert.That(File.Exists(path), Is.True, "The image write should have completed before sidecar persistence failed.");
            Assert.That(viewModel.ImageFilePath, Is.EqualTo(path));
            Assert.That(viewModel.IsDirty, Is.True);
        }
        finally
        {
            if (Directory.Exists(sidecarTempPath))
            {
                Directory.Delete(sidecarTempPath);
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static Task InvokeSaveToPathAsync(MainViewModel viewModel, Func<SKBitmap?> snapshotFactory, string path)
    {
        var method = typeof(MainViewModelHelper).GetMethod("SaveToPathAsync", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(MainViewModelHelper), "SaveToPathAsync");

        return (Task)(method.Invoke(null, new object?[] { viewModel, snapshotFactory, path })
            ?? throw new InvalidOperationException("SaveToPathAsync returned null."));
    }
}
