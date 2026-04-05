using FluentValidation;
using Larchik.API.Configuration;
using Larchik.Application.Operations.ImportBroker;
using Microsoft.Extensions.Options;

namespace Larchik.API.DTOs;

public class ImportBrokerReportRequestValidator : AbstractValidator<ImportBrokerReportRequest>
{
    public ImportBrokerReportRequestValidator(IOptions<ImportOptions> importOptions)
    {
        RuleFor(x => x.File)
            .Custom((file, context) =>
            {
                if (file is null)
                {
                    context.AddFailure("file", "Файл отчета не загружен");
                    return;
                }

                if (file.Length == 0)
                {
                    context.AddFailure("file", "Файл отчета не загружен");
                    return;
                }

                var sizeError = BrokerReportFileValidator.ValidateFileSize(
                    file.Length,
                    importOptions.Value.MaxFileSizeBytes,
                    importOptions.Value.MaxFileSizeMb);

                if (sizeError is not null)
                {
                    context.AddFailure("file", sizeError);
                }
            });
    }
}
