using System;
using Foundation;
using ObjCRuntime;

namespace ShinyMusicKit
{
    // @interface DotnetShinyMusicKit : NSObject
    [BaseType(typeof(NSObject))]
    interface DotnetShinyMusicKit
    {
        // +(void)hasStreamingSubscriptionWithCompletion:(void (^ _Nonnull)(BOOL))completion;
        [Static]
        [Export("hasStreamingSubscriptionWithCompletion:")]
        [Async]
        void HasStreamingSubscription(Action<bool> completion);
    }
}
