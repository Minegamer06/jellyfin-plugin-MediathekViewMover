using System.Globalization;
using System.IO;
using System.Linq;

namespace Jellyfin.Plugin.MediathekViewMover.Models;

/// <summary>
/// The input model for file processing in the MediathekView Mover plugin.
/// </summary>
public class FileInput
{
    private string? _hash;

    /// <summary>
    /// Gets or sets the Language of the media file.
    /// </summary>
    public CultureInfo Language { get; set; } = null!;

    /// <summary>
    /// Gets or sets a value indicating whether the file contains audio description.
    /// </summary>
    public bool IsAudioDescription { get; set; }

    /// <summary>
    /// Gets or sets the file information for the media file.
    /// </summary>
    public FileInfo File { get; set; } = null!;

    /// <summary>
    /// Gets the file Hash.
    /// </summary>
    public string Hash
    {
        get
        {
            if (!string.IsNullOrEmpty(_hash))
            {
                return _hash;
            }

            using var stream = File.OpenRead();
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(stream);
            _hash = string.Concat(hashBytes.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));

            return _hash;
        }
    }
}
