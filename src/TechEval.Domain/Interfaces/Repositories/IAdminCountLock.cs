namespace TechEval.Domain.Interfaces.Repositories;

/// <summary>
/// Pone en fila las operaciones que pueden dejar el sistema sin administradores activos.
/// </summary>
/// <remarks>
/// Un administrador no puede desactivarse ni cambiarse el rol a sí mismo, así que actuando
/// solo nunca deja cero. El riesgo es la concurrencia: A desactiva a B mientras B desactiva
/// a A. Las dos transacciones cuentan dos administradores y las dos confirman. Con este
/// bloqueo, la segunda espera a que la primera termine y cuenta uno.
///
/// Se llama dentro de <see cref="IUnitOfWork.ExecuteInTransactionAsync"/>, antes de contar.
/// El bloqueo se libera con la transacción.
/// </remarks>
public interface IAdminCountLock
{
    Task AcquireAsync(CancellationToken ct = default);
}
