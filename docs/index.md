<div class="docs-hero">
  <h1>AwsTranscribe</h1>
  <p class="docs-hero-lead">Microsoft.Extensions.AI speech-to-text adapter built on the official AWS Transcribe Streaming and batch .NET SDKs.</p>
  <div class="docs-badge-row">
    <a href="https://www.nuget.org/packages/AwsTranscribe/"><img alt="Nuget package" src="https://img.shields.io/nuget/vpre/AwsTranscribe"></a>
    <a href="https://github.com/tryAGI/AwsTranscribe/actions/workflows/dotnet.yml"><img alt="dotnet" src="https://github.com/tryAGI/AwsTranscribe/actions/workflows/dotnet.yml/badge.svg?branch=main"></a>
    <a href="https://github.com/tryAGI/AwsTranscribe/blob/main/LICENSE"><img alt="License: MIT" src="https://img.shields.io/github/license/tryAGI/AwsTranscribe"></a>
    <a href="https://discord.gg/Ca2xhfBf3v"><img alt="Discord" src="https://img.shields.io/discord/1115206893015662663?label=Discord&amp;logo=discord&amp;logoColor=white&amp;color=d82679"></a>
  </div>
  <div class="docs-hero-actions">
    <a href="#usage">Get started</a>
    <a href="#support">Get support</a>
  </div>
</div>

<div class="docs-feature-grid">
  <div class="docs-feature-card">
    <h3>Official AWS clients</h3>
    <p>Uses <code>AWSSDK.TranscribeStreaming</code> and <code>AWSSDK.TranscribeService</code> with the standard AWS credential chain and regional endpoints.</p>
  </div>
  <div class="docs-feature-card">
    <h3>True bidirectional streaming</h3>
    <p>Implements both Microsoft.Extensions.AI STT methods over the AWS HTTP/2 event-stream API without temporary S3 uploads.</p>
  </div>
  <div class="docs-feature-card">
    <h3>Provider details preserved</h3>
    <p>Alternatives, item timings, confidence, speaker, stability, language, channel, and raw AWS results remain accessible.</p>
  </div>
  <div class="docs-feature-card">
    <h3>Batch API included</h3>
    <p>Use the exposed official service client for S3-backed batch, medical, call analytics, vocabulary, and language-model operations.</p>
  </div>
</div>

## Installation

```bash
dotnet add package AwsTranscribe --prerelease
```

## Usage

```csharp
using AwsTranscribe;
using Microsoft.Extensions.AI;

// Uses the standard AWS SDK credential chain.
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

Streaming accepts the encodings supported by AWS Transcribe Streaming: raw
little-endian signed 16-bit PCM, FLAC, or Ogg Opus. The media encoding and
sample rate must describe the bytes in the supplied stream; a WAV container is
not stripped automatically.

For S3-backed jobs, call `client.BatchClient.StartTranscriptionJobAsync(...)`.
Advanced streaming options can be applied without losing the MEAI abstraction:

```csharp
options.AdditionalProperties[AwsTranscribePropertyNames.ConfigureRequest] =
    (Amazon.TranscribeStreaming.Model.StartStreamTranscriptionRequest request) =>
    {
        request.ShowSpeakerLabel = true;
        request.VocabularyName = "product-terms";
    };
```

<!-- EXAMPLES:START -->
### Generate
Transcribe a raw PCM, FLAC, or Ogg Opus file with AWS Transcribe Streaming and Microsoft.Extensions.AI.

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
<!-- EXAMPLES:END -->

## Support

<div class="docs-card-grid">
  <div class="docs-card">
    <h3>Bugs</h3>
    <p>Open an issue in <a href="https://github.com/tryAGI/AwsTranscribe/issues">tryAGI/AwsTranscribe</a>.</p>
  </div>
  <div class="docs-card">
    <h3>Ideas and questions</h3>
    <p>Use <a href="https://github.com/tryAGI/AwsTranscribe/discussions">GitHub Discussions</a> for design questions and usage help.</p>
  </div>
  <div class="docs-card">
    <h3>Community</h3>
    <p>Join the <a href="https://discord.gg/Ca2xhfBf3v">tryAGI Discord</a> for broader discussion across SDKs.</p>
  </div>
</div>

## Acknowledgments

This adapter depends on the official AWS SDK for .NET packages and Amazon
Transcribe is subject to AWS service terms and pricing.

![JetBrains logo](https://resources.jetbrains.com/storage/products/company/brand/logos/jetbrains.png)

This project is supported by JetBrains through the [Open Source Support Program](https://jb.gg/OpenSourceSupport).
