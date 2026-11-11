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

using XerahS.Platform.Abstractions;

namespace XerahS.Assistant.Ai;

/// <summary>
/// Plugs the AI features into the rest of the application. Call once at start-up, after the OCR service is in place.
/// </summary>
public static class AiIntegrations
{
    public static void Register()
    {
        // The engine list asks the engine whether a provider is set up each time, so this is safe before one is.
        (PlatformServices.Ocr as IOcrEngineHost)?.AddEngine(new AiOcrEngine());
    }
}
