using APromisedLand.Api.DiberyTree.Controllers;
using APromisedLand.Api.DiberyTree.Services;
using Microsoft.AspNetCore.Mvc;

namespace DiberyTreeService.Controllers;

[ApiController]
[Route("attributes")]
public class AttributesController(
    AttributeService attributeService,
    ILogger<AttributesController> logger) 
    : AttributesControllerBase(attributeService, logger) 
{
}