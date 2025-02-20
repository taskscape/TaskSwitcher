﻿using System.Collections.Generic;

namespace TaskSwitcher.Core.Matchers
{
    public class MatchResult
    {
        public bool Matched { get; set; }
        public int Score { get; set; }
        public IList<StringPart> StringParts { get; } = new List<StringPart>();
    }
}