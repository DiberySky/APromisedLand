using APromisedLand.Api.DiberyTree.Controllers;
using APromisedLand.Api.DiberyTree.Services;

namespace DiberyTreeService.Controllers;

public class AttributeTableValueController(
    AttributeTableValueService valueService,
    ILogger<AttributeTableValueController> logger) 
    : AttributeTableValueControllerBase(valueService, logger)
{
}