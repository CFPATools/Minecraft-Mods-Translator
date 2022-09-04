using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.CodeCompletion;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using MahApps.Metro.Controls;

namespace Translator;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    private List<TransEntry> transLists = new();
    private List<TransEntry> searchLists = new();
    private int transWordIndex = 0;
    private string FileDir = "";
    private StringBuilder EnJsonText = new();
    private ScrollViewer ScrollViewer;

    public MainWindow()
    {
        InitializeComponent();
        AvalonEditor.TextArea.TextEntered += TextAreaOnTextEntered;
    }

    private static Hashtable SerialText(string json, out List<string> keys)
    {
        keys = new List<string>();
        var hash = new Hashtable();
        var reg = Regex.Matches(json, @"""[^""]+""\:\s*""((\\\S)|[^""])+""");
        var count = new List<string>();
        foreach (Match r in reg)
        {
            var str = r.Value;
            var key = Regex.Match(str, @"(?<="")[^""]+(?=""\:\s*""((\\\S)|[^""])+"")").Value;
            keys.Add(key);
            if (count.Contains(key)) continue;
            if (hash.Contains(key))
            {
                hash.Remove(key);
                count.Add(key);
                continue;
            }
            var value = Regex.Match(str, @"(?<=""[^""]+""\:\s*"")((\\\S)|[^""])+(?="")").Value;
            value = value
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\")
                .Replace("\\/", "/")
                .Replace("\\b", "\b")
                .Replace("\\f", "\f")
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t")
                .Replace("\\v", "\v")
                .Replace("\\0", "\0")
                .Replace("\\a", "\a");
            hash.Add(key, value);
        }

        return hash;
    }

    private List<string> SerialJson(string enJson, string zhJson)
    {
        EnJsonText = new StringBuilder();
        var errorKey = new List<string>();
        try
        {
            EnJsonText.Append(enJson);
            var en = SerialText(enJson, out var keys);
            var transEntryList = new List<TransEntry>();
            var hashTable = new Hashtable();
            foreach (var key in keys)
            {
                var transEntry = new TransEntry
                {
                    TranslationKey = key,
                    EnText = en[key].ToString(),
                    MemoryText = ToLiteral(en[key].ToString())
                };
                transEntryList.Add(transEntry);
                hashTable.Add(key, transEntry);
            }
            var zh = SerialText(zhJson, out var placeholder);
            foreach (var key in zh.Keys)
            {
                if (hashTable.Contains(key.ToString()))
                {
                    ((TransEntry)hashTable[key.ToString()]).ZhText = zh[key].ToString();
                    ReplaceItem((TransEntry)hashTable[key.ToString()]);
                }
                else
                    errorKey.Add(key.ToString());
            }

            // transEntryList = transEntryList.OrderBy(p => p.TranslationKey).ToList();

            transLists = transEntryList;
        }
        catch (Exception e)
        {
            MessageBox.Show("读取文件失败，请检查文件是否有误。");
            return new List<string>();
        }
        AddTransWordList(transLists);
        
        ScrollViewer.ScrollToHorizontalOffset(0);
        return errorKey;
    }

    //添加翻译推进与词典参考
    private void SetTransDictAndReferHidden()
    {
        TranslateSelector.ItemsSource = null;
        TransDict.ItemsSource = null;
        TransHidden1.Visibility = Visibility.Hidden;
        TransHidden2.Visibility = Visibility.Hidden;
    }

    // 翻译推荐点击触发事件
    private void TransSelectClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        AvalonEditor.Text = button.Content.ToString();
    }

    // 清空翻译推荐及词典推荐
    private void RemoveAllTransRecommend()
    {
        TranslateSelector.ItemsSource = null;
        TransDict.ItemsSource = null;
    }

    private void AddTransWordList(IEnumerable<TransEntry> translist)
    {
        TransWordList.ItemsSource = null;
        transWordIndex = 0;
        TransWordList.ItemsSource = translist;
        TransWordList.SelectedIndex = transWordIndex;
        NextTransList();
        
        ScrollViewer.ScrollToHorizontalOffset(0);
    }

    private void SubmitOnClick(object sender, RoutedEventArgs e)
    {
        if (AvalonEditor.Text == "") return;
        transLists[transWordIndex].ZhText = AvalonEditor.Text.Replace("\r", "");
        ReplaceItem(transLists[transWordIndex]);
        SaveFile();
        NextTransList();
    }

    private void SkipOnClick(object sender, RoutedEventArgs e)
    {
        NextTransList();
    }

    private void LastOnClick(object sender, RoutedEventArgs e)
    {
        transWordIndex--;
        if (transWordIndex < 0) transWordIndex = 0;
        TransWordList.SelectedIndex = transWordIndex;
    }

    private void NextTransList()
    {
        SearchBox.Text = "";
        transWordIndex++;
        if (!ReviewCheckBox.IsOn)
            while (transWordIndex < transLists.Count)
            {
                if (MarkCheckBox.IsOn) 
                {
                    if (transLists[transWordIndex].ZhText == "" ||
                        transLists[transWordIndex].ZhText == transLists[transWordIndex].EnText)
                    {
                        break;
                    }
                }
                else if (transLists[transWordIndex].ZhText == "")
                {
                    break;
                }
                transWordIndex++;
            }
        if (transWordIndex >= transLists.Count) transWordIndex = transLists.Count - 1;
        TransWordList.SelectedIndex = transWordIndex;
        
        ScrollViewer.ScrollToHorizontalOffset(0);
    }
    
    private ScrollViewer GetScrollViewer(DependencyObject parent)
    {
        if (parent == null)
            return null;
        var count = VisualTreeHelper.GetChildrenCount(parent);

        for (int i = 0; i < count; i++)
        {
            var item = VisualTreeHelper.GetChild(parent, i);
            if (item is ScrollViewer viewer)
            {
                return viewer;
            }
            return GetScrollViewer(item);
        }
        return null;
    }

    private async void TransWordList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        transWordIndex = TransWordList.SelectedIndex;
        if (transWordIndex == -1) return;
        TransWordList.ScrollIntoView(TransWordList.Items[transWordIndex]);
        AvalonEditor.SelectionStart = 0;
        AvalonEditor.SelectionLength = 0;
        AvalonText.Text = transLists[TransWordList.SelectedIndex].EnText;
        FindFormat();
        AvalonEditor.Text = transLists[TransWordList.SelectedIndex].ZhText;
        AvalonEditor.SelectionStart = AvalonEditor.Text.Length;
        RemoveAllTransRecommend();
        TransProgress.Content = "翻译进度：" + transLists.Count(word => word.ZhText != "") + "/" + transLists.Count;
        KeyName.Content = "当前键名：" + transLists[TransWordList.SelectedIndex].TranslationKey;
        SetTransDictAndReferHidden();
        if (TransCheckBox.IsOn)
        {
            await GetTransDict();
        }
    }

    private async Task GetTransDict()
    {
        await Task.Delay(new Random().Next(50,300));
        var text = await TransApi.Google.Contect(AvalonText.Text);
        var dictObject = new DictObject();
        dictObject.DictTrans.Add(text);
        Dispatcher.Invoke(() =>
        {
            TransHidden1.Visibility = Visibility.Visible;
            TranslateSelector.ItemsSource = dictObject.GetDictTrans();
            // TransHidden2.Visibility = Visibility.Visible;
            // TransDict.ItemsSource = dictObject.GetDictRefer();
        });
    }

    private void OpenFileOnClick(object sender, RoutedEventArgs e)
    {
        OpenFile();
    }

    private async Task OpenFile()
    {
        var fileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择英文JSON文件",
            Filter = "json文件|*.json"
        };
        if (fileDialog.ShowDialog() != true) return;
        var enDir = fileDialog.FileName;
        if (!enDir.EndsWith("en_us.json"))
        {
            MessageBox.Show("请打开英文文件en_us.json，暂时只支持英语到汉语的翻译过程。");
            return;
        }

        FileDir = Path.GetDirectoryName(enDir);

        var zhDir = Path.Combine(FileDir, "zh_cn.json");
        if (File.Exists(zhDir))
        {
            var enAllText = File.ReadAllText(enDir);
            var zhAllText = File.ReadAllText(zhDir);
            SerialJson(enAllText, zhAllText);
        }
        else
        {
            var enAllText = File.ReadAllText(enDir);
            SerialJson(enAllText, "{}");
        }
        FirstPage.Visibility = Visibility.Hidden;
        await SyncScrollPos();
    }

    private async Task SyncScrollPos()
    {
        await Task.Delay(1);
        Dispatcher.Invoke(() =>
        {
            ScrollViewer.ScrollToHorizontalOffset(0);
        });
    }

    private void SaveFileOnClick(object sender, RoutedEventArgs e)
    {
        foreach (var transEntry in transLists)
            ReplaceItem(transEntry);
        if (SaveFile())
            SaveFloatWindow();
    }

    private bool SaveFile()
    {
        try
        {
            File.WriteAllText(Path.Combine(FileDir, "zh_cn.json"), EnJsonText.ToString());
            return true;
        }
        catch (Exception exception)
        {
            MessageBox.Show("文件保存出错");
            return false;
        }
    }

    private void ReplaceItem(TransEntry t)
    {
        if (t.MemoryText == ToLiteral(t.ZhText)) return;
        EnJsonText.Replace(t.MemoryText, ToLiteral(t.ZhText));
        t.MemoryText = ToLiteral(t.ZhText);
    }

    //将C#字符串值转换为转义的字符串文字
    private static string ToLiteral(string input)
    {
        return "\"" + input
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t")
            .Replace("\n", "\\n")
            .Replace("\0", "\\0")
            .Replace("\a", "\\a")
            .Replace("\b", "\\b")
            .Replace("\f", "\\f")
            .Replace("\v", "\\v") + "\"";
    }

    private void SaveFloatWindow()
    {
        var animation = new DoubleAnimationUsingKeyFrames();
        var keyFrames = animation.KeyFrames;
        keyFrames.Add(new LinearDoubleKeyFrame(0, TimeSpan.FromSeconds(0)));
        keyFrames.Add(new LinearDoubleKeyFrame(1, TimeSpan.FromSeconds(0.5)));
        keyFrames.Add(new LinearDoubleKeyFrame(1, TimeSpan.FromSeconds(3)));
        keyFrames.Add(new LinearDoubleKeyFrame(0, TimeSpan.FromSeconds(3.5)));
        
        FloatWindow.BeginAnimation(OpacityProperty, animation);
    }

    private void CtrlSOnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        foreach (var transEntry in transLists)
            ReplaceItem(transEntry);
        if (SaveFile())
            SaveFloatWindow();
    }

    private void Grid_Loaded(object sender, RoutedEventArgs e)
    {
        string name = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + ".mc.xshd";
        System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
        using (System.IO.Stream s = assembly.GetManifestResourceStream(name))
        {
            using (XmlTextReader reader = new XmlTextReader(s))
            {
                var xshd = HighlightingLoader.LoadXshd(reader);
                AvalonEditor.SyntaxHighlighting = HighlightingLoader.Load(xshd, HighlightingManager.Instance);
                AvalonText.SyntaxHighlighting = HighlightingLoader.Load(xshd, HighlightingManager.Instance);
            }
        }
        FirstPage.Visibility = Visibility.Visible;
        ScrollViewer = GetScrollViewer(TransWordList);
        
        ScrollViewer.ScrollToHorizontalOffset(0);
    }

    private List<string> formatList1 = new();
    private List<string> formatList2 = new();
    private List<string> match = new();

    private void FindFormat()
    {
        // (§[0-9a-f])(.*?)(?=§|$)匹配颜色代码
        // %(\d+\$)?[a-hs%]匹配字符串格式化代码
        formatList1 = new List<string>();
        formatList2 = new List<string>();
        foreach (Match m in new Regex(@"%(\d+\$)[a-hs%]").Matches(AvalonText.Text))
            formatList1.Add(m.Value);
        foreach (Match m in new Regex(@"%[a-hs%]").Matches(AvalonText.Text))
            formatList2.Add(m.Value);
    }

    private void AvalonEditor_OnTextChanged(object sender, EventArgs e)
    {
        match = new List<string>();
        WrongHint.Content = "";
        var match1 = (from Match match in new Regex(@"%(\d+\$)[a-hs%]").Matches(AvalonEditor.Text) select match.Value).ToList();
        var match2 = (from Match match in new Regex(@"%[a-hs%]").Matches(AvalonEditor.Text) select match.Value).ToList();

        if (match2.Count <= formatList2.Count && match2.SequenceEqual(formatList2.Take(match2.Count)))
        {
            if (match2.Count != formatList2.Count)
                match.Add(formatList2[match2.Count]);
        }
        else
            WrongHint.Content = "翻译文本的格式化字符与原文不匹配";
        if (match1.Count(formatList1.Contains) == match1.Count && match1.Count <= formatList1.Count)
            match.AddRange(formatList1.Where(s => !match1.Contains(s)).ToList());
        else
            WrongHint.Content = "翻译文本的格式化字符与原文不匹配";
    }

    private void TextAreaOnTextEntered(object sender, TextCompositionEventArgs e)
    {
        
        switch (e.Text)
        {
            case "%":
            {
                if (match.Count == 0) break;
                _completionWindow = new CompletionWindow(AvalonEditor.TextArea);
                
                var data = _completionWindow.CompletionList.CompletionData;
                foreach (var s in match)
                    data.Add(new CompletionData(s, AvalonEditor));
                _completionWindow.CompletionList.SelectedItem = _completionWindow.CompletionList.CompletionData[0];
                _completionWindow.Show();

                _completionWindow.Closed += (o, args) => _completionWindow = null;
                break;
            }
            case "\n":
            {
                if (AvalonEditor.LineCount > AvalonText.LineCount) AvalonEditor.Undo();
                transLists[transWordIndex].ZhText = AvalonEditor.Text.Replace("\r", "");
                ReplaceItem(transLists[transWordIndex]);
                SaveFile();
                NextTransList();
                break;
            }
        }
    }
    
    private CompletionWindow _completionWindow;

    private void MarkCheckBox_OnToggled(object sender, RoutedEventArgs e)
    {
        foreach (var entry in transLists)
        {
            entry.SameCheck = MarkCheckBox.IsOn;
        }
    }

    private async void TransCheckBox_OnToggled(object sender, RoutedEventArgs e)
    {
        if (TransCheckBox.IsOn)
            await GetTransDict();
        else
            SetTransDictAndReferHidden();
    }

    private void SearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (SearchBox.Text != "")
        {
            SearchWordList.SelectedItem = null;
            SearchWordList.ItemsSource = null;
            SearchWordList.Visibility = Visibility.Visible;
            searchLists = transLists.Where(x => x.EnText.Contains(SearchBox.Text) || x.ZhText.Contains(SearchBox.Text)).ToList();
            SearchWordList.ItemsSource = searchLists;
            return;
        }

        SearchWordList.SelectedItem = null;
        SearchWordList.Visibility = Visibility.Hidden;
    }

    private void SearchWordList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SearchWordList.SelectedIndex == -1) return;
        var search = searchLists[SearchWordList.SelectedIndex];
        var index = transLists.FindIndex(x => x.TranslationKey == search.TranslationKey);
        TransWordList.SelectedIndex = index;
        SearchBox.Text = "";
    }

    private void OpenGithubSite(object sender, RoutedEventArgs e)
    {
        Process.Start("https://github.com/Tryanks/Minecraft-Mods-Translator");
    }
}

