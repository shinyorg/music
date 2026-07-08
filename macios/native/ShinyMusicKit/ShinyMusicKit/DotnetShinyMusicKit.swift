import Foundation
import MusicKit

/// Slim wrapper exposing the MusicKit surface needed by Shiny.Music
/// (subscription status and catalog search).
@objc(DotnetShinyMusicKit)
public class DotnetShinyMusicKit: NSObject {

    /// Checks whether the user has an active Apple Music streaming subscription.
    /// Reads the first value emitted by MusicSubscription.subscriptionUpdates,
    /// which reflects the current subscription state.
    @objc(hasStreamingSubscriptionWithCompletion:)
    public static func hasStreamingSubscription(completion: @escaping (Bool) -> Void) {
        Task {
            for await subscription in MusicSubscription.subscriptionUpdates {
                completion(subscription.canPlayCatalogContent)
                return
            }
            completion(false)
        }
    }

    /// Searches the Apple Music streaming catalog for songs matching `term`.
    /// Results need not be in the user's library. The completion is invoked with a JSON
    /// array string of song objects on success, or an NSError on failure (including when
    /// MusicKit authorization is not granted). Each song object carries the catalog id used
    /// for playback via MPMusicPlayerStoreQueueDescriptor on the managed side.
    @objc(searchCatalogWithTerm:limit:completion:)
    public static func searchCatalog(term: String, limit: Int, completion: @escaping (String?, NSError?) -> Void) {
        Task {
            let status = await MusicAuthorization.request()
            guard status == .authorized else {
                completion(nil, NSError(
                    domain: "ShinyMusicKit",
                    code: 1,
                    userInfo: [NSLocalizedDescriptionKey: "MusicKit authorization was not granted."]
                ))
                return
            }

            do {
                var request = MusicCatalogSearchRequest(term: term, types: [Song.self])
                request.limit = limit
                let response = try await request.response()

                var results: [[String: Any]] = []
                for song in response.songs {
                    var dict: [String: Any] = [
                        "id": song.id.rawValue,
                        "title": song.title,
                        "artist": song.artistName,
                        "isExplicit": song.contentRating == .explicit
                    ]
                    if let album = song.albumTitle {
                        dict["album"] = album
                    }
                    if let duration = song.duration {
                        dict["durationMillis"] = Int(duration * 1000)
                    }
                    if let genre = song.genreNames.first {
                        dict["genre"] = genre
                    }
                    if let artworkUrl = song.artwork?.url(width: 600, height: 600) {
                        dict["artworkUrl"] = artworkUrl.absoluteString
                    }
                    if let releaseDate = song.releaseDate {
                        dict["year"] = Calendar.current.component(.year, from: releaseDate)
                    }
                    results.append(dict)
                }

                let data = try JSONSerialization.data(withJSONObject: results)
                completion(String(data: data, encoding: .utf8), nil)
            } catch {
                completion(nil, error as NSError)
            }
        }
    }
}
