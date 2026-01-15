using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using XerahS.Core;

namespace XerahS.UI.ViewModels;

public partial class FFmpegOptionsViewModel : ObservableObject
{
    private readonly FFmpegOptions _options;

    public FFmpegOptionsViewModel() : this(new FFmpegOptions())
    {
    }

    public FFmpegOptionsViewModel(FFmpegOptions options)
    {
        _options = options ?? new FFmpegOptions();
    }

    public IReadOnlyList<FFmpegCaptureDevice> VideoCaptureDevices { get; } = new[]
    {
        FFmpegCaptureDevice.None,
        FFmpegCaptureDevice.GDIGrab,
        FFmpegCaptureDevice.DDAGrab,
        FFmpegCaptureDevice.ScreenCaptureRecorder
    };

    public IReadOnlyList<FFmpegCaptureDevice> AudioCaptureDevices { get; } = new[]
    {
        FFmpegCaptureDevice.None,
        FFmpegCaptureDevice.VirtualAudioCapturer
    };

    public IReadOnlyList<FFmpegVideoCodec> VideoCodecs { get; } = Enum.GetValues(typeof(FFmpegVideoCodec)).Cast<FFmpegVideoCodec>().ToList();
    public IReadOnlyList<FFmpegAudioCodec> AudioCodecs { get; } = Enum.GetValues(typeof(FFmpegAudioCodec)).Cast<FFmpegAudioCodec>().ToList();
    public IReadOnlyList<FFmpegPreset> X264Presets { get; } = Enum.GetValues(typeof(FFmpegPreset)).Cast<FFmpegPreset>().ToList();
    public IReadOnlyList<FFmpegNVENCPreset> NvencPresets { get; } = Enum.GetValues(typeof(FFmpegNVENCPreset)).Cast<FFmpegNVENCPreset>().ToList();
    public IReadOnlyList<FFmpegNVENCTune> NvencTunes { get; } = Enum.GetValues(typeof(FFmpegNVENCTune)).Cast<FFmpegNVENCTune>().ToList();
    public IReadOnlyList<FFmpegPaletteGenStatsMode> GifStatsModes { get; } = Enum.GetValues(typeof(FFmpegPaletteGenStatsMode)).Cast<FFmpegPaletteGenStatsMode>().ToList();
    public IReadOnlyList<FFmpegPaletteUseDither> GifDithers { get; } = Enum.GetValues(typeof(FFmpegPaletteUseDither)).Cast<FFmpegPaletteUseDither>().ToList();
    public IReadOnlyList<FFmpegAMFUsage> AmfUsages { get; } = Enum.GetValues(typeof(FFmpegAMFUsage)).Cast<FFmpegAMFUsage>().ToList();
    public IReadOnlyList<FFmpegAMFQuality> AmfQualities { get; } = Enum.GetValues(typeof(FFmpegAMFQuality)).Cast<FFmpegAMFQuality>().ToList();
    public IReadOnlyList<FFmpegQSVPreset> QsvPresets { get; } = Enum.GetValues(typeof(FFmpegQSVPreset)).Cast<FFmpegQSVPreset>().ToList();

