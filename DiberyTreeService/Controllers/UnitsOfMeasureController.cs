using APromisedLand.Api.Controllers;
using APromisedLand.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DiberyTreeService.Controllers;


[ApiController]
[Route("[controller]")]
public class UnitsOfMeasureController(IUnitOfMeasureService service)
    : UnitsOfMeasureControllerBase(service)
{
    
}