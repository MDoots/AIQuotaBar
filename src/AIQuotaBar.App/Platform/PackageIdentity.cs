namespace AIQuotaBar.App.Platform;

using System.Runtime.InteropServices;
using System.Text;

public interface IPackageIdentity
{
    bool IsPackaged { get; }
}

public sealed class PackageIdentity : IPackageIdentity
{
    private static readonly Lazy<bool> _isPackagedLazy = new(CheckIsPackaged);

    public static bool IsPackaged => _isPackagedLazy.Value;

    bool IPackageIdentity.IsPackaged => IsPackaged;

    private const int APPMODEL_ERROR_NO_PACKAGE = 15700;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder? packageFullName);

    private static bool CheckIsPackaged()
    {
        try
        {
            int length = 0;
            int result = GetCurrentPackageFullName(ref length, null);
            return result != APPMODEL_ERROR_NO_PACKAGE;
        }
        catch
        {
            return false;
        }
    }
}
