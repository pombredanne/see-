// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;

namespace CSharpRepl.Services.Completion;

using System.Linq;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.QuickInfo;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Caching.Memory;

public record CompletionItemWithDescription(
    CompletionItem Item,
    Lazy<Task<string>> DescriptionProvider
);

internal sealed class AutoCompleteService
{
    public AutoCompleteService(IMemoryCache cache)
        => _cache = cache;

    public async Task<CompletionItemWithDescription[]> Complete(
        Document document,
        string text,
        int caret
    )
    {
        string cacheKey = AutoCompleteService.CACHE_KEY_PREFIX + document.Name + text + caret;

        if ((text != string.Empty) &&
            _cache.Get<CompletionItemWithDescription[]>(cacheKey) is CompletionItemWithDescription[]
                cached)
        {
            return cached;
        }

        CompletionList? completions = await CompletionService.GetService(document)
            .GetCompletionsAsync(document, caret)
            .ConfigureAwait(false);

        CompletionItemWithDescription[] completionsWithDescriptions = completions?.Items
                .Select(
                    item => new CompletionItemWithDescription(
                        item,
                        new Lazy<Task<string>>(
                            () => AutoCompleteService.GetExtendedDescription(document, item)
                        )
                    )
                )
                .ToArray() ??
            Array.Empty<CompletionItemWithDescription>();

        _cache.Set(cacheKey, completionsWithDescriptions, DateTimeOffset.Now.AddMinutes(1));

        return completionsWithDescriptions;
    }

    private readonly IMemoryCache _cache;
    private const string CACHE_KEY_PREFIX = "AutoCompleteService_";

    private static async Task<string> GetExtendedDescription(Document document, CompletionItem item)
    {
        SourceText currentText = await document.GetTextAsync()
            .ConfigureAwait(false);
        SourceText completedText = currentText.Replace(item.Span, item.DisplayText);
        Document completedDocument = document.WithText(completedText);

        QuickInfoService? infoService = QuickInfoService.GetService(completedDocument);

        if (infoService is null)
        {
            return string.Empty;
        }

        QuickInfoItem? info = await infoService.GetQuickInfoAsync(completedDocument, item.Span.End)
            .ConfigureAwait(false);

        return info is null
            ? string.Empty
            : string.Join(Environment.NewLine, info.Sections.Select(s => s.Text));
    }
}
