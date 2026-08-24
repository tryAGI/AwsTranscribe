# Generate

Transcribe a raw PCM, FLAC, or Ogg Opus file with AWS Transcribe Streaming and Microsoft.Extensions.AI.

This example assumes `using AwsTranscribe;` is in scope, `regionSystemName` contains an AWS region such as `us-east-1`, and the standard AWS credential chain is configured.

```csharp
using var client = new AwsTranscribeClient(regionSystemName);
var samplePath =
    Environment.GetEnvironmentVariable("AWS_TRANSCRIBE_SAMPLE_AUDIO") is { Length: > 0 } samplePathValue
        ? samplePathValue
        : throw new AssertInconclusiveException(
            "AWS_TRANSCRIBE_SAMPLE_AUDIO environment variable is not found.");
var language = Environment.GetEnvironmentVariable("AWS_TRANSCRIBE_LANGUAGE") is { Length: > 0 } languageValue
    ? languageValue
    : AwsTranscribeClient.DefaultLanguage;
var encoding = Environment.GetEnvironmentVariable("AWS_TRANSCRIBE_MEDIA_ENCODING") is { Length: > 0 } encodingValue
    ? encodingValue
    : "pcm";
var sampleRate = Environment.GetEnvironmentVariable("AWS_TRANSCRIBE_SAMPLE_RATE_HERTZ") is { Length: > 0 } rateValue &&
    int.TryParse(rateValue, out var parsedRate)
        ? parsedRate
        : AwsTranscribeClient.DefaultSampleRateHertz;

await using var audio = File.OpenRead(samplePath);
var response = await client.GetTextAsync(audio, new SpeechToTextOptions
{
    SpeechLanguage = language,
    AdditionalProperties = new()
    {
        [AwsTranscribePropertyNames.MediaEncoding] = encoding,
        [AwsTranscribePropertyNames.SampleRateHertz] = sampleRate,
    },
});
```