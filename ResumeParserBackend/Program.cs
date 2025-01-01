using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using ResumeParserBackend.Util;
using ResumeParserBackend;
using ResumeParserBackend.Collection;
using ResumeParserBackend.Document;
using ResumeParserBackend.Entity;
using ResumeParserBackend.Helper;
using C = ResumeParserBackend.Util.Convert;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// app.UseHttpsRedirection();
app.UseCors("AllowAll");

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
        var uploadedFiles = new List<UploadResp>();
    
        foreach (var formFile in formFiles)
        {
            if (!supportedFileFormats.Any(x => formFile.FileName.EndsWith(x, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Json(new { success = false, message = $"Unsupported file format: {formFile.FileName}" }, statusCode: StatusCodes.Status400BadRequest);
            }
        
            var fileExtension = Path.GetExtension(formFile.FileName);
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
            var newFileName = $"{Path.GetFileNameWithoutExtension(formFile.FileName)}-{timestamp}{fileExtension}";

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var filePath = Path.Combine(uploadsFolder, newFileName);
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

            try
            {
                await new ElasticSearchHelper().InsertDataAsync(ConfigManager.Instance.Get(c => c.Es.Index),
                    new ResumeDoc
                    {
                        ResumeId = resumeId,
                        Content = fileContent
                    });
            }
            catch (Exception e)
            {
                return Results.Json(new { success = false, message = e.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
 
            uploadedFiles.Add(new UploadResp
            {
                Id = resumeId,
                Filename = formFile.FileName
            });
        }
    
        return Results.Json(new { success = true, data = uploadedFiles }, statusCode: StatusCodes.Status200OK);
    })
    .Accepts<IFormFile>("multipart/form-data")
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status400BadRequest)
    .WithName("UploadFile")
    .WithOpenApi();

// 获取所有简历
app.MapGet("/list", async (HttpContext ctx) =>
    {
        var mongo = new MongoDbHelper<Resume>("Resume");

        var resumeList = await mongo.FindAllAsync();

        var resumeListRes = new List<ResumeEntity>();
        resumeList.ForEach(item =>
        {
            resumeListRes.Add(new ResumeEntity
            {
                ResumeId = item.ResumeId,
                OriginFileName = item.OriginalFileName,
                UploadTime = item.UploadTime,
                ParseStatus = item.ParseStatus
            });
        });
        
        return Results.Json(new {success = true, data = resumeListRes}, statusCode: StatusCodes.Status200OK);
    })
    .WithName("List")
    .WithOpenApi();

// 文件内容
app.MapGet("filecontent/{resumeId}", async (string resumeId, HttpContext ctx) =>
    {
        var mongo = new MongoDbHelper<ResumeContent>("ResumeContent");
        
        var resumeContent = await mongo.FindOneAsync(f => f.ResumeId == resumeId);
        
        var filename = (await new MongoDbHelper<Resume>("Resume").FindOneAsync(f => f.ResumeId == resumeId)).OriginalFileName;
        
        return Results.Json(new {success = true, data = new { filename, content = resumeContent}}, statusCode: StatusCodes.Status200OK);
    })
    .WithName("GetFileContent")
    .WithOpenApi();

// 按照文件名查找
app.MapPost("/findbyfilename", async (HttpContext ctx) =>
    {
        var reqData = await ctx.Request.ReadFromJsonAsync<FindByFilenameReq>();
        if (reqData == null)
        {
            return Results.Json(new { success = false, message = "Invalid request data." }, statusCode: StatusCodes.Status400BadRequest);
        }
        
        var fileName = reqData.Filename;
        
        var mongo = new MongoDbHelper<Resume>("Resume");

        var resumeList = await mongo.FindManyAsync(f => f.OriginalFileName.Contains(fileName));
        
        var resumeListRes = new List<ResumeEntity>();
        resumeList.ForEach(item =>
        {
            resumeListRes.Add(new ResumeEntity
            {
                ResumeId = item.ResumeId,
                OriginFileName = item.OriginalFileName,
                UploadTime = item.UploadTime,
                ParseStatus = item.ParseStatus
            });
        });

        return Results.Json(new { success = true, data = resumeListRes }, statusCode: StatusCodes.Status200OK);
    })
    .WithName("FindByFileName")
    .WithOpenApi();

// 按照日期查找
app.MapPost("/findbydate", async (HttpContext ctx) =>
    {
        var reqData = await ctx.Request.ReadFromJsonAsync<FindByDataReq>();

        if (reqData == null)
        {
            return Results.Json(new { success = false, message = "Invalid request data." }, statusCode: StatusCodes.Status400BadRequest);
        }

        if (reqData.Start > reqData.End)
        {
            return Results.Json(new { success = false, message = "Start date cannot be later than end date." }, statusCode: StatusCodes.Status400BadRequest);
        }
        
        var mongo = new MongoDbHelper<Resume>("Resume");
        
        var resumeList = await mongo.FindManyAsync(f => f.UploadTime >= reqData.Start && f.UploadTime <= reqData.End);
        
        var resumeListRes = new List<ResumeEntity>();
        resumeList.ForEach(item =>
        {
            resumeListRes.Add(new ResumeEntity
            {
                ResumeId = item.ResumeId,
                OriginFileName = item.OriginalFileName,
                UploadTime = item.UploadTime,
                ParseStatus = item.ParseStatus
            });
        });
        
        return Results.Json(new { success = true, data = resumeListRes }, statusCode: StatusCodes.Status200OK);
    })
    .WithName("FindByDate")
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

// 解析岗位jd
app.MapPost("/parsejob", async (HttpContext ctx) =>
    {
        var reqData = await ctx.Request.ReadFromJsonAsync<ParseJobReq>();
        
        if (reqData == null)
        {
            return Results.Json(new { success = false, message = "Invalid request data." }, statusCode: StatusCodes.Status400BadRequest);
        }

        var jobJsonString = await new RpcCall().Call("job_parse", reqData.Content);
        
        return Results.Json(new {success = true, data = jobJsonString}, statusCode: StatusCodes.Status200OK);
    })
    .WithName("ParseJob")
    .WithOpenApi();

// 解析简历
app.MapPost("/parse/{resumeId}", async (string resumeId, HttpContext ctx) =>
    {
        if (resumeId == "")
        {
            return Results.Json(new { success = false, message = "Invalid request data." }, statusCode: StatusCodes.Status400BadRequest);
        }
        
        var r = await new MongoDbHelper<ResumeMetadata>("ResumeMetadata").FindOneAsync(f => f.ResumeId == resumeId);

        // if (r.Id != "")
        // {
        //     return Results.Json(new { success = false, message = "Already parsed the resume." }, statusCode: StatusCodes.Status200OK);
        // }
        
        var mongo = new MongoDbHelper<ResumeContent>("ResumeContent");
        var resume = await mongo.FindOneAsync(f => f.ResumeId == resumeId);
        
        var resumeJsonString = await new RpcCall().Call("resume_parse", resume.FileContent);
        
        Console.WriteLine(resumeJsonString);

        await new MongoDbHelper<ResumeMetadata>("ResumeMetadata").InsertOneAsync(new ResumeMetadata
        {
            ResumeId = resume.ResumeId,
            Metadata = BsonSerializer.Deserialize<BsonDocument>(resumeJsonString)
        });

        await new MongoDbHelper<Resume>("Resume").UpdateOneAsync(f => f.ResumeId == resume.ResumeId,
            Builders<Resume>.Update.Set(r => r.ParseStatus, "success"));
        
        return Results.Json(new { success = true, data = resumeJsonString }, statusCode: StatusCodes.Status200OK);
    })
    .WithName("Parse")
    .WithOpenApi();

// 结构化数据获取
app.MapGet("/structure/{resumeId}", async (string resumeId, HttpContext ctx) =>
    {
        var r = await new MongoDbHelper<Resume>("Resume").FindOneAsync(f => f.ResumeId == resumeId);
        if (r.ParseStatus != "success")
        {
            return Results.Json(new { success = false, message = "No parsed resume." }, statusCode: StatusCodes.Status400BadRequest);
        }
        
        var mongo = new MongoDbHelper<ResumeMetadata>("ResumeMetadata");
        
        var resume = await mongo.FindOneAsync(f => f.ResumeId == resumeId);

        var jsonString = resume.Metadata.ToJson();

        var xmlString = C.JsonToXml(jsonString);

        var tomlString = C.JsonToToml(jsonString);
        
        return Results.Json(new { success = true, data = new {json = jsonString, xml = xmlString, toml = tomlString} }, statusCode: StatusCodes.Status200OK);
    })
    .WithName("Structure")
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

        var matchResList = new List<MatchResp>();
        matchRes.Documents.ToList().ForEach(item =>
        {
            matchResList.Add(new MatchResp
            {
                ResumeId = item.ResumeId,
                Content = item.Content
            });
        });
        
        return Results.Json(new {success = true, data = matchResList}, statusCode: StatusCodes.Status200OK);
    })
    .Accepts<MatchReq>("application/json")
    .WithName("MatchByKeywords")
    .WithOpenApi();

