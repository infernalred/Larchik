using System.Data;
using System.Text;
using Larchik.Domain;
using Larchik.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Larchik.Infrastructure.ExchangeServices.Cbr;

public class CbrExchangeRates
{
    private readonly ILogger<CbrExchangeRates> _logger;
    private readonly DataContext _context;
    private readonly IOptions<CbrSettings> _config;

    public CbrExchangeRates(ILogger<CbrExchangeRates> logger, DataContext context, IOptions<CbrSettings> config)
    {
        _logger = logger;
        _context = context;
        _config = config;
    }

    public async Task GetLastRates(CancellationToken cancellationToken)
    {
        var currency = await _context.Currencies
            .ToDictionaryAsync(x => x.Code, cancellationToken);
        
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        
        var url = _config.Value.Address;
        var ds = new DataSet();
        ds.ReadXml(url);

        var dataTable = ds.Tables["Valute"];

        if (dataTable == null)
        {
            _logger.LogError("Error occurred executing {service}. Message: datatable is null", nameof(CbrExchangeRates));
        }
        else
        {
            foreach (DataRow row in dataTable.Rows)
            {
                var charCode = row["CharCode"].ToString();

                if (charCode == null || !currency.ContainsKey(charCode)) continue;
            
                var code = $"{charCode}_RUB";
                var nominal = Convert.ToInt32(row["Nominal"]);
                var value = Convert.ToDouble(row["Value"]);
                var date = DateOnly.FromDateTime(DateTime.Now.AddDays(1));
            
                var exchange = await _context.Exchanges
                    .FirstOrDefaultAsync(x => x.Code == code && x.Date == date, cancellationToken);

                if (exchange == null)
                {
                    exchange = new Exchange
                    {
                        Code = code,
                        Nominal = nominal,
                        Date = date,
                        Rate = value
                    };
                    
                    await _context.Exchanges.AddAsync(exchange, cancellationToken);
                }
                else
                {
                    exchange.Rate = value;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}