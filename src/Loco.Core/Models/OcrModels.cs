using System;
using System.Collections.Generic;

namespace Loco.Core.Models;

/// <summary>
/// OCR configuration settings
/// </summary>
public class OcrConfig
{
    public string Provider { get; set; } = "tesseract";
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
    public string? Region { get; set; }
    public int TimeoutSeconds { get; set; } = 300;
    public Dictionary<string, string> AdditionalSettings { get; set; } = new();
}

/// <summary>
/// OCR processing options
/// </summary>
public class OcrOptions
{
    public string Language { get; set; } = "auto";
    public bool DetectOrientation { get; set; } = true;
    public bool EnhanceImage { get; set; } = true;
    public int ConfidenceThreshold { get; set; } = 60;
    public List<string> RegionsOfInterest { get; set; } = new();
}

/// <summary>
/// OCR processing result
/// </summary>
public class OcrResult
{
    public bool Success { get; set; }
    public string? ExtractedText { get; set; }
    public List<TextRegion>? TextRegions { get; set; }
    public int Confidence { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Text region in OCR result
/// </summary>
public class TextRegion
{
    public string Text { get; set; } = string.Empty;
    public int Confidence { get; set; }
    public BoundingBox BoundingBox { get; set; } = new();
    public string? Language { get; set; }
}

/// <summary>
/// Bounding box for text region
/// </summary>
public class BoundingBox
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

/// <summary>
/// Structured data extraction result
/// </summary>
public class StructuredDataResult
{
    public bool Success { get; set; }
    public List<DataField>? Fields { get; set; }
    public List<DataTable>? Tables { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Extracted data field
/// </summary>
public class DataField
{
    public string FieldName { get; set; } = string.Empty;
    public string FieldValue { get; set; } = string.Empty;
    public int Confidence { get; set; }
    public BoundingBox BoundingBox { get; set; } = new();
}

/// <summary>
/// Extracted data table
/// </summary>
public class DataTable
{
    public string TableTitle { get; set; } = string.Empty;
    public List<List<string>> Rows { get; set; } = new();
    public int Confidence { get; set; }
    public BoundingBox BoundingBox { get; set; } = new();
}
