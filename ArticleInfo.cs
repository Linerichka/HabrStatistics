using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using HtmlAgilityPack;

// ReSharper disable UnusedMember.Global


namespace HabrMetaAnalysis;

[SuppressMessage("ReSharper", "UnassignedReadonlyField")]
public class ArticleInfo
{
    private const string _articlesLocation = @$"{Program.DataPath}Articles";
    private const string _preparedArticlesLocation = $@"{Program.DataPath}PreparedArticlesInfo\";
    
    public readonly int Id;
    public readonly string Author;
    public readonly DateTime CreatedDate;
    public readonly bool IsCompany;
    public readonly float UsingAi;
    public readonly int TimeForReading;
    public readonly int Comments;
    public readonly int Views;
    public readonly int Saves;
    public readonly int UpRankArticle;
    public readonly int DownRankArticle;
    public readonly int RankArticle;
    public readonly int RankAuthor;
    public readonly string ArticleTypes;
    public readonly string ArticleHubs;
    public readonly string ArticleTags;
    public const char Separator = ';';
    
    public readonly float RankArticleToViewRatio;
    public readonly float RankAuthorToViewRatio;
    public readonly float CommentsArticleToViewRatio;
    public readonly float SavesArticleToViewRatio;
    public TimeSpan TimeInDraft;
    
    public ArticleInfo(string pathToArticle, bool forceCreate = false)
    {
        var idArticle = Path.GetRelativePath(_articlesLocation, pathToArticle);
        var preparedPath = Path.Combine(_preparedArticlesLocation, idArticle);
        
        if (pathToArticle.Contains(_preparedArticlesLocation)) preparedPath = pathToArticle;
        
        if (File.Exists(preparedPath) && !forceCreate)
        {
            using (StreamReader sr = new StreamReader(preparedPath))
            {
                this.Id = int.Parse(sr.ReadLine());
                this.Author = sr.ReadLine();
                this.CreatedDate = DateTime.Parse(sr.ReadLine());
                this.IsCompany = Convert.ToBoolean(sr.ReadLine());
                this.UsingAi = Convert.ToSingle(sr.ReadLine());
                this.Comments = Convert.ToInt32(sr.ReadLine());
                this.TimeForReading = Convert.ToInt32(sr.ReadLine());
                this.Views = Convert.ToInt32(sr.ReadLine());
                this.Saves = Convert.ToInt32(sr.ReadLine());
                this.UpRankArticle = Convert.ToInt32(sr.ReadLine());
                this.DownRankArticle = Convert.ToInt32(sr.ReadLine());
                this.RankArticle = Convert.ToInt32(sr.ReadLine());
                this.RankAuthor = Convert.ToInt32(sr.ReadLine());
                this.ArticleTypes = sr.ReadLine();
                this.ArticleHubs = sr.ReadLine();
                this.ArticleTags = sr.ReadLine();
                this.RankArticleToViewRatio = Convert.ToSingle(sr.ReadLine());
                this.RankAuthorToViewRatio = Convert.ToSingle(sr.ReadLine());
                this.CommentsArticleToViewRatio = Convert.ToSingle(sr.ReadLine());
                this.SavesArticleToViewRatio = Convert.ToSingle(sr.ReadLine());
                this.TimeInDraft = TimeSpan.Parse(sr.ReadLine());
            }
        }
        else
        {
            using (StreamReader sr = new StreamReader(pathToArticle))
            {
                var text = sr.ReadToEnd();
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(text);
                
                this.Id = int.Parse(idArticle);
                this.Author = doc.DocumentNode.SelectSingleNode($"//*[contains(concat(' ', normalize-space(@class), ' '), ' tm-user-info__username ')]")?.InnerText ?? "none";
                this.CreatedDate = DateTime.Parse(doc.DocumentNode.SelectSingleNode("//time").Attributes["datetime"].Value);
                this.IsCompany = doc.DocumentNode.SelectNodes($"//a[contains(concat(' ', normalize-space(@class), ' '), ' tm-hubs-list__link ')]//span")?
                    .Nodes().Select(n => n.InnerText).Any(t => t.Contains("Блог компании ")) ?? false;
                this.UsingAi = IsAi(text);
                this.Comments = int.Parse(doc.DocumentNode.SelectSingleNode($"//*[contains(concat(' ', normalize-space(@class), ' '), ' tm-article-comments-counter-link__value ')]").InnerText);
                this.TimeForReading = int.Parse(doc.DocumentNode.SelectSingleNode($"//span[contains(concat(' ', normalize-space(@class), ' '), ' tm-article-reading-time__label ')]")?.InnerText.Replace(" мин", "") ?? "1");
                this.Views = ParseWithSuffix(doc.DocumentNode.SelectSingleNode($"//*[contains(concat(' ', normalize-space(@class), ' '), ' tm-icon-counter__value ')]").InnerText);
                this.Saves = int.Parse(doc.DocumentNode.SelectSingleNode($"//span[contains(concat(' ', normalize-space(@class), ' '), ' bookmarks-button__counter ')]").InnerText);
                {
                    var stringRanks = doc.DocumentNode.SelectSingleNode($"//svg[contains(concat(' ', normalize-space(@class), ' '), ' tm-svg-img tm-votes-meter__icon tm-votes-meter__icon tm-votes-meter__icon_appearance-article ')]//title")
                        .InnerText;
                    if (stringRanks.Contains("Всего голосов "))
                    {
                        stringRanks = stringRanks.Replace("Всего голосов ", "");
                        // - ignore
                        var sum = stringRanks.Substring(0, stringRanks.IndexOf(':'));
                        var up = stringRanks.Substring(stringRanks.IndexOf('↑') + 1,
                            stringRanks.IndexOf(" и") - stringRanks.IndexOf('↑'));
                        var down = stringRanks.Substring(stringRanks.IndexOf('↓') + 1,
                            stringRanks.Length - stringRanks.IndexOf('↓') - 1);

                        this.UpRankArticle = int.Parse(up);
                        this.DownRankArticle = int.Parse(down);
                        //this.RankArticle = int.Parse(sum);
                        this.RankArticle = UpRankArticle - DownRankArticle;
                    }
                    else
                    {
                        this.UpRankArticle = 0;
                        this.DownRankArticle = 0;
                        this.RankArticle = 0;
                    }
                }
                this.RankAuthor = int.Parse(doc.DocumentNode.SelectSingleNode($"//div[contains(@class, 'karma-display')]")?.InnerText ?? "0");
                
                
                this.ArticleTypes = string.Join(Separator, doc.DocumentNode.SelectNodes($"//div[contains(@class, 'tm-article-labels__container')]//div")?
                    .Nodes().Select(n => n.FirstChild.InnerText) ?? Array.Empty<string>());
                this.ArticleHubs = string.Join(Separator, doc.DocumentNode.SelectNodes($"//a[contains(concat(' ', normalize-space(@class), ' '), ' tm-hubs-list__link ')]//span")?
                    .Nodes().Select(n => n.InnerText) ?? Array.Empty<string>());
                this.ArticleTags = string.Join(Separator, doc.DocumentNode.SelectNodes($"//a[contains(concat(' ', normalize-space(@class), ' '), ' tm-tags-list__link ')]//span")?
                    .Nodes().Select(n => n.InnerText) ?? Array.Empty<string>());
                
                
                this.RankArticleToViewRatio = RankArticle / (float)Views;
                this.RankAuthorToViewRatio = RankAuthor / (float)Views;
                this.CommentsArticleToViewRatio = Comments / (float)Views;
                this.SavesArticleToViewRatio = Saves / (float)Views;
            }
        }
    }

