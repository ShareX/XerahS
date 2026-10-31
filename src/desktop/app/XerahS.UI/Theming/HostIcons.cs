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

using ShareX.ImageEditor.Presentation.Theming;

namespace XerahS.UI.Theming
{
    /// <summary>
    /// Host glyphs for the Lucide icon font. Always reference <see cref="LucideIcons"/> so every
    /// glyph exists in the bundled font; raw codepoints from other icon fonts render as blank boxes.
    /// </summary>
    public static class HostIcons
    {
        public const string NavigationCapture = LucideIcons.camera;
        public const string NavigationRecording = LucideIcons.film;
        public const string NavigationEditor = LucideIcons.pencil;
        public const string NavigationHistory = LucideIcons.hard_drive;
        public const string NavigationStatistics = LucideIcons.chart_column;
        public const string NavigationWorkflows = LucideIcons.combine;
        public const string NavigationUpload = LucideIcons.cloud_upload;
        public const string NavigationTools = LucideIcons.wand_sparkles;
        public const string NavigationSettings = LucideIcons.sliders_vertical;
        public const string NavigationDebug = LucideIcons.terminal;
        public const string NavigationAbout = LucideIcons.aperture;

        public const string SectionLinks = LucideIcons.link;
        public const string SectionSocial = LucideIcons.users;
        public const string ActionOpenLink = LucideIcons.external_link;
        public const string ActionAdd = LucideIcons.plus;
        public const string ActionMoveUp = LucideIcons.move_up;
        public const string ActionMoveDown = LucideIcons.move_down;
        public const string ActionStart = LucideIcons.play;
        public const string ActionStop = LucideIcons.square;
        public const string ActionPause = LucideIcons.pause;
        public const string ActionResume = LucideIcons.play;
        public const string ActionAbort = LucideIcons.x;
        public const string ActionRestart = LucideIcons.rotate_ccw;
        public const string StatusWarning = LucideIcons.triangle_alert;
        public const string OnboardingTheme = LucideIcons.sun_moon;
        public const string OnboardingHotkeys = LucideIcons.keyboard;
    }
}
