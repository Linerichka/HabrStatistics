using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;


namespace HabrMetaAnalysis;

public static class Extension
{
    public static void SaveToExcel(string filePath, string[] parameterNames, params (string columnName, double[] values)[] columnToValues)
    {
        filePath = Path.GetFullPath(Path.Combine(Program.DataPath, filePath));
        if (Path.GetExtension(filePath) != ".xlsx") filePath += ".xlsx";

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Данные");

        // Заголовки
        for (int i = 0; i < columnToValues.Length; i++)
        {
            worksheet.Cell(1, i + 2).Value = columnToValues[i].columnName;
        }

        // Данные
        for (int i = 0; i < parameterNames.Length; i++)
        {
            worksheet.Cell(i + 2, 1).Value = parameterNames[i];
            for (int j = 0; j < columnToValues.Length; j++)
            {
                worksheet.Cell(i + 2, j + 2).Value = columnToValues[j].values[i];
            }
        }

        workbook.SaveAs(filePath);
    }

    public static string ToText(this IXLWorksheet worksheet)
    {
        var sb = new StringBuilder();

        // Определяем границы заполненной области
        var usedRange = worksheet.RangeUsed();
        if (usedRange == null)
        {
            return "Пустой лист";
        }

        var firstRow = usedRange.RangeAddress.FirstAddress.RowNumber;
        var lastRow = usedRange.RangeAddress.LastAddress.RowNumber;
        var firstCol = usedRange.RangeAddress.FirstAddress.ColumnNumber;
        var lastCol = usedRange.RangeAddress.LastAddress.ColumnNumber;

        for (int row = firstRow; row <= lastRow; row++)
        {
            for (int col = firstCol; col <= lastCol; col++)
            {
                var cell = worksheet.Cell(row, col);
                sb.Append(cell.GetFormattedString());

                if (col < lastCol)
                    sb.Append(" | ");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public static void ForEach<T>(this IEnumerable<T> collection, Action<T> action)
    {
        foreach (var item in collection)
        {
            action(item);
        }
    }
    public static void ForEach<T>(this IEnumerable<T> collection, Action<T, int> action)
    {
        int i = 0;
        foreach (var item in collection)
        {
            i++;
            action(item, i);
        }
    }
    public static void ForEach(this IEnumerable collection, Action<int> action)
    {
        int i = 0;
        foreach (var item in collection)
        {
            i++;
            action(i);
        }
    }

    public static void AddValue(this OutputDataFile data, IEnumerable<string> columnNames, IEnumerable<string> values)
    {
        var ve = values.GetEnumerator();
        foreach (var c in columnNames)
        {
            ve.MoveNext();
            var v = ve.Current;
            data.AddValue(c, v);
        }
    }
    public static void AddValue(this OutputDataFile data, IEnumerable<(string, string)> columnNamesToValues)
    {
        foreach (var cv in columnNamesToValues)
        {
            data.AddValue(cv.Item1, cv.Item2);
        }
    }
}