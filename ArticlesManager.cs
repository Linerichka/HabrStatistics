using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using ClosedXML.Excel;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearRegression;


namespace HabrMetaAnalysis;

public class ArticlesManager
{ 
    const string _articlesLocation = @$"{Program.DataPath}Articles";
    const string _preparedArticlesLocation = @$"{Program.DataPath}PreparedArticlesInfo\";

    public static List<ArticleInfo> ArticlesAll = new List<ArticleInfo>();
    public static List<ArticleInfo> Articles = new List<ArticleInfo>();



    public void Load()
    {
        foreach (var file in Directory.GetFiles(_preparedArticlesLocation))
        {
            ArticlesAll.Add(new ArticleInfo(file));
        }
        Articles = ArticlesAll.Where(a => a.CreatedDate.Year == 2024).ToList();
    }

    public void CreateAndSave(bool force = false)
    {
        foreach (var file in Directory.GetFiles(_articlesLocation))
        {
            try
            {
                var a = new ArticleInfo(file, force);
                a.SaveToFile();
                ArticlesAll.Add(a);
            }
            catch (Exception ex)
            {
                Console.WriteLine(file);
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        Articles = ArticlesAll.Where(a => a.CreatedDate.Year == 2024).ToList();
    }

    public void Save()
    {
        foreach (var article in ArticlesAll)
        {
            article.SaveToFile();
        }
    }

    public void SaveToExcel()
    {
        SaveToExcel(ArticlesAll);
        SaveToExcel(Articles, "Articles2024");
    }

    private void SaveToExcel(List<ArticleInfo> articles, string name = "ArticlesAll")
    {
        var path = Program.DataPath + $"{name}.xlsx";
        var wb = new XLWorkbook();
        var w = wb.Worksheets.Add("Articles");
        var cul = CultureInfo.CurrentCulture;

        var fields = typeof(ArticleInfo).GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance);
        for (int i = 0; i < fields.Length; i++)
        {
            var field = fields[i];
            w.FirstRow().Cell(i + 1).Value = field.Name;

            if (field.FieldType == typeof(TimeSpan))
            {
                for (int j = 0; j < articles.Count; j++)
                {
                    var a = articles[j];
                    w.Cell(j + 2, i + 1).Value = XLCellValue.FromObject(((TimeSpan)field.GetValue(a)).TotalDays, cul);
                }
            }
            else
            {
                for (int j = 0; j < articles.Count; j++)
                {
                    var a = articles[j];
                    w.Cell(j + 2, i + 1).Value = XLCellValue.FromObject(field.GetValue(a), cul);
                }
            }

            Console.WriteLine($"Field:  {field.Name} end");
        }

        fields.ForEach((int i) => w.Column(i+1).AdjustToContents());
        wb.SaveAs(path);
    }

    public void CalculateArticleTimeInDraft()
    {
        foreach (var a in Articles)
        {
            a.TimeInDraft = a.CreatedDate - (ArticlesAll.Where(d => d.Id > a.Id).MinBy(d => d.CreatedDate)?.CreatedDate ?? a.CreatedDate);
        }
    }
}