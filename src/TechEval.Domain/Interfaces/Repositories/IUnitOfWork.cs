namespace TechEval.Domain.Interfaces.Repositories;

/// <summary>
/// Agrupa varias escrituras en una sola transacción de base de datos.
/// </summary>
/// <remarks>
/// Recibe la operación en vez de devolver un objeto de transacción a propósito. Devolverlo
/// se presta a olvidar la confirmación, que es un fallo silencioso: la operación parece
/// terminar bien y nada queda escrito. Así el caso normal no se puede escribir mal — si la
/// función termina, se confirma; si lanza, se revierte.
///
/// Los repositorios siguen confirmando en cada operación. Dentro de una transacción abierta
/// sobre el mismo contexto, esas confirmaciones participan de ella y no se materializan
/// hasta el final.
/// </remarks>
public interface IUnitOfWork
{
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation, CancellationToken ct = default);
}
