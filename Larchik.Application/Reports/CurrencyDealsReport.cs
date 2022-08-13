﻿namespace Larchik.Application.Reports;

public class CurrencyDealsReport
{
    public string Account { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public decimal Amount { get; set; }
}