#region License Information (GPL v3)

/*
    ShareX.Avalonia - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

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

#nullable enable

using System;
using SkiaSharp;

namespace ShareX.Ava.Common;

public static class ConvolutionMatrixManager
{
    public static SKBitmap Apply(this ConvolutionMatrix kernel, SKBitmap bmp)
    {
        if (kernel is null) throw new ArgumentNullException(nameof(kernel));
        if (bmp is null) throw new ArgumentNullException(nameof(bmp));

        float[] matrix = kernel.ToKernelArray();
        float divisor = kernel.GetDivisor();
        if (Math.Abs(divisor) < float.Epsilon)
        {
            divisor = 1f;
        }

        using SKPaint paint = new SKPaint
        {
            ImageFilter = SKImageFilter.CreateMatrixConvolution(
                new SKSizeI(kernel.Width, kernel.Height),
                NormalizeKernel(matrix, divisor),
                1f,
                kernel.Offset,
                new SKPointI(kernel.Width / 2, kernel.Height / 2),
                SKShaderTileMode.Clamp,
                kernel.ConsiderAlpha)
        };

        SKBitmap result = new SKBitmap(bmp.Info);
        using SKCanvas canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(bmp, 0, 0, paint);
        return result;
    }

    public static ConvolutionMatrix Smooth(int weight = 1)
    {
        ConvolutionMatrix cm = new ConvolutionMatrix();
        double factor = weight + 8;
        cm.SetAll(1 / factor);
        cm[1, 1] = weight / factor;
        return cm;
    }

    public static ConvolutionMatrix GaussianBlur(int height, int width, double sigma)
    {
        ConvolutionMatrix cm = new ConvolutionMatrix(height, width)
        {
            ConsiderAlpha = true
        };

        double sum = 0.0;
        double midpointX = (width - 1) / 2.0;
        double midpointY = (height - 1) / 2.0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                sum += cm[y, x] = GaussianFunction(x - midpointX, sigma) * GaussianFunction(y - midpointY, sigma);
            }
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                cm[y, x] /= sum;
            }
        }

        return cm;
    }

    public static ConvolutionMatrix MeanRemoval(int weight = 9)
    {
        ConvolutionMatrix cm = new ConvolutionMatrix();
        double factor = weight - 8;
        cm.SetAll(-1 / factor);
        cm[1, 1] = weight / factor;
        return cm;
    }

    public static ConvolutionMatrix Sharpen(int weight = 11)
    {
        ConvolutionMatrix cm = new ConvolutionMatrix();
        double factor = weight - 8;
        cm.SetAll(0);
        cm[1, 1] = weight / factor;
        cm[1, 0] = cm[0, 1] = cm[2, 1] = cm[1, 2] = -2 / factor;
        return cm;
    }

    public static ConvolutionMatrix Emboss()
    {
        ConvolutionMatrix cm = new ConvolutionMatrix();
        cm.SetAll(-1);
        cm[1, 1] = 4;
        cm[1, 0] = cm[0, 1] = cm[2, 1] = cm[1, 2] = 0;
        cm.Offset = 127;
        return cm;
    }

    public static ConvolutionMatrix EdgeDetect()
    {
        ConvolutionMatrix cm = new ConvolutionMatrix();
        cm[0, 0] = cm[0, 1] = cm[0, 2] = -1;
        cm[1, 0] = cm[1, 1] = cm[1, 2] = 0;
        cm[2, 0] = cm[2, 1] = cm[2, 2] = 1;
        cm.Offset = 127;
        return cm;
    }

    private static double GaussianFunction(double x, double sigma)
    {
        double left = 1.0 / (Math.Sqrt(2 * Math.PI) * sigma);
        double exponentNumerator = -x * x;
        double exponentDenominator = 2 * Math.Pow(sigma, 2);
        double right = Math.Exp(exponentNumerator / exponentDenominator);
        return left * right;
    }

    private static float[] NormalizeKernel(float[] kernel, float divisor)
    {
        float[] normalized = new float[kernel.Length];
        for (int i = 0; i < kernel.Length; i++)
        {
            normalized[i] = kernel[i] / divisor;
        }

        return normalized;
    }
}