// 语义匹配
app.MapPost("/smatch", async (HttpContext ctx) =>
    {
        var reqData = await ctx.Request.ReadFromJsonAsync<ParseResumeReq>();

        if (reqData == null)
        {
            return Results.Json(new { success = false, message = "Invalid request data." }, statusCode: StatusCodes.Status400BadRequest);
        }

        var jobJson = reqData.Content;
        
        var resumeMetaList = await new MongoDbHelper<ResumeMetadata>("ResumeMetadata").FindAllAsync();

        var matchRes = new List<JobMatchResp>();
        
        resumeMetaList.ForEach(resume =>
        {
            var resumeJson = resume.Metadata.ToJson();
            
            var result = new RpcCall().Call("match", jobJson, resumeJson).Result;
            
            Console.WriteLine(result);

            var e = JsonSerializer.Deserialize<JsonElement>(result);
            
            matchRes.Add(new JobMatchResp
            {
                Id = resume.ResumeId,
                TextSimilarity = e.GetProperty("text_similarity").GetDouble(),
                SkillScore = e.GetProperty("structured_score").GetDouble(),
                TotalScore = e.GetProperty("total_score").GetDouble()
            });
        });
        
        matchRes.Sort((x, y) => y.TotalScore.CompareTo(x.TotalScore));
        
        return Results.Json(new {success = true, data = matchRes}, statusCode: StatusCodes.Status200OK);
    })
    .WithName("MatchByJobId")
    .WithOpenApi();

