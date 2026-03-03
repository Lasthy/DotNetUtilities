using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;

namespace DualDbUtilities;

/// <summary>
/// Contexto dual que coordena operações entre o banco temporário (SQLite) e o banco final (SQL Server).
/// <para>Leituras são feitas do banco final; escritas vão para o banco temporário.</para>
/// <para>A sincronização transfere dados do temporário para o final com upsert e controle transacional.</para>
/// </summary>
public class DualDbContext
{
    private readonly TemporarioDbContext _temporario;
    private readonly FinalDbContext _final;
    private readonly DualDbOptions _options;
    private readonly DualDbSyncCoordinator _coordinator;
    private readonly ILogger<DualDbContext> _logger;

    public DualDbContext(
        TemporarioDbContext temporario,
        FinalDbContext finalDb,
        DualDbOptions options,
        DualDbSyncCoordinator coordinator,
        ILogger<DualDbContext> logger)
    {
        _temporario = temporario;
        _final = finalDb;
        _options = options;
        _coordinator = coordinator;
        _logger = logger;
    }

    #region Leitura (Banco Final)

    /// <summary>
    /// Retorna um <see cref="IQueryable{T}"/> para consulta de entidades no banco final (sem tracking).
    /// </summary>
    public IQueryable<T> Consultar<T>() where T : class, IEntidade
        => _final.Set<T>().AsNoTracking();

    /// <summary>
    /// Busca uma entidade por chave primária no banco final.
    /// </summary>
    public async ValueTask<T?> BuscarAsync<T>(params object[] chaves) where T : class, IEntidade
        => await _final.Set<T>().FindAsync(chaves);

    #endregion

    #region Escrita (Banco Temporário)

