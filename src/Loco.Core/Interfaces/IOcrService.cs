using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.Interfaces;

/// <summary>
/// Interface for OCR (Optical Character Recognition) services
/// </summary>
public interface IOcrService
{
    /// <summary>
    /// Extract text from image file
    /// </summary>
    Task<OcrResult> ExtractTextAsync(string imagePath, OcrOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract text from image bytes
    /// </summary>
    Task<OcrResult> ExtractTextFromBytesAsync(byte[] imageBytes, string filename, OcrOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract structured data (forms, tables) from image
    /// </summary>
    Task<StructuredDataResult> ExtractStructuredDataAsync(string imagePath, OcrOptions? options = null, CancellationToken cancellationToken = default);
}
