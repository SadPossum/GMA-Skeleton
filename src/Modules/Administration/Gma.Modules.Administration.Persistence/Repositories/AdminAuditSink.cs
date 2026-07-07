namespace Gma.Modules.Administration.Persistence.Repositories;

using Gma.Modules.Administration.Persistence.Entities;
using Gma.Framework.Administration;

internal sealed class AdminAuditSink(AdminDbContext dbContext) : IAdminAuditSink
{
    public async Task RecordAsync(AdminAuditRecord record, CancellationToken cancellationToken)
    {
        dbContext.AuditEntries.Add(new AdminAuditEntry(record));

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
