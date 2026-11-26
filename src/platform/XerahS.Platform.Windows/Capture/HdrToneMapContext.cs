#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.
*/

#endregion License Information (GPL v3)

using Vortice.DXGI;

namespace XerahS.Platform.Windows.Capture;

internal readonly record struct HdrToneMapContext(float SdrWhiteScale, float PeakNits)
{
    public const float SceneReferredSdrWhiteNits = 80f;
    public const float DefaultPeakNits = 1000f;

    public static HdrToneMapContext Default { get; } = new(1f, DefaultPeakNits);

    public HdrToneMapContext Normalize()
    {
        float sdrWhiteScale = float.IsFinite(SdrWhiteScale) && SdrWhiteScale > 0f
            ? Math.Clamp(SdrWhiteScale, 1f, 125f)
            : 1f;
        float peakNits = float.IsFinite(PeakNits) && PeakNits >= SceneReferredSdrWhiteNits
            ? PeakNits
            : DefaultPeakNits;
        return new HdrToneMapContext(sdrWhiteScale, peakNits);
    }

    public static HdrToneMapContext FromOutput(IDXGIOutput output)
    {
        try
        {
            using IDXGIOutput6 output6 = output.QueryInterface<IDXGIOutput6>();
            return FromDescription(output6.Description1);
        }
        catch
        {
            return Default;
        }
    }

    public static HdrToneMapContext FromDescription(OutputDescription1 description)
    {
        float peakNits = description.MaxLuminance;
        float sdrWhiteScale = DisplayConfigHelper.GetSdrWhiteScale(description.DeviceName);
        return new HdrToneMapContext(sdrWhiteScale, peakNits).Normalize();
    }

    public static bool IsHdrOutput(IDXGIOutput output)
    {
        try
        {
            using IDXGIOutput6 output6 = output.QueryInterface<IDXGIOutput6>();
            return IsHdrOutput(output6.Description1);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsHdrOutput(OutputDescription1 description)
    {
        if (!description.AttachedToDesktop ||
            description.ColorSpace != ColorSpaceType.RgbFullG2084NoneP2020)
        {
            return false;
        }

        // The PQ colour space alone can survive an HDR toggle that Windows did not actually apply to
        // the composition, so confirm it with the OS. When the query is unavailable, trust DXGI.
        AdvancedColorState? state = AdvancedColorInfo.GetState(description.DeviceName);
        return state is null || state.Value.Enabled;
    }
}
