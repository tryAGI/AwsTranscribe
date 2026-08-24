namespace AwsTranscribe;

/// <summary>Word or punctuation item returned by AWS Transcribe Streaming.</summary>
public sealed record AwsTranscribeItem(
    string Text,
    TimeSpan? StartTime,
    TimeSpan? EndTime,
    double? Confidence,
    string? Speaker,
    bool? IsStable,
    string? Type);

/// <summary>A transcription alternative returned by AWS Transcribe Streaming.</summary>
public sealed record AwsTranscribeAlternative(
    string Text,
    IReadOnlyList<AwsTranscribeItem> Items);
