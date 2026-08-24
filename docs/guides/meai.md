# Microsoft.Extensions.AI Integration

!!! tip "Cross-SDK comparison"
    See the [centralized MEAI documentation](https://tryagi.github.io/docs/meai/) for feature matrices and comparisons across all tryAGI SDKs.

AwsTranscribe implements `ISpeechToTextClient` on top of the official AWS
Transcribe Streaming event-stream client. It supports buffered and incremental
audio while preserving AWS-native result objects and metadata.

## Installation

```bash
dotnet add package AwsTranscribe
```

## Usage

```csharp
using Microsoft.Extensions.AI;
using AwsTranscribe;

using var client = new AwsTranscribeClient(
    regionSystemName: Environment.GetEnvironmentVariable("AWS_REGION")!);

await using var audio = File.OpenRead("sample.pcm");
var response = await client.GetTextAsync(audio, new SpeechToTextOptions
{
    SpeechLanguage = "en-US",
    AdditionalProperties = new()
    {
        [AwsTranscribePropertyNames.MediaEncoding] = "pcm",
        [AwsTranscribePropertyNames.SampleRateHertz] = 16_000,
    },
});

Console.WriteLine(response.Text);
```

The standard AWS credential chain supplies authentication. Use
`AwsTranscribePropertyNames.ConfigureRequest` for provider-specific streaming
settings and the exposed `BatchClient` for S3-backed transcription jobs.

## Next Steps

- Check the [Examples](../index.md) for complete working code
- See the [centralized MEAI docs](https://tryagi.github.io/docs/meai/) for cross-SDK comparisons
- Visit the [Microsoft.Extensions.AI documentation](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai) for framework details
