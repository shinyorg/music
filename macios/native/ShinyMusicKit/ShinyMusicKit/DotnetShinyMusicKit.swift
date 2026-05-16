import Foundation
import MusicKit

/// Slim wrapper exposing only the MusicSubscription check needed by Shiny.Music.
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
}
