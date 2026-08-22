using APromisedLand.Api.Projects.DiberyTree.Controllers;
using APromisedLand.Api.Projects.DiberyTree.Services;

namespace DiberyTreeService.Controllers;

public class AttributeTableValueController(
    AttributeTableValueService valueService,
    ILogger<AttributeTableValueController> logger) 
    : AttributeTableValueControllerBase(valueService, logger)
{
}