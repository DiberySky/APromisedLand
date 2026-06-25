using System;
using System.Collections.Generic;
using System.Text;

namespace APromisedLand.Shared.Clients;

public class WeatherForecast
{
    public DateOnly Date { get; set; }
    public int TemperatureC { get; set; }
    public string? Summary { get; set; }
}