    /// <summary>
    /// Adiciona ou atualiza uma entidade no banco temporário (upsert por PK).
    /// <para>
    /// Se a entidade já existir no temporário, ela será atualizada.
    /// Se a entidade implementar <see cref="IEntidadePlaceholder"/> e for placeholder,
    /// ela <b>não</b> sobrescreverá uma entidade completa já existente.
    /// </para>
    /// </summary>
    public async Task AdicionarAsync<T>(T entidade, CancellationToken ct = default) where T : class, IEntidade
    {
        await using var _ = await _coordinator.AdquirirOperacaoAsync(ct);
        await UpsertTemporarioAsync(entidade, ct);
        await _temporario.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Adiciona ou atualiza várias entidades no banco temporário em batch (upsert por PK).
    /// Aplica as mesmas regras de placeholder de <see cref="AdicionarAsync{T}"/>.
    /// </summary>
    public async Task AdicionarVariosAsync<T>(IEnumerable<T> entidades, CancellationToken ct = default) where T : class, IEntidade
    {
        await using var _ = await _coordinator.AdquirirOperacaoAsync(ct);
        foreach (var entidade in entidades)
            await UpsertTemporarioAsync(entidade, ct);
        await _temporario.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Marca uma entidade como modificada no banco temporário e persiste imediatamente.
    /// </summary>
    public async Task AtualizarAsync<T>(T entidade, CancellationToken ct = default) where T : class, IEntidade
    {
        await using var _ = await _coordinator.AdquirirOperacaoAsync(ct);
        _temporario.Set<T>().Update(entidade);
        await _temporario.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Remove uma entidade do banco temporário e persiste imediatamente.
    /// </summary>
    public async Task RemoverAsync<T>(T entidade, CancellationToken ct = default) where T : class, IEntidade
    {
        await using var _ = await _coordinator.AdquirirOperacaoAsync(ct);
        _temporario.Set<T>().Remove(entidade);
        await _temporario.SaveChangesAsync(ct);
    }

    #endregion

    #region Acesso Direto

    /// <summary>
    /// Acesso direto ao <see cref="DbSet{T}"/> do banco temporário para cenários avançados.
    /// Use <see cref="SalvarTemporarioAsync"/> para persistir as alterações.
    /// </summary>
    public DbSet<T> TemporarioSet<T>() where T : class, IEntidade
        => _temporario.Set<T>();

    /// <summary>
    /// Acesso direto ao <see cref="DbSet{T}"/> do banco final para cenários avançados.
    /// </summary>
    public DbSet<T> FinalSet<T>() where T : class, IEntidade
        => _final.Set<T>();

    /// <summary>
    /// Salva mudanças pendentes no banco temporário (uso com <see cref="TemporarioSet{T}"/>).
    /// </summary>
    public async Task SalvarTemporarioAsync(CancellationToken ct = default)
    {
        await using var _ = await _coordinator.AdquirirOperacaoAsync(ct);
        await _temporario.SaveChangesAsync(ct);
    }

    #endregion

    #region Upsert helpers

    /// <summary>
    /// Faz upsert de uma entidade no banco temporário respeitando regras de placeholder
    /// e remapeamento de PK via <see cref="IIdentificavelPorNome"/>.
    /// <para>Fluxo:</para>
    /// <list type="number">
    ///   <item><description>Busca por PK no temporário.</description></item>
    ///   <item><description>Se não encontrado por PK e implementa <see cref="IIdentificavelPorNome"/> + <see cref="IEntidadePlaceholder"/>:
    ///     busca placeholder existente por <c>Nome</c>.</description></item>
    ///   <item><description>Se encontrado por Nome com PK diferente e o existente é placeholder:
    ///     remapeia a PK antiga para a nova em cascata (incluindo FKs dependentes).</description></item>
    ///   <item><description>Aplica regras de placeholder normais para decidir se atualiza ou ignora.</description></item>
    /// </list>
    /// </summary>
    private async Task UpsertTemporarioAsync<T>(T entidade, CancellationToken ct) where T : class, IEntidade
    {
        var entityType = _temporario.Model.FindEntityType(typeof(T));
        var pkProperties = entityType?.FindPrimaryKey()?.Properties;

        if (pkProperties is not { Count: > 0 } || entityType is null)
        {
            _temporario.Set<T>().Add(entidade);
            return;
        }

        var pkValues = pkProperties
            .Select(p => p.PropertyInfo!.GetValue(entidade))
            .ToArray()!;

        var existente = await _temporario.Set<T>().FindAsync(pkValues, ct);

        if (existente != null)
        {
            // Encontrou por PK — upsert normal com regra de placeholder
            if (DeveIgnorarPorPlaceholder(entidade, existente))
            {
                _logger.LogDebug(
                    "Entidade {Tipo} com PK [{PK}] é placeholder e não sobrescreverá registro completo no temporário.",
                    typeof(T).Name,
                    string.Join(", ", pkValues));
                return;
            }

            _temporario.Entry(existente).CurrentValues.SetValues(entidade);
            return;
        }

        // Não encontrou por PK — verificar se existe placeholder com mesmo Nome
        if (entidade is IIdentificavelPorNome nomeada
            && typeof(IEntidadePlaceholder).IsAssignableFrom(typeof(T)))
        {
            var nome = nomeada.Nome;
            var placeholderExistente = await BuscarPlaceholderPorNomeAsync<T>(nome, ct);

            if (placeholderExistente != null)
            {
                var existentePkValues = pkProperties
                    .Select(p => p.PropertyInfo!.GetValue(placeholderExistente))
                    .ToArray()!;

                var pksIguais = pkValues.Length == existentePkValues.Length
                    && pkValues.Zip(existentePkValues).All(pair =>
                        Equals(pair.First, pair.Second));

                if (!pksIguais)
                {
                    if (DeveIgnorarPorPlaceholder(entidade, placeholderExistente))
                    {
                        _logger.LogDebug(
                            "Entidade {Tipo} \"{Nome}\" é placeholder e não sobrescreverá registro completo no temporário.",
                            typeof(T).Name, nome);
                        return;
                    }

                    _logger.LogInformation(
                        "Remapeando PK de {Tipo} \"{Nome}\": [{PKAntiga}] → [{PKNova}].",
                        typeof(T).Name, nome,
                        string.Join(", ", existentePkValues),
                        string.Join(", ", pkValues));

                    // Salva mudanças pendentes antes do remap (que usa SQL raw)
                    await _temporario.SaveChangesAsync(ct);
                    _temporario.ChangeTracker.Clear();

                    await RemapearChavePrimariaAsync(entityType, existentePkValues!, pkValues!, entidade, ct);
                    return;
                }

                // PK igual — upsert normal
                if (DeveIgnorarPorPlaceholder(entidade, placeholderExistente))
                    return;

                _temporario.Entry(placeholderExistente).CurrentValues.SetValues(entidade);
                return;
            }
        }

        // Não existe nada — inserir
        _temporario.Set<T>().Add(entidade);
    }

    /// <summary>
    /// Busca um placeholder por Nome no banco temporário.
    /// Retorna <c>null</c> se a entidade não implementar <see cref="IEntidadePlaceholder"/>.
    /// </summary>
    private async Task<T?> BuscarPlaceholderPorNomeAsync<T>(string nome, CancellationToken ct) where T : class
    {
        if (!typeof(IIdentificavelPorNome).IsAssignableFrom(typeof(T)))
            return null;

        // Busca pelo Nome usando LINQ dinâmico via expressão lambda compilada
        var resultados = await _temporario.Set<T>().ToListAsync(ct);
        return resultados.FirstOrDefault(e =>
            e is IIdentificavelPorNome n && n.Nome == nome
            && e is IEntidadePlaceholder p && p.EhPlaceholder);
    }

    /// <summary>
    /// Remapeia a chave primária de uma entidade no banco temporário e atualiza todas
    /// as FKs dependentes em cascata. Utiliza SQL raw pois EF Core não permite alterar PKs.
    /// </summary>
    private async Task RemapearChavePrimariaAsync<T>(
        IEntityType entityType,
        object[] pkAntiga,
        object[] pkNova,
        T entidadeNova,
        CancellationToken ct) where T : class
    {
        var pkProperties = entityType.FindPrimaryKey()!.Properties;
        var tableName = entityType.GetTableName()!;

        // Desabilita FK constraints no SQLite para permitir atualização de PK
        await _temporario.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;", ct);

        try
        {
            // 1. Atualiza todas as FKs dependentes que referenciam esta entidade
            var allEntityTypes = _temporario.Model.GetEntityTypes()
                .Where(e => typeof(IEntidade).IsAssignableFrom(e.ClrType));

            foreach (var dependentType in allEntityTypes)
            {
                foreach (var fk in dependentType.GetForeignKeys())
                {
                    if (fk.PrincipalEntityType != entityType) continue;

                    var depTableName = dependentType.GetTableName()!;
                    var fkProps = fk.Properties;
                    var principalProps = fk.PrincipalKey.Properties;

                    // Constrói WHERE para os valores antigos e SET para os novos
                    var setClauses = new List<string>();
                    var whereClauses = new List<string>();

                    for (int i = 0; i < principalProps.Count; i++)
                    {
                        var fkColName = fkProps[i].GetColumnName()!;
                        var pkIndex = pkProperties.ToList().IndexOf(principalProps[i]);
                        if (pkIndex < 0) continue;

                        setClauses.Add($"\"{fkColName}\" = {FormatSqlValue(pkNova[pkIndex])}");
                        whereClauses.Add($"\"{fkColName}\" = {FormatSqlValue(pkAntiga[pkIndex])}");
                    }

                    if (setClauses.Count == 0) continue;

                    var sql = $"UPDATE \"{depTableName}\" SET {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", whereClauses)}";
#pragma warning disable EF1002 // values come from entity PKs, not user input
                    await _temporario.Database.ExecuteSqlRawAsync(sql, ct);
#pragma warning restore EF1002

                    _logger.LogDebug("FK atualizada em \"{Tabela}\": {SQL}", depTableName, sql);
                }
            }

            // 2. Atualiza a PK da própria entidade
            var pkSetClauses = new List<string>();
            var pkWhereClauses = new List<string>();

            for (int i = 0; i < pkProperties.Count; i++)
            {
                var colName = pkProperties[i].GetColumnName()!;
                pkSetClauses.Add($"\"{colName}\" = {FormatSqlValue(pkNova[i])}");
                pkWhereClauses.Add($"\"{colName}\" = {FormatSqlValue(pkAntiga[i])}");
            }

            var pkSql = $"UPDATE \"{tableName}\" SET {string.Join(", ", pkSetClauses)} WHERE {string.Join(" AND ", pkWhereClauses)}";
#pragma warning disable EF1002
            await _temporario.Database.ExecuteSqlRawAsync(pkSql, ct);
#pragma warning restore EF1002

            // 3. Atualiza os demais campos da entidade (non-PK) via EF
            _temporario.ChangeTracker.Clear();
            var reloaded = await _temporario.Set<T>().FindAsync(pkNova, ct);
            if (reloaded != null)
            {
                _temporario.Entry(reloaded).CurrentValues.SetValues(entidadeNova);
            }
        }
        finally
        {
            // Reabilita FK constraints e verifica integridade
            await _temporario.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;", ct);

            try
            {
                await _temporario.Database.ExecuteSqlRawAsync("PRAGMA foreign_key_check;", ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Verificação de FK após remap detectou inconsistências.");
            }
        }
    }

    /// <summary>
    /// Formata um valor para uso em SQL raw (escapa strings, mantém números).
    /// </summary>
    private static string FormatSqlValue(object value) => value switch
    {
        string s => $"'{s.Replace("'", "''")}'",
        Guid g => $"'{g}'",
        DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
        null => "NULL",
        _ => value.ToString()!
    };

    /// <summary>
    /// Retorna <c>true</c> se a entidade nova é placeholder e a existente não é
    /// (ou seja, a escrita deve ser ignorada para preservar dados completos).
    /// Para tipos que não implementam <see cref="IEntidadePlaceholder"/>, sempre retorna <c>false</c>.
    /// </summary>
    private static bool DeveIgnorarPorPlaceholder<T>(T nova, T existente)
    {
        if (nova is IEntidadePlaceholder novaP && novaP.EhPlaceholder)
        {
            if (existente is IEntidadePlaceholder existenteP && !existenteP.EhPlaceholder)
                return true;
        }
        return false;
    }

    #endregion

    #region Sincronização

    /// <summary>
    /// Sincroniza todos os dados do banco temporário para o banco final.
    /// <list type="bullet">
    ///   <item><description>Realiza upsert (insere ou atualiza) respeitando chaves primárias.</description></item>
    ///   <item><description>Respeita a ordem de dependências (FK) na inserção.</description></item>
    ///   <item><description>Processa em batches configuráveis para eficiência de memória.</description></item>
    ///   <item><description>Realiza rollback completo em caso de falha.</description></item>
    ///   <item><description>Limpa o banco temporário após sincronização bem-sucedida.</description></item>
    /// </list>
    /// </summary>
    public async Task SincronizarAsync(CancellationToken ct = default)
    {
        await using var _ = await _coordinator.AdquirirSincronizacaoAsync(ct);

        _logger.LogInformation("Iniciando sincronização do banco temporário para o final.");

        var entityTypes = OrdenarPorDependencias(_final.Model);

        if (entityTypes.Count == 0)
        {
            _logger.LogWarning("Nenhum tipo de entidade encontrado para sincronização.");
            return;
        }

        var strategy = _final.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _final.Database.BeginTransactionAsync(ct);
            try
            {
                foreach (var entityType in entityTypes)
                {
                    await SincronizarTipoAsync(entityType, ct);
                }

                await transaction.CommitAsync(ct);

                _logger.LogInformation("Dados transferidos com sucesso. Limpando banco temporário.");

                // Limpa o banco temporário na ordem inversa (filhos antes de pais)
                foreach (var entityType in ((IEnumerable<IEntityType>)entityTypes).Reverse())
                {
                    await LimparEntidadeAsync(entityType, ct);
                }

                _logger.LogInformation("Sincronização concluída com sucesso.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante sincronização. Realizando rollback.");
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    #endregion

    #region Sync internals

    private async Task SincronizarTipoAsync(IEntityType entityType, CancellationToken ct)
    {
        var method = typeof(DualDbContext)
            .GetMethod(nameof(SincronizarTipoGenericoAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(entityType.ClrType);

        await (Task)method.Invoke(this, [entityType, ct])!;
    }

    private async Task SincronizarTipoGenericoAsync<T>(IEntityType entityType, CancellationToken ct) where T : class
    {
        var tempSet = _temporario.Set<T>();
        var finalSet = _final.Set<T>();
        var pkProperties = entityType.FindPrimaryKey()?.Properties;

        if (pkProperties is not { Count: > 0 })
        {
            _logger.LogWarning("Entidade {Tipo} não possui chave primária definida. Pulando.", typeof(T).Name);
            return;
        }

        var total = await tempSet.AsNoTracking().CountAsync(ct);
        if (total == 0)
        {
            _logger.LogDebug("Nenhum registro de {Tipo} para sincronizar.", typeof(T).Name);
            return;
        }

        _logger.LogInformation("Sincronizando {Total} registro(s) de {Tipo}.", total, typeof(T).Name);

        // Para SQL Server: habilita IDENTITY_INSERT quando a PK é auto-gerada
        var needsIdentityInsert = pkProperties.Any(p => p.ValueGenerated == ValueGenerated.OnAdd)
                                  && IsSqlServer();

        if (needsIdentityInsert)
        {
            var tableName = entityType.GetTableName();
            var schema = entityType.GetSchema() ?? "dbo";
#pragma warning disable EF1002 // table/schema names come from EF metadata, not user input
            await _final.Database.ExecuteSqlRawAsync(
                $"SET IDENTITY_INSERT [{schema}].[{tableName}] ON", ct);
#pragma warning restore EF1002
        }

        try
        {
            var allEntities = await tempSet.AsNoTracking().ToListAsync(ct);

            foreach (var batch in allEntities.Chunk(_options.TamanhoBatch))
            {
                foreach (var entity in batch)
                {
                    var pkValues = pkProperties
                        .Select(p => p.PropertyInfo!.GetValue(entity))
                        .ToArray()!;

                    var existing = await finalSet.FindAsync(pkValues, ct);

                    if (existing != null)
                    {
                        if (DeveIgnorarPorPlaceholder(entity, existing))
                        {
                            _logger.LogDebug(
                                "Entidade {Tipo} com PK [{PK}] é placeholder e não sobrescreverá registro completo no final.",
                                typeof(T).Name,
                                string.Join(", ", pkValues));
                            continue;
                        }

                        _final.Entry(existing).CurrentValues.SetValues(entity);
                    }
                    else
                    {
                        finalSet.Add(entity);
                    }
                }

                await _final.SaveChangesAsync(ct);
                _final.ChangeTracker.Clear();
            }
        }
        finally
        {
            if (needsIdentityInsert)
            {
                var tableName = entityType.GetTableName();
                var schema = entityType.GetSchema() ?? "dbo";
#pragma warning disable EF1002
                await _final.Database.ExecuteSqlRawAsync(
                    $"SET IDENTITY_INSERT [{schema}].[{tableName}] OFF", ct);
#pragma warning restore EF1002
            }
        }
    }

    private async Task LimparEntidadeAsync(IEntityType entityType, CancellationToken ct)
    {
        var tableName = entityType.GetTableName();
        if (tableName == null) return;

        _logger.LogDebug("Limpando tabela temporária \"{Tabela}\".", tableName);

#pragma warning disable EF1002 // table names come from EF metadata, not user input
        await _temporario.Database.ExecuteSqlRawAsync($"DELETE FROM \"{tableName}\"", ct);

        // Reseta auto-increment do SQLite (ignora se não existir)
        try
        {
            await _temporario.Database.ExecuteSqlRawAsync(
                $"DELETE FROM sqlite_sequence WHERE name = '{tableName}'", ct);
#pragma warning restore EF1002
        }
        catch
        {
            // sqlite_sequence pode não existir se AUTOINCREMENT não foi utilizado
        }
    }

    private bool IsSqlServer()
        => _final.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Ordena os tipos de entidade por dependências de FK (pais antes de filhos) usando
    /// ordenação topológica. Referências circulares são toleradas.
    /// </summary>
    private static List<IEntityType> OrdenarPorDependencias(IModel model)
    {
        var entityTypes = model.GetEntityTypes()
            .Where(e => typeof(IEntidade).IsAssignableFrom(e.ClrType))
            .ToList();

        var sorted = new List<IEntityType>();
        var visited = new HashSet<IEntityType>();
        var inStack = new HashSet<IEntityType>();

        void Visit(IEntityType et)
        {
            if (visited.Contains(et)) return;
            if (inStack.Contains(et)) return; // referência circular — evita loop infinito

            inStack.Add(et);

            foreach (var fk in et.GetForeignKeys())
            {
                if (fk.PrincipalEntityType != et && entityTypes.Contains(fk.PrincipalEntityType))
                    Visit(fk.PrincipalEntityType);
            }

            inStack.Remove(et);
            visited.Add(et);
            sorted.Add(et);
        }

        foreach (var et in entityTypes)
            Visit(et);

        return sorted;
    }

    #endregion
}