// 获取教育背景分布
app.MapGet("/education", async (HttpContext ctx) =>
    {
        var mongo = new MongoDbHelper<ResumeMetadata>("ResumeMetadata");
        
        var resumeMetaList = await mongo.FindAllAsync();

        var eduMap = new Dictionary<string, int>();
        
        resumeMetaList.ForEach(item =>
        {
            var meta = item.Metadata;
            var enumerator = meta.Elements.GetEnumerator();
            for (var i = enumerator; i.MoveNext();)
            {
                var curr = i.Current;
                if (curr.Name == "education")
                {
                    var eduEnumerator = curr.Value.AsBsonArray.GetEnumerator();
                    for (var j = eduEnumerator; j.MoveNext();)
                    {
                        var cur = j.Current;
                        var itemE = cur.AsBsonDocument.Elements.GetEnumerator();
                        for (var k = itemE; k.MoveNext();)
                        {
                            var cur2 = k.Current;
                            if (cur2.Name != "degree") continue;
                            var deg = cur2.Value.AsString;
                            if (deg != null)
                            {
                                if (!eduMap.ContainsKey(deg))
                                {
                                    eduMap.Add(deg, 1);
                                }
                                else
                                {
                                    eduMap[deg]++;
                                }
                            }
                        }
                    }
                }
            }
        });
        
        return Results.Json(new {success = true, data = eduMap}, statusCode: StatusCodes.Status200OK);
    })
    .WithName("Education")
    .WithOpenApi();


// 获取技术分布
app.MapGet("/skill", async (HttpContext ctx) =>
    {
        
        var mongo = new MongoDbHelper<ResumeMetadata>("ResumeMetadata");
        
        var resumeMetaList = await mongo.FindAllAsync();

        var skMap = new Dictionary<string, int>();
        
        resumeMetaList.ForEach(item =>
        {
            var meta = item.Metadata;
            var enumerator = meta.Elements.GetEnumerator();
            for (var i = enumerator; i.MoveNext();)
            {
                var curr = i.Current;
                if (curr.Name == "tech_tags")
                {
                    var techTagsEnumerator = curr.Value.AsBsonArray.GetEnumerator();
                    for (var j = techTagsEnumerator; j.MoveNext();)
                    {
                        var cur = j.Current;
                        var tag = cur.AsString;
                        if (tag != null)
                        {
                            if (!skMap.ContainsKey(tag))
                            {
                                skMap.Add(tag, 1);
                            }
                            else
                            {
                                skMap[tag] += 1;
                            }
                        }
                    }
                }
            }
        });
        
        return Results.Json(new {success = true, data = skMap}, statusCode: StatusCodes.Status200OK);
    })
    .WithName("Skill")
    .WithOpenApi();

app.Run();