    public void SaveToFile()
    {
        using (StreamWriter stream = new StreamWriter(Path.Combine(_preparedArticlesLocation, Id.ToString())))
        {
            stream.WriteLine(this.Id);
            stream.WriteLine(this.Author);
            stream.WriteLine(this.CreatedDate);
            stream.WriteLine(this.IsCompany);
            stream.WriteLine(this.UsingAi);
            stream.WriteLine(this.Comments);
            stream.WriteLine(this.TimeForReading);
            stream.WriteLine(this.Views);
            stream.WriteLine(this.Saves);
            stream.WriteLine(this.UpRankArticle);
            stream.WriteLine(this.DownRankArticle);
            stream.WriteLine(this.RankArticle);
            stream.WriteLine(this.RankAuthor);
            stream.WriteLine(this.ArticleTypes);
            stream.WriteLine(this.ArticleHubs);
            stream.WriteLine(this.ArticleTags);
            stream.WriteLine(this.RankArticleToViewRatio);
            stream.WriteLine(this.RankAuthorToViewRatio);
            stream.WriteLine(this.CommentsArticleToViewRatio);
            stream.WriteLine(this.SavesArticleToViewRatio);
            stream.WriteLine(this.TimeInDraft.ToString(@"d\.hh\:mm\:ss"));
        }
    }
    
     private static float IsAi(string text)
    {
        Program.Input v = new Program.Input(){Text = text};
        var r = Program.PredictionEngine.Predict(v);
        return r.Score;
    }
     
    private static int ParseWithSuffix(string s)
    {
        s = s.ToUpper().Trim();

        double number;
        if (s.EndsWith("K"))
        {
            // Удаляем суффикс K
            string numPart = s.Substring(0, s.Length - 1);
            if (double.TryParse(numPart, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out number))
            {
                return (int)(number * 1000);
            }
        }
        else
        {
            // Если нет суффикса, просто парсим как int
            if (int.TryParse(s, out int value))
            {
                return value;
            }
        }
        throw new Exception();
    }
}