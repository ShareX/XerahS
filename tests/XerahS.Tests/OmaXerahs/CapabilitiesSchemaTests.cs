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

using System.Text.Json;
using NUnit.Framework;
using XerahS.OmaXerahs.Commands;
using XerahS.OmaXerahs.Services;

namespace XerahS.Tests.OmaXerahs;

[TestFixture]
public class CapabilitiesSchemaTests
{
    [Test]
    public void BuildResponse_MatchesPluginContractSchema()
    {
        var response = CapabilitiesCommand.BuildResponse();
        string json = JsonStdout.Serialize(response);

        Assert.That(JsonStdout.IsSingleJsonObject(json, out string? error), Is.True, error);

        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.That(root.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(1));
        Assert.That(root.GetProperty("name").GetString(), Is.EqualTo("omaxerahs"));
        Assert.That(root.GetProperty("minPluginProtocol").GetInt32(), Is.EqualTo(1));
        Assert.That(root.GetProperty("version").GetString(), Is.Not.Null.And.Not.Empty);

        var capabilities = root.GetProperty("capabilities").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.That(capabilities, Does.Contain("doctor.image"));
        Assert.That(capabilities, Does.Contain("upload.image"));
        Assert.That(capabilities, Does.Contain("upload.destination"));
        Assert.That(capabilities, Does.Contain("workflow.list"));
        Assert.That(capabilities, Does.Contain("effects.import"));
        Assert.That(capabilities, Does.Contain("skill.install"));
    }
}
