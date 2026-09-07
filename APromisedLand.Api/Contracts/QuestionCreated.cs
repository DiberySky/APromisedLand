using System;
using System.Collections.Generic;

namespace APromisedLand.Api.Contracts;

public record QuestionCreated(string QuestionId, string Title, string Content,
    DateTimeOffset Created, List<string> Tags);