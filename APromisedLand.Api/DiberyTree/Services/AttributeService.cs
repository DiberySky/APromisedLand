using APromisedLand.Api.Data;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace APromisedLand.Api.DiberyTree.Services;

public partial class AttributeService(DiberyDbContext db, 
    IMemoryCache cache,
    ILogger<AttributeService> logger)
{
    
}