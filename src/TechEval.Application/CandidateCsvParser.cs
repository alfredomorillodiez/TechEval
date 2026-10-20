using TechEval.Application.DTOs;

namespace TechEval.Application;

/// <summary>Una línea del CSV que no se importó, con su número (desde 1) y el motivo.</summary>
public record CsvLineIssue(int Line, string Reason);

/// <summary>Candidatos importados y líneas ignoradas.</summary>
public record CsvParseResult(List<BulkCandidateDto> Candidates, List<CsvLineIssue> Issues);

/// <summary>
/// Importa candidatos de un texto pegado: «Nombre;correo», una línea por candidato.
/// Antes solo aceptaba la coma, y un CSV de Excel en castellano (que separa con punto y coma)
/// importaba 0 candidatos sin decir por qué.
/// </summary>
public static class CandidateCsvParser
{
    private static readonly string[] EmailHeaders = { "email", "e-mail", "correo", "correo electrónico", "mail" };

    public static CsvParseResult Parse(string? text)
    {
        var candidates = new List<BulkCandidateDto>();
        var issues = new List<CsvLineIssue>();
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var lines = (text ?? string.Empty).TrimStart('﻿').Split('\n');
        var firstContent = true;

        for (var i = 0; i < lines.Length; i++)
        {
            var number = i + 1;
            var line = lines[i].TrimEnd('\r').Trim();
            if (line.Length == 0) continue;

            var isFirst = firstContent;
            firstContent = false;

            var split = SplitAtLastSeparator(line);
            if (split is null)
            {
                issues.Add(new CsvLineIssue(number, "falta el separador entre el nombre y el correo (usa ;, coma o tabulador)"));
                continue;
            }

            var name = Unquote(split.Value.Name);
            var email = Unquote(split.Value.Email);

            // Un encabezado («Nombre;Email») no es un error: es lo que exporta una hoja de cálculo.
            if (isFirst && !TechEval.Application.Services.ExamTokenService.IsValidEmail(email)
                && EmailHeaders.Contains(email, StringComparer.OrdinalIgnoreCase))
                continue;

            if (name.Length == 0)
            {
                issues.Add(new CsvLineIssue(number, "falta el nombre"));
                continue;
            }
            if (!TechEval.Application.Services.ExamTokenService.IsValidEmail(email))
            {
                issues.Add(new CsvLineIssue(number, $"el correo «{email}» no es válido"));
                continue;
            }
            if (seen.TryGetValue(email, out var firstLine))
            {
                issues.Add(new CsvLineIssue(number, $"el correo {email} ya estaba en la línea {firstLine}"));
                continue;
            }

            seen[email] = number;
            candidates.Add(new BulkCandidateDto(name, email));
        }

        return new CsvParseResult(candidates, issues);
    }

    /// <summary>
    /// Parte la línea por el ÚLTIMO separador (`;`, tabulador o coma) que no esté entre comillas.
    /// El correo no puede contener ninguno de los tres, y el nombre sí puede llevar una coma
    /// entre comillas: «"García, Juan",juan@x.com».
    /// </summary>
    private static (string Name, string Email)? SplitAtLastSeparator(string line)
    {
        var inQuotes = false;
        var last = -1;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"') inQuotes = !inQuotes;
            else if (!inQuotes && (c == ';' || c == '\t' || c == ',')) last = i;
        }

        return last < 0 ? null : (line[..last], line[(last + 1)..]);
    }

    private static string Unquote(string value)
    {
        var v = value.Trim();
        if (v.Length >= 2 && v[0] == '"' && v[^1] == '"') v = v[1..^1];
        return v.Trim();
    }
}
