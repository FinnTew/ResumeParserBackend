using Microsoft.AspNetCore.Routing.Constraints;
using Microsoft.Extensions.Logging.EventSource;
using MongoDB.Bson;
using ResumeParserBackend.Util;
using NodaTime;
using ResumeParserBackend;
using ResumeParserBackend.Collection;
using ResumeParserBackend.Document;
using ResumeParserBackend.Entity;
using ResumeParserBackend.Helper;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// test
app.MapGet("/ping", () => "pong")
    .WithName("Ping")
    .WithOpenApi();

// 简历上传 & 读取
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
            
            var fileContent = FileReader.ReadFile(filePath);

            var resumeId = Guid.NewGuid().ToString();

            try
            {
                await new MongoDbHelper<Resume>("Resume").InsertOneAsync(new Resume
                {
                    ResumeId = resumeId,
                    FilePath = filePath,
                    OriginalFileName = formFile.FileName,
                    UploadedFileName = newFileName,
                    FileFormat = fileExtension.Split(".")[1].ToLower(),
                    UploadTime = DateTime.Now,
                    ParseStatus = "pending"
                });
            }
            catch (Exception e)
            {
                return Results.Json(new { success = false, message = e.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }

            try
            {
                await new MongoDbHelper<ResumeContent>("ResumeContent").InsertOneAsync(new ResumeContent
                {
                    ResumeId = resumeId,
                    FileContent = fileContent
                });
            }
            catch (Exception e)
            {
                return Results.Json(new { success = false, message = e.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
 
            uploadedFiles.Add(newFileName);
        }
    
        return Results.Json(new { success = true, uploadedFiles }, statusCode: StatusCodes.Status200OK);
    })
    .Accepts<IFormFile>("multipart/form-data")
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status400BadRequest)
    .WithName("UploadFile")
    .WithOpenApi();

// 按照文件名查找
app.MapGet("/find/{fileName}", async (string fileName, HttpContext ctx) =>
    {
        var mongo = new MongoDbHelper<Resume>("Resume");

        var resumeList = await mongo.FindManyAsync(f => f.OriginalFileName.Contains(fileName));

        return Results.Json(new { success = true, resumeList }, statusCode: StatusCodes.Status200OK);
    })
    .WithName("Find")
    .WithOpenApi();

// 按照日期查找
app.MapPost("/find", async (HttpContext ctx) =>
    {
        var reqData = await ctx.Request.ReadFromJsonAsync<FindByDataReq>();

        if (reqData == null)
        {
            return Results.Json(new { success = false, message = "Invalid request data." }, statusCode: StatusCodes.Status400BadRequest);
        }

        if (reqData.StartDate > reqData.EndDate)
        {
            return Results.Json(new { success = false, message = "Start date cannot be later than end date." }, statusCode: StatusCodes.Status400BadRequest);
        }
        
        var mongo = new MongoDbHelper<Resume>("Resume");
        
        var resumeList = await mongo.FindManyAsync(f => f.UploadTime >= reqData.StartDate && f.UploadTime <= reqData.EndDate);
        
        return Results.Json(new { success = true, resumeList }, statusCode: StatusCodes.Status200OK);
    })
    .WithName("Find")
    .WithOpenApi();

// 下载简历
app.MapGet("/download/{resumeId}", async (string resumeId, HttpContext ctx) =>
    {
        var mongo = new MongoDbHelper<Resume>("Resume");
        
        var resume = await mongo.FindOneAsync(f => f.ResumeId == resumeId);

        var resumeFilePath = resume.FilePath;

        if (!File.Exists(resumeFilePath))
        {
            return Results.NotFound();
        }
        
        var fileBytes = await File.ReadAllBytesAsync(resumeFilePath);
        const string contentType = "application/octet-stream";
        
        return Results.File(fileContents: fileBytes, contentType: contentType, fileDownloadName: resume.OriginalFileName);
    })
    .WithName("Download")
    .WithOpenApi();

// 关键词匹配
app.MapPost("/match", async (HttpContext ctx) =>
    {
        var reqData = await ctx.Request.ReadFromJsonAsync<MatchReq>();

        if (reqData == null)
        {
            return Results.Json(new { success = false, message = "Invalid request data." }, statusCode: StatusCodes.Status400BadRequest);
        }
        
        var es = new ElasticSearchHelper();

        var matchRes = await es.BoolSearchAsync<ResumeDoc>(
            ConfigManager.Instance.Get(c => c.Es.Index),
            mustQuery =>
            {
                return mustQuery.Bool(b =>
                {
                    reqData.Must.ForEach(keyword =>
                    {
                        b.Must(m =>
                        {
                            return m.Term(t =>
                            {
                                return t.Field(f => f.Content).Value(keyword);
                            });
                        });
                    });
                    return b;
                });
            },
            shouldQuery =>
            {
                return shouldQuery.Bool(b =>
                {
                    reqData.Should.ForEach(keyword =>
                    {
                        b.Should(m =>
                        {
                            return m.Match(t =>
                            {
                                return t.Field(f => f.Content).Query(keyword);
                            });
                        });
                    });
                    return b;
                });
            }
        );

        var matchResList = new List<string>();
        matchRes.Documents.ToList().ForEach(item =>
        {
            matchResList.Add(item.ResumeId);
        });
        
        return Results.Json(new {success = true, matchResList}, statusCode: StatusCodes.Status200OK);
    })
    .Accepts<MatchReq>("application/json")
    .WithName("Match")
    .WithOpenApi();



app.Run();