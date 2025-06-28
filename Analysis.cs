using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
using MathNet.Numerics.Statistics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.Optimization;


namespace HabrMetaAnalysis;

public class Analysis
{
    List<ArticleInfo> articles => ArticlesManager.Articles;
    private OutputDataFile dataFile;
    
    public void Run()
    {
        CultureInfo.CurrentCulture = new CultureInfo("en-US");
        CultureInfo.CurrentCulture.NumberFormat.NumberDecimalDigits = 0;
        
        dataFile = new OutputDataFile();
        dataFile.New("Correlations");
        CalculateCorrelations();
        dataFile.Save();
        dataFile.New("GeneralStatics");
        GeneralStatics();
        dataFile.Save();
        dataFile.New("TargetStatics");
        TargetStatics();
        dataFile.Save();
        dataFile.Dispose();
    }

    public void TargetStatics()
    {
        dataFile.AddNewWorksheet("AI");
        {
            var aiGroup = articles.Select(a => new { Month = a.CreatedDate.Month, IsAi = a.UsingAi, a.IsCompany });
            var firstMonthAverageS = aiGroup.Where(d => d.Month == 1 && !d.IsCompany).Average(d => d.IsAi);
            var sg = aiGroup.Where(d => !d.IsCompany).GroupBy(d => d.Month).OrderBy(g => g.Key);
            var firstMonthAverageC = aiGroup.Where(d => d.Month == 1 && d.IsCompany).Average(d => d.IsAi);
            var cg = aiGroup.Where(d => d.IsCompany).GroupBy(d => d.Month).OrderBy(g => g.Key);
            
            dataFile.AddRowName("IsAiCoefficientSingle");
            dataFile.AddValue(sg
                .Select( g => $"{new DateTime(2000, g.Key, 1).ToString("MMMM", new CultureInfo("ru-RU"))}"), 
                sg.Select(g => g.Select(d => d.IsAi).Average().ToString()));
            dataFile.AddRowName("IsAiDynamicCoefficientSingle");
            dataFile.AddValue(sg
                    .Select( g => $"{new DateTime(2000, g.Key, 1).ToString("MMMM", new CultureInfo("ru-RU"))}"), 
                sg.Select(g => (g.Select(d => d.IsAi).Average() - firstMonthAverageS).ToString()));
            
            dataFile.AddRowName("IsAiCoefficientCompany");
            dataFile.AddValue(cg
                    .Select( g => $"{new DateTime(2000, g.Key, 1).ToString("MMMM", new CultureInfo("ru-RU"))}"), 
                cg.Select(g => g.Select(d => d.IsAi).Average().ToString()));
            dataFile.AddRowName("IsAiDynamicCoefficientCompany");
            dataFile.AddValue(cg
                    .Select( g => $"{new DateTime(2000, g.Key, 1).ToString("MMMM", new CultureInfo("ru-RU"))}"), 
                cg.Select(g => (g.Select(d => d.IsAi).Average() - firstMonthAverageC).ToString()));
        }
    }
    
