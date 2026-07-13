using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ComputerInventory.Infrastructure.Data;

/// <summary>
/// EF Core value converter that transparently GZip-compresses string data
/// to byte[] for storage and decompresses on read. Applied to restore point
/// snapshot JSON columns to reduce database bloat.
/// </summary>
public class GZipCompressedStringConverter : ValueConverter<string?, byte[]?>
{
    public GZipCompressedStringConverter()
        : base(
            str => Compress(str),
            bytes => Decompress(bytes))
    {
    }

    /// <summary>
    /// Returns the compressed byte count for a string, or 0 if null.
    /// Used to calculate actual storage size for reporting.
    /// </summary>
    public static long GetCompressedSize(string? value)
    {
        if (value is null) return 0;
        return Compress(value)!.Length;
    }

    private static byte[]? Compress(string? value)
    {
        if (value is null)
            return null;

        var raw = Encoding.UTF8.GetBytes(value);
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(raw, 0, raw.Length);
        }
        return output.ToArray();
    }

    private static string? Decompress(byte[]? value)
    {
        if (value is null)
            return null;

        using var input = new MemoryStream(value);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return Encoding.UTF8.GetString(output.ToArray());
    }
}
