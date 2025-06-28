using System;
using System.IO;
using System.Net.Http;
using HtmlAgilityPack;


namespace HabrMetaAnalysis;

public class ArticlesDownloader : IDisposable
{
    const string _saveArticlesLocation = @$"{Program.DataPath}Articles\";
    
    private int _startArticleId = 0;
    private int _endArticleId = 0;
    const string _urlToArticles = "https://habr.com/ru/articles/";
    const string _pathToIdSavedValue = @$"{Program.DataPath}lastId";
    
    
    private int _lastArticleID = 0;
    private int _workerId = 0;

    public ArticlesDownloader(int start, int end, int workerId)
    {
        _startArticleId = start;
        _endArticleId = end;
        _workerId = workerId;
        
        if (!TryLoadLastId())
        {
            _lastArticleID = _startArticleId;
        }

        Console.WriteLine($"Last ID: {_lastArticleID}");
    }
    
    private bool TryLoadLastId()
    {
        var path = _pathToIdSavedValue + _workerId;
        if (!File.Exists(path)) return false;

        var idString = File.ReadAllText(path);
        var id = Convert.ToInt32(idString);
        
        if (id == 0 || id < _startArticleId || id > _endArticleId)  return false;
        _lastArticleID = id;
        
        return true;
    }
    private void SaveLastId()
    {
        File.WriteAllText(_pathToIdSavedValue + _workerId, _lastArticleID.ToString());
    }
    
    public void Dispose()
    {
        SaveLastId();
    }

    public void DownloadNewArticle()
    {
        using HttpClient httpClient = new HttpClient();
        httpClient.Timeout = new TimeSpan(0, 0, 20);
        HtmlDocument doc = new HtmlDocument();
        while (_lastArticleID < _endArticleId)
        {
            if (File.Exists(_saveArticlesLocation + _lastArticleID))
            {
                _lastArticleID++;
                continue;
            }
            
            string url = _urlToArticles + _lastArticleID;
            var task = httpClient.GetStringAsync(url);
            string content;
            try
            {
                task.Wait();
                content = task.Result;
            }
            catch (Exception e)
            {
                _lastArticleID++;
                continue;
            }
            doc.LoadHtml(content);

            HtmlNode bodyNode = doc.DocumentNode.SelectSingleNode($"//div[contains(@class, 'tm-layout__wrapper')]");;
            
            File.WriteAllText(_saveArticlesLocation + _lastArticleID, bodyNode?.InnerHtml);
            SaveLastId();
            
            _lastArticleID++;
        }
    }
}