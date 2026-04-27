using AVFoundation;
using ShazamKit;

namespace Shiny.Music;

public class MusicIdentifier : IMusicIdentifier
{
    // Strong references to prevent GC collection while native callbacks are pending
    SHSession? shSession;
    SessionDelegate? sessionDelegate;
    SHSignatureGenerator? signatureGenerator;
    AVAudioEngine? audioEngine;

    public async Task<MusicIdentificationResult?> ListenAsync(CancellationToken cancellationToken = default)
    {
        var granted = await AVAudioApplication.RequestRecordPermissionAsync();
        if (!granted)
            throw new InvalidOperationException("Microphone permission is required to identify songs.");

        var tcs = new TaskCompletionSource<MusicIdentificationResult?>();

        using var reg = cancellationToken.Register(() =>
        {
            this.Cleanup();
            // MainThread.BeginInvokeOnMainThread(Cleanup);
            tcs.TrySetCanceled(cancellationToken);
        });

        // await MainThread.InvokeOnMainThreadAsync(() =>
        // {
            var audioSession = AVAudioSession.SharedInstance();
            audioSession.SetCategory(AVAudioSessionCategory.Record);
            audioSession.SetActive(true);

            shSession = new SHSession();
            sessionDelegate = new SessionDelegate(tcs);
            shSession.Delegate = sessionDelegate;
            signatureGenerator = new SHSignatureGenerator();
            audioEngine = new AVAudioEngine();

            var inputNode = audioEngine.InputNode;
            var recordingFormat = inputNode.GetBusOutputFormat(0);

            inputNode.InstallTapOnBus(0, 1024, recordingFormat, (buffer, when) =>
            {
                signatureGenerator.Append(buffer, when, out _);
            });

            audioEngine.Prepare();
            audioEngine.StartAndReturnError(out var engineError);
            if (engineError != null)
            {
                tcs.TrySetException(new Exception($"Failed to start audio engine: {engineError.LocalizedDescription}"));
                return null;
            }

            // Collect audio then match
            Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ContinueWith(_ =>
            {
                // MainThread.BeginInvokeOnMainThread(() =>
                // {
                    StopAudioEngine();
                    var signature = signatureGenerator.Signature;
                    shSession.Match(signature);
                // });
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        // });

        try
        {
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            this.Cleanup();
            // MainThread.BeginInvokeOnMainThread(Cleanup);
        }
    }

    void Cleanup()
    {
        StopAudioEngine();
        shSession?.Dispose();
        shSession = null;
        sessionDelegate = null;
        signatureGenerator = null;

        var audioSession = AVAudioSession.SharedInstance();
        audioSession.SetActive(false);
    }

    void StopAudioEngine()
    {
        if (audioEngine == null) return;
        try { audioEngine.InputNode.RemoveTapOnBus(0); } catch { }
        audioEngine.Stop();
        audioEngine.Dispose();
        audioEngine = null;
    }

    class SessionDelegate : NSObject, ISHSessionDelegate
    {
        readonly TaskCompletionSource<MusicIdentificationResult?> _tcs;

        public SessionDelegate(TaskCompletionSource<MusicIdentificationResult?> tcs)
            => _tcs = tcs;

        [Export("session:didFindMatch:")]
        public void DidFindMatch(SHSession session, SHMatch match)
        {
            var item = match.MediaItems.FirstOrDefault();
            if (item == null)
            {
                _tcs.TrySetResult(null);
                return;
            }

            var result = new MusicIdentificationResult(
                Title: item.Title ?? "Unknown",
                Artist: item.Artist,
                Album: item.Subtitle,
                Genre: item.Genres?.FirstOrDefault(),
                ArtworkUrl: item.ArtworkUrl?.AbsoluteString,
                MusicUrl: item.AppleMusicUrl?.AbsoluteString,
                Isrc: item.Isrc
            );
            _tcs.TrySetResult(result);
        }

        [Export("session:didNotFindMatchForSignature:error:")]
        public void DidNotFindMatch(SHSession session, SHSignature signature, NSError? error)
        {
            if (error != null)
                _tcs.TrySetException(new Exception(error.LocalizedDescription));
            else
                _tcs.TrySetResult(null);
        }
    }
}
