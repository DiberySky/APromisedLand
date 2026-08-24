using APromisedLand.Api.DiberyTree.Controllers;
using APromisedLand.Api.DiberyTree.Services;

namespace DiberyTreeService.Controllers;

public class AttributesController(
    AttributeService attributeService,
    ILogger<AttributesController> logger) 
    : AttributesControllerBase(attributeService, logger)
{
}