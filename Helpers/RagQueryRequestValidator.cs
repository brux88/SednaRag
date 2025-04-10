using SednaRag.Models;
using System.Text.RegularExpressions;
using FluentValidation;
using System.Linq;
using SednaRag.Helpers;
using FluentValidation.AspNetCore;

namespace SednaRag.Helpers
{
    public class RagQueryRequestValidator : AbstractValidator<RagQueryRequest>
    {
        public RagQueryRequestValidator()
        {
            RuleFor(x => x.Query)
                .NotEmpty().WithMessage("La query è obbligatoria")
                .MaximumLength(2000).WithMessage("La query non può superare 2000 caratteri")
                .Must(BeValidQuery).WithMessage("La query contiene caratteri o pattern non validi");

            RuleFor(x => x.ClientId)
                .NotEmpty().WithMessage("ClientId è obbligatorio")
                .Matches("^[a-zA-Z0-9-_]+$").WithMessage("ClientId contiene caratteri non validi");

            RuleFor(x => x.Module)
                .MaximumLength(100).WithMessage("Module non può superare 100 caratteri")
                .Matches("^[a-zA-Z0-9-_. ]*$").WithMessage("Module contiene caratteri non validi");

 
        }

        private bool BeValidQuery(string query)
        {
            if (string.IsNullOrEmpty(query))
                return false;

            // Controlla pattern pericolosi
            var dangerousPatterns = new[]
            {
            @"(?i);\s*SELECT",
            @"(?i);\s*INSERT",
            @"(?i);\s*UPDATE",
            @"(?i);\s*DELETE",
            @"(?i);\s*DROP",
            @"(?i);\s*CREATE",
            @"(?i);\s*ALTER",
            @"(?i);\s*EXEC",
            @"(?i)-{2,}",
            @"(?i)/\*.*\*/",
            @"(?i)xp_cmdshell"
        };

            return !dangerousPatterns.Any(pattern => Regex.IsMatch(query, pattern));
        }
    }


    public class SchemaImportRequestValidator : AbstractValidator<SchemaImportRequest>
    {
        public SchemaImportRequestValidator()
        {
            RuleFor(x => x.ClientId)
                .NotEmpty().WithMessage("ClientId è obbligatorio")
                .Matches("^[a-zA-Z0-9-_]+$").WithMessage("ClientId contiene caratteri non validi");

            // Validazione per Tables
            RuleForEach(x => x.Tables).ChildRules(table =>
            {
                table.RuleFor(t => t.Name)
                    .NotEmpty().WithMessage("Nome tabella obbligatorio")
                    .Matches("^[a-zA-Z0-9-_]+$").WithMessage("Nome tabella contiene caratteri non validi");

                table.RuleFor(t => t.Description)
                    .MaximumLength(2000).WithMessage("Descrizione tabella troppo lunga");

                table.RuleForEach(t => t.Columns).ChildRules(column =>
                {
                    column.RuleFor(c => c.Name)
                        .NotEmpty().WithMessage("Nome colonna obbligatorio")
                        .Matches("^[a-zA-Z0-9-_]+$").WithMessage("Nome colonna contiene caratteri non validi");
                });
            });

            // Validazione per StoredProcedures 
            RuleForEach(x => x.StoredProcedures).ChildRules(proc =>
            {
                proc.RuleFor(p => p.Name)
                    .NotEmpty().WithMessage("Nome procedura obbligatorio")
                    .Matches("^[a-zA-Z0-9-_]+$").WithMessage("Nome procedura contiene caratteri non validi");
            });

            // Validazione per BusinessRules
            RuleForEach(x => x.BusinessRules).ChildRules(rule =>
            {
                rule.RuleFor(r => r.Name)
                    .NotEmpty().WithMessage("Nome regola obbligatorio");

                rule.RuleFor(r => r.Description)
                    .NotEmpty().WithMessage("Descrizione regola obbligatoria");
            });
        }
    }

    // Setup dei validatori nel Program.cs o Startup.cs
    public static class ValidationSetup
    {
        public static IServiceCollection AddRequestValidation(this IServiceCollection services)
        {
            // Ensure FluentValidation is properly configured
            services.AddValidatorsFromAssemblyContaining<RagQueryRequestValidator>();
            services.AddFluentValidationAutoValidation();

            return services;
        }
    }
}