#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
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

using System.Runtime.InteropServices;

namespace XerahS.Common
{
    public class TimerResolutionManager : IDisposable
    {
        private static readonly object thisLock = new object();

        private static bool enabled;
        private static uint lastPeriod;

        public TimerResolutionManager(uint period = 1)
        {
            Enable(period);
        }

        public void Dispose()
        {
            Disable();
        }

        public static bool Enable(uint period = 1)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return false;
            }

            lock (thisLock)
            {
                if (!enabled)
                {
                    TimeCaps timeCaps = new TimeCaps();

                    try
                    {
                        uint result = NativeMethods.TimeGetDevCaps(ref timeCaps, (uint)Marshal.SizeOf(typeof(TimeCaps)));

                        if (result == 0)
                        {
                            period = Math.Max(period, timeCaps.wPeriodMin);
                            result = NativeMethods.TimeBeginPeriod(period);

                            if (result == 0)
                            {
                                lastPeriod = period;
                                enabled = true;
                            }
                        }
                    }
                    catch (DllNotFoundException)
                    {
                        // Ignore if winmm.dll is missing
                    }
                    catch (EntryPointNotFoundException)
                    {
                        // Ignore if entry point is missing
                    }
                }

                return enabled;
            }
        }

        public static bool Disable()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return false;
            }

            lock (thisLock)
            {
                if (enabled)
                {
                    try
                    {
                        uint result = NativeMethods.TimeEndPeriod(lastPeriod);

                        if (result == 0)
                        {
                            enabled = false;
                        }
                    }
                    catch (DllNotFoundException)
                    {
                    }
                    catch (EntryPointNotFoundException)
                    {
                    }
                }

                return !enabled;
            }
        }
    }
}