    public void GeneralStatics()
    {
        Console.WriteLine($"Всего охвачено id: {(Program.EndArticleId - Program.StartArticleId):N}");
        Console.WriteLine($"Из них было доступно статей на момент анализа: {Directory.GetFiles(@"E:\Projects\HabrMetaAnalysis\Res\Articles").Length:N}");
        Console.WriteLine($"Статей попало в выборку: {articles.Count:N}");
        Console.WriteLine($"Из них статей написанных компаниями: {articles.Where(a => a.IsCompany).Count():N}");
        Console.WriteLine($"Суммарное количество просмотров: {articles.Select(a => a.Views).Sum():N}");
        Console.WriteLine($"Потрачено времени на прочтение статей: {FormatMinutes(articles.Select(a => a.Views * (long)Math.Max(a.TimeForReading, 1)).Sum())}");
        Console.WriteLine($"Наибольший рейтинг статьи: {articles.OrderBy(a => a.RankArticle).TakeLast(1).Select(a => $"{a.RankArticle} - {a.Id}").Single()}");
        Console.WriteLine($"Наименьший рейтинг статьи: {articles.OrderBy(a => a.UpRankArticle - a.DownRankArticle).Take(1).Select(a => $"{a.UpRankArticle - a.DownRankArticle} - {a.Id}").Single()}");
        Console.WriteLine($"Максимум просмотров на статье: {articles.OrderBy(a => a.Views).Last().Views:N} - {articles.OrderBy(a => a.Views).Last().Id}");
        Console.WriteLine($"Самая длинная статья: {articles.OrderBy(a => a.TimeForReading).Last().TimeForReading} минут id {articles.OrderBy(a => a.TimeForReading).Last().Id}");
        var month = articles.GroupBy(a => a.CreatedDate.Month).OrderBy(g => g.Count()).Last().Key;
        Console.WriteLine($"Месяц с наибольшим количеством публикаций - {new DateTime(2000, month, 1).ToString("MMMM", new CultureInfo("ru-RU"))}, " +
                          $"количество публикаций - {articles.Count(a => a.CreatedDate.Month == month)}");
        Console.WriteLine($"Максимальное количество публикаций на автора: {articles.GroupBy(a => a.Author).Select(g => new {Tag = g.Key, Count = g.Count()})
            .OrderBy(g => g.Count).TakeLast(1).Select(d => $"{d.Tag} - {d.Count:N} ({d.Count / (float)articles.Count:P})").Single()}");
        Console.WriteLine($"Самые популярные типы публикаций: {string.Join(", ", articles.SelectMany(a => 
                a.ArticleTypes.Split(ArticleInfo.Separator).Select(h => h)).Where(s => !string.IsNullOrWhiteSpace(s)).GroupBy(a => a)
            .OrderBy(g => g.Count()).TakeLast(5).Reverse().Select(a => a.Key))}");
        Console.WriteLine($"Самые популярные хабы по количеству статей | просмотрам: {string.Join(", ", articles.SelectMany(a => 
            a.ArticleHubs.Split(ArticleInfo.Separator).Where(s => !string.IsNullOrWhiteSpace(s)).Select(tag => new { Tag = tag, Article = a })).GroupBy(a => a.Tag)
            .Select(g => new 
            { 
                Tag = g.Key, 
                Count = g.Count()
            })
            .OrderBy(g => g.Count).TakeLast(5).Reverse().Select(a => $"{a.Tag} - {a.Count}"))} | " +
                          $"{string.Join(", ", 
                              articles
                                  .SelectMany(a => a.ArticleHubs.Split(ArticleInfo.Separator).Where(s => !string.IsNullOrWhiteSpace(s))
                                      .Select(tag => new { Tag = tag, Article = a }))
                                  .GroupBy(x => x.Tag)
                                  .Select(g => new 
                                  { 
                                      Tag = g.Key, 
                                      Views = g.Sum(x => x.Article.Views)
                                  })
                                  .OrderBy(d => d.Views).TakeLast(5).Reverse().Select(d => $"{d.Tag} - {d.Views:N}"))}");
        Console.WriteLine($"Самые популярные теги по количеству статей | просмотрам: {string.Join(", ", articles.SelectMany(a => 
                a.ArticleTags.Split(ArticleInfo.Separator).Select(h => h)).Where(s => !string.IsNullOrWhiteSpace(s)).GroupBy(a => a)
            .Select(g => new { Tag = g.Key, Count = g.Count()})
            .OrderBy(g => g.Count).TakeLast(5).Reverse().Select(d => $"{d.Tag} - {d.Count:N}"))} | " +
                          $"{string.Join(", ", 
                              articles
                                  .SelectMany(a => a.ArticleTags.Split(ArticleInfo.Separator).Where(s => !string.IsNullOrWhiteSpace(s))
                                      .Select(tag => new { Tag = tag, Article = a }))
                                  .GroupBy(x => x.Tag)
                                  .Select(g => new 
                                  { 
                                      Tag = g.Key, 
                                      Views = g.Sum(x => x.Article.Views)
                                  })
                                  .OrderBy(d => d.Views).TakeLast(5).Reverse().Select(d => $"{d.Tag} - {d.Views:N}"))}");
        Console.WriteLine($"Статей от компаний в написании которых использовался ИИ: " +
                          $"{articles.Where(a => a.IsCompany && a.UsingAi > 235).Count() / (float)articles.Where(a => a.IsCompany).Count():P}");
        Console.WriteLine($"Статей от одиночных авторов в написании которых использовался ИИ: " +
                          $"{(float)articles.Where(a => !a.IsCompany && a.UsingAi > 235).Count() / articles.Where(a => !a.IsCompany).Count():P}");
    }
    
    public void CalculateCorrelations()
    {
        // Получаем все числовые свойства
        var numericProps = typeof(ArticleInfo).GetFields()
            .Where(p => p.FieldType == typeof(float) || p.FieldType == typeof(int) ||  p.FieldType == typeof(DateTime) ||  p.FieldType == typeof(TimeSpan) || p.FieldType == typeof(string) ||  p.FieldType == typeof(bool))
            .Where(p => p.Name != nameof(ArticleInfo.Id) && p.Name != nameof(ArticleInfo.Author) && p.Name != nameof(ArticleInfo.ArticleTypes))
            .ToList();

        var data = new Dictionary<string, List<double>>();

        // Собираем данные по каждому признаку
        foreach (var prop in numericProps)
        {
            if (prop.FieldType == typeof(string))
            {
                data[prop.Name] = articles.Select(a => Convert.ToDouble(((string)prop.GetValue(a)).Split(ArticleInfo.Separator).Length)).ToList();
            }
            else if (prop.FieldType == typeof(DateTime))
            {
                data[prop.Name] = articles.Select(a => ((DateTime)prop.GetValue(a)).ToOADate()).ToList();
            }
            else if (prop.FieldType == typeof(TimeSpan))
            {
                data[prop.Name] = articles.Select(a => ((TimeSpan)prop.GetValue(a)).TotalMinutes).ToList();
            }
            else if (prop.FieldType == typeof(bool))
            {
                data[prop.Name] = articles.Select(a => ((bool)prop.GetValue(a)) ? 100d : 1d).ToList();
            }
            else
            {
                var values = articles.Select(a => Convert.ToDouble(prop.GetValue(a))).ToList();
                data[prop.Name] = values;
            }
        }
        
        // Считаем корреляции между всеми парами
        for (int i = 0; i < numericProps.Count; i++)
        {
            for (int j = i + 1; j < numericProps.Count; j++)
            {
                var name1 = numericProps[i].Name;
                var name2 = numericProps[j].Name;
                
                var corr = Correlation.Pearson(
                    data[name1].Where((d, i) => 
                        (!double.IsNaN(d) && !double.IsInfinity(d) && d != 0 &&
                         !double.IsNaN(data[name2][i]) && !double.IsInfinity(data[name2][i]) && data[name2][i] != 0)
                    ), 
                    data[name2].Where((d, i) => 
                    (!double.IsNaN(d) && !double.IsInfinity(d) && d != 0 &&
                     !double.IsNaN(data[name1][i]) && !double.IsInfinity(data[name1][i]) && data[name1][i] != 0)
                    ));
                
                

                
                var corr2 = Correlation.Spearman(
                    data[name1].Where((d, i) => 
                        (!double.IsNaN(d) && !double.IsInfinity(d) && d != 0 &&
                         !double.IsNaN(data[name2][i]) && !double.IsInfinity(data[name2][i]) && data[name2][i] != 0)
                    ), 
                    data[name2].Where((d, i) => 
                        (!double.IsNaN(d) && !double.IsInfinity(d) && d != 0 &&
                         !double.IsNaN(data[name1][i]) && !double.IsInfinity(data[name1][i]) && data[name1][i] != 0)
                    ));
                
                
                 if (Math.Abs(corr) < 0.15f && Math.Abs(corr2) < 0.15f) continue;
                 
                Console.WriteLine($"Параметр {name1} к {name2}:");
                Console.WriteLine($"Pearson = {corr:F3}");
                Console.WriteLine($"Spearman = {corr2:F3} \n");
                
                dataFile.AddRowName($"Параметр {name1} к {name2}:");
                dataFile.AddValue("Pearson", corr.ToString("F3"));
                dataFile.AddValue("Spearman", corr2.ToString("F3"));
            }
        }
    }
    
    string FormatMinutes(long totalMinutes)
    {
        const long minutesInYear = 525600; // 365 * 24 * 60
        const long minutesInMonth = 43200; // 30 * 24 * 60 (условно)
        const long minutesInDay = 1440;
        const long minutesInHour = 60;

        long years = totalMinutes / minutesInYear;
        totalMinutes %= minutesInYear;

        long months = totalMinutes / minutesInMonth;
        totalMinutes %= minutesInMonth;

        long days = totalMinutes / minutesInDay;
        totalMinutes %= minutesInDay;

        long hours = totalMinutes / minutesInHour;
        long minutes = totalMinutes % minutesInHour;

        var sb = new StringBuilder();
        if (years > 0) sb.Append($"Years {years} ");
        if (months > 0) sb.Append($"Months {months} ");
        if (days > 0) sb.Append($"Days {days} ");
        if (hours > 0) sb.Append($"Hours {hours} ");
        if (minutes > 0) sb.Append($"Minutes {minutes}");

        return sb.ToString().Trim();
    }
}