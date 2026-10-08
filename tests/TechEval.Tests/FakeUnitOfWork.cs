using TechEval.Domain.Interfaces.Repositories;

namespace TechEval.Tests;

/// <summary>
/// Unidad de trabajo de prueba. Ejecuta la operación igual que la real y deja constancia de
/// si confirmó o revirtió, para poder afirmar que una escritura fue transaccional sin
/// necesitar una base de datos.
/// </summary>
internal sealed class FakeUnitOfWork : IUnitOfWork
{
    /// <summary>La operación llegó a envolverse en una transacción.</summary>
    public bool Executed { get; private set; }

    /// <summary>La operación terminó y se confirmó.</summary>
    public bool Committed { get; private set; }

    /// <summary>La operación lanzó y se revirtió.</summary>
    public bool RolledBack { get; private set; }

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation, CancellationToken ct = default)
    {
        Executed = true;
        try
        {
            await operation(ct);
            Committed = true;
        }
        catch
        {
            RolledBack = true;
            throw;
        }
    }
}
