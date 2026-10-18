using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ReaGui.Views.Controls;

/// <summary>
/// 内置代码/文本查看器：行号 + JSON/伪代码着色 + 查找 + 复制。
/// 采用等宽字体内联行号前缀，保证与内容严格对齐且滚动同步。
/// By leewheel 2026-10-16
/// </summary>
public partial class CodeViewer : UserControl
{
    private string _rawText = "";
    private readonly List<int> _matchOffsets = new();
    private int _activeMatch = -1;

    // 令牌画刷
    private static readonly Brush DefaultBrush = Frozen(Color.FromRgb(0xE6, 0xED, 0xF3));
    private static readonly Brush LineNumBrush = Frozen(Color.FromRgb(0x3B, 0x45, 0x52));
    private static readonly Brush SepBrush = Frozen(Color.FromRgb(0x26, 0x31, 0x3F));
    private static readonly Brush CommentBrush = Frozen(Color.FromRgb(0x5C, 0x67, 0x73));
    private static readonly Brush KeywordBrush = Frozen(Color.FromRgb(0x22, 0xD3, 0xEE));
    private static readonly Brush StringBrush = Frozen(Color.FromRgb(0x9E, 0xC9, 0x7A));
    private static readonly Brush KeyBrush = Frozen(Color.FromRgb(0x79, 0xC0, 0xFF));
    private static readonly Brush NumberBrush = Frozen(Color.FromRgb(0xD2, 0xA8, 0xFF));
    private static readonly Brush PunctBrush = Frozen(Color.FromRgb(0xFF, 0xB4, 0x54));

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "if","else","for","while","return","void","int","char","struct","class","function","def",
        "true","false","null","undefined","new","delete","this","break","continue","switch","case",
        "do","goto","static","const","auto","unsigned","signed","long","short","float","double","bool","string",
    };

    public CodeViewer()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty DocumentProperty = DependencyProperty.Register(
        nameof(Document), typeof(string), typeof(CodeViewer),
        new PropertyMetadata("", OnDocumentChanged));

    public string Document
    {
        get => (string)GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    /// <summary>语言标记（仅用于角标显示；着色对 JSON/伪代码通用）。</summary>
    public static readonly DependencyProperty SyntaxProperty = DependencyProperty.Register(
        nameof(Syntax), typeof(string), typeof(CodeViewer),
        new PropertyMetadata("auto", (d, e) => ((CodeViewer)d).LangBadge.Text = (string)e.NewValue));

    public string Syntax
    {
        get => (string)GetValue(SyntaxProperty);
        set => SetValue(SyntaxProperty, value);
    }

    private static void OnDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((CodeViewer)d).Render();

    private void Render()
    {
        _rawText = Document ?? "";
        var text = _rawText.Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = text.Split('\n');

        bool colorize = lines.Length <= 20000 && text.Length <= 400000;
        int width = Math.Max(2, lines.Length.ToString().Length);

        var doc = new FlowDocument
        {
            FontFamily = (FontFamily)FindResource("MonoFont"),
            FontSize = 13,
            PagePadding = new Thickness(0),
            PageWidth = 100000, // 关闭自动换行，长行走横向滚动
        };
        var para = new Paragraph
        {
            Margin = new Thickness(0),
            LineHeight = 18,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            TextIndent = 0,
        };

        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) para.Inlines.Add(new LineBreak());
            para.Inlines.Add(new Run((i + 1).ToString().PadLeft(width)) { Foreground = LineNumBrush });
            para.Inlines.Add(new Run("  ") { Foreground = LineNumBrush });
            para.Inlines.Add(new Run("│ ") { Foreground = SepBrush });

            var line = lines[i];
            if (!colorize)
            {
                para.Inlines.Add(new Run(line) { Foreground = DefaultBrush });
                continue;
            }
            TokenizeLine(para, line);
        }

        doc.Blocks.Add(para);
        Editor.Document = doc;
        Editor.ScrollToHorizontalOffset(0);
        UpdateMatchCount();
    }

    private static void TokenizeLine(Paragraph para, string line)
    {
        if (line.TrimStart().StartsWith("//", StringComparison.Ordinal))
        {
            para.Inlines.Add(new Run(line) { Foreground = CommentBrush });
            return;
        }

        int n = line.Length, i = 0;
        var def = new System.Text.StringBuilder();
        void FlushDefault()
        {
            if (def.Length > 0) { para.Inlines.Add(new Run(def.ToString()) { Foreground = DefaultBrush }); def.Clear(); }
        }

        while (i < n)
        {
            char c = line[i];

            if (c == '"')
            {
                FlushDefault();
                int start = i++;
                while (i < n)
                {
                    if (line[i] == '\\') { i += 2; continue; }
                    if (line[i] == '"') { i++; break; }
                    i++;
                }
                string tok = line.Substring(start, i - start);
                int j = i;
                while (j < n && line[j] == ' ') j++;
                bool isKey = j < n && line[j] == ':';
                para.Inlines.Add(new Run(tok) { Foreground = isKey ? KeyBrush : StringBrush });
                continue;
            }

            if (char.IsDigit(c) || (c == '.' && i + 1 < n && char.IsDigit(line[i + 1])))
            {
                FlushDefault();
                int start = i;
                while (i < n && (char.IsLetterOrDigit(line[i]) || line[i] is '.' or 'x' or 'X' or '_'))
                    i++;
                para.Inlines.Add(new Run(line.Substring(start, i - start)) { Foreground = NumberBrush });
                continue;
            }

            if (c is '{' or '}' or '[' or ']' or '(' or ')' or ',')
            {
                FlushDefault();
                para.Inlines.Add(new Run(c.ToString()) { Foreground = PunctBrush });
                i++;
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                int start = i;
                while (i < n && (char.IsLetterOrDigit(line[i]) || line[i] == '_')) i++;
                string word = line.Substring(start, i - start);
                if (Keywords.Contains(word))
                {
                    FlushDefault();
                    para.Inlines.Add(new Run(word) { Foreground = KeywordBrush });
                }
                else
                {
                    def.Append(word);
                }
                continue;
            }

            def.Append(c);
            i++;
        }
        FlushDefault();
    }

    // ---------------- 查找 ----------------
    private void OnSearchTextChanged(object sender, TextChangedEventArgs e) => UpdateMatchCount();

    private string FullText => new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd).Text;

    private void UpdateMatchCount()
    {
        _matchOffsets.Clear();
        _activeMatch = -1;
        var q = SearchBox.Text;
        if (string.IsNullOrEmpty(q)) { MatchCount.Text = ""; return; }
        var full = FullText;
        int idx = full.IndexOf(q, StringComparison.OrdinalIgnoreCase);
        while (idx >= 0)
        {
            _matchOffsets.Add(idx);
            idx = full.IndexOf(q, idx + q.Length, StringComparison.OrdinalIgnoreCase);
        }
        MatchCount.Text = _matchOffsets.Count + " 处";
    }

    private void OnFindNext(object sender, RoutedEventArgs e) => Move(1);
    private void OnFindPrev(object sender, RoutedEventArgs e) => Move(-1);

    private void Move(int step)
    {
        if (_matchOffsets.Count == 0) { UpdateMatchCount(); if (_matchOffsets.Count == 0) return; }
        _activeMatch = ((_activeMatch + step) % _matchOffsets.Count + _matchOffsets.Count) % _matchOffsets.Count;
        SelectAt(_matchOffsets[_activeMatch], SearchBox.Text.Length);
        MatchCount.Text = $"{_activeMatch + 1}/{_matchOffsets.Count}";
    }

    private void SelectAt(int offset, int length)
    {
        try
        {
            var start = Editor.Document.ContentStart.GetPositionAtOffset(offset, LogicalDirection.Forward);
            if (start is null) return;
            var end = start.GetPositionAtOffset(length, LogicalDirection.Forward) ?? start;
            Editor.CaretPosition = end;
            Editor.Selection.Select(start, end);
        }
        catch { /* 忽略越界 */ }
    }

    // ---------------- 复制 ----------------
    private void OnCopy(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(FullText); } catch { }
    }

    private void OnCopyRaw(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(_rawText); } catch { }
    }

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
