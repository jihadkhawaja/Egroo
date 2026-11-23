using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Egroo.UI;

public sealed partial class ChatWorkspace
{
    private static readonly MarkdownPipeline MessageMarkdown = new MarkdownPipelineBuilder().DisableHtml().Build();
    private static readonly FontFamily Monospace = new("Cascadia Mono, Consolas, monospace");

    private Control RenderMessageBody(string text, IBrush foreground)
    {
        if (MessageTokens.IsMatch(text) || !text.Any(character => character is '\n' or '*' or '_' or '`' or '#'))
            return RenderMessage(text, foreground);

        var content = new StackPanel { Spacing = 6 };
        RenderBlocks(content, Markdown.Parse(text, MessageMarkdown), foreground);
        return content.Children.Count == 0 ? RenderMessage(text, foreground) : content;
    }

    private void RenderBlocks(StackPanel panel, ContainerBlock blocks, IBrush foreground)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case CodeBlock code:
                    panel.Children.Add(new Border
                    {
                        Padding = new Thickness(8),
                        Background = Canvas, CornerRadius = new CornerRadius(6),
                        Child = new TextBlock
                        {
                            Text = code.Lines.ToString(), Foreground = foreground, FontFamily = Monospace,
                            TextWrapping = TextWrapping.Wrap
                        }
                    });
                    break;
                case HeadingBlock heading:
                    var title = MarkdownText(heading.Inline, foreground);
                    title.FontSize = Math.Max(15, 23 - heading.Level * 2);
                    title.FontWeight = FontWeight.Bold;
                    panel.Children.Add(title);
                    break;
                case ParagraphBlock paragraph:
                    panel.Children.Add(MarkdownText(paragraph.Inline, foreground));
                    break;
                case ListBlock list:
                    var index = 1;
                    foreach (var item in list.OfType<ListItemBlock>())
                    {
                        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 7,
                            Margin = new Thickness(10, 0, 0, 0) };
                        row.Children.Add(Text(list.IsOrdered ? $"{index++}." : "•", 14, foreground));
                        var content = new StackPanel { Spacing = 4 };
                        RenderBlocks(content, item, foreground);
                        Grid.SetColumn(content, 1);
                        row.Children.Add(content);
                        panel.Children.Add(row);
                    }
                    break;
                case ContainerBlock nested:
                    RenderBlocks(panel, nested, foreground);
                    break;
            }
        }
    }

    private static TextBlock MarkdownText(ContainerInline? content, IBrush foreground)
    {
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = foreground };
        if (content is not null)
            AddMarkdownInlines(text.Inlines!, content, FontWeight.Normal, FontStyle.Normal);
        return text;
    }

    private static void AddMarkdownInlines(InlineCollection output, ContainerInline parent, FontWeight weight, FontStyle style)
    {
        for (var item = parent.FirstChild; item is not null; item = item.NextSibling)
        {
            switch (item)
            {
                case LiteralInline literal:
                    output.Add(new Run(literal.Content.ToString()) { FontWeight = weight, FontStyle = style });
                    break;
                case CodeInline code:
                    output.Add(new Run(code.Content) { FontFamily = Monospace });
                    break;
                case LineBreakInline line:
                    output.Add(new Run(line.IsHard ? "\n" : " "));
                    break;
                case EmphasisInline emphasis:
                    AddMarkdownInlines(output, emphasis, emphasis.DelimiterCount >= 2 ? FontWeight.Bold : weight,
                        emphasis.DelimiterCount == 1 ? FontStyle.Italic : style);
                    break;
                case ContainerInline nested:
                    AddMarkdownInlines(output, nested, weight, style);
                    break;
            }
        }
    }
}
