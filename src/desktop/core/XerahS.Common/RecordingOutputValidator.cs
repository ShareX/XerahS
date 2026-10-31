using System.Buffers.Binary;

namespace XerahS.Common;

/// <summary>Rejects empty output and MP4 files whose container was never finalized.</summary>
public static class RecordingOutputValidator
{
    public static void EnsureUsable(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length == 0) throw new InvalidDataException("Recording produced an empty file.");
        string extension = Path.GetExtension(path);
        if (!extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".mov", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".m4v", StringComparison.OrdinalIgnoreCase)) return;

        bool hasMetadata = false, hasMedia = false;
        Span<byte> header = stackalloc byte[16];
        while (file.Position < file.Length)
        {
            long start = file.Position;
            if (file.Length - start < 8) break; // An interrupted trailing fragment may be incomplete.
            file.ReadExactly(header[..8]);
            ulong length = BinaryPrimitives.ReadUInt32BigEndian(header);
            int headerSize = 8;
            if (length == 1)
            {
                if (file.Length - file.Position < 8) break;
                file.ReadExactly(header[8..]);
                length = BinaryPrimitives.ReadUInt64BigEndian(header[8..]);
                headerSize = 16;
            }
            else if (length == 0) length = (ulong)(file.Length - start);
            if (length < (ulong)headerSize || length > (ulong)(file.Length - start)) break;
            if (header.Slice(4, 4).SequenceEqual("moov"u8) && length > (ulong)headerSize) hasMetadata = true;
            if (header.Slice(4, 4).SequenceEqual("mdat"u8) && length > (ulong)headerSize) hasMedia = true;
            file.Position = start + (long)length;
        }
        if (!hasMetadata || !hasMedia)
            throw new InvalidDataException("Recording MP4 is incomplete (missing movie metadata or media). The partial file has been kept for recovery.");
    }
}
