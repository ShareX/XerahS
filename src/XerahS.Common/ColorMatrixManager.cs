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

using SkiaSharp;

namespace XerahS.Common;

public static class ColorMatrixManager
{
    private const float Rw = 0.3086f;
    private const float Gw = 0.6094f;
    private const float Bw = 0.0820f;

    public static SKBitmap Apply(SKBitmap bmp, float[] matrix)
    {
        if (bmp is null) throw new ArgumentNullException(nameof(bmp));
        if (matrix is null) throw new ArgumentNullException(nameof(matrix));

        SKBitmap result = new SKBitmap(bmp.Info);
        using SKPaint paint = new SKPaint { ColorFilter = SKColorFilter.CreateColorMatrix(matrix) };
        using SKCanvas canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(bmp, 0, 0, paint);
        return result;
    }

    public static SKBitmap ChangeGamma(SKBitmap bmp, float value)
    {
        if (bmp is null) throw new ArgumentNullException(nameof(bmp));

        value = MathHelpers.Clamp(value, 0.1f, 5.0f);
        byte[] table = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            table[i] = (byte)MathHelpers.Clamp((int)(Math.Pow(i / 255f, value) * 255f + 0.5f), 0, 255);
        }

        using SKColorFilter filter = SKColorFilter.CreateTable(table, table, table, table);
        SKBitmap result = new SKBitmap(bmp.Info);
        using SKCanvas canvas = new SKCanvas(result);
        using SKPaint paint = new SKPaint { ColorFilter = filter };
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(bmp, 0, 0, paint);
        return result;
    }

    public static float[] Inverse()
    {
        return CreateMatrix(new[]
        {
            new[] { -1f, 0f, 0f, 0f, 0f },
            new[] { 0f, -1f, 0f, 0f, 0f },
            new[] { 0f, 0f, -1f, 0f, 0f },
            new[] { 0f, 0f, 0f, 1f, 0f },
            new[] { 1f, 1f, 1f, 0f, 1f }
        });
    }

    public static float[] Alpha(float value, float add = 0f)
    {
        return CreateMatrix(new[]
        {
            new[] { 1f, 0f, 0f, 0f, 0f },
            new[] { 0f, 1f, 0f, 0f, 0f },
            new[] { 0f, 0f, 1f, 0f, 0f },
            new[] { 0f, 0f, 0f, value, 0f },
            new[] { 0f, 0f, 0f, add, 1f }
        });
    }

    public static float[] Brightness(float value)
    {
        return CreateMatrix(new[]
        {
            new[] { 1f, 0f, 0f, 0f, 0f },
            new[] { 0f, 1f, 0f, 0f, 0f },
            new[] { 0f, 0f, 1f, 0f, 0f },
            new[] { 0f, 0f, 0f, 1f, 0f },
            new[] { value, value, value, 0f, 1f }
        });
    }

    public static float[] Contrast(float value)
    {
        return CreateMatrix(new[]
        {
            new[] { value, 0f, 0f, 0f, 0f },
            new[] { 0f, value, 0f, 0f, 0f },
            new[] { 0f, 0f, value, 0f, 0f },
            new[] { 0f, 0f, 0f, 1f, 0f },
            new[] { 0f, 0f, 0f, 0f, 1f }
        });
    }

    public static float[] BlackWhite()
    {
        return CreateMatrix(new[]
        {
            new[] { 1.5f, 1.5f, 1.5f, 0f, 0f },
            new[] { 1.5f, 1.5f, 1.5f, 0f, 0f },
            new[] { 1.5f, 1.5f, 1.5f, 0f, 0f },
            new[] { 0f, 0f, 0f, 1f, 0f },
            new[] { -1f, -1f, -1f, 0f, 1f }
        });
    }

    public static float[] Polaroid()
    {
        return CreateMatrix(new[]
        {
            new[] { 1.438f, -0.062f, -0.062f, 0f, 0f },
            new[] { -0.122f, 1.378f, -0.122f, 0f, 0f },
            new[] { -0.016f, -0.016f, 1.483f, 0f, 0f },
            new[] { 0f, 0f, 0f, 1f, 0f },
            new[] { -0.03f, 0.05f, -0.02f, 0f, 1f }
        });
    }

    public static float[] Grayscale(float value = 1f)
    {
        return CreateMatrix(new[]
        {
            new[] { Rw * value, Rw * value, Rw * value, 0f, 0f },
            new[] { Gw * value, Gw * value, Gw * value, 0f, 0f },
            new[] { Bw * value, Bw * value, Bw * value, 0f, 0f },
            new[] { 0f, 0f, 0f, 1f, 0f },
            new[] { 0f, 0f, 0f, 0f, 1f }
        });
    }

    public static float[] Sepia(float value = 1f)
    {
        return CreateMatrix(new[]
        {
            new[] { 0.393f * value, 0.349f * value, 0.272f * value, 0f, 0f },
            new[] { 0.769f * value, 0.686f * value, 0.534f * value, 0f, 0f },
            new[] { 0.189f * value, 0.168f * value, 0.131f * value, 0f, 0f },
            new[] { 0f, 0f, 0f, 1f, 0f },
            new[] { 0f, 0f, 0f, 0f, 1f }
        });
    }

    public static float[] Hue(float angle)
    {
        float a = angle * (float)(Math.PI / 180);
        float c = (float)Math.Cos(a);
        float s = (float)Math.Sin(a);

        return CreateMatrix(new[]
        {
            new[] { (Rw + (c * (1 - Rw))) + (s * -Rw), (Rw + (c * -Rw)) + (s * 0.143f), (Rw + (c * -Rw)) + (s * -(1 - Rw)), 0f, 0f },
            new[] { (Gw + (c * -Gw)) + (s * -Gw), (Gw + (c * (1 - Gw))) + (s * 0.14f), (Gw + (c * -Gw)) + (s * Gw), 0f, 0f },
            new[] { (Bw + (c * -Bw)) + (s * (1 - Bw)), (Bw + (c * -Bw)) + (s * -0.283f), (Bw + (c * (1 - Bw))) + (s * Bw), 0f, 0f },
            new[] { 0f, 0f, 0f, 1f, 0f },
            new[] { 0f, 0f, 0f, 0f, 1f }
        });
    }

    public static float[] Saturation(float value)
    {
        return CreateMatrix(new[]
        {
            new[] { ((1f - value) * Rw) + value, (1f - value) * Rw, (1f - value) * Rw, 0f, 0f },
            new[] { (1f - value) * Gw, ((1f - value) * Gw) + value, (1f - value) * Gw, 0f, 0f },
            new[] { (1f - value) * Bw, (1f - value) * Bw, ((1f - value) * Bw) + value, 0f, 0f },
            new[] { 0f, 0f, 0f, 1f, 0f },
            new[] { 0f, 0f, 0f, 0f, 1f }
        });
    }

    public static float[] Colorize(SKColor color, float value)
    {
        float r = color.Red / 255f;
        float g = color.Green / 255f;
        float b = color.Blue / 255f;
        float invAmount = 1 - value;

        return CreateMatrix(new[]
        {
            new[] { invAmount + (value * r * Rw), value * g * Rw, value * b * Rw, 0f, 0f },
            new[] { value * r * Gw, invAmount + (value * g * Gw), value * b * Gw, 0f, 0f },
            new[] { value * r * Bw, value * g * Bw, invAmount + (value * b * Bw), 0f, 0f },
            new[] { 0f, 0f, 0f, 1f, 0f },
            new[] { 0f, 0f, 0f, 0f, 1f }
        });
    }

    public static float[] Mask(float opacity, SKColor color)
    {
        return CreateMatrix(new[]
        {
            new[] { 0f, 0f, 0f, 0f, 0f },
            new[] { 0f, 0f, 0f, 0f, 0f },
            new[] { 0f, 0f, 0f, 0f, 0f },
            new[] { 0f, 0f, 0f, color.Alpha / 255f * opacity, 0f },
            new[] { color.Red / 255f, color.Green / 255f, color.Blue / 255f, 0f, 1f }
        });
    }

    private static float[] CreateMatrix(float[][] matrix)
    {
        // Skia expects a 4x5 matrix flattened row-major: R, G, B, A rows.
        return new[]
        {
            matrix[0][0], matrix[0][1], matrix[0][2], matrix[0][3], matrix[0][4],
            matrix[1][0], matrix[1][1], matrix[1][2], matrix[1][3], matrix[1][4],
            matrix[2][0], matrix[2][1], matrix[2][2], matrix[2][3], matrix[2][4],
            matrix[3][0], matrix[3][1], matrix[3][2], matrix[3][3], matrix[3][4]
        };
    }
}