    public bool OverrideCLIPath
    {
        get => _options.OverrideCLIPath;
        set
        {
            if (_options.OverrideCLIPath != value)
            {
                _options.OverrideCLIPath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EffectiveFFmpegPath));
            }
        }
    }

    public string CLIPath
    {
        get => _options.CLIPath;
        set
        {
            if (_options.CLIPath != value)
            {
                _options.CLIPath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EffectiveFFmpegPath));
            }
        }
    }

    public FFmpegCaptureDevice? SelectedVideoDevice
    {
        get => VideoCaptureDevices.FirstOrDefault(v => v.Value == _options.VideoSource);
        set
        {
            var newValue = value?.Value ?? string.Empty;
            if (_options.VideoSource != newValue)
            {
                _options.VideoSource = newValue;
                OnPropertyChanged();
            }
        }
    }

    public FFmpegCaptureDevice? SelectedAudioDevice
    {
        get => AudioCaptureDevices.FirstOrDefault(v => v.Value == _options.AudioSource);
        set
        {
            var newValue = value?.Value ?? string.Empty;
            if (_options.AudioSource != newValue)
            {
                _options.AudioSource = newValue;
                OnPropertyChanged();
            }
        }
    }

    public FFmpegVideoCodec VideoCodec
    {
        get => _options.VideoCodec;
        set
        {
            if (_options.VideoCodec != value)
            {
                _options.VideoCodec = value;
                OnPropertyChanged();
            }
        }
    }

    public FFmpegAudioCodec AudioCodec
    {
        get => _options.AudioCodec;
        set
        {
            if (_options.AudioCodec != value)
            {
                _options.AudioCodec = value;
                OnPropertyChanged();
            }
        }
    }

    public string UserArgs
    {
        get => _options.UserArgs;
        set
        {
            if (_options.UserArgs != value)
            {
                _options.UserArgs = value;
                OnPropertyChanged();
            }
        }
    }

    public bool UseCustomCommands
    {
        get => _options.UseCustomCommands;
        set
        {
            if (_options.UseCustomCommands != value)
            {
                _options.UseCustomCommands = value;
                OnPropertyChanged();
            }
        }
    }

    public string CustomCommands
    {
        get => _options.CustomCommands;
        set
        {
            if (_options.CustomCommands != value)
            {
                _options.CustomCommands = value;
                OnPropertyChanged();
            }
        }
    }

    public FFmpegPreset X264Preset
    {
        get => _options.x264_Preset;
        set
        {
            if (_options.x264_Preset != value)
            {
                _options.x264_Preset = value;
                OnPropertyChanged();
            }
        }
    }

    public int X264CRF
    {
        get => _options.x264_CRF;
        set
        {
            if (_options.x264_CRF != value)
            {
                _options.x264_CRF = value;
                OnPropertyChanged();
            }
        }
    }

    public bool X264UseBitrate
    {
        get => _options.x264_Use_Bitrate;
        set
        {
            if (_options.x264_Use_Bitrate != value)
            {
                _options.x264_Use_Bitrate = value;
                OnPropertyChanged();
            }
        }
    }

    public int X264Bitrate
    {
        get => _options.x264_Bitrate;
        set
        {
            if (_options.x264_Bitrate != value)
            {
                _options.x264_Bitrate = value;
                OnPropertyChanged();
            }
        }
    }

    public int VPxBitrate
    {
        get => _options.VPx_Bitrate;
        set
        {
            if (_options.VPx_Bitrate != value)
            {
                _options.VPx_Bitrate = value;
                OnPropertyChanged();
            }
        }
    }

    public int XviDQScale
    {
        get => _options.XviD_QScale;
        set
        {
            if (_options.XviD_QScale != value)
            {
                _options.XviD_QScale = value;
                OnPropertyChanged();
            }
        }
    }

    public FFmpegNVENCPreset NvencPreset
    {
        get => _options.NVENC_Preset;
        set
        {
            if (_options.NVENC_Preset != value)
            {
                _options.NVENC_Preset = value;
                OnPropertyChanged();
            }
        }
    }

    public FFmpegNVENCTune NvencTune
    {
        get => _options.NVENC_Tune;
        set
        {
            if (_options.NVENC_Tune != value)
            {
                _options.NVENC_Tune = value;
                OnPropertyChanged();
            }
        }
    }

    public int NvencBitrate
    {
        get => _options.NVENC_Bitrate;
        set
        {
            if (_options.NVENC_Bitrate != value)
            {
                _options.NVENC_Bitrate = value;
                OnPropertyChanged();
            }
        }
    }

    public FFmpegPaletteGenStatsMode GifStatsMode
    {
        get => _options.GIFStatsMode;
        set
        {
            if (_options.GIFStatsMode != value)
            {
                _options.GIFStatsMode = value;
                OnPropertyChanged();
            }
        }
    }

    public FFmpegPaletteUseDither GifDither
    {
        get => _options.GIFDither;
        set
        {
            if (_options.GIFDither != value)
            {
                _options.GIFDither = value;
                OnPropertyChanged();
            }
        }
    }

    public int GifBayerScale
    {
        get => _options.GIFBayerScale;
        set
        {
            if (_options.GIFBayerScale != value)
            {
                _options.GIFBayerScale = value;
                OnPropertyChanged();
            }
        }
    }

    public FFmpegAMFUsage AmfUsage
    {
        get => _options.AMF_Usage;
        set
        {
            if (_options.AMF_Usage != value)
            {
                _options.AMF_Usage = value;
                OnPropertyChanged();
            }
        }
    }

    public FFmpegAMFQuality AmfQuality
    {
        get => _options.AMF_Quality;
        set
        {
            if (_options.AMF_Quality != value)
            {
                _options.AMF_Quality = value;
                OnPropertyChanged();
            }
        }
    }

    public int AmfBitrate
    {
        get => _options.AMF_Bitrate;
        set
        {
            if (_options.AMF_Bitrate != value)
            {
                _options.AMF_Bitrate = value;
                OnPropertyChanged();
            }
        }
    }

    public FFmpegQSVPreset QsvPreset
    {
        get => _options.QSV_Preset;
        set
        {
            if (_options.QSV_Preset != value)
            {
                _options.QSV_Preset = value;
                OnPropertyChanged();
            }
        }
    }

    public int QsvBitrate
    {
        get => _options.QSV_Bitrate;
        set
        {
            if (_options.QSV_Bitrate != value)
            {
                _options.QSV_Bitrate = value;
                OnPropertyChanged();
            }
        }
    }

    public int AACBitrate
    {
        get => _options.AAC_Bitrate;
        set
        {
            if (_options.AAC_Bitrate != value)
            {
                _options.AAC_Bitrate = value;
                OnPropertyChanged();
            }
        }
    }

    public int OpusBitrate
    {
        get => _options.Opus_Bitrate;
        set
        {
            if (_options.Opus_Bitrate != value)
            {
                _options.Opus_Bitrate = value;
                OnPropertyChanged();
            }
        }
    }

    public int VorbisQScale
    {
        get => _options.Vorbis_QScale;
        set
        {
            if (_options.Vorbis_QScale != value)
            {
                _options.Vorbis_QScale = value;
                OnPropertyChanged();
            }
        }
    }

    public int MP3QScale
    {
        get => _options.MP3_QScale;
        set
        {
            if (_options.MP3_QScale != value)
            {
                _options.MP3_QScale = value;
                OnPropertyChanged();
            }
        }
    }

    public string EffectiveFFmpegPath => _options.FFmpegPath;
}
