using FluentScheduler;
using iText.Forms.Fields.Merging;
using ResumeParserBackend.Collection;
using ResumeParserBackend.Document;
using ResumeParserBackend.Helper;

namespace ResumeParserBackend;

public class ElasticSearchTimer : Registry
{
    public ElasticSearchTimer()
    {
        Schedule(ScheduleTask).ToRunEvery(1).Days().At(0, 0);
    }

    private static async void ScheduleTask()
    {
        try
        {
            var es = new ElasticSearchHelper();

            var clearSuccess = false;
            var retryCount = ConfigManager.Instance.Get(c => c.Es.Retry);
            int retryNow;
            for (retryNow = 0; retryNow < retryCount; retryNow++)
            {
                clearSuccess = await es.ClearAllDataAsync(ConfigManager.Instance.Get(c => c.Es.Index));
                if (clearSuccess)
                {
                    break;
                }
            }

            if (!clearSuccess && retryNow >= retryCount)
            {
                throw new Exception("Could not clear all data");
            }

            var mongo = new MongoDbHelper<ResumeContent>("ResumeContent");

            var resumeContentList = await mongo.FindAllAsync();
            
            var resumeList = new List<ResumeDoc>();
            resumeContentList.ForEach(item =>
            {
                resumeList.Add(new ResumeDoc
                {
                    ResumeId = item.ResumeId,
                    Content = item.FileContent
                });
            });
            
            await es.InitializeDataAsync(ConfigManager.Instance.Get(c => c.Es.Index), resumeList);
        }
        catch (Exception e)
        {
            // ReSharper disable once AsyncVoidMethod
            throw new Exception($"Failed to schedule task: {e.Message}");
        }
    }
}