using APromisedLand.Api.Projects.DiberyTree.Controllers;
using APromisedLand.Api.Projects.DiberyTree.Services;

namespace DiberyTreeService.Controllers;

public class AttributesController(
    AttributeDefinitionService attributeService,
    ILogger<AttributesControllerBase> logger) 
    : AttributesControllerBase(attributeService, logger)
{
}