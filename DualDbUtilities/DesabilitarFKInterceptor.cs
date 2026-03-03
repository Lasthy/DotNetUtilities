using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DualDbUtilities;

/// <summary>
/// Interceptor que desabilita a verificação de foreign keys no SQLite temporário.
/// <para>
/// O banco temporário é uma área de staging — entidades podem referenciar FKs que
/// existem apenas no banco final. A integridade referencial é validada durante a
/// sincronização, quando os dados são transferidos para o banco final.
/// </para>
/// </summary>
public sealed class DesabilitarFKInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = OFF;";
        cmd.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = OFF;";
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
