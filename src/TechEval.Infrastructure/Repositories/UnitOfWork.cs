using TechEval.Domain.Interfaces.Repositories;
using TechEval.Infrastructure.Data;

namespace TechEval.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context) => _context = context;

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation, CancellationToken ct = default)
    {
        // El DbContext es Scoped y los repositorios comparten instancia, así que una
        // transacción abierta aquí cubre todas sus escrituras durante la petición.
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        try
        {
            await operation(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            // Se revierte y se relanza: quien llamó tiene que enterarse del fallo. Tragarse
            // la excepción dejaría creer que la operación salió bien sin nada escrito.
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
