using System;
using System.Threading.Tasks;
using Windows.Security.Credentials.UI;

namespace NightKey.Services;

public enum HelloResult { Verified, Canceled, Failed, Unavailable }

/// <summary>Windows Hello (PIN, fingerprint or face) check before codes are shown.</summary>
public static class HelloGate
{
    public static async Task<bool> IsAvailableAsync()
    {
        try
        {
            return await UserConsentVerifier.CheckAvailabilityAsync() == UserConsentVerifierAvailability.Available;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<HelloResult> VerifyAsync(IntPtr ownerWindow, string message)
    {
        if (!await IsAvailableAsync()) return HelloResult.Unavailable;
        try
        {
            // The interop version parents the Hello prompt to our window so it doesn't open behind it.
            var result = await UserConsentVerifierInterop.RequestVerificationForWindowAsync(ownerWindow, message);
            return result switch
            {
                UserConsentVerificationResult.Verified => HelloResult.Verified,
                UserConsentVerificationResult.Canceled => HelloResult.Canceled,
                UserConsentVerificationResult.DeviceNotPresent
                    or UserConsentVerificationResult.NotConfiguredForUser
                    or UserConsentVerificationResult.DisabledByPolicy => HelloResult.Unavailable,
                _ => HelloResult.Failed,
            };
        }
        catch
        {
            return HelloResult.Failed;
        }
    }
}
