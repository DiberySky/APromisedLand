using APromisedLand.Api.DiberyTree.Controllers;
using APromisedLand.Api.DiberyTree.Services;

namespace DiberyTreeService.Controllers;

public class AttributesController(
    AttributeDefinitionService attributeService,
    ILogger<AttributesControllerBase> logger) 
    : AttributesControllerBase(attributeService, logger)
{
}