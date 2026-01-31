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

using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using XerahS.Common;

namespace XerahS.Media
{
    public class GifFrameInfo
    {
        public int Duration { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public static class GifHelpers
    {
        public static bool IsGif(string filePath)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                using var codec = SKCodec.Create(stream);
                return codec != null && codec.EncodedFormat == SKEncodedImageFormat.Gif;
            }
            catch
            {
                return false;
            }
        }

        public static int GetFrameCount(string filePath)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                using var codec = SKCodec.Create(stream);
                return codec?.FrameCount ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        public static List<GifFrameInfo> GetFrameInfos(string filePath)
        {
            var infos = new List<GifFrameInfo>();
            try
            {
                using var stream = File.OpenRead(filePath);
                using var codec = SKCodec.Create(stream);
                
                if (codec != null)
                {
                    for (int i = 0; i < codec.FrameCount; i++)
                    {
                        var opts = new SKCodecOptions(i);
                        // Duration is in milliseconds
                        infos.Add(new GifFrameInfo 
                        { 
                            Duration = codec.FrameInfo[i].Duration,
                            Width = codec.Info.Width,
                            Height = codec.Info.Height
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex);
            }
            return infos;
        }

        public static List<SKBitmap> ExtractFrames(string filePath)
        {
            var frames = new List<SKBitmap>();
            try
            {
                using var stream = File.OpenRead(filePath);
                using var codec = SKCodec.Create(stream);

                if (codec != null)
                {
                    for (int i = 0; i < codec.FrameCount; i++)
                    {
                        var info = codec.Info;
                        var bitmap = new SKBitmap(info.Width, info.Height);
                        var ptr = bitmap.GetPixels();
                        
                        // We need to handle disposal/frame accumulation if we want to render strictly correct animation
                        // checking disposal method etc, but SKCodec usually gives the raw frame pixels.
                        // For simple extraction, getting pixels for frame index is key.
                        
                        var opts = new SKCodecOptions(i);
                        var result = codec.GetPixels(info, ptr, opts);
                        
                        if (result == SKCodecResult.Success || result == SKCodecResult.IncompleteInput)
                        {
                            frames.Add(bitmap);
                        }
                        else
                        {
                            bitmap.Dispose();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugHelper.WriteException(ex);
            }
            return frames;
        }

        public static SKBitmap? ExtractFrame(string filePath, int frameIndex)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                using var codec = SKCodec.Create(stream);

                if (codec != null && frameIndex >= 0 && frameIndex < codec.FrameCount)
                {
                    var info = codec.Info;
                    var bitmap = new SKBitmap(info.Width, info.Height);
                    var ptr = bitmap.GetPixels();
                    var opts = new SKCodecOptions(frameIndex);
                    
                    var result = codec.GetPixels(info, ptr, opts);
                    if (result == SKCodecResult.Success || result == SKCodecResult.IncompleteInput)
                    {
                        return bitmap;
                    }
                    bitmap.Dispose();
                }
            }
            catch (Exception ex)
            {
                 DebugHelper.WriteException(ex);
            }
            return null;
        }
    }
}
