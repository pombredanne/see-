// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;

namespace CSharpRepl.Services.Extensions;

using Microsoft.CodeAnalysis;

internal static class RoslynExtensions
{
    public static Solution ApplyChanges(this Solution edit, Workspace workspace)
    {
        if (!workspace.TryApplyChanges(edit))
        {
            throw new InvalidOperationException("Failed to apply edit to workspace");
        }

        return workspace.CurrentSolution;
    }
}
