using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using TransAPICSharpDemo;
using System.Text.RegularExpressions;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.CodeCompletion;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;

namespace Translator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<TransEntry> transLists = new();
        private int transWordIndex = 0;
        private string FileDir = "";

        public MainWindow()
        {
            InitializeComponent();
            AvalonEditor.TextArea.TextEntered += TextAreaOnTextEntered;
        }

        private List<string> SerialJson(string enJson, string zhJson)
        {
            var errorKey = new List<string>();
            try
            {
                var en = JsonConvert.DeserializeObject<Hashtable>(enJson);
                var zh = JsonConvert.DeserializeObject<Hashtable>(zhJson);
                // var en = JsonDocument.Parse(enJson).RootElement.EnumerateObject().Where(obj => !obj.Name.StartsWith("_"));
                // var zh = JsonDocument.Parse(zhJson).RootElement.EnumerateObject().Where(obj => !obj.Name.StartsWith("_"));
                var transEntryList = new List<TransEntry>();
                var hashTable = new Hashtable();
                foreach (var key in en.Keys)
                {
                    if (key.ToString().StartsWith("_")) continue;
                    var transEntry = new TransEntry()
                    {
                        TranslationKey = key.ToString(),
                        EnText = en[key].ToString()
                    };
                    transEntryList.Add(transEntry);
                    hashTable.Add(key.ToString(), transEntryList.Count - 1);
                }
                foreach (var key in zh.Keys)
                {
                    if (key.ToString().StartsWith("_")) continue;
                    if (hashTable.Contains(key.ToString()))
                    {
                        transEntryList[(int)hashTable[key.ToString()]].ZhText = zh[key].ToString();
                    }
                    else
                    {
                        errorKey.Add(key.ToString());
                    }
                }

                transEntryList = transEntryList.OrderBy(p => p.TranslationKey).ToList();

                transLists = transEntryList;
            }
            catch (Exception e)
            {
                MessageBox.Show("读取文件失败，请检查文件是否有误。");
                return new List<string>();
            }
            AddTransWordList(transLists);
            return errorKey;
        }

        //添加翻译推进与词典参考
        private void SetTransDictAndRefer(DictObject dictObject)
        {
            TranslateSelector.ItemsSource = null;
            TransDict.ItemsSource = null;

            TransHidden1.Visibility = Visibility.Visible;
            TranslateSelector.ItemsSource = dictObject.GetDictTrans();
            TransDict.ItemsSource = dictObject.GetDictRefer();
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
        }

        private void SubmitOnClick(object sender, RoutedEventArgs e)
        {
            if (AvalonEditor.Text == "") return;
            transLists[transWordIndex].ZhText = AvalonEditor.Text.Replace("\r", "");
            SaveFile();
            NextTransList();
        }

        private void SkipOnClick(object sender, RoutedEventArgs e)
        {
            transWordIndex++;
            if (transWordIndex >= transLists.Count) transWordIndex = transLists.Count - 1;
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
            if (ReviewCheckBox.IsChecked == true)
            {
                transWordIndex++;
                if (transWordIndex >= transLists.Count) transWordIndex = transLists.Count - 1;
            }
            else
            {
                while (transLists[transWordIndex].ZhText != "")
                {
                    transWordIndex++;
                    if (transWordIndex < transLists.Count) continue;
                    transWordIndex = transLists.Count - 1;
                    break;
                }
            }
            TransWordList.SelectedIndex = transWordIndex;
        }
        private void TransWordList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            transWordIndex = TransWordList.SelectedIndex;
            if (transWordIndex == -1) return;
            TransWordList.ScrollIntoView(TransWordList.Items[transWordIndex]);
            AvalonText.Text = transLists[TransWordList.SelectedIndex].EnText;
            FindFormat();
            AvalonEditor.Text = transLists[TransWordList.SelectedIndex].ZhText;
            RemoveAllTransRecommend();
            TransProgress.Content = "翻译进度：" + transLists.Count(word => word.ZhText != "") + "/" + transLists.Count;
            KeyName.Content = "当前键名：" + transLists[TransWordList.SelectedIndex].TranslationKey;

            // var dictObejct = new DictObject();
            // dictObejct.DictTrans.Add(TransAPI.Contect(OriginText.Text));
            // SetTransDictAndRefer(dictObejct);
            AvalonEditor.SelectionStart = AvalonEditor.Text.Length;
        }

        private void GetTransDict1()
        {
            Thread.Sleep(2);
        }

        //将C#字符串值转换为转义的字符串文字
        public static string ToLiteral(string input)
        {
            using var writer = new StringWriter();
            using var provider = CodeDomProvider.CreateProvider("CSharp");
            provider.GenerateCodeFromExpression(new CodePrimitiveExpression(input), writer, new CodeGeneratorOptions { IndentString = "\t" });
            return writer.ToString().Replace($"\" +{Environment.NewLine}\t\"", "");
        }

        private void OpenFileOnClick(object sender, RoutedEventArgs e)
        {
            OpenFile();
        }

        private void OpenFile()
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
        }

        private void SaveFileOnClick(object sender, RoutedEventArgs e)
        {
            if (SaveFile())
                SaveFloatWindow();
        }

        private bool SaveFile()
        {
            var jsonText = new StringBuilder();
            jsonText.Append("{\n");
            foreach (var transEntry in transLists.Where(transEntry => transEntry.ZhText != ""))
            {
                jsonText.Append('\t');
                jsonText.Append(ToLiteral(transEntry.TranslationKey));
                jsonText.Append(':');
                jsonText.Append(ToLiteral(transEntry.ZhText));
                jsonText.Append(',');
                jsonText.Append('\n');
            }

            jsonText.Remove(jsonText.Length - 2, 1);
            jsonText.Append('}');
            try
            {
                File.WriteAllText(Path.Combine(FileDir, "zh_cn.json"), jsonText.ToString());
                return true;
            }
            catch (Exception exception)
            {
                MessageBox.Show("文件保存出错");
                return false;
            }
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

            OpenFile();
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
                    if (AvalonEditor.LineCount <= AvalonText.LineCount) break;
                    if (AvalonEditor.SelectionStart != AvalonEditor.Text.Length)
                    {
                        AvalonEditor.SelectionStart -= 1;
                        AvalonEditor.SelectionLength = 1;
                        AvalonEditor.SelectedText = "";
                    }
                    if (AvalonEditor.Text.EndsWith("\n"))
                        AvalonEditor.Text = AvalonEditor.Text.Substring(0, AvalonEditor.Text.Length - 2);
                    transLists[transWordIndex].ZhText = AvalonEditor.Text.Replace("\r", "");
                    SaveFile();
                    NextTransList();
                    break;
                }
            }
        }
        
        private CompletionWindow _completionWindow;
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
        public string TranslationKey = "";
        public string? EnText { get; set; } = "";

        private string? _zhText = "";
        public string Color => ZhText == "" ? "red" : "green";
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
}
