using APromisedLand.Api.Controllers;
using APromisedLand.Api.DiberyTree.Interface;
using Microsoft.AspNetCore.Mvc;

namespace DiberyTreeService.Controllers;

[ApiController]
[Route("[controller]")]
public class DiberyTreeController(
    ITreeService<string> treeService,
    ILogger<TreeController<string>> logger)
    : TreeController<string>(treeService, logger);