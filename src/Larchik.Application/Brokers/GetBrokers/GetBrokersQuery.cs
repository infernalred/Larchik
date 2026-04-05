using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Brokers.GetBrokers;

public class GetBrokersQuery : IRequest<Result<BrokerDto[]>>;
