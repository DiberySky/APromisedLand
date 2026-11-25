using Microsoft.AspNetCore.DataProtection;
using TreeGraph.IdentityAdmin.Backup;

namespace TreeGraph.IdentityAdmin.Api.Infrastructure;

public sealed class DataProtectionBackupProtector : IBackupProtector
{
    private readonly IDataProtector _protector;

    public DataProtectionBackupProtector(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector("TreeGraph.IdentityAdmin.RealmBackup.v1");

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string protectedText) => _protector.Unprotect(protectedText);
}
