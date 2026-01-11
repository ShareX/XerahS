#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
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

using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text;
using DrawingPoint = System.Drawing.Point;
using DrawingPointF = System.Drawing.PointF;
using DrawingSize = System.Drawing.Size;

namespace XerahS.Common
{
    public static class Extensions
    {
        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (action == null) throw new ArgumentNullException(nameof(action));

            foreach (T item in source)
            {
                action(item);
            }
        }

        public static byte[] GetBytes(this Image img)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                img.Save(ms, img.RawFormat);
                return ms.ToArray();
            }
        }

        public static Stream GetStream(this Image img)
        {
            MemoryStream ms = new MemoryStream();
            img.Save(ms, img.RawFormat);
            return ms;
        }

        public static ImageCodecInfo GetCodecInfo(this ImageFormat format)
        {
            return ImageCodecInfo.GetImageEncoders().FirstOrDefault(info => info.FormatID.Equals(format.Guid));
        }

        public static string GetMimeType(this ImageFormat format)
        {
            ImageCodecInfo codec = format.GetCodecInfo();
            return codec != null ? codec.MimeType : "image/unknown";
        }

        public static double ToDouble(this Version value)
        {
            return (Math.Max(value.Major, 0) * Math.Pow(10, 12)) +
                (Math.Max(value.Minor, 0) * Math.Pow(10, 9)) +
                (Math.Max(value.Build, 0) * Math.Pow(10, 6)) +
                Math.Max(value.Revision, 0);
        }

        public static DrawingPoint Add(this DrawingPoint point, int offsetX, int offsetY)
        {
            return new DrawingPoint(point.X + offsetX, point.Y + offsetY);
        }

        public static DrawingPoint Add(this DrawingPoint point, DrawingPoint offset)
        {
            return new DrawingPoint(point.X + offset.X, point.Y + offset.Y);
        }

        public static DrawingPoint Add(this DrawingPoint point, int offset)
        {
            return point.Add(offset, offset);
        }

        public static DrawingPointF Add(this DrawingPointF point, float offsetX, float offsetY)
        {
            return new DrawingPointF(point.X + offsetX, point.Y + offsetY);
        }

        public static DrawingPointF Add(this DrawingPointF point, DrawingPointF offset)
        {
            return new DrawingPointF(point.X + offset.X, point.Y + offset.Y);
        }

        public static DrawingPointF Scale(this DrawingPoint point, float scaleFactor)
        {
            return new DrawingPointF(point.X * scaleFactor, point.Y * scaleFactor);
        }

        public static DrawingPointF Scale(this DrawingPointF point, float scaleFactor)
        {
            return new DrawingPointF(point.X * scaleFactor, point.Y * scaleFactor);
        }

        public static DrawingPoint Round(this DrawingPointF point)
        {
            return DrawingPoint.Round(point);
        }

        public static void Offset(this DrawingPointF point, DrawingPointF offset)
        {
            point.X += offset.X;
            point.Y += offset.Y;
        }

        public static DrawingSize Offset(this DrawingSize size, int offset)
        {
            return size.Offset(offset, offset);
        }

        public static DrawingSize Offset(this DrawingSize size, int width, int height)
        {
            return new DrawingSize(size.Width + width, size.Height + height);
        }

        public static string Join<T>(this T[] array, string separator = " ")
        {
            StringBuilder sb = new StringBuilder();

            if (array != null)
            {
                foreach (T t in array)
                {
                    if (sb.Length > 0 && !string.IsNullOrEmpty(separator)) sb.Append(separator);
                    sb.Append(t);
                }
            }

            return sb.ToString();
        }

        public static int WeekOfYear(this DateTime dateTime)
        {
            return CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(dateTime, CalendarWeekRule.FirstDay, DayOfWeek.Monday);
        }

        public static void ApplyDefaultPropertyValues(this object self)
        {
            foreach (PropertyDescriptor prop in TypeDescriptor.GetProperties(self))
            {
                if (prop.Attributes[typeof(DefaultValueAttribute)] is DefaultValueAttribute attr)
                {
                    prop.SetValue(self, attr.Value);
                }
            }
        }

        public static Bitmap CreateEmptyBitmap(this Image img, int widthOffset = 0, int heightOffset = 0, PixelFormat pixelFormat = PixelFormat.Format32bppArgb)
        {
            Bitmap bmp = new Bitmap(img.Width + widthOffset, img.Height + heightOffset, pixelFormat);
            bmp.SetResolution(img.HorizontalResolution, img.VerticalResolution);
            return bmp;
        }

        public static Bitmap CreateEmptyBitmap(this Image img, PixelFormat pixelFormat)
        {
            return img.CreateEmptyBitmap(0, 0, pixelFormat);
        }

        public static string GetDescription(this Type type)
        {
            DescriptionAttribute[] attributes = (DescriptionAttribute[])type.GetCustomAttributes(typeof(DescriptionAttribute), false);
            return attributes.Length > 0 ? attributes[0].Description : type.Name;
        }

        public static IEnumerable<T> TakeLast<T>(this IEnumerable<T> source, int count)
        {
            return source.Reverse().Take(count).Reverse();
        }

        public static Version Normalize(this Version version, bool ignoreRevision = false, bool ignoreBuild = false, bool ignoreMinor = false)
        {
            return new Version(Math.Max(version.Major, 0),
                ignoreMinor ? 0 : Math.Max(version.Minor, 0),
                ignoreBuild ? 0 : Math.Max(version.Build, 0),
                ignoreRevision ? 0 : Math.Max(version.Revision, 0));
        }

        public static Task ContinueInCurrentContext(this Task task, Action action)
        {
            TaskScheduler scheduler = TaskScheduler.FromCurrentSynchronizationContext();
            return task.ContinueWith(_ => action(), scheduler);
        }

        public static T CloneSafe<T>(this T obj) where T : class, ICloneable
        {
            try
            {
                if (obj != null)
                {
                    return obj.Clone() as T;
                }
            }
            catch (Exception e)
            {
                DebugHelper.WriteException(e);
            }

            return null;
        }

        public static bool IsTransparent(this Color color)
        {
            return color.A < 255;
        }

        public static void ShowError(this Exception e)
        {
            DebugHelper.WriteException(e);
            // TODO: [Avalonia] Implement MessageBox or notification for error display
        }
    }
}
