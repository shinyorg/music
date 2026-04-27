using Android.Content.PM;
using AndroidX.Fragment.App;

namespace Shiny.Music;

internal class PermissionRequestFragment : Fragment
{
    const int RequestCode = 9001;

    TaskCompletionSource<PermissionStatus>? tcs;
    string? permission;

    public Task<PermissionStatus> RequestAsync(FragmentActivity activity, string perm)
    {
        tcs = new TaskCompletionSource<PermissionStatus>();
        permission = perm;

        activity.SupportFragmentManager
            .BeginTransaction()
            .Add(this, "shiny_music_perm")
            .CommitAllowingStateLoss();
        activity.SupportFragmentManager.ExecutePendingTransactions();

        RequestPermissions([permission], RequestCode);
        return tcs.Task;
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        if (requestCode != RequestCode) return;

        var granted = grantResults.Length > 0 && grantResults[0] == Permission.Granted;
        tcs?.TrySetResult(granted ? PermissionStatus.Granted : PermissionStatus.Denied);

        Activity?.SupportFragmentManager
            .BeginTransaction()
            .Remove(this)
            .CommitAllowingStateLoss();
    }
}
