/*
order: 10
title: Generate
slug: generate

Transcribe a raw PCM, FLAC, or Ogg Opus file with AWS Transcribe Streaming and Microsoft.Extensions.AI.
*/

using Microsoft.Extensions.AI;

namespace AwsTranscribe.IntegrationTests;

public partial class Tests
{
    [TestMethod]
    public async Task Example_TranscribeAudio()
    {
        using var client = GetAuthenticatedClient();
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

        response.Text.Should().NotBeNullOrWhiteSpace();
    }
}
