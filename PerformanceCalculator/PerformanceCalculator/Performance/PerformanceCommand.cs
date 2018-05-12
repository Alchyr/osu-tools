﻿// Copyright (c) 2007-2018 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using McMaster.Extensions.CommandLineUtils;

namespace PerformanceCalculator.Performance
{
    public class PerformanceCommand : ProcessorCommand
    {
        [UsedImplicitly]
        [Required, FileExists]
        [Argument(0, Name = "beatmap", Description = "Required. The beatmap corresponding to the replays.")]
        public string Beatmap { get; }

        [UsedImplicitly]
        [FileExists]
        [Option("-r|--replay", Description = "One for each replay. The replay file.")]
        public string[] Replays { get; }

        protected override Processor CreateProcessor() => new PerformanceProcessor(this);
    }
}
