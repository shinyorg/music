namespace Shiny.Music.Internal;

/// <summary>
/// Shared, platform-agnostic post-processing for the offline level analysis. Platform code decodes the
/// track to PCM and hands us the raw per-window RMS and peak magnitudes; we normalize them and collapse
/// the envelope into contiguous <see cref="AudioSection"/> runs. Keeping this here means Android and Apple
/// produce identical, comparable results.
/// </summary>
static class AudioAnalysis
{
    // Section energy thresholds, expressed as a fraction of the track's own loudest window RMS, so a
    // quietly-mastered track is still classified by its internal dynamics rather than absolute level.
    const float SilentThreshold = 0.05f;
    const float QuietThreshold = 0.45f;
    const float LoudThreshold = 0.80f;

    // Sections shorter than this are merged into their neighbour to avoid a noisy, fragmented structure.
    static readonly TimeSpan MinSectionDuration = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Builds an <see cref="AudioLevels"/> from raw (un-normalized) per-window measurements.
    /// </summary>
    /// <param name="window">The duration each window represents.</param>
    /// <param name="duration">The total track duration.</param>
    /// <param name="rawRms">Raw RMS magnitude per window (0..1 float PCM scale, not yet normalized).</param>
    /// <param name="rawPeak">Raw peak magnitude per window, aligned with <paramref name="rawRms"/>.</param>
    public static AudioLevels Build(TimeSpan window, TimeSpan duration, IReadOnlyList<float> rawRms, IReadOnlyList<float> rawPeak)
    {
        var count = rawRms.Count;
        var rms = new float[count];
        var peak = new float[count];

        // Normalize everything against the single loudest sample so 1.0 == "as loud as this track gets".
        var maxPeak = 0f;
        for (var i = 0; i < count; i++)
        {
            if (rawPeak[i] > maxPeak)
                maxPeak = rawPeak[i];
        }

        if (maxPeak > 0f)
        {
            for (var i = 0; i < count; i++)
            {
                rms[i] = Math.Clamp(rawRms[i] / maxPeak, 0f, 1f);
                peak[i] = Math.Clamp(rawPeak[i] / maxPeak, 0f, 1f);
            }
        }

        var sections = DeriveSections(rms, window, duration);
        return new AudioLevels(window, duration, rms, peak, sections);
    }

    static IReadOnlyList<AudioSection> DeriveSections(float[] rms, TimeSpan window, TimeSpan duration)
    {
        if (rms.Length == 0)
            return Array.Empty<AudioSection>();

        var maxRms = 0f;
        foreach (var v in rms)
        {
            if (v > maxRms)
                maxRms = v;
        }

        if (maxRms <= 0f)
            return new[] { new AudioSection(TimeSpan.Zero, duration, AudioEnergy.Silent, 0f) };

        // 1. Classify every window, then 2. group equal-classification runs, then 3. absorb short runs.
        var raw = new List<(AudioEnergy Energy, int Start, int Count, double Sum)>();
        for (var i = 0; i < rms.Length; i++)
        {
            var energy = Classify(rms[i], maxRms);
            if (raw.Count > 0 && raw[^1].Energy == energy)
            {
                var last = raw[^1];
                raw[^1] = (last.Energy, last.Start, last.Count + 1, last.Sum + rms[i]);
            }
            else
            {
                raw.Add((energy, i, 1, rms[i]));
            }
        }

        var minWindows = Math.Max(1, (int)Math.Round(MinSectionDuration.TotalSeconds / Math.Max(window.TotalSeconds, 0.001)));
        var merged = new List<(AudioEnergy Energy, int Start, int Count, double Sum)>();
        foreach (var run in raw)
        {
            if (run.Count < minWindows && merged.Count > 0)
            {
                // Fold this too-short run into the previous section, keeping the dominant section's energy.
                var prev = merged[^1];
                merged[^1] = (prev.Energy, prev.Start, prev.Count + run.Count, prev.Sum + run.Sum);
            }
            else
            {
                merged.Add(run);
            }
        }

        var sections = new List<AudioSection>(merged.Count);
        for (var i = 0; i < merged.Count; i++)
        {
            var run = merged[i];
            var start = window * run.Start;
            // The last section runs to the true track end so the timeline covers the whole song.
            var end = i == merged.Count - 1 ? Max(duration, window * (run.Start + run.Count)) : window * (run.Start + run.Count);
            var average = (float)(run.Sum / run.Count);
            sections.Add(new AudioSection(start, end - start, run.Energy, average));
        }

        return sections;
    }

    static AudioEnergy Classify(float value, float maxRms)
    {
        var ratio = value / maxRms;
        if (ratio < SilentThreshold)
            return AudioEnergy.Silent;
        if (ratio < QuietThreshold)
            return AudioEnergy.Quiet;
        if (ratio < LoudThreshold)
            return AudioEnergy.Moderate;
        return AudioEnergy.Loud;
    }

    static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}
