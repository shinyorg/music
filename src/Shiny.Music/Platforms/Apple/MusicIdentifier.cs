using AVFoundation;
using ShazamKit;

namespace Shiny.Music;

public class MusicIdentifier : IMusicIdentifier
{
    public async Task<MusicIdentificationResult?> ListenAsync(CancellationToken cancellationToken = default)
    {
        if (!await AVAudioApplication.RequestRecordPermissionAsync())
            throw new InvalidOperationException("Microphone permission is required to identify songs.");

        var audioSession = AVAudioSession.SharedInstance();
        audioSession.SetCategory(AVAudioSessionCategory.Record);
        audioSession.SetActive(true);

        var sigGen = new SHSignatureGenerator();
        var engine = new AVAudioEngine();
        var inputNode = engine.InputNode;
        var format = inputNode.GetBusOutputFormat(0);

        inputNode.InstallTapOnBus(0, 4096, format, (buffer, when) =>
        {
            try { sigGen.Append(buffer, when, out _); }
            catch { }
        });

        engine.Prepare();
        engine.StartAndReturnError(out var engineError);
        if (String.IsWhitespaceOrNull(engineError?.LocalizedDescription))
        {
            engine.Dispose();
            try { audioSession.SetActive(false); } catch { }
            throw new InvalidOperationException($"Failed to start audio engine: {engineError.LocalizedDescription}");
        }

        // Record for ~5 seconds, then stop
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
        finally
        {
            try { inputNode.RemoveTapOnBus(0); } catch { }
            try { engine.Stop(); } catch { }
            engine.Dispose();
        }

        var signature = sigGen.Signature;
        if (signature == null)
        {
            try { audioSession.SetActive(false); } catch { }
            return null;
        }

        // ShazamKit requires Match to be called on the main thread
        var tcs = new TaskCompletionSource<MusicIdentificationResult?>();
        var session = new SHSession();
        session.Delegate = new SessionDelegate(tcs);

        using var reg = cancellationToken.Register(() =>
        {
            session.Dispose();
            tcs.TrySetCanceled(cancellationToken);
        });

        CoreFoundation.DispatchQueue.MainQueue.DispatchAsync(() => session.Match(signature));

        try
        {
            return await tcs.Task;
        }
        finally
        {
            session.Dispose();
            try { audioSession.SetActive(false); } catch { }
        }
    }

    class SessionDelegate : Foundation.NSObject, ISHSessionDelegate
    {
        readonly TaskCompletionSource<MusicIdentificationResult?> _tcs;

        public SessionDelegate(TaskCompletionSource<MusicIdentificationResult?> tcs)
            => _tcs = tcs;

        [Foundation.Export("session:didFindMatch:")]
        public void DidFindMatch(SHSession session, SHMatch match)
        {
            var item = match.MediaItems.FirstOrDefault();
            if (item == null)
            {
                _tcs.TrySetResult(null);
                return;
            }

            _tcs.TrySetResult(new MusicIdentificationResult(
                Title: item.Title ?? "Unknown",
                Artist: item.Artist,
                Album: item.Subtitle,
                Genre: item.Genres?.FirstOrDefault(),
                ArtworkUrl: item.ArtworkUrl?.AbsoluteString,
                MusicUrl: item.AppleMusicUrl?.AbsoluteString,
                Isrc: item.Isrc
            ));
        }

        [Foundation.Export("session:didNotFindMatchForSignature:error:")]
        public void DidNotFindMatch(SHSession session, SHSignature signature, Foundation.NSError? error)
        {
            if (error != null)
                _tcs.TrySetException(new Exception(error.LocalizedDescription));
            else
                _tcs.TrySetResult(null);
        }
    }
}
