using System.Buffers.Binary;
using System.Security.Cryptography;
using TechEval.Domain.Entities;

namespace TechEval.Application.Services;

/// <summary>
/// Orden en que una sesión ve las preguntas y las opciones. Cada candidato recibe su propia
/// permutación, para que una prueba filtrada no sirva tal cual al siguiente.
///
/// El orden es una función pura de la semilla de la sesión y de los identificadores: la
/// misma sesión da el mismo orden en cada reanudación sin guardar la permutación. No usa
/// <see cref="Random"/> con semilla porque su secuencia no es un contrato entre versiones
/// de .NET, y una actualización del runtime cambiaría el orden de una sesión abierta.
///
/// Sin semilla —una sesión anterior a este cambio— se conserva el orden del examen.
/// </summary>
public static class SessionOrder
{
    private const byte QuestionKind = (byte)'q';
    private const byte AnswerKind = (byte)'a';

    public static IEnumerable<ExamQuestion> Questions(IEnumerable<ExamQuestion> questions, int? seed)
        => seed is null
            ? questions.OrderBy(eq => eq.Order)
            : questions
                .OrderBy(eq => KeyOf(seed.Value, QuestionKind, eq.QuestionId, 0))
                .ThenBy(eq => eq.QuestionId);

    public static IEnumerable<Answer> Answers(IEnumerable<Answer> answers, int questionId, int? seed)
        => seed is null
            ? answers.OrderBy(a => a.Order)
            : answers
                .OrderBy(a => KeyOf(seed.Value, AnswerKind, questionId, a.Id))
                .ThenBy(a => a.Id);

    /// <summary>
    /// Respuestas en el orden del examen, que es la referencia de los resultados: así el
    /// corrector compara candidatos sobre el mismo orden, sea cual sea el que vio cada uno.
    /// Una respuesta cuya pregunta ya no está en el examen va al final, por identificador.
    /// </summary>
    public static IEnumerable<UserAnswer> ByExamOrder(IEnumerable<UserAnswer> answers, Exam? exam)
    {
        var position = exam?.ExamQuestions.ToDictionary(eq => eq.QuestionId, eq => eq.Order)
            ?? new Dictionary<int, int>();

        return answers
            .OrderBy(ua => position.TryGetValue(ua.QuestionId, out var p) ? p : int.MaxValue)
            .ThenBy(ua => ua.Id);
    }

    private static ulong KeyOf(int seed, byte kind, int first, int second)
    {
        Span<byte> input = stackalloc byte[13];
        BinaryPrimitives.WriteInt32LittleEndian(input, seed);
        input[4] = kind;
        BinaryPrimitives.WriteInt32LittleEndian(input[5..], first);
        BinaryPrimitives.WriteInt32LittleEndian(input[9..], second);

        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(input, hash);
        return BinaryPrimitives.ReadUInt64LittleEndian(hash);
    }
}