public class DictTransList
{
    public string? Trans { get; set; } = "";

    public double Width => GetTextWidth(Trans) + 20;

    private double GetTextWidth(string? text)
    {
        var textBlock = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.NoWrap,
            FontSize = 15
        };
        textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        textBlock.Arrange(new Rect(textBlock.DesiredSize));
        return textBlock.ActualWidth;
    }
}

public class DictReferList
{
    private string? _refer = "";
    public string? Refer
    {
        get => "　　" + _refer;
        set => _refer = value;
    }
}

public class DictObject
{
    public string OriginText = "";
    public string Time = "";
    public List<string> DictTrans = new();
    public List<string> DictRefer = new();

    public IEnumerable<DictTransList> GetDictTrans()
    {
        return DictTrans.Select(s => new DictTransList() { Trans = s });
    }

    public IEnumerable<DictReferList> GetDictRefer()
    {
        return DictRefer.Select(s => new DictReferList() { Refer = s });
    }
}

public class TransEntry : INotifyPropertyChanged
{
    public string MemoryText = "";
    
    private bool _sameCheck = false;

    public bool SameCheck
    {
        get => _sameCheck;
        set
        {
            _sameCheck = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SameCheck)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Background)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FontBold)));
        }
    }
    public string TranslationKey = "";
    public string? EnText { get; set; } = "";

    private string? _zhText = "";
    public string Color => ZhText == "" ? "red" : "green";
    public string Background => ZhText == EnText && SameCheck ? "Brown" : "Black";
    public string FontBold => ZhText == EnText && SameCheck ? "Bold" : "Normal";
    public string Judge => ZhText == "" ? "×" : "√";
    public string? ZhText
    {
        get => _zhText;
        set
        {
            _zhText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ZhText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Color)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Judge)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Background)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FontBold)));
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}
    
public class CompletionData : ICompletionData
{
    public CompletionData(string text, TextEditor avalon)
    {
        Text = text;
        Avalon = avalon;
    }

    public TextEditor Avalon;

    public ImageSource Image => null;

    public string Text { get; }

    public object Content => Text;

    public object Description => "选择" + this.Text;

    /// <inheritdoc />
    public double Priority { get; }

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        Avalon.SelectedText = Text.Substring(1);
        Avalon.SelectionLength = 0;
        Avalon.SelectionStart += Text.Length - 1;
        // textArea.Document.Replace(completionSegment, Text);
    }
}