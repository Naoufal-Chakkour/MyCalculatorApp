using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseStaticFiles(); 
app.UseDefaultFiles();
app.UseStaticFiles(); 


app.MapPost("/calculate", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    
    if (!double.TryParse(form["num1"], out double num1) || 
        !double.TryParse(form["num2"], out double num2))
    {
        await context.Response.WriteAsync("خطأ في إدخال الأرقام!");
        return;
    }

    string operation = form["operation"].ToString() ?? "add";
    double result = operation switch
    {
        "add"      => num1 + num2,
        "subtract" => num1 - num2,
        "multiply" => num1 * num2,
        "modulo" => num1 % num2,
        "divide"   => num2 != 0 ? num1 / num2 : double.NaN,
        _ => double.NaN
    };

    await context.Response.WriteAsync(result.ToString());
});


app.Run();