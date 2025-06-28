using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;


namespace HabrMetaAnalysis;

public class OutputDataFile : IDisposable
{
    public IXLWorksheet Worksheet => _worksheet;
    public string Text => _writer.ToString(); 
    
    private TextWriter _writer;
    private TextWriter _console;
    private StringBuilder _sb;
    private XLWorkbook _workbook;
    private IXLWorksheet _worksheet;
    
    private Dictionary<string, List<(string, int)>> _columnNameToValuesRowsIndex;
    private List<string> _valuesNames;
    private int _lastValueIndex;

    private const string folder = @"Data\";
    
    string _name = "";
    


    public OutputDataFile()
    {
        _sb = new StringBuilder(512);
        _writer = new StringWriter(_sb);

        _console = Console.Out;
        Console.SetOut(_writer);
        
        _workbook = new XLWorkbook();
        _columnNameToValuesRowsIndex = new Dictionary<string, List<(string, int)>>();
        _valuesNames = new List<string>();
    }
    
    public void New(string name)
    {
        _name = name;
        _workbook.Worksheets.ForEach(w => w.Delete());
        _sb.Clear();
        _worksheet = _workbook.AddWorksheet(0);
    }

    public OutputDataFile Save()
    {
        var textData = _writer.ToString();
        using (StreamWriter sw = new StreamWriter(Program.DataPath+ folder + _name + ".txt"))
        {
            sw.Write(textData);
        }

        FormatToWorksheet();
        if (_workbook.Worksheets.Any(w => w.LastCellUsed() != null))
        {
            _workbook.Worksheets.ForEach(w => w.Columns().AdjustToContents());
            _workbook.SaveAs(Program.DataPath+ folder + _name + ".xlsx");
        }

        if (_workbook.Worksheets.Any(w => w.LastCellUsed() != null))
        {
            foreach (var w in _workbook.Worksheets)
            {
                _console.WriteLine(w.ToText());
            }
        }
        else
        {
            _console.WriteLine(textData);
        }

        return this;
    }

    public void AddNewWorksheet(string name)
    {
        FormatToWorksheet();
        
        if (_worksheet.IsEmpty()) _worksheet.Delete();
        
        _worksheet = _workbook.AddWorksheet(name);
    }

    private void FormatToWorksheet()
    {
        if (_valuesNames.Count == 0) return;
        
        var w = Worksheet;
        w.FirstRow().Cell(1).Value = " ";
        var e = _columnNameToValuesRowsIndex.GetEnumerator();

        for (int r = 0; r < _valuesNames.Count; r++)
        {
            w.Cell(r + 2, 1).Value = _valuesNames[r];
        }
        for (int c = 0; c < _columnNameToValuesRowsIndex.Count; c++)
        {
            e.MoveNext();
            var keysValue = e.Current;
            var column = w.Column(2 + c);
            column.Cell(1).Value = keysValue.Key;
            foreach (var valueIndex in keysValue.Value)
            {
                column.Cell(valueIndex.Item2 + 2).Value = valueIndex.Item1;
            }
        }
        
        _columnNameToValuesRowsIndex.Clear();
        _valuesNames.Clear();
        _lastValueIndex = 0;
    }

    
    public void AddValue(string columnName, string value)
    {
        List<(string, int)> values;
        if (_columnNameToValuesRowsIndex.TryGetValue(columnName, out values))
        {
            
        }
        else
        {
            values = new List<(string, int)>();
            _columnNameToValuesRowsIndex.Add(columnName, values);
        }
        
        values.Add((value, _lastValueIndex));
    }

    public void AddRowName(string rowName)
    {
        if (_lastValueIndex == 0 && _valuesNames.Count == 0 && _columnNameToValuesRowsIndex.Count == 0)
        {
            _lastValueIndex--;
        }
        
        _lastValueIndex++;
        _valuesNames.Add(rowName);
    }

    public void Dispose()
    {
        Console.SetOut(_console);
        _writer.Dispose();
    }
}