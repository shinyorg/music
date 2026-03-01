namespace Shiny.Music;

/// <summary>
/// Represents the current authorization status for accessing the device music library.
/// </summary>
public enum PermissionStatus
{
    /// <summary>The user has not yet been prompted for permission.</summary>
    Unknown,

    /// <summary>The user explicitly denied access to the music library.</summary>
    Denied,

    /// <summary>The user granted access to the music library.</summary>
    Granted,

    /// <summary>Access is restricted by the system (e.g., parental controls or device management). iOS only.</summary>
    Restricted
}
