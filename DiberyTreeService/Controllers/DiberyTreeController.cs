using APromisedLand.Api.Controllers;
using APromisedLand.Api.Projects.DiberyTree;
using APromisedLand.Api.Projects.DiberyTree.Interface;
using APromisedLand.Api.Projects.DiberyTree.Services;
using APromisedLand.Shared.DiberyTree.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DiberyTreeService.Controllers;

[ApiController]
[Route("[controller]")]
public class DiberyTreeController(
    ITreeService<string> treeService,
    ILogger<TreeController<string>> logger)
    : TreeController<string>(treeService, logger);