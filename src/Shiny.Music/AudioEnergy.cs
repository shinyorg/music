namespace Shiny.Music;

/// <summary>
/// Relative energy classification of an <see cref="AudioSection"/>, derived from its RMS level
/// relative to the loudest part of the same track (not an absolute loudness in dB).
/// </summary>
public enum AudioEnergy
{
    /// <summary>Effectively silent — a lead-in, lead-out, or a gap between sections.</summary>
    Silent,

    /// <summary>Low energy — e.g. a sparse intro, a breakdown, or a quiet verse.</summary>
    Quiet,

    /// <summary>Mid energy — a typical verse or build.</summary>
    Moderate,

    /// <summary>High energy — a chorus, drop, or a driving instrumental such as a solo.</summary>
    Loud
}
