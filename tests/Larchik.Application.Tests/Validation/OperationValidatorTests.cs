using Larchik.Application.Models;
using Larchik.Application.Operations.Validators;
using Larchik.Persistence.Entities;
using Xunit;

namespace Larchik.Application.Tests.Validation;

public class OperationValidatorTests
{
    private static readonly DateTimeOffset TradeDate = new(2026, 4, 19, 10, 0, 0, TimeSpan.Zero);
    private readonly OperationValidator validator = new();

    [Fact]
    public void Validate_RejectsInstrumentOnDeposit()
    {
        var result = validator.Validate(CreateModel(
            Type: OperationType.Deposit,
            InstrumentId: Guid.NewGuid(),
            Price: 100m));

        Assert.Contains(result.Errors, x => x.ErrorMessage.Contains("cannot use an instrument", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsDividendWithQuantity()
    {
        var result = validator.Validate(CreateModel(
            Type: OperationType.Dividend,
            InstrumentId: Guid.NewGuid(),
            Quantity: 1m,
            Price: 100m));

        Assert.Contains(result.Errors, x => x.ErrorMessage.Contains("do not match the selected operation type", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsCashTransferWithBothQuantityAndPrice()
    {
        var result = validator.Validate(CreateModel(
            Type: OperationType.TransferIn,
            Quantity: 10m,
            Price: 10m));

        Assert.Contains(result.Errors, x => x.ErrorMessage.Contains("do not match the selected operation type", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_AllowsInstrumentTransferWithQuantityOnly()
    {
        var result = validator.Validate(CreateModel(
            Type: OperationType.TransferOut,
            InstrumentId: Guid.NewGuid(),
            Quantity: 5m));

        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_AllowsFeeWithFeeFieldOnly()
    {
        var result = validator.Validate(CreateModel(
            Type: OperationType.Fee,
            Fee: 12.5m));

        Assert.Empty(result.Errors);
    }

    private static OperationModel CreateModel(
        Guid? InstrumentId = null,
        OperationType Type = OperationType.Deposit,
        decimal Quantity = 0m,
        decimal Price = 0m,
        decimal Fee = 0m) =>
        new(
            InstrumentId,
            Type,
            Quantity,
            Price,
            Fee,
            "USD",
            TradeDate,
            null,
            null);
}
