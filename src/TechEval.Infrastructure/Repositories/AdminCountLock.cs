using Microsoft.EntityFrameworkCore;
using TechEval.Domain.Interfaces.Repositories;
using TechEval.Infrastructure.Data;

namespace TechEval.Infrastructure.Repositories;

/// <summary>
/// Bloqueo de aplicación de SQL Server, ligado a la transacción en curso.
/// </summary>
/// <remarks>
/// Se prefiere a una transacción Serializable porque da una espera ordenada: con
/// Serializable las dos transacciones leen, las dos intentan escribir y SQL Server mata una
/// por interbloqueo, un error que habría que traducir aparte. Un <c>SemaphoreSlim</c> solo
/// valdría con una instancia de la API.
///
/// La base en memoria de las pruebas no es relacional ni tiene transacciones: ahí no hace
/// nada, y la concurrencia se prueba contra SQL Server.
/// </remarks>
public class AdminCountLock : IAdminCountLock
{
    private readonly AppDbContext _context;

    public AdminCountLock(AppDbContext context) => _context = context;

    public async Task AcquireAsync(CancellationToken ct = default)
    {
        if (!_context.Database.IsRelational()) return;

        await _context.Database.ExecuteSqlRawAsync("""
            DECLARE @result INT;
            EXEC @result = sp_getapplock
                @Resource = 'techeval-admins',
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 10000;
            IF @result < 0
                THROW 50000, 'No se pudo obtener el bloqueo de administradores.', 1;
            """, ct);
    }
}
