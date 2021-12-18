// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace CSharpRepl.Services.SyntaxHighlighting;

using Microsoft.CodeAnalysis.Classification;

internal sealed class DefaultTheme : Theme
{
    public DefaultTheme()
        : base(
            new[]
            {
                new Color(ClassificationTypeNames.ClassName, "BrightCyan"),
                new Color(ClassificationTypeNames.StructName, "BrightCyan"),
                new Color(ClassificationTypeNames.DelegateName, "BrightCyan"),
                new Color(ClassificationTypeNames.InterfaceName, "BrightCyan"),
                new Color(ClassificationTypeNames.ModuleName, "BrightCyan"),
                new Color(ClassificationTypeNames.RecordClassName, "BrightCyan"),
                new Color("record struct name", "BrightCyan"),
                new Color(ClassificationTypeNames.EnumName, "Green"),
                new Color(ClassificationTypeNames.Text, "White"),
                new Color(ClassificationTypeNames.ConstantName, "White"),
                new Color(ClassificationTypeNames.EnumMemberName, "White"),
                new Color(ClassificationTypeNames.EventName, "White"),
                new Color(ClassificationTypeNames.ExtensionMethodName, "White"),
                new Color(ClassificationTypeNames.Identifier, "White"),
                new Color(ClassificationTypeNames.LabelName, "White"),
                new Color(ClassificationTypeNames.LocalName, "White"),
                new Color(ClassificationTypeNames.MethodName, "White"),
                new Color(ClassificationTypeNames.PropertyName, "White"),
                new Color(ClassificationTypeNames.NamespaceName, "White"),
                new Color(ClassificationTypeNames.ParameterName, "White"),
                new Color(ClassificationTypeNames.NumericLiteral, "Blue"),
                new Color(ClassificationTypeNames.ControlKeyword, "BrightMagenta"),
                new Color(ClassificationTypeNames.Keyword, "BrightMagenta"),
                new Color(ClassificationTypeNames.Operator, "BrightMagenta"),
                new Color(ClassificationTypeNames.OperatorOverloaded, "BrightMagenta"),
                new Color(ClassificationTypeNames.PreprocessorKeyword, "BrightMagenta"),
                new Color(ClassificationTypeNames.StringEscapeCharacter, "BrightMagenta"),
                new Color(ClassificationTypeNames.VerbatimStringLiteral, "BrightYellow"),
                new Color(ClassificationTypeNames.StringLiteral, "BrightYellow"),
                new Color(ClassificationTypeNames.TypeParameterName, "Yellow"),
                new Color(ClassificationTypeNames.Comment, "Cyan"),
                new Color(ClassificationTypeNames.XmlDocCommentAttributeQuotes, "Green"),
                new Color(ClassificationTypeNames.XmlDocCommentAttributeValue, "Green"),
                new Color(ClassificationTypeNames.XmlDocCommentAttributeName, "Cyan"),
                new Color(ClassificationTypeNames.XmlDocCommentCDataSection, "Cyan"),
                new Color(ClassificationTypeNames.XmlDocCommentComment, "Cyan"),
                new Color(ClassificationTypeNames.XmlDocCommentDelimiter, "Cyan"),
                new Color(ClassificationTypeNames.XmlDocCommentEntityReference, "Cyan"),
                new Color(ClassificationTypeNames.XmlDocCommentName, "Cyan"),
                new Color(ClassificationTypeNames.XmlDocCommentProcessingInstruction, "Cyan"),
                new Color(ClassificationTypeNames.XmlDocCommentText, "Cyan"),}
        )
    {
    }
}

internal class Theme
{
    public Color[] Colors { get; }

    public Theme(Color[] colors)
        => Colors = colors;
}

internal sealed class Color
{
    public string Name { get; }
    public string Foreground { get; }

    public Color(string name, string foreground)
    {
        Name = name;
        Foreground = foreground;
    }
}
