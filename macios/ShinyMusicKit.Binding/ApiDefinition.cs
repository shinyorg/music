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

        // +(void)searchCatalogWithTerm:(NSString * _Nonnull)term limit:(NSInteger)limit completion:(void (^ _Nonnull)(NSString * _Nullable, NSError * _Nullable))completion;
        [Static]
        [Export("searchCatalogWithTerm:limit:completion:")]
        [Async]
        void SearchCatalog(string term, nint limit, Action<NSString, NSError> completion);
    }
}
