using System;

namespace Hybrasyl;

public class GuidReference
{
    private readonly object _lock = new();

    public GuidReference() { }

    public GuidReference(string name)
    {
        UserName = name;
    }

    public string UserName { get; set; }

    public Guid UserGuid { get; set; }

    public Guid AccountGuid { get; set; }

    public Guid VaultGuid { get; set; }

    public Guid ParcelStoreGuid { get; set; }

    public Guid MailboxGuid { get; set; }

    public Guid AuthInfoGuid { get; set; }
}