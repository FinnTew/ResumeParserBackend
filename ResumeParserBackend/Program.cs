var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// endpoints
app.MapGet("/ping", () => "pong")
    .WithName("Ping")
    .WithOpenApi();

app.MapPost("/upload", async (HttpContext ctx) =>
    {
        var formFiles = ctx.Request.Form.Files;

        if (formFiles.Count == 0)
        {
            return Results.Json(new { success = false, message = "No files were uploaded." }, statusCode: StatusCodes.Status400BadRequest);
        }

        string[] supportedFileFormats = [".doc", ".docx", ".pdf", ".txt"];
        var uploadedFiles = new List<string>();
    
        foreach (var formFile in formFiles)
        {
            if (!supportedFileFormats.Any(x => formFile.FileName.EndsWith(x, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Json(new { success = false, message = $"Unsupported file format: {formFile.FileName}" }, statusCode: StatusCodes.Status400BadRequest);
            }
        
            var fileExtension = Path.GetExtension(formFile.FileName);
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
            var newFileName = $"{Path.GetFileNameWithoutExtension(formFile.FileName)}-{timestamp}{fileExtension}";

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", newFileName);
            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await formFile.CopyToAsync(stream);
            }
        
            // TODO
        
            uploadedFiles.Add(newFileName);
        }
    
        return Results.Json(new { success = true, uploadedFiles }, statusCode: StatusCodes.Status200OK);
    })
    .Accepts<IFormFile>("multipart/form-data")
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status400BadRequest)
    .WithName("UploadFile")
    .WithOpenApi();

app.Run();