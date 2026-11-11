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

using Microsoft.ML.OnnxRuntime;
using SkiaSharp;
using System.Runtime.InteropServices;

namespace XerahS.OCR;

/// <summary>
/// Offline OCR with the PaddleOCR PP-OCRv6 models: a detection model finds the text lines and a recognition model
/// reads each one. Ported from ShareX.OCR. The recognition model reads Latin, Greek, Chinese and Japanese text;
/// <see cref="CanRead"/> tells whether a piece of text is within its character set.
/// </summary>
public sealed class PaddleOcrEngine : IDisposable
{
    public const string DetectionModelName = "PP-OCRv6_small_det";
    public const string RecognitionModelName = "PP-OCRv6_small_rec";

    // Recognized text below this confidence is usually an icon or noise read as characters.
    public const float DefaultMinScore = 0.5f;

    private static readonly Lock SharedLock = new();
    private static PaddleOcrEngine? _shared;

    private readonly TextDetector _detector;
    private readonly TextRecognizer _recognizer;

    // The models are copied next to the application under this folder.
    public static string DefaultModelsFolder => Path.Combine(AppContext.BaseDirectory, "PaddleOCR");

    private static readonly Lazy<bool> Available = new(() =>
        File.Exists(Path.Combine(DefaultModelsFolder, DetectionModelName, "inference.onnx")) &&
        File.Exists(Path.Combine(DefaultModelsFolder, RecognitionModelName, "inference.onnx")) &&
        NativeLibrary.TryLoad("onnxruntime", typeof(InferenceSession).Assembly, null, out _));

    private static readonly Lazy<HashSet<string>> Characters = new(() =>
        [.. TextRecognizer.LoadDictionary(Path.Combine(DefaultModelsFolder, RecognitionModelName))]);

    // Whether the models and the ONNX runtime are here. The runtime has no Intel Mac (osx-x64) build.
    public static bool IsAvailable => Available.Value;

    // Loading the models takes a moment, so one engine is kept for the life of the process.
    public static PaddleOcrEngine Shared
    {
        get
        {
            lock (SharedLock)
            {
                return _shared ??= new PaddleOcrEngine(DefaultModelsFolder);
            }
        }
    }

    public PaddleOcrEngine(string modelsFolder)
    {
        using SessionOptions sessionOptions = new()
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
        };

        _detector = new TextDetector(Path.Combine(modelsFolder, DetectionModelName), sessionOptions);

        try
        {
            _recognizer = new TextRecognizer(Path.Combine(modelsFolder, RecognitionModelName), sessionOptions);
        }
        catch
        {
            _detector.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Whether every letter of <paramref name="text"/> is in the recognition model's character set. Reads only the
    /// model's dictionary, so it does not load the models.
    /// </summary>
    public static bool CanRead(string text)
    {
        HashSet<string> characters = Characters.Value;

        foreach (System.Text.Rune rune in text.EnumerateRunes())
        {
            if (System.Text.Rune.IsLetter(rune) && !characters.Contains(rune.ToString()))
            {
                return false;
            }
        }

        return true;
    }

    public PaddleOcrResult Recognize(SKBitmap bitmap, float minScore = DefaultMinScore)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        if (bitmap.Width <= 0 || bitmap.Height <= 0)
        {
            return new PaddleOcrResult([]);
        }

        using SKBitmap source = ImageTensor.ToOpaqueBgra(bitmap);
        using SKImage image = SKImage.FromBitmap(source);
        List<SKPoint[]> boxes = _detector.Detect(image);

        if (boxes.Count == 0)
        {
            return new PaddleOcrResult([]);
        }

        (string Text, float Score)[] texts = _recognizer.Recognize(image, boxes);
        List<PaddleOcrTextRegion> regions = [];

        for (int i = 0; i < boxes.Count; i++)
        {
            string text = texts[i].Text.Trim();

            if (text.Length > 0 && texts[i].Score >= minScore)
            {
                regions.Add(new PaddleOcrTextRegion(boxes[i], text, texts[i].Score));
            }
        }

        return new PaddleOcrResult(regions);
    }

    public void Dispose()
    {
        _detector.Dispose();
        _recognizer.Dispose();
    }
}